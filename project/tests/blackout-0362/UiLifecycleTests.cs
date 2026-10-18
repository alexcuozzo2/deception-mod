using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackoutTests
 {
  static void UiMask(GameState s,int x=2)=>Write(s,Read(s) with{Blackouts=new[]{new LedgerBlackout(2,x,1)}});
  static void UiLifecycleCases()
  {
   Test("fog-render-published-before-native-construction",()=>
   {
    var s=Make();BindUi(s,false);var map=MapRenderer.Current!;NativeBlackoutUi.Tick();UiMask(s);NativeBlackoutUi.Tick();map.RefreshIfNeeded();
    Check(!map.IsRendered&&!map.shouldRefresh&&map.NativeRefreshes==0,"published renderer with no tiles never receives early refresh");
    map.RenderMap(s.Map);NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"native first render already applied latest mask");
    UiMask(s,3);NativeBlackoutUi.Tick();map.RefreshIfNeeded();Check(map.NativeRefreshes==1,"later real delta still refreshes");
   });
   Test("fog-render-allocated-array-is-not-completion",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;
    map.DuringRender=()=>{Check(map.IsRendered&&!map.TileSlotsReady,"native array exists before tile slots are filled");UiMask(s);NativeBlackoutUi.Tick();map.RefreshIfNeeded();Check(!map.shouldRefresh,"Update during synchronous construction cannot request Refresh");};
    map.RenderMap(s.Map);map.DuringRender=null;NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"completed rebuild establishes current baseline");UiMask(s,3);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"post-completion delta remains active");
   });
   Test("fog-render-first-cast-before-first-update",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;Cast(s,2,1);NativeBlackoutUi.Tick();map.RefreshIfNeeded();NativeBlackoutUi.Tick();
    Check(map.NativeRefreshes==1&&!map.shouldRefresh,"completion baseline detects first real cast, exactly once");
   });
   Test("fog-render-delayed-client-readiness-preserves-delta",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;GameManager.Client!.IsReady=false;UiMask(s);NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"not-ready client cannot schedule renderer");
    GameManager.Client.IsReady=true;NativeBlackoutUi.Tick();map.RefreshIfNeeded();NativeBlackoutUi.Tick();Check(map.NativeRefreshes==1&&!map.shouldRefresh,"deferred delta was not consumed");
   });
   Test("fog-render-viewer-change-refreshes-once",()=>
   {
    var s=Make();UiMask(s);BindUi(s);var map=MapRenderer.Current!;GameManager.LocalPlayer=Native.Items(s.PlayerStates).Cast<PlayerState>().First();NativeBlackoutUi.Tick();map.RefreshIfNeeded();NativeBlackoutUi.Tick();
    Check(map.NativeRefreshes==1&&!map.shouldRefresh,"hotseat viewer transition updates ordinary fog once");
   });
   Test("fog-render-clear-requires-new-completed-render",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;map.Clear();UiMask(s);NativeBlackoutUi.Tick();map.RefreshIfNeeded();Check(!map.shouldRefresh&&!map.IsRendered,"clear retires witness before native tiles disappear");
    map.RenderMap(s.Map);NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"native reconstruction is baseline, not forced refresh");UiMask(s,3);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"new completed map resumes normal delta refresh");
   });
   Test("fog-render-return-menu-reopen-same-game",()=>
   {
    var s=Make();BindUi(s);var old=MapRenderer.Current!;GameManager.instance=null;NativeBlackoutUi.Tick();old.OnDestroy();GameManager.instance=new();MapRenderer.Current=new();UiMask(s);NativeBlackoutUi.Tick();MapRenderer.Current.RefreshIfNeeded();
    Check(!MapRenderer.Current.shouldRefresh&&MapRenderer.Current.NativeRefreshes==0,"reopened game cannot reuse destroyed scene witness");
    MapRenderer.Current.RenderMap(s.Map);UiMask(s,3);NativeBlackoutUi.Tick();Check(MapRenderer.Current.shouldRefresh&&!old.shouldRefresh,"new scene owns subsequent refresh");
   });
   Test("fog-render-new-renderer-without-menu-clear",()=>
   {
    var s=Make();BindUi(s);var old=MapRenderer.Current!;MapRenderer.Current=new();UiMask(s);NativeBlackoutUi.Tick();MapRenderer.Current.RefreshIfNeeded();Check(!old.shouldRefresh&&!MapRenderer.Current.shouldRefresh,"new Current never inherits old completion");
    MapRenderer.Current.RenderMap(s.Map);UiMask(s,3);NativeBlackoutUi.Tick();Check(MapRenderer.Current.shouldRefresh,"matching new completion admits changes");
   });
   Test("fog-render-replaced-state-same-map-retains-delta",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;var copy=(GameState)s.MemberwiseClone();NativeGameStateEnvelope.Set(copy,0,NativeGameStateEnvelope.Get(s)!.Bytes());NativeRegroupSession.Owned.Add(copy);GameManager.GameState=copy;GameManager.Client!.GameState=copy;
    UiMask(copy);NativeBlackoutUi.Tick();map.RefreshIfNeeded();NativeBlackoutUi.Tick();Check(map.NativeRefreshes==1&&!map.shouldRefresh,"seek/replay replacement with identical MapData preserves baseline and emits real delta once");
    Check(ReferenceEquals(copy.Map,s.Map)&&(Read(s).Blackouts??Array.Empty<LedgerBlackout>()).Length==0,"state-local new mask does not mutate old ledger");
   });
   foreach(string change in new[]{"map-data","client","client-state"})Test("fog-render-stale-identity-"+change,()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;UiMask(s);
    switch(change){case"map-data":s.Map=(MapData)s.Map.MemberwiseClone();break;case"client":GameManager.Client=new(){GameState=s};break;default:GameManager.Client!.GameState=(GameState)s.MemberwiseClone();break;}
    NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"foreign state/client/map cannot spend old renderer witness");
    GameManager.Client!.GameState=GameManager.GameState;map.RenderMap(GameManager.GameState!.Map);UiMask(GameManager.GameState,3);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"fresh native state update render rebinds exact identities");
   });
   foreach(string guard in new[]{"inactive","destroyed","missing-client","missing-frame"})Test("fog-render-lifecycle-guard-"+guard,()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;UiMask(s);
    switch(guard){case"inactive":map.gameObject.activeInHierarchy=false;break;case"destroyed":map.OnDestroy();break;case"missing-client":GameManager.Client=null;break;default:NativeGameStateEnvelope.Values.Clear();break;}
    NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"unusable scene/context receives no mod refresh");
   });
   Test("fog-render-preserves-native-pending-refresh",()=>
   {
    var s=Make();BindUi(s,false);var map=MapRenderer.Current!;map.shouldRefresh=true;NativeBlackoutUi.Tick();Check(map.shouldRefresh,"adapter does not swallow native pending work");
   });
   Test("fog-render-native-exception-is-preserved",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;map.FailRender=true;Exception? caught=null;try{map.RenderMap(s.Map);}catch(Exception e){caught=e;}
    Check(caught is InvalidOperationException&&caught.Message=="native render failed"&&map.IsRendered&&!map.TileSlotsReady,"failure after allocation keeps original native exception");UiMask(s);NativeBlackoutUi.Tick();map.RefreshIfNeeded();Check(!map.shouldRefresh,"failed RenderMap never qualifies despite IsRendered true");
    map.FailRender=false;map.RenderMap(s.Map);UiMask(s,3);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"successful retry restores delta behavior");
   });
   Test("fog-render-suppressed-original-does-not-admit",()=>
   {
    var s=Make();BindUi(s,false);var map=MapRenderer.Current!;map.SkipRender=true;map.RenderMap(s.Map);UiMask(s);NativeBlackoutUi.Tick();map.RefreshIfNeeded();Check(!map.shouldRefresh,"suppressed original cannot manufacture a completed native map");
   });
   foreach(bool fail in new[]{false,true})Test("fog-render-nested-same-instance-"+(fail?"failure":"success"),()=>
   {
    var s=Make();BindUi(s,false);var map=MapRenderer.Current!;map.FailRender=fail;map.DuringRender=()=>
    {map.DuringRender=null;map.FailRender=false;map.RenderMap(s.Map);UiMask(s);NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"inner completion cannot publish while same renderer outer scope is active");map.FailRender=fail;};
    Exception? caught=null;try{map.RenderMap(s.Map);}catch(Exception e){caught=e;}
    Check((caught is not null)==fail,"outer failure is preserved");UiMask(s,3);NativeBlackoutUi.Tick();Check(map.shouldRefresh==!fail,"only successful outer completion admits a later fog delta");
   });
   Test("fog-render-unrelated-preview-clear-preserves-witness",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;var preview=new MapRenderer();preview.Clear();preview.OnDestroy();UiMask(s);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"clearing a different preview does not lose active map updates");
   });
   Test("fog-render-preview-clear-during-construction-is-isolated",()=>
   {
    var s=Make();BindUi(s);var map=MapRenderer.Current!;var preview=new MapRenderer();preview.DuringRender=()=>preview.Clear();preview.RenderMap(s.Map);UiMask(s);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"invalidated preview construction does not retire completed active scene");
   });
   foreach(string reset in new[]{"session","renderer"})Test("fog-render-lifecycle-reset-during-render-"+reset,()=>
   {
    var s=Make();BindUi(s,false);var map=MapRenderer.Current!;map.DuringRender=()=>{if(reset=="session")NativeBlackoutUi.Clear();else map.Clear();};map.RenderMap(s.Map);UiMask(s);NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"stale completion cannot resurrect retired witness");
    map.DuringRender=null;map.RenderMap(s.Map);UiMask(s,3);NativeBlackoutUi.Tick();Check(map.shouldRefresh,"later independent successful render can rebind");
   });
   Test("fog-render-wrong-map-completion-cannot-admit",()=>
   {
    var s=Make();BindUi(s,false);var map=MapRenderer.Current!;map.RenderMap(new());UiMask(s);NativeBlackoutUi.Tick();Check(!map.shouldRefresh,"render completion must use current client state's exact MapData");
   });
  }
 }
}
