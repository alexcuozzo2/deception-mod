using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Private ornaments on the local viewer's existing tech cells. Native
    // research, prices, unlock icons and purchase handlers remain authoritative.
    internal static class NativeTechnologyIntelligence
    {
        sealed record View(object State,object Client,object Map,byte Player,int Command,uint Turn);
        sealed record Head(object Handle,object Image){internal bool Completed;}
        sealed record Mark(object Item,object Data,object Root,Head[] Heads);
        static readonly Dictionary<long,Mark> Marks=new();
        static readonly List<(Mark Mark,Head Head)> PendingHeads=new();
        static readonly HashSet<string> Errors=new();
        static object? Tree;
        static View? Last;
        static NativeTradeSecrets.Research? Research;
        static int? LastIncome;
        static bool Dirty,Refreshing;
        static long Generation;
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("TechView","Show","System.Boolean"),typeof(NativeTechnologyIntelligence),null,nameof(TreeChanged));
            Native.Hook(h,Native.M("TechView","RefreshTechItems"),typeof(NativeTechnologyIntelligence),null,nameof(TreeChanged));
            Native.Hook(h,Native.M("TechView","OnDisable"),typeof(NativeTechnologyIntelligence),nameof(TreeDisabled));
            Native.Hook(h,Native.M("TechItem","SetData","Polytopia.Data.TechData"),typeof(NativeTechnologyIntelligence),nameof(ItemChanging));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeTechnologyIntelligence),null,nameof(Tick));
        }
        static void Error(Exception e){if(Errors.Count<8&&Errors.Add(e.Message))Entry.Note("technology-intelligence-ui-error",e.ToString());}
        static bool Active(object? c)=>Native.UnityAlive(c)&&(bool)Native.Get(Native.Get(c!,"gameObject")!,"activeInHierarchy")!;
        static void Destroy(object? o){if(Native.UnityAlive(o))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,o);}
        static void Hide(object root){if(Native.UnityAlive(root))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(root,"gameObject"),false);}
        static void Retire()
        {
            foreach(var mark in Marks.Values){Hide(mark.Root);if(Native.UnityAlive(mark.Root))Destroy(Native.Get(mark.Root,"gameObject"));}
            Marks.Clear();PendingHeads.Clear();
        }
        internal static void Clear(){Generation++;Retire();Tree=null;Last=null;Research=null;LastIncome=null;Dirty=false;Errors.Clear();}
        public static void TreeDisabled(object __instance)
        {if(Native.Same(Tree,__instance)){Retire();Tree=null;Dirty=true;}}
        public static void ItemChanging(object __instance)
        {
            if(Marks.Remove(Native.Pointer(__instance),out var mark)){Hide(mark.Root);Destroy(Native.Get(mark.Root,"gameObject"));}Dirty=true;
        }
        public static void TreeChanged(object __instance,bool __runOriginal)
        {
            if(!__runOriginal)return;
            try
            {
                if(!Native.Same(Tree,__instance))Retire();Tree=__instance;Dirty=true;
                // RefreshTechItems is called during hotseat transitions too;
                // retire first so a pooled cell cannot show the prior viewer.
                Retire();Tick();
            }catch(Exception e){Retire();Error(e);}
        }
        static bool Context(out View view)
        {
            view=null!;var state=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");
            var local=Native.Static("GameManager","LocalPlayer");var map=Native.Static("MapRenderer","Current");
            if(state is null||client is null||local is null||!Active(map)||!NativeTradeSecrets.Ready(state)||
                !Native.Same(state,Native.Get(client,"GameState"))||!(bool)Native.Get(client,"IsReady")!||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||(bool)Native.Get(client,"IsRecap")!||
                (bool)Native.Static("HotSeatOverlay","IsActive")!)return false;
            byte player=Convert.ToByte(Native.Get(local,"Id"));
            if(player is 0 or 255||!Native.Same(local,NativeTradeSecrets.Player(state,player))||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,player)!)return false;
            view=new(state,client,map!,player,Native.Int(Native.Get(state,"CurrentCommand")),Convert.ToUInt32(Native.Get(state,"CurrentTurn")));return true;
        }
        static bool Same(View a,View b)=>Native.Same(a.State,b.State)&&Native.Same(a.Client,b.Client)&&Native.Same(a.Map,b.Map)&&a.Player==b.Player;
        static bool Current(View v)=>Context(out var now)&&Same(v,now)&&v.Command==now.Command&&v.Turn==now.Turn;
        public static void Tick()
        {
            if(Refreshing)return;Refreshing=true;
            try
            {
                if(!Context(out var view)){Retire();Last=null;Research=null;LastIncome=null;return;}
                bool same=Last is not null&&Same(Last,view),changed=!same||Last!.Command!=view.Command||Last.Turn!=view.Turn;
                if(changed||Dirty)
                {
                    long generation=Generation;var research=NativeTradeSecrets.Capture(view.State,view.Player);
                    if(!Current(view)||generation!=Generation){Retire();return;}
                    int income=research?.Projection.Income??0;
                    if(!same||LastIncome!=income)Native.Invoke(Native.M("ResourceEvents","IncomeChanged","System.Byte"),null,view.Player);
                    if(!Current(view)||generation!=Generation){Retire();return;}
                    Research=research;Last=view;LastIncome=income;Dirty=true;
                }
                if(!Active(Tree)||Native.Int(Native.Get(Tree!,"lastPlayer"))!=view.Player||Research is null)
                {Retire();Dirty=false;return;}
                if(Dirty){Rebuild(view,Research);Dirty=false;}
                bool stale=false;
                // Only asynchronous requests still waiting for an atlas are
                // polled. Completed cells need no per-frame sprite/data scan.
                for(int i=PendingHeads.Count-1;i>=0;i--)
                {
                    var (mark,head)=PendingHeads[i];
                    if(!Active(mark.Item)||!Native.Same(mark.Data,Native.Get(mark.Item,"TechData"))||!Owned(mark))
                    {stale=true;break;}
                    if((bool)Native.Get(head.Handle,"isCompleted")!&&Native.UnityAlive(Native.Get(head.Handle,"sprite")))
                    {
                        Native.Set(head.Image,"sprite",Native.Get(head.Handle,"sprite"));Native.Set(head.Image,"color",Native.Static("UnityEngine.Color","white"));head.Completed=true;PendingHeads.RemoveAt(i);
                    }
                }
                if(stale){Retire();Dirty=true;}
            }
            catch(Exception e){Retire();Last=null;Research=null;Error(e);}
            finally{Refreshing=false;}
        }
        static bool Owned(Mark mark)=>Native.UnityAlive(mark.Root)&&Native.Same(Native.Get(Native.Get(mark.Root,"rectTransform")!,"parent"),Native.Get(mark.Item,"rectTransform"));
        static object V(float x,float y)=>Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!;
        static void Rect(object rect,float x,float y,float width,float height)
        {
            Native.Set(rect,"anchorMin",V(.5f,.5f));Native.Set(rect,"anchorMax",V(.5f,.5f));Native.Set(rect,"pivot",V(.5f,.5f));
            Native.Set(rect,"sizeDelta",V(width,height));Native.Set(rect,"anchoredPosition",V(x,y));
            Native.Set(rect,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),1f,1f,1f));
            Native.Set(rect,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
        }
        static object Image(object parent,object? sprite,object color,float x,float y,float size)
        {
            var image=Native.Invoke(Native.M("UIUtils","GetImage"),null)!;
            try
            {
                Native.Set(image,"raycastTarget",false);Native.Set(image,"preserveAspect",true);Native.Set(image,"sprite",sprite);Native.Set(image,"color",color);
                var rect=Native.Get(image,"rectTransform")!;Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),rect,parent,false);
                Rect(rect,x,y,size,size);return image;
            }catch{Destroy(Native.Get(image,"gameObject"));throw;}
        }
        static void Rebuild(View view,NativeTradeSecrets.Research research)
        {
            Retire();var cells=research.Projection.Cells.ToDictionary(c=>c.TechId,StringComparer.Ordinal);
            var items=Native.Get(Tree!,"items")!;int count=Native.Int(Native.Get(items,"Count"));if(count<0||count>256)throw new InvalidOperationException("Unexpected tech cell count");
            foreach(var item in Native.Items(items))
            {
                if(!Active(item))continue;var data=Native.Get(item,"TechData");if(data is null)continue;
                string id=Native.EnumName(Native.Get(data,"type")!);
                if(!cells.TryGetValue(id,out var cell)||(cell.Researchers.Length==0&&cell.Income==0))continue;
                var itemRect=Native.Get(item,"rectTransform")!;var bounds=Native.Get(itemRect,"rect")!;
                float size=Math.Min(Convert.ToSingle(Native.Get(bounds,"width")),Convert.ToSingle(Native.Get(bounds,"height")));
                if(!float.IsFinite(size)||size<=0)continue;
                object? root=null;
                try
                {
                    root=Native.Invoke(Native.M("UILibrary","NewEmptyComponent","UnityEngine.RectTransform"),null,itemRect)!;Hide(root);
                    var frame=Native.Get(root,"rectTransform")!;Rect(frame,0,0,size,size);
                    var heads=new List<Head>();float diameter=size*.23f;
                    var white=Native.Static("UnityEngine.Color","white")!;var bg=Native.Get(Native.Get(item,"bg")!,"sprite");
                    // Wrap the little tribe badges along the top of the cell.
                    // Every researcher is represented, without enlarging the
                    // node or changing its original click target.
                    int columns=Math.Min(4,cell.Researchers.Length);
                    for(int i=0;i<cell.Researchers.Length;i++)
                    {
                        object opponent=research.Opponents[cell.Researchers[i]];
                        float x=(i%columns-(columns-1)/2f)*diameter*.92f,y=size*.48f+(i/columns)*diameter*.90f;
                        var color=BlackmailArtAssets.TeamColor(BlackmailArtAssets.PlayerColor(view.State,opponent));
                        Image(frame,bg,color,x,y,diameter);
                        var head=Image(frame,null,white,x,y,diameter*.9f);Hide(head);
                        var addresses=Native.Invoke(Native.M("SpriteData","GetHeadSpriteAddresses","GameState","PlayerState"),null,view.State,opponent)!;
                        var handle=Activator.CreateInstance(Native.T("SpriteHandle"))!;
                        Native.Invoke(Native.M("SpriteHandle","Request",addresses.GetType().FullName!),handle,addresses);
                        // Keep uncompleted images transparent, not the white
                        // square produced by a Unity Image without a sprite.
                        if((bool)Native.Get(handle,"isCompleted")!&&Native.UnityAlive(Native.Get(handle,"sprite")))Native.Set(head,"sprite",Native.Get(handle,"sprite"));
                        Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(head,"gameObject"),true);
                        Native.Set(head,"color",Native.UnityAlive(Native.Get(head,"sprite"))?white:Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,0f));
                        heads.Add(new(handle,head){Completed=Native.UnityAlive(Native.Get(head,"sprite"))});
                    }
                    if(cell.Income>0)
                    {
                        var resource=Native.Invoke(Native.M("UILibrary","NewResourceWidget","UnityEngine.RectTransform","System.Int32"),null,frame,cell.Income)!;
                        Native.Set(resource,"WarnIfLow",false);Native.Set(resource,"AllowUpdateColors",false);
                        Native.Set(resource,"Amount",(float)cell.Income);
                        Rect(Native.Get(resource,"rectTransform")!,0,-size*.35f,size*.52f,size*.22f);
                        foreach(string name in new[]{"icon","drop","label"}){var part=Native.Get(resource,name);if(Native.UnityAlive(part))Native.Set(part!,"raycastTarget",false);}
                    }
                    var mark=new Mark(item,data,root,heads.ToArray());
                    if(!Current(view)||!Active(Tree)||Native.Int(Native.Get(Tree!,"lastPlayer"))!=view.Player||!Native.Same(data,Native.Get(item,"TechData"))||!Owned(mark))
                        throw new InvalidOperationException("Tech intelligence view changed during construction");
                    Marks.Add(Native.Pointer(item),mark);foreach(var head in mark.Heads)if(!head.Completed)PendingHeads.Add((mark,head));
                    Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(root,"gameObject"),true);root=null;
                }
                finally{if(root is not null){Hide(root);Destroy(Native.Get(root,"gameObject"));}}
            }
        }
    }
}
