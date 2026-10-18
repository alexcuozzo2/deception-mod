using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Local presentation acknowledgments only. Native contact, income and the
    // persistent Blackmail relationship remain owned by their action adapters.
    internal static class NativeBlackmailNotices
    {
        sealed record Notice(LedgerBlackmail Row,string Key);
        sealed class Notices
        {
            internal readonly Notice[] Rows;
            internal Notices(NativeGameStateEnvelope.Snapshot snapshot)
            {
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                // The per-actor/target/turn purchase stamp is authoritative.
                // Generic unit receipts also describe Regroup/Shed/etc. and do
                // not contain an actor/kind; they cannot identify this notice.
                // Legacy city-only liens are retired, so never announce them.
                Rows=(ledger.Blackmails??Array.Empty<LedgerBlackmail>()).Where(r=>r.UnitId>0)
                    .OrderBy(r=>r.X).ThenBy(r=>r.Y).ThenBy(r=>r.Blackmailer).ThenBy(r=>r.UnitId)
                    .Select(r=>new Notice(r,$"deception.blackmail.notice.v2.{ledger.Epoch:N}.{r.Victim}.{r.Blackmailer}.{r.UnitId}.{r.LastAppliedTurn}" )).ToArray();
            }
        }
        sealed record Context(object State,object Client,object Map,object DataMap,byte Viewer,uint Turn,long Generation);
        sealed record Shown(Context View,object Popup,object? Buttons,string Description);
        static ConditionalWeakTable<NativeGameStateEnvelope.Snapshot,Notices> Cache=new();
        static readonly HashSet<string> Acknowledged=new(),Errors=new();
        static Shown? Current;
        static long Generation;
        static bool Busy;
        internal static void Install(Harmony h)=>Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeBlackmailNotices),null,nameof(Tick));
        static void Error(Exception e){if(Errors.Count<8&&Errors.Add(e.Message))Entry.Note("blackmail-notice-error",e.ToString());}
        static bool Active(object? component)=>Native.UnityAlive(component)&&(bool)Native.Get(Native.Get(component!,"gameObject")!,"activeInHierarchy")!;
        static object? Popup()=>Native.Invoke(Native.M("PopupManager","GetCurrentPopup"),null);
        static bool Owned(Shown s)=>Native.UnityAlive(s.Popup)&&Native.Same(Popup(),s.Popup)&&
            (string?)Native.Get(s.Popup,"Header")=="Blackmailed!"&&(string?)Native.Get(s.Popup,"Description")==s.Description&&Native.Same(Native.Get(s.Popup,"buttonData"),s.Buttons);
        static void Retire(Shown? exact=null)
        {
            var shown=exact??Current;if(shown is null)return;
            if(Owned(shown))
            {
                // Clear may have run inside native Show. Retain this exact
                // token if Hide fails so the next frame can finish cleanup.
                if(Current is null)Current=shown;
                Native.Invoke(Native.M("PopupBase","Hide"),shown.Popup);
            }
            if(ReferenceEquals(Current,shown))Current=null;
        }
        internal static void Clear()
        {
            Generation++;try{Retire();}catch(Exception e){Error(e);}
            Cache=new();Acknowledged.Clear();Errors.Clear();
            // An in-flight Tick remains fenced until its finally completes.
        }
        static bool View(out Context c)
        {
            c=null!;if(!NativeRegroupSession.Enabled||!RecruitmentHooks.Registered)return false;
            var s=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");var map=Native.Static("MapRenderer","Current");var p=Native.Static("GameManager","LocalPlayer");
            if(s is null||client is null||p is null||!Active(map)||!NativeRegroupSession.Ready(s)||!Native.Same(s,Native.Get(client,"GameState"))||
                !(bool)Native.Get(client,"IsReady")!||(bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||(bool)Native.Get(client,"IsRecap")!||
                (bool)Native.Static("HotSeatOverlay","IsActive")!||Native.EnumName(Native.Get(s,"CurrentState")!)!="started")return false;
            byte viewer=Convert.ToByte(Native.Get(p,"Id"));
            if(viewer is 0 or 255||Native.Int(Native.Get(s,"CurrentPlayer"))!=viewer||!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
            c=new(s,client,map!,Native.Get(s,"Map")!,viewer,Convert.ToUInt32(Native.Get(s,"CurrentTurn")),Generation);return true;
        }
        static bool Same(Context c)=>c.Generation==Generation&&View(out var v)&&Native.Same(c.State,v.State)&&Native.Same(c.Client,v.Client)&&Native.Same(c.Map,v.Map)&&Native.Same(c.DataMap,v.DataMap)&&c.Viewer==v.Viewer&&c.Turn==v.Turn;
        static bool Idle(Context c)
        {
            var actions=Native.Get(c.Client,"ActionManager");
            return Same(c)&&actions is not null&&!(bool)Native.Get(c.Client,"IsWaitingForCommand")!&&!(bool)Native.Get(actions,"IsProcessing")!&&
                !(bool)Native.Get(actions,"IsRecap")!&&Native.Int(Native.Get(Native.Get(c.State,"ActionStack")!,"Count"))==0&&!(bool)Native.Static("PopupManager","PopupShowing")!;
        }
        static bool Ack(string key)
        {
            if(Acknowledged.Contains(key))return true;
            if(Native.Int(Native.Invoke(Native.M("UnityEngine.PlayerPrefs","GetInt","System.String","System.Int32"),null,key,0))!=1)return false;
            Remember(key);return true;
        }
        static void Remember(string key){if(Acknowledged.Count>=16384)Acknowledged.Clear();Acknowledged.Add(key);}
        static object? Player(object state,byte id){object?[] a={id,null};return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,a)!?a[1]:null;}
        static LedgerBlackmail? ActiveRow(Context c,Notice n)
        {
            var at=Activator.CreateInstance(Native.T("WorldCoordinates"),n.Row.X,n.Row.Y)!;
            var home=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(c.State,"Map"),at);
            if(home is null)return null;
            // Only this supporting city's targets can compete with the lien.
            // A paused pending notice must not rescan every city each frame.
            return NativeBlackmail.At(c.State,home).SingleOrDefault(r=>r.UnitId==n.Row.UnitId&&r.Blackmailer==n.Row.Blackmailer&&r.Victim==n.Row.Victim&&
                r.X==n.Row.X&&r.Y==n.Row.Y&&r.CreatedTurn==n.Row.CreatedTurn&&r.LastAppliedTurn==n.Row.LastAppliedTurn);
        }
        static object? City(Context c,Notice n)
        {
            if(ActiveRow(c,n) is null)return null;
            var at=Activator.CreateInstance(Native.T("WorldCoordinates"),n.Row.X,n.Row.Y)!;
            return Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(c.State,"Map"),at);
        }
        static string Plain(string value)=>value.Replace("<","‹").Replace(">","›").Replace('\r',' ').Replace('\n',' ');
        public static void Tick()
        {
            if(Busy)return;Busy=true;
            try
            {
                if(Current is Shown shown)
                {if(!Owned(shown))Current=null;else if(!Same(shown.View)){Retire();return;}else return;}
                if(!View(out var c)||!Idle(c))return;
                var snapshot=NativeGameStateEnvelope.Get(c.State);if(snapshot is null)return;
                var notices=Cache.GetValue(snapshot,s=>new Notices(s));
                foreach(var notice in notices.Rows)
                {
                    if(notice.Row.Victim!=c.Viewer||Ack(notice.Key))continue;
                    var city=City(c,notice);var actor=Player(c.State,notice.Row.Blackmailer);if(city is null||actor is null)continue;
                    string name=Plain((string?)Native.Get(Native.Get(city,"improvement")!,"name")??"");
                    string tribe=Plain((string?)Native.Invoke(Native.M("ClientPlayerExtensions","GetLocalizedTribeName","PlayerState","GameState"),null,actor,c.State)??"");
                    if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(tribe))continue;
                    var active=ActiveRow(c,notice);if(active is null)continue;
                    string stars=active.Layers==1?"1 star":$"{active.Layers} stars";
                    string description=$"A unit supported by {name} is being blackmailed by {tribe}. It diverts {stars} per turn from {name} to {tribe} until the Blackmail ends.";
                    if(!Idle(c)||!ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(c.State))||City(c,notice) is null)return;
                    var popup=Native.Invoke(Native.M("PopupManager","GetBasicPopup"),null)!;
                    Native.Invoke(Native.M("BasicPopup","SetHeader","System.String"),popup,"Blackmailed!");
                    Native.Invoke(Native.M("BasicPopup","SetDescription","System.String"),popup,description);
                    Native.Invoke(Native.M("BasicPopup","SetDefaultOkbutton","Il2CppSystem.Action"),popup,new object?[]{null});
                    if(!Idle(c)||!ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(c.State))||City(c,notice) is null)return;
                    var showing=new Shown(c,popup,Native.Get(popup,"buttonData"),description);Current=showing;
                    try{Native.Invoke(Native.M("PopupBase","Show"),popup);}catch{Retire(showing);throw;}
                    if(!Same(c)||!ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(c.State))||City(c,notice) is null){Retire(showing);return;}
                    if(!Owned(showing)||!(bool)Native.Static("PopupManager","PopupShowing")!){Current=null;return;}
                    // Persist only after this exact popup is visibly current.
                    Native.Invoke(Native.M("UnityEngine.PlayerPrefs","SetInt","System.String","System.Int32"),null,notice.Key,1);
                    Native.Invoke(Native.M("UnityEngine.PlayerPrefs","Save"),null);Remember(notice.Key);return;
                }
            }
            catch(Exception e){Error(e);try{Retire();}catch(Exception cleanup){Error(cleanup);}}
            finally{Busy=false;}
        }
    }
}
