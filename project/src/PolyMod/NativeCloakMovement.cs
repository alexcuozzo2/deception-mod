using System;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static class NativeCloakMovement
    {
        internal static Func<object,object,object,object,bool>? FallbackRoute;
        [ThreadStatic] static ActualQuery? Current;
        static long Generation;
        internal sealed record ActualQuery(object State,object Map,object Unit,object Frame,object Origin,
            byte Owner,uint Turn,bool InheritedCloak,ActualQuery? Previous,long Epoch);
        internal static void Clear(){Generation++;Current=null;NativeSpyMovementProfile.Clear();}
        static bool SameCoordinate(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static bool Valid(ActualQuery q)=>q.Frame is not null&&q.Epoch==Generation&&NativeRegroupSession.Ready(q.State)&&
            ReferenceEquals(q.Frame,NativeGameStateEnvelope.Get(q.State))&&Native.Same(q.Map,Native.Get(q.State,"Map"))&&
            q.Owner==Native.Int(Native.Get(q.Unit,"owner"))&&q.Turn==Convert.ToUInt32(Native.Get(q.State,"CurrentTurn"))&&
            SameCoordinate(q.Origin,Native.Get(q.Unit,"coordinates")!)&&Tile(q.State,q.Origin) is object tile&&Native.Same(Native.Get(tile,"unit"),q.Unit);
        static bool ScopedUnit(object unit)=>Current is ActualQuery q&&Native.Same(unit,q.Unit)&&Valid(q);
        static ActualQuery? Begin(object state,object? unit,object from)
        {
            // A nested native query for another/ordinary/detached unit masks
            // the outer scope. Otherwise its neighbor reads could mistake the
            // outer invisible body's presentation for an actual ZOC source.
            ActualQuery? Unchanged()
            {
                if(Current is null)return null;
                var nested=new ActualQuery(state,null!,unit!,null!,from,0,0,false,Current,Generation);Current=nested;return nested;
            }
            if(unit is null||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state)||
                Native.EnumName(Native.Get(unit,"type")!) is "cloak" or "mermaidcloak" or "cloak_boat"||
                !SameCoordinate(from,Native.Get(unit,"coordinates")!)||Tile(state,from) is not object tile||!Native.Same(Native.Get(tile,"unit"),unit))return Unchanged();
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            // Native Hide/Invisible also grant occupied-tile traversal / ZOC
            // immunity. Borrowed concealment must not grant those mechanical
            // powers to a genuine body. Spies earn actual invisibility but keep
            // their existing physical movement, including without a disguise.
            // Mimics inherit a Cloak's traversal;
            // actual Cloaks use native rules unchanged, with any appearance.
            bool actualSpy=SpyCatalog.IsSpyId(Native.EnumName(Native.Get(unit,"type")!));
            if(row is not {Alive:true}||(!actualSpy&&row is not {Independent:true,Appearance:not null}))return Unchanged();
            bool inherited=row.Kind==LedgerActualKind.Mimic&&row.Appearance?.Form is ("cloak" or "mermaidcloak" or "cloak_boat")&&
                Native.EnumName(Native.Get(unit,"type")!)==RecruitmentCatalog.Variant(row.Appearance.Form);
            // Spy appearances do not hide; their native movement obeys
            // occupied tiles and ZOC. An invisible Mimic using that appearance
            // therefore needs the same narrow movement-only concealment mask.
            bool inheritedSpy=row.Kind==LedgerActualKind.Mimic&&row.Appearance is LedgerAppearance a&&SpyCatalog.IsSpyId(a.Form)&&
                Native.EnumName(Native.Get(unit,"type")!)==RecruitmentCatalog.Variant(a.Form);
            if(row.Kind!=LedgerActualKind.Genuine&&!inherited&&!inheritedSpy)return Unchanged();
            var frame=NativeGameStateEnvelope.Get(state);if(frame is null)return Unchanged();
            var q=new ActualQuery(state,Native.Get(state,"Map")!,unit,frame,from,Convert.ToByte(Native.Get(unit,"owner")),
                Convert.ToUInt32(Native.Get(state,"CurrentTurn")),inherited,Current,Generation);Current=q;return q;
        }
        [HarmonyPriority(Priority.First)]
        public static void BeforePath(object __0,object __1,object? __4,out ActualQuery? __state)=>__state=Begin(__0,__4,__1);
        [HarmonyPriority(Priority.First)]
        public static void BeforeOptions(object __0,object __1,object? __3,out ActualQuery? __state)=>__state=Begin(__0,__3,__1);
        // Validate after the existing last-priority route preference too.
        [HarmonyPriority(Priority.Last-1)]
        public static void AfterQuery(ActualQuery? __state,ref object? __result)
        {if(__state is {Frame:not null}&&!Valid(__state))__result=Native.NewList("WorldCoordinates",Array.Empty<object>());}
        [HarmonyPriority(Priority.Last-1)]
        public static void GuardedOptions(object __0,object __1,object __3,ActualQuery? __state,ref object? __result)
        {AfterOptions(__0,__1,__3,ref __result);AfterQuery(__state,ref __result);}
        public static Exception? QueryFinished(Exception? __exception,ActualQuery? __state)
        {if(__state is not null&&ReferenceEquals(Current,__state))Current=__state.Epoch==Generation?__state.Previous:null;return __exception;}
        [HarmonyPriority(Priority.Last)]
        public static void MovementAbility(object __0,object __1,ref bool __result)
        {if(Native.Int(__1)==5){if(NativeSpyMovementProfile.Suppress(__0))__result=false;else if(ScopedUnit(__0))__result=Current!.InheritedCloak;}}
        [HarmonyPriority(Priority.Last)]
        public static void MovementDataAbility(object __0,object __1,ref bool __result)
        {
            // Native tile ZOC costs read UnitData directly, while traversal
            // reads UnitState. Restrict both only during this exact actor's
            // path query; the shared definition and all rendering stay intact.
            if(Native.Int(__1)==5)
            {if(NativeSpyMovementProfile.Suppress(__0,data:true))__result=false;
             else if(Current is ActualQuery q&&Valid(q)&&Native.Same(__0,Native.Get(q.Unit,"UnitData")))__result=q.InheritedCloak;}
        }
        [HarmonyPriority(Priority.Last)]
        public static void MovementEffect(object __instance,object __0,ref bool __result)
        {if(__result&&Native.Int(__0)==3&&(NativeSpyMovementProfile.Suppress(__instance)||ScopedUnit(__instance)&&!Current!.InheritedCloak))__result=false;}
        internal static void Install(Harmony harmony)
        {
            // PathFinderSettings is a native inline 56-byte value whose generated
            // wrapper is a reference type. The installed interop trampoline cannot
            // detour that ABI. WorldCoordinates is instead a true blittable CLR
            // value type (two Int32 fields); leave the settings boundary untouched.
            Native.Hook(harmony,Native.M("PathFinder","GetMoveOptions","GameState","WorldCoordinates","System.Int32","UnitState"),
                typeof(NativeCloakMovement),nameof(BeforeOptions),nameof(GuardedOptions),nameof(QueryFinished));
            Native.Hook(harmony,Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),
                typeof(NativeCloakMovement),nameof(BeforePath),nameof(AfterQuery),nameof(QueryFinished));
            Native.Hook(harmony,Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),
                typeof(NativeCloakMovement),null,nameof(MovementAbility));
            Native.Hook(harmony,Native.M("UnitDataExtensions","HasAbility","Polytopia.Data.UnitData","Polytopia.Data.UnitAbility+Type"),
                typeof(NativeCloakMovement),null,nameof(MovementDataAbility));
            Native.Hook(harmony,Native.M("UnitState","HasEffect","UnitEffect"),
                typeof(NativeCloakMovement),null,nameof(MovementEffect));
            Native.Hook(harmony,Native.M("MoveCommand","IsValid","GameState","System.String&"),
                typeof(NativeCloakMovement),null,nameof(AfterMoveValid));
        }
        static object? Tile(object state,object at)=>Native.Invoke(
            Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool Relevant(object? state,object? unit,object start,out byte owner)
        {
            owner=0;if(state is null||unit is null)return false;
            // Ordinary units never enter the ledger/state-validation path.
            string carrier=Native.EnumName(Native.Get(unit,"type")!);
            if(carrier is not ("dcp_mimic_form_cloak" or "dcp_mimic_form_mermaidcloak" or "dcp_mimic_form_cloak_boat"))return false;
            if(!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state))return false;
            object? origin=Tile(state,Native.Get(unit,"coordinates")!);
            if(origin is null||!Native.Same(Native.Get(origin,"unit"),unit)||!Native.Same(Tile(state,start),origin))return false;
            owner=Convert.ToByte(Native.Get(unit,"owner"));object?[] players={owner,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,players)!||
                players[1] is null||Convert.ToByte(Native.Get(players[1]!,"Id"))!=owner)return false;
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            return row is {Alive:true,Independent:true,Kind:LedgerActualKind.Mimic,Appearance:not null}&&
                row.Appearance.Form is ("cloak" or "mermaidcloak" or "cloak_boat")&&carrier==RecruitmentCatalog.Variant(row.Appearance.Form);
        }
        static bool OpponentCity(object state,object at,byte owner)
        {
            var tile=Tile(state,at);return tile is not null&&
                (bool)Native.Invoke(Native.M("TileData","HasOpponentCity","System.Byte"),tile,owner)!;
        }
        public static void AfterOptions(object __0,object __1,object __3,ref object? __result)
        {
            if(__result is null||!Relevant(__0,__3,__1,out byte owner))return;
            var original=Native.Items(__result).ToArray();
            var allowed=original.Where(at=>!OpponentCity(__0,at,owner)||FallbackRoute?.Invoke(__0,__3,__1,at)==true).ToArray();
            // Do not mutate a native/cached/shared result. Preserve its exact
            // identity when there is no restriction; otherwise publish one copy.
            if(allowed.Length!=original.Length)__result=Native.NewList("WorldCoordinates",allowed);
        }
        public static void AfterMoveValid(object __instance,object __0,ref bool __result,ref string __1)
        {
            if(!__result)return;
            object?[] units={Convert.ToUInt32(Native.Get(__instance,"UnitId")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,units)!||units[1] is not object unit)return;
            if(!Relevant(__0,unit,Native.Get(__instance,"From")!,out byte owner)||
                Convert.ToByte(Native.Get(__instance,"PlayerId"))!=owner||!OpponentCity(__0,Native.Get(__instance,"To")!,owner))return;
            if(FallbackRoute?.Invoke(__0,unit,Native.Get(__instance,"From")!,Native.Get(__instance,"To")!)==true)return;
            // Native Cloaks inherit this endpoint ban through Infiltrate.
            // Mimics copy the restriction without gaining the genuine weapon.
            __result=false;__1="This appearance cannot enter another player's city.";
        }
    }
}
