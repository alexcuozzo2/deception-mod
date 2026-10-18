using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Prefer a route that both bodies can execute. Native movement still owns
    // the command and its allowance; this only replaces an owned path result.
    internal static class NativeDisguisePathPreference
    {
        const int MaxTiles=4096,MaxLabels=8192;
        [ThreadStatic] static bool Searching;
        static long Generation;
        static int Notes,SlowNotes;
        internal static void Clear(){Generation++;Searching=false;Notes=SlowNotes=0;}
        internal static void Install(Harmony h)=>Native.Hook(h,
            Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),
            typeof(NativeDisguisePathPreference),null,nameof(AfterPath));
        static (int X,int Y) Key(object at)=>(Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static object[] Items(object? value,int bound)
        {if(value is null)return Array.Empty<object>();int n=Native.Int(Native.Get(value,"Count"));if(n<0||n>bound)throw new InvalidDataException("Disguise route exceeds native map bound.");return Native.Items(value).ToArray();}
        sealed class Label
        {
            internal readonly object Tile,At;internal readonly int Real,Apparent;internal readonly Label? Parent;
            internal bool Active=true;
            internal Label(object tile,int real,int apparent,Label? parent){Tile=tile;At=Native.Get(tile,"coordinates")!;Real=real;Apparent=apparent;Parent=parent;}
            internal object[] Path(){var path=new List<object>();for(Label? p=this;p is not null;p=p.Parent){if(path.Count>=MaxTiles)throw new InvalidDataException("Disguise path is cyclic or too long.");path.Add(p.At);}return path.ToArray();}
        }
        // Last runs after the existing native-water/fallback/Rogue filters.
        // Search independently intersects those same restrictions; it cannot
        // resurrect an endpoint forbidden to the actual or apparent body.
        [HarmonyPriority(Priority.Last)]
        public static void AfterPath(object __0,object __1,object __2,int __3,object? __4,ref object? __result)
        {
            if(Searching||__4 is null||__3<=0||!RecruitmentHooks.Registered||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(__0))return;
            long epoch=Generation;object? frame=null,map=null;byte owner=0;uint turn=0;bool captured=false;
            try
            {
                frame=NativeGameStateEnvelope.Get(__0);map=Native.Get(__0,"Map");owner=Convert.ToByte(Native.Get(__4,"owner"));turn=Convert.ToUInt32(Native.Get(__0,"CurrentTurn"));captured=true;
                var row=NativeRegroupSession.ReadIdentity(__0,Convert.ToUInt32(Native.Get(__4,"id")));
                if(row is not {Alive:true,Independent:true,Appearance:not null}||Key(__1)!=Key(Native.Get(__4,"coordinates")!)||Key(__1)==Key(__2)||
                    Tile(__0,__1) is not object origin||!Native.Same(Native.Get(origin,"unit"),__4)||Native.Int(Native.Get(__4,"health"))<=0)return;
                bool Valid()=>epoch==Generation&&NativeRegroupSession.Ready(__0)&&ReferenceEquals(frame,NativeGameStateEnvelope.Get(__0))&&
                    Native.Same(map,Native.Get(__0,"Map"))&&turn==Convert.ToUInt32(Native.Get(__0,"CurrentTurn"))&&owner==Convert.ToByte(Native.Get(__4,"owner"))&&
                    Key(__1)==Key(Native.Get(__4,"coordinates")!)&&Native.Same(Native.Get(origin,"unit"),__4);
                var path=Items(__result,MaxTiles);
                if(path.Length>=2&&NativeDisguiseMovement.Compatible(__0,__4,row.Appearance,path))
                {if(!Valid())__result=Native.NewList("WorldCoordinates",Array.Empty<object>());return;}
                if(!Valid()){__result=Native.NewList("WorldCoordinates",Array.Empty<object>());return;}
                if(row.Appearance.Effects.Contains("Frozen",StringComparer.Ordinal))return;
                Searching=true;
                try{var preferred=Search(__0,__4,__1,__2,__3,row,origin,map!,Valid);if(!Valid())__result=Native.NewList("WorldCoordinates",Array.Empty<object>());else if(preferred is not null)__result=preferred;}
                finally{Searching=false;}
            }
            catch(Exception e)
            {
                bool stale=true;
                try{stale=captured&&(epoch!=Generation||!NativeRegroupSession.Ready(__0)||!ReferenceEquals(frame,NativeGameStateEnvelope.Get(__0))||
                    !Native.Same(map,Native.Get(__0,"Map"))||turn!=Convert.ToUInt32(Native.Get(__0,"CurrentTurn"))||owner!=Convert.ToByte(Native.Get(__4,"owner"))||Key(__1)!=Key(Native.Get(__4,"coordinates")!)||
                    Tile(__0,__1) is not object tile||!Native.Same(Native.Get(tile,"unit"),__4));}catch{}
                if(stale)__result=Native.NewList("WorldCoordinates",Array.Empty<object>());
                if(Notes++<8)Entry.Note("disguise-route-preference-error",e.Message);
            }
        }
        static object? Search(object state,object unit,object from,object to,int requested,LedgerUnit row,object origin,object map,Func<bool> Valid)
        {
            object profile=NativeDisguiseMovement.Profile(state,unit,row.Appearance!);
            using var spyMovement=NativeSpyMovementProfile.Begin(state,unit,profile);
            object real=Native.Invoke(Native.M("PathFinderSettings","CreateForUnit","UnitState","GameState"),null,unit,state)!;
            object apparent=Native.Invoke(Native.M("PathFinderSettings","CreateForUnit","UnitState","GameState"),null,profile,state)!;
            int realBudget=checked(10*Math.Min(requested,Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,unit,state))));
            int apparentBudget=checked(10*Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,profile,state)));
            if(realBudget<=0||apparentBudget<=0||Tile(state,to) is not object destination)return null;
            var finish=Native.M("PathFinder","IsAllowedToFinishOnTile","PathFinderSettings","TileData");
            if(!(bool)Native.Invoke(finish,null,real,destination)!||!(bool)Native.Invoke(finish,null,apparent,destination)!)return null;
            var water=new NativeMimicWaterMovement.Scope(state,profile,from,apparentQuery:true);
            var realWater=row.Kind==LedgerActualKind.Mimic?new NativeMimicWaterMovement.Scope(state,unit,from,apparentQuery:true):null;
            if(!water.Bound||!water.Allows(from)||realWater is not null&&(!realWater.Bound||!realWater.Allows(from)))return null;
            var neighbors=Native.M("PathFinder","ValidNeighbors","MapData","WorldCoordinates","PathFinderSettings");
            var accessible=Native.M("PathFinder","IsTileAccessible","TileData","TileData","PathFinderSettings");
            var cost=Native.M("TileData","GetMovementCost","MapData","TileData","PathFinderSettings");
            var labels=new Dictionary<(int,int),List<Label>>();
            var pending=new PriorityQueue<Label,(int Apparent,int Real,long Order)>();long order=0;
            bool realControl=NativeRogueTactics.NeedsControlCheck(state,unit),apparentControl=NativeRogueTactics.NeedsControlCheck(state,profile);
            var adjacent=new Dictionary<(int,int),object[]>();var permits=new Dictionary<(int,int),bool>();
            var edges=new Dictionary<((int,int) From,(int,int) To),(int Real,int Apparent)>();
            var continuation=new Dictionary<(int,int),bool>();var timer=Stopwatch.StartNew();
            var start=new Label(origin,0,0,null);labels.Add(Key(from),new(){start});pending.Enqueue(start,(0,0,order++));
            try{while(pending.TryDequeue(out var current,out _))
            {
                if(!Valid())return Native.NewList("WorldCoordinates",Array.Empty<object>());
                if(!current.Active)continue;
                if(Key(current.At)==Key(to))
                {
                    var path=current.Path();
                    if(NativeDisguiseMovement.Compatible(state,unit,row.Appearance!,path)&&Valid())return Native.NewList("WorldCoordinates",path);
                    continue;
                }
                // Native Step checks the accumulated cost BEFORE the next
                // edge. A costly final edge is legal; continuing afterward is not.
                if(current.Real>=realBudget||current.Apparent>=apparentBudget)continue;
                var key=Key(current.At);
                if(!continuation.TryGetValue(key,out bool canContinue))continuation[key]=canContinue=
                    (!realControl||NativeRogueTactics.CanContinue(state,unit,from,current.At,unit,true))&&
                    (!apparentControl||NativeRogueTactics.CanContinue(state,profile,from,current.At,unit,true));
                if(!canContinue)continue;
                if(!adjacent.TryGetValue(key,out var nextTiles))adjacent[key]=nextTiles=Items(Native.Invoke(neighbors,null,map,current.At,real),8);
                foreach(var next in nextTiles)
                {
                    object at=Native.Get(next,"coordinates")!;
                    var nextKey=Key(at);if(!permits.TryGetValue(nextKey,out bool permit))permits[nextKey]=permit=water.Allows(at)&&(realWater is null||realWater.Allows(at));
                    if(!permit)continue;
                    if(!edges.TryGetValue((key,nextKey),out var edge))
                    {
                        if(!(bool)Native.Invoke(accessible,null,next,current.Tile,apparent)!)edges[(key,nextKey)]=edge=(-1,-1);
                        else
                        {
                            edge=(Native.Int(Native.Invoke(cost,next,map,current.Tile,real)),Native.Int(Native.Invoke(cost,next,map,current.Tile,apparent)));
                            if(edge.Real<0||edge.Apparent<0)throw new InvalidDataException("Negative native movement cost.");
                            edges[(key,nextKey)]=edge;
                        }
                    }
                    if(edge.Real<0)continue;
                    var added=new Label(next,checked(current.Real+edge.Real),checked(current.Apparent+edge.Apparent),current);
                    if(!labels.TryGetValue(Key(at),out var peers))
                    {if(labels.Count>=MaxTiles)throw new InvalidDataException("Joint route exceeds map bound.");labels.Add(Key(at),peers=new());}
                    // Both costs matter: a route cheaper for the real unit can
                    // leave too little apparent budget, and vice versa.
                    if(peers.Any(p=>p.Active&&p.Real<=added.Real&&p.Apparent<=added.Apparent))continue;
                    foreach(var p in peers.Where(p=>p.Active&&added.Real<=p.Real&&added.Apparent<=p.Apparent))p.Active=false;
                    peers.Add(added);if(order>=MaxLabels)throw new InvalidDataException("Joint route search exceeds bounded native work.");
                    pending.Enqueue(added,(added.Apparent,added.Real,order++));
                }
            }
            return null;
            }
            finally{if(timer.ElapsedMilliseconds>=25&&SlowNotes++<4)Entry.Note("disguise-route-search",$"milliseconds={timer.ElapsedMilliseconds}; tiles={labels.Count}; labels={order}; edges={edges.Count}");}
        }
    }
}
