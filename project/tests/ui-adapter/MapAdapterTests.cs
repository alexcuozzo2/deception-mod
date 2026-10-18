// Executes the production map adapter against the audited synthetic renderer
// contracts. These assertions do not establish native game rendering success.
using System;
using System.Linq;
using Deception.PolyModAdapter;
using UnityEngine;

namespace Deception.Tests
{
    public static class MapAdapterTests
    {
        sealed record Actor(Unit Unit,PolytopiaSpriteRenderer Poly,SpriteRenderer Body,PolytopiaSpriteRenderer Outline,SpriteRenderer Status);
        static readonly Color PolyColor=new(0.8f,0.6f,0.4f,0.8f),BodyColor=new(0.8f,0.6f,0.4f,0.4f),OutlineColor=new(0.2f,0.3f,0.4f,0.9f);
        static void Check(bool value,string message="Map adapter assertion failed") {if(!value)throw new Exception(message);}
        static void Near(float actual,float expected,string message="Map alpha differs")=>Check(Math.Abs(actual-expected)<0.00001f,message+$": {actual} vs {expected}");
        static void NearPoint(Vector3 actual,Vector3 expected,string message)
        {Near(actual.x,expected.x,message+" x");Near(actual.y,expected.y,message+" y");Near(actual.z,expected.z,message+" z");}
        static Vector3 Add(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        static Vector3 Subtract(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        static void Reset()
        {
            MimicMapInspection.Clear();MapRenderer.ProductionCallbacks=false;MimicPresentationState.Clear();
            GameManager.GameState=new();GameManager.LocalPlayer=new();GameManager.Client=new();
            MapRenderer.Current=new();MapRenderer.Pool=new();UIManager.Instance=new();PopupManager.ProductionCallbacks=false;Entry.Notes.Clear();
        }
        static Actor Create(bool locked=true,uint id=3)
        {
            var state=new UnitState{id=id};GameManager.GameState.Units[state.id]=state;
            var unit=new Unit{UnitState=state};unit.transform.position=new(10,20,0);unit.spriteContainer.localScale=new(2,2,1);
            var bodyObject=new GameObject("Body"){layer=4};bodyObject.transform.SetParent(unit.spriteContainer,false);
            var body=(SpriteRenderer)bodyObject.AddComponent(new Il2CppSystem.Type(typeof(SpriteRenderer)));
            var sprite=new Sprite("Warrior body"){rect=new(40,60)};
            body.sprite=sprite;body.bounds=new(new(12,24,0),new(4,6,0));body.sortingLayerID=7;body.sortingOrder=11;
            var poly=new PolytopiaSpriteRenderer{gameObject=bodyObject,Sprite=sprite,spriteRenderer=body,Bounds=body.bounds,SortingLayer=7,SortingOrder=13};
            poly.Color=PolyColor;poly.isColorLocked=locked;body.color=BodyColor;
            var outline=new PolytopiaSpriteRenderer{Sprite=new Sprite("Outline"),SortingLayer=7,SortingOrder=12};
            outline.transform.SetParent(unit.spriteContainer,false);outline.Color=OutlineColor;outline.isColorLocked=locked;
            unit.skinVisuals.visualParts=new(new[]{new SkinVisualsReference.VisualPart{
                renderer=new(){spriteRenderer=body,polytopiaSpriteRenderer=poly},outlineRenderer=new(){polytopiaSpriteRenderer=outline}}});
            unit.SpriteRenderer=poly; // Same renderer appears through both native access paths.
            var status=new SpriteRenderer{sprite=new Sprite("Health decoration")};status.transform.SetParent(unit.transform,false);
            status.color=new(1,0,0,0.75f);MapRenderer.Current!.Register(unit);
            return new(unit,poly,body,outline,status);
        }
        static GameObject[] Children(Actor actor)=>Enumerable.Range(0,actor.Unit.spriteContainer.childCount)
            .Select(i=>actor.Unit.spriteContainer.GetChild(i).gameObject)
            .Where(g=>g.name=="Deception private Mimic"&&!g.Destroyed).ToArray();
        static SpriteRenderer[] Art(Actor actor)=>Children(actor).Single().ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
        static UnitStatusDisplay AttachBadge(Actor actor)
        {
            var status=new UnitStatusDisplay();actor.Unit.statusDisplay=status;status.SetUnit(actor.Unit);
            status.transform.SetParent(actor.Unit.spriteContainer,false);status.typeContainer.localPosition=new(2.4f,3.5f,0);
            var renderers=new[]{status.typeBg,status.typeOutline,status.typeIconShadow,status.typeIcon};
            for(int i=0;i<renderers.Length;i++)
            {
                var renderer=renderers[i];renderer.sprite=new Sprite(i<2?"Native badge framing "+i:"Warrior type badge"){rect=new(40,40)};
                renderer.bounds=new(new(14.8f,27,0),new(0.8f,0.8f,0));renderer.gameObject.layer=9;renderer.sortingLayerID=8;renderer.sortingOrder=30+i;
                renderer.color=new(0.9f,0.8f,0.7f,i==2?0.6f:1);
            }
            status.RefreshColor();status.Show();return status;
        }
        static GameObject[] Badges(UnitStatusDisplay status)=>Enumerable.Range(0,status.typeContainer.childCount)
            .Select(i=>status.typeContainer.GetChild(i).gameObject).Where(g=>g.name=="Deception private Mimic badge"&&!g.Destroyed).ToArray();
        static void Original(Actor actor,Color? body=null)
        {
            Check(actor.Poly.Color==PolyColor&&actor.Body.color==(body??BodyColor)&&actor.Outline.Color==OutlineColor,"Original renderer colors were not restored exactly");
            Check(actor.Status.color==new Color(1,0,0,0.75f),"Map adapter modified status decoration outside visual parts");
        }
        static void UnchangedState(Actor actor)
        {var s=actor.Unit.UnitState;Check(s.health==10&&s.owner==1&&s.type==Polytopia.Data.UnitData.Type.dcp_mimic_form_warrior&&!s.moved&&!s.attacked&&!s.frozen&&GameManager.LocalPlayer.Currency==15,"Private map view mutated gameplay state");}
        public static void Run(Action<string,Action> test)
        {
            void Case(string id,Action action)=>test(id,()=> {
                Reset();try{action();}finally{MimicMapInspection.Clear();MimicMapInspection.Configure(_=>null);MapRenderer.ProductionCallbacks=false;MapRenderer.Current=null;}
            });
            Case("UI-122-outline-follows-native-activity-and-owned-cleanup",()=> {
                var a=Create();MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);
                var child=Children(a).Single();var outlines=child.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name=="Deception actual outline").ToArray();
                Check(outlines.Length==4&&outlines.All(r=>r.color==OutlineColor));
                Check(outlines.All(r=>Art(a).All(body=>r.sortingOrder<body.sortingOrder)));
                Check(outlines.All(r=>!ReferenceEquals(r.sprite,a.Outline.Sprite)),"Disguise outline was reused as actual silhouette");
                var cached=outlines.Select(r=>r.sprite!).ToArray();MapRenderer.ProductionCallbacks=true;
                a.Unit.SetShowOutline(false);Check(outlines.All(r=>!r.gameObject.activeSelf)&&Art(a).Length==4);
                a.Unit.SetShowOutline(true);Check(outlines.All(r=>r.gameObject.activeSelf));
                var underlying=new SpriteRenderer{sprite=a.Outline.Sprite};underlying.transform.SetParent(a.Unit.spriteContainer,false);
                a.Outline.spriteRenderer=underlying;underlying.enabled=false;MimicMapInspection.Tick();
                Check(outlines.All(r=>!r.gameObject.activeSelf),"Disabled native outline was bypassed via PSR");
                a.Outline.spriteRenderer=null;MimicMapInspection.Clear();
                Check(outlines.All(r=>r.gameObject.Destroyed)&&cached.All(sprite=>!sprite.Destroyed&&!sprite.texture.Destroyed));Original(a);UnchangedState(a);
            });
            Case("UI-123-native-overlay-rejects-foreign-renderer-reference",()=> {
                var a=Create();var foreign=new SpriteRenderer{sprite=new Sprite("Other unit")};
                foreign.Properties.SetFloat("_OverlayStrength",0.9f);a.Body.Properties.SetFloat("_OverlayStrength",0.4f);
                a.Unit.skinVisuals.visualParts=new(new[]{new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=foreign}},a.Unit.skinVisuals.visualParts[0]});
                MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);
                Near(Art(a)[0].Properties.GetFloat("_OverlayStrength"),0.4f);
                Check(Art(a)[0].sharedMaterial==a.Body.sharedMaterial&&foreign.Properties.GetFloat("_OverlayStrength")==0.9f);
                MimicMapInspection.Clear();Original(a);UnchangedState(a);
            });
            Case("UI-116-map-turn-continuity-retains-pivot-correct-art",()=> {
                var a=Create();var head=GameManager.Atlas.Head!;var old=head.BoundsCenter;
                head.BoundsCenter=new(1.2f,-0.7f,0);
                try
                {
                    MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);
                    var child=Children(a).Single();var art=Art(a)[2];var layer=UnitArtAssets.Layers(GameManager.LocalPlayer)[2];
                    var center=art.transform.TransformPoint(head.bounds.center);
                    var expected=child.transform.TransformPoint(new(layer.X/100,layer.Y/100,0));
                    NearPoint(center,expected,"Native off-center pivot displaced head");
                    GameManager.GameState.CurrentPlayer=2;MimicMapInspection.Tick();
                    Check(Children(a).Single()==child&&!child.Destroyed&&a.Body.color.a==0,"Opponent turn exposed disguise");
                    GameManager.GameState.CurrentPlayer=1;MimicMapInspection.Tick();Check(Children(a).Single()==child);
                    GameManager.LocalPlayer.Id=2;MimicMapInspection.Tick();Original(a);Check(child.Destroyed,"Viewer change retained private body");
                }
                finally{head.BoundsCenter=old;}
            });
            Case("UI-117-map-copies-native-overlay-only-and-preserves-source",()=> {
                var a=Create();var tinted=new SpriteRenderer{sprite=new Sprite("Tinted native")};tinted.transform.SetParent(a.Unit.spriteContainer,false);
                a.Unit.skinVisuals.visualParts=new(new[]{a.Unit.skinVisuals.visualParts[0],new SkinVisualsReference.VisualPart{tintable=true,renderer=new(){spriteRenderer=tinted}}});
                var fixedColor=new Color(0.2f,0.4f,0.8f,1);var spent=new Color(1,1,1,1);
                a.Body.Properties.SetColor("_OverlayColor",fixedColor);a.Body.Properties.SetFloat("_OverlayStrength",0.2f);
                tinted.Properties.SetColor("_OverlayColor",spent);tinted.Properties.SetFloat("_OverlayStrength",0.4f);
                tinted.Properties.SetFloat("ForeignTextureBinding",42);
                MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var art=Art(a);
                foreach(int n in new[]{0,2,3}){Check(art[n].sharedMaterial==a.Body.sharedMaterial&&art[n].Properties.GetColor("_OverlayColor")==fixedColor);Near(art[n].Properties.GetFloat("_OverlayStrength"),0.2f);}
                Check(art[1].sharedMaterial==tinted.sharedMaterial&&art[1].Properties.GetColor("_OverlayColor")==spent);
                Near(art[1].Properties.GetFloat("_OverlayStrength"),0.4f);Check(!art[1].Properties.Values.ContainsKey("ForeignTextureBinding"));
                tinted.Properties.SetFloat("_OverlayStrength",0);MimicMapInspection.Tick();Near(Art(a)[1].Properties.GetFloat("_OverlayStrength"),0);
                Check(tinted.Properties.GetFloat("ForeignTextureBinding")==42&&a.Body.Properties.GetColor("_OverlayColor")==fixedColor);
                MimicMapInspection.Clear();Original(a);UnchangedState(a);
            });
            Case("UI-103-delayed-head-map-upgrades-with-stable-badges-and-state",()=> {
                SpriteHandle.Delay=true;var a=Create();var status=AttachBadge(a);bool actual=true;
                MimicMapInspection.Configure(_=>actual);MimicMapInspection.Refresh(a.Unit);
                var pending=Children(a).Single();Check(Art(a).Length==1&&ReferenceEquals(Art(a)[0].sprite,UnitArtAssets.Icon()));
                var badge=Badges(status).Single();var badgePosition=badge.transform.localPosition;var nativePosition=status.typeContainer.localPosition;
                Check(a.Body.color.a==0&&badge.activeInHierarchy);
                SpriteHandle.Requested[0].Complete(GameManager.Atlas.Head);MimicMapInspection.Tick();
                Check(pending.Destroyed&&Art(a).Length==4&&Art(a).Last().sprite==UnitArtAssets.Get("mimic_mask"));
                Check(Badges(status).Single()==badge&&badge.transform.localPosition==badgePosition&&status.typeContainer.localPosition==nativePosition);
                actual=false;MimicMapInspection.Refresh(a.Unit);Original(a);UnchangedState(a);
                actual=true;MimicMapInspection.Refresh(a.Unit);Check(Art(a).Length==4&&SpriteHandle.Requested.Count==1);
                GameManager.LocalPlayer.Id=2;MimicMapInspection.Refresh(a.Unit);Check(Children(a).Length==0&&Badges(status).Length==0);Original(a);
                Check(GameManager.Atlas.Head!.DestroyCalls==0);UnchangedState(a);
            });
            Case("UI-068-map-native-color-lock-alpha-and-pool-contracts",()=> {
                var hooks=new HarmonyLib.Harmony();MimicMapInspection.Install(hooks);
                Check(hooks.Patched.Contains("Unit::SetAlpha")&&hooks.Patched.Count(x=>x=="Unit::UpdateObject")==2&&hooks.Patched.Contains("PolytopiaSpriteRenderer::ReturnToPool"));
                var a=Create();a.Poly.Color=new(1,1,1,0);Check(a.Poly.Color==PolyColor&&a.Body.color==BodyColor,"Locked native color accepted ordinary setter");
                var locked=new Color(0.1f,0.2f,0.3f,0.6f);a.Poly.SetUnchangeableColor(locked);
                Check(a.Poly.isColorLocked&&a.Poly.Color==locked&&a.Body.color==locked&&a.Poly.Dirty);
                a.Unit.SetAlpha(0.3f);a.Unit.SetAlpha(0.3f);Near(a.Body.color.a,0.3f);
                Check(a.Poly.Color==locked&&a.Outline.Color==OutlineColor,"Native SetAlpha incorrectly changed PSR backing or outline");
                a.Poly.ReturnToPool();Check(a.Poly.Color==locked&&a.Poly.isColorLocked&&!a.Poly.IsUsed&&!a.Poly.gameObject.activeSelf&&a.Poly.transform.parent==MapRenderer.Pool,"Fixture pool hid a missing color restoration by resetting colors");
            });
            Case("UI-069-map-apparent-alpha-does-not-compound-across-native-refresh",()=> {
                foreach(bool locked in new[]{false,true})
                {
                    Reset();var a=Create(locked);MimicMapInspection.Configure(_=>false);MimicMapInspection.Refresh(a.Unit);
                    for(int i=0;i<4;i++)MimicMapInspection.Tick();
                    Original(a);
                    Check(a.Poly.isColorLocked==locked&&a.Outline.isColorLocked==locked&&Children(a).All(g=>!g.activeInHierarchy));
                    MapRenderer.ProductionCallbacks=true;int updates=0;
                    a.Unit.UpdateVisuals=unit=> {updates++;Original(a);unit.SetAlpha(0.25f);Near(a.Body.color.a,0.25f,"Nested native SetAlpha was projected before outer refresh completed");};
                    a.Unit.UpdateObject();Check(updates==1);Near(a.Body.color.a,0.25f);Check(a.Poly.Color==PolyColor);
                    MimicMapInspection.Tick();Near(a.Body.color.a,0.25f);
                    var replacement=(UnitState)a.Unit.UnitState.MemberwiseClone();a.Unit.UnitState=replacement;GameManager.GameState.Units[replacement.id]=replacement;
                    MimicMapInspection.Tick();Near(a.Body.color.a,0.25f,"State snapshot replacement changed native alpha");
                    MimicMapInspection.Clear();Original(a,BodyColor with{a=0.25f});UnchangedState(a);
                }
            });
            Case("UI-070-map-actual-child-uses-warrior-frame-and-restores-apparent",()=> {
                var a=Create();bool actual=true;MimicMapInspection.Configure(_=>actual);MimicMapInspection.Refresh(a.Unit);
                var child=Children(a).Single();var art=Art(a);var layers=UnitArtAssets.Layers(a.Unit.Owner);
                Check(art.Length==4&&child.activeInHierarchy&&child.transform.parent==a.Unit.spriteContainer);
                for(int i=0;i<art.Length;i++)
                {
                    var image=art[i];var color=(Color)layers[i].Color;
                    Check(ReferenceEquals(image.sprite,layers[i].Sprite)&&image.gameObject.layer==4&&image.sortingLayerID==7&&image.sortingOrder==14+i);
                    Check(image.color==color with{a=color.a*BodyColor.a});
                    Near(image.transform.localPosition.x,layers[i].X/100f);Near(image.transform.localPosition.y,layers[i].Y/100f);
                }
                Check(child.layer==4);
                float left=-.199165656f,right=.113965656f,bottom=-.023454542f,top=.319166667f;
                float fit=Math.Min((right-left)/(UnitArtAssets.CanvasWidth/100f),(top-bottom)/(UnitArtAssets.CanvasHeight/100f));
                Near(child.transform.localPosition.x,(left+right)/2);
                Near(child.transform.localPosition.y,bottom-UnitArtAssets.FootBaseline/100f*fit*.95f);
                Near(child.transform.localScale.x,fit*0.95f);Near(child.transform.localScale.y,fit*0.95f);
                Check(a.Poly.Color==(PolyColor with{a=0})&&a.Body.color==(BodyColor with{a=0})&&a.Outline.Color==(OutlineColor with{a=0}),"Actual map view left the disguise body visible");
                MimicMapInspection.Tick();Check(Children(a).Single()==child);
                actual=false;MimicMapInspection.Refresh(a.Unit);Check(Children(a).All(g=>!g.activeInHierarchy),"Apparent map view retained a second visible body");
                Original(a);MimicMapInspection.Clear();Check(child.Destroyed&&child.transform.parent is null);Original(a);UnchangedState(a);
            });
            Case("UI-071-map-private-view-rejects-ineligible-and-preview-units",()=> {
                foreach(string loss in new[]{"predicate","viewer","replay","spectator","hidden","invisible","map-instance","unregistered","map-missing"})
                {
                    Reset();var a=Create();bool? choice=true;MimicMapInspection.Configure(_=>choice);MimicMapInspection.Refresh(a.Unit);var child=Children(a).Single();var parts=Art(a);var head=GameManager.Atlas.Head!;
                    Check(parts.Length==4);
                    switch(loss)
                    {
                        case "predicate":choice=null;break;
                        case "viewer":GameManager.LocalPlayer.Id=2;break;
                        case "replay":GameManager.Client.IsReplay=true;break;
                        case "spectator":GameManager.Client.IsSpectating=true;break;
                        case "hidden":a.Unit.Visible=false;break;
                        case "invisible":a.Unit.Hidden=true;break;
                        case "map-instance":MapRenderer.Current!.Units[a.Unit.UnitState.id]=new Unit{UnitState=a.Unit.UnitState};break;
                        case "unregistered":GameManager.GameState.Units.Clear();break;
                        case "map-missing":MapRenderer.Current=null;break;
                    }
                    MimicMapInspection.Refresh(a.Unit);Original(a);Check(child.DestroyCalls==1&&!child.activeSelf,"Private child survived guard: "+loss);
                    Check(parts.All(p=>p.gameObject.Destroyed)&&head.DestroyCalls==0&&head.texture.DestroyCalls==0,"Owned layers or borrowed head survived/retired incorrectly: "+loss);
                }
                Reset();var mapActor=Create();var preview=new Unit{UnitState=mapActor.Unit.UnitState};
                preview.skinVisuals=mapActor.Unit.skinVisuals;MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(preview);
                Original(mapActor);Check(Children(mapActor).Length==0,"UI preview triggered projection of real map renderer");
            });
            Case("UI-072-map-pool-return-restores-before-reassignment",()=> {
                var a=Create();MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var child=Children(a).Single();
                MapRenderer.ProductionCallbacks=true;a.Poly.ReturnToPool();Original(a);
                Check(child.Destroyed&&a.Poly.transform.parent==MapRenderer.Pool&&a.Poly.PoolReturns==1);
                var newOwner=new Unit();var nextSprite=new Sprite("Native reused body");var nextColor=new Color(0.9f,0.7f,0.5f,0.6f);
                a.Poly.Sprite=nextSprite;a.Body.sprite=nextSprite;a.Poly.transform.SetParent(newOwner.spriteContainer,false);
                a.Poly.SetUnchangeableColor(nextColor);a.Poly.IsUsed=true;a.Poly.gameObject.SetActive(true);
                MimicMapInspection.Clear();Check(a.Poly.Color==nextColor&&a.Body.color==nextColor,"Retired view recolored renderer owned by another unit");
                // If ownership changes before a cleanup callback, comparisons
                // must decline restoration rather than write an old snapshot.
                Reset();a=Create();MimicMapInspection.Configure(_=>false);MimicMapInspection.Refresh(a.Unit);
                a.Poly.transform.SetParent(newOwner.spriteContainer,false);a.Poly.Sprite=nextSprite;a.Body.sprite=nextSprite;a.Poly.SetUnchangeableColor(nextColor);
                MimicMapInspection.Clear();Check(a.Poly.Color==nextColor&&a.Body.color==nextColor);
            });
            Case("UI-073-map-new-native-colors-and-projection-failures-clean-up",()=> {
                var a=Create();MimicMapInspection.Configure(_=>false);MimicMapInspection.Refresh(a.Unit);
                // Native alpha writes can occur outside the hooked unit call;
                // retain that newer Unity color while restoring lagging PSR.
                var newer=BodyColor with{a=0.2f};a.Body.color=newer;MimicMapInspection.Refresh(a.Unit);Near(a.Body.color.a,0.2f);
                MimicMapInspection.Clear();Original(a,newer);
                Reset();a=Create();a.Body.bounds=new(new(12,24,0),new(0,0,0));MimicMapInspection.Configure(_=>true);
                MimicMapInspection.Refresh(a.Unit);Check(Children(a).Length==1,"Valid PSR bounds did not cover empty underlying Unity bounds");
                Near(Art(a)[0].color.a,BodyColor.a);
                MimicMapInspection.Clear();Original(a);
                Reset();a=Create();MimicMapInspection.Configure(_=>true);
                a.Body.RejectColorWrites=true;
                MimicMapInspection.Refresh(a.Unit);Original(a);Check(Children(a).Length==0,"Rejected native body hide left two visible bodies");
                Reset();a=Create();MimicMapInspection.Configure(_=>true);
                a.Body.bounds=new(new(12,24,0),new(0,0,0));a.Poly.Bounds=a.Body.bounds;
                MimicMapInspection.Refresh(a.Unit);Original(a);Check(Children(a).Length==0,"Failed projection left actual child displayed");
                Check(Entry.Notes.Any(n=>n.Kind=="mimic-map-project-error"));UnchangedState(a);
                Reset();a=Create();MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var owned=Children(a).Single();
                MapRenderer.ProductionCallbacks=true;a.Unit.Destroy();Check(owned.Destroyed&&!owned.activeSelf);Original(a);
            });
            Case("UI-076-ui-duplication-excludes-private-map-renderers",()=> {
                var hooks=new HarmonyLib.Harmony();MimicMapInspection.Install(hooks);
                Check(hooks.Patched.Count(name=>name=="UISpriteDuplicator::RenderSprites")==2,"Both native duplication entry points must be scoped");
                foreach(bool actual in new[]{false,true})foreach(bool unitRoot in new[]{false,true})
                {
                    Reset();var a=Create();var status=AttachBadge(a);var factory=UIManager.Instance.SpriteDuplicator;
                    var bar=new InteractionBar{unit=a.Unit,NativePortraitLayout=true};
                    bar.spriteDuplicationData.source=unitRoot?a.Unit.transform:a.Unit.spriteContainer;bar.spriteDuplicationData.offset=new(13,-4);
                    bar.infoIcon.rectTransform.rect=new(12,12);
                    bar.Show(true,true);
                    var baseline=bar.duplicatedSprites.CopiedImages.Select(c=>(c.Source,c.Sprite,c.Color,c.Rect.rect,c.Rect.anchoredPosition)).ToArray();
                    var nativeInfo=bar.NativeInfoRect;var nativePosition=bar.infoIcon.rectTransform.anchoredPosition;
                    Check(baseline.Length==(unitRoot?7:6)&&bar.duplicatedSprites.CopiedImages.Any(c=>ReferenceEquals(c.Source,a.Body))&&
                        bar.duplicatedSprites.CopiedImages.All(c=>!ReferenceEquals(c.Source,a.Poly))&&
                        bar.duplicatedSprites.CopiedImages.Any(c=>ReferenceEquals(c.Source,a.Status))==unitRoot,"Native fallback lost source boundaries or duplicated an underlying PSR");
                    void NativeCopy(UIDuplicatedSprites container)
                    {
                        Check(container.CopiedImages.Select(c=>(c.Source,c.Sprite,c.Color,c.Rect.rect,c.Rect.anchoredPosition)).SequenceEqual(baseline),"UI portrait copied map-only artwork, tint, or geometry");
                    }
                    MimicMapInspection.Configure(_=>actual);MimicMapInspection.Refresh(a.Unit);
                    var privateRoots=Children(a).Concat(Badges(status)).Where(g=>g.activeInHierarchy).ToArray();
                    Check(privateRoots.Length==(actual?2:1),"Map identity did not produce exactly its body and badge projections");
                    void Detached()=>Check(privateRoots.All(g=>g.transform.parent is null&&!g.activeInHierarchy),"Duplication scope did not detach every owned map projection");
                    void Projected()
                    {
                        Check(privateRoots.All(g=>g.activeInHierarchy&&g.transform.parent is not null),"Map projection was not resumed after copy");
                        if(actual)Check(a.Body.color.a==0&&a.Poly.Color.a==0&&a.Outline.Color.a==0);else Original(a);
                    }
                    // Positive control: unhooked native traversal really can see
                    // the map-only child and let its geometry move the info icon.
                    bar.Show(true,true);
                    Check(bar.duplicatedSprites.CopiedImages.Any(c=>privateRoots.Any(g=>c.Source.transform.IsChildOf(g.transform)))&&bar.NativeInfoRect!=nativeInfo,"Fixture concealed the native private-child contamination");
                    MapRenderer.ProductionCallbacks=true;
                    var nested=new UIDuplicatedSprites();bool entered=false;
                    factory.RenderOverride=data=> {
                        Original(a);Detached();
                        MimicMapInspection.Tick();MimicMapInspection.Refresh(a.Unit);
                        Detached();
                        if(!entered)
                        {
                            entered=true;
                            factory.RenderSprites(bar.spriteDuplicationData.source,nested,data.scale,data.excludeSortingLayer,data.offset,false);
                            Detached();
                        }
                        factory.CopyNativeChildren(data);
                    };
                    bar.Show(true,true);Check(entered);NativeCopy(bar.duplicatedSprites);NativeCopy(nested);
                    Check(bar.NativeInfoRect==nativeInfo&&bar.infoIcon.rectTransform.anchoredPosition==nativePosition&&bar.infoIcon.gameObject.activeInHierarchy,"Info geometry did not use native apparent portrait bounds");
                    Projected();
                    // A native exception must escape unchanged while the outer
                    // finalizer still returns the owned map projection to view.
                    var expected=new InvalidOperationException("Native duplication interrupted");
                    factory.RenderOverride=_=> {Detached();throw expected;};
                    Exception? caught=null;try{factory.RenderSprites(bar.spriteDuplicationData);}catch(Exception e){caught=e;}
                    Check(ReferenceEquals(caught,expected),"Native duplication exception changed");Projected();
                    factory.RenderOverride=null;
                    var popup=new UIUnitRenderer();popup.spriteDuplicationData.source=bar.spriteDuplicationData.source;popup.spriteDuplicationData.offset=bar.spriteDuplicationData.offset;
                    factory.RenderSprites(popup.spriteDuplicationData);NativeCopy(popup.duplicatedSpritesContainer);
                    popup.spriteDuplicationData.forceFullAlpha=true;factory.RenderSprites(popup.spriteDuplicationData);
                    Check(popup.duplicatedSpritesContainer.CopiedImages.Count==baseline.Length&&popup.duplicatedSpritesContainer.CopiedImages.All(c=>c.Color.a==1&&baseline.Any(b=>ReferenceEquals(b.Source,c.Source))),"Full-alpha native popup reintroduced private map art");
                    var preview=new UIUnitRenderer();factory.RenderOverride=data=> {
                        Projected();factory.CopyNativeChildren(data);
                    };
                    factory.RenderSprites(preview.spriteDuplicationData);Check(preview.duplicatedSpritesContainer.CopiedImages.Count==1);Projected();UnchangedState(a);
                }
            });
            Case("UI-078-one-map-body-and-two-owned-type-badges",()=> {
                var a=Create();var status=AttachBadge(a);var other=Create(id:4);var otherStatus=AttachBadge(other);
                bool actual=false;MimicMapInspection.Configure(unit=>ReferenceEquals(unit,a.Unit)?actual:false);
                var native=new[]{status.typeBg,status.typeOutline,status.typeIconShadow,status.typeIcon};
                var containerPosition=status.typeContainer.localPosition;
                var original=native.Select(r=>(r.sprite,r.sortingLayerID,r.sortingOrder,r.transform.localPosition,r.transform.localScale)).ToArray();
                var nativeColors=native.Select(r=>r.color).ToArray();
                void NativeBadgeExpected(bool? rear=null)
                {
                    Check(native.Select(r=>(r.sprite,r.sortingLayerID,r.sortingOrder,r.transform.localPosition,r.transform.localScale)).SequenceEqual(original),"Private badge changed native framing metadata");
                    for(int i=0;i<native.Length;i++)Check(native[i].color==(nativeColors[i] with{a=nativeColors[i].a*((rear??actual)?0.5f:1)}),"Native badge opacity did not match rear/front preference");
                    if(!(rear??actual))Check(status.typeContainer.localPosition==containerPosition,"Native front/cleanup did not restore the original badge anchor");
                }
                void OwnedBadgeExpected()
                {
                    NativeBadgeExpected();var current=Badges(status).Single().ComponentsInChildren<SpriteRenderer>().ToArray();
                    foreach(var (role,index) in new[]{("background",0),("outline",1),("shadow",2),("mask",3)})
                    {
                        var layer=current.SingleOrDefault(r=>r.gameObject.name=="Deception private badge "+role);if(layer is null)continue;
                        var expected=role=="mask"?new Color(1,1,1,nativeColors[index].a):nativeColors[index];
                        expected=expected with{a=expected.a*(actual?1:0.5f)};
                        Check(layer.color==expected,"Owned "+role+" alpha was not multiplied once from native baseline");
                    }
                }
                MimicMapInspection.Tick();Original(a);Original(other);OwnedBadgeExpected();
                var badge=Badges(status).Single();var otherBadge=Badges(otherStatus).Single();
                var layers=badge.ComponentsInChildren<SpriteRenderer>().ToArray();Check(layers.Length==4,"Private badge lost native framing or mask shadow");
                Check(layers.All(r=>r.sortingLayerID==8&&r.sortingOrder<30),"Disguise body preference did not place the native disguise badge in front");
                Check(badge.transform.localPosition.x<0&&badge.transform.localPosition.y>0&&Children(a).All(g=>!g.activeInHierarchy));
                Check(layers.Any(r=>ReferenceEquals(r.sprite,status.typeBg.sprite))&&layers.Any(r=>ReferenceEquals(r.sprite,status.typeOutline.sprite)),"Private badge ignored native framing");
                var otherPosition=otherBadge.transform.localPosition;var otherOrders=otherBadge.ComponentsInChildren<SpriteRenderer>().Select(r=>r.sortingOrder).ToArray();
                var otherColors=otherBadge.ComponentsInChildren<SpriteRenderer>().Select(r=>r.color).ToArray();
                actual=true;MimicMapInspection.Refresh(a.Unit);badge=Badges(status).Single();layers=badge.ComponentsInChildren<SpriteRenderer>().ToArray();
                Check(Children(a).Count(g=>g.activeInHierarchy)==1&&a.Body.color.a==0&&a.Outline.Color.a==0,"Actual preference displayed more than one body");
                Check(layers.All(r=>r.sortingOrder>33)&&badge.transform.localPosition.x>0&&badge.transform.localPosition.y<0,"Actual body preference did not place its type badge in front");
                Check(Badges(otherStatus).Single()==otherBadge&&otherBadge.transform.localPosition==otherPosition&&otherBadge.ComponentsInChildren<SpriteRenderer>().Select(r=>r.sortingOrder).SequenceEqual(otherOrders)&&otherBadge.ComponentsInChildren<SpriteRenderer>().Select(r=>r.color).SequenceEqual(otherColors));
                Original(other);OwnedBadgeExpected();Check(a.Status.color==new Color(1,0,0,0.75f),"Body inspection changed a native health marker");
                for(int i=0;i<5;i++){MimicMapInspection.Tick();OwnedBadgeExpected();Near(Art(a)[0].color.a,BodyColor.a);}
                // Native SpriteHandle completion can replace the icon/shadow
                // sprite without changing the role's color or native owner.
                status.SetTypeSprite(new Sprite("Asynchronously refreshed Warrior badge"));
                original=native.Select(r=>(r.sprite,r.sortingLayerID,r.sortingOrder,r.transform.localPosition,r.transform.localScale)).ToArray();
                for(int i=0;i<3;i++){MimicMapInspection.Tick();OwnedBadgeExpected();Near(status.typeIcon.color.a,0.5f);}
                actual=false;MimicMapInspection.Refresh(a.Unit);Original(a);OwnedBadgeExpected();
                for(int i=0;i<3;i++){MimicMapInspection.Tick();Original(a);OwnedBadgeExpected();}
                actual=true;MimicMapInspection.Refresh(a.Unit);OwnedBadgeExpected();
                MapRenderer.ProductionCallbacks=true;
                foreach(var source in new[]{status.transform,status.typeContainer})
                {
                    var copy=new UISpriteDuplicator.SpriteDuplicationData{source=source};
                    UIManager.Instance.SpriteDuplicator.RenderSprites(copy);
                    Check(copy.container.CopiedImages.Count==4&&copy.container.CopiedImages.All(c=>native.Contains(c.Source)),"Direct native badge copy included a private badge layer");
                    foreach(var copied in copy.container.CopiedImages)Check(copied.Color==nativeColors[Array.FindIndex(native,r=>ReferenceEquals(r,copied.Source))],"UI copy inherited rear badge opacity");
                    Check(Badges(status).Single().activeInHierarchy&&a.Body.color.a==0);OwnedBadgeExpected();
                }
                // Native SetState toggles the type outline independently of its
                // parent container. A private clone must not invent that marker.
                status.typeOutline.gameObject.SetActive(false);status.SetState(a.Unit.UnitState);
                Check(!status.typeOutline.gameObject.activeSelf&&Badges(status).Single().ComponentsInChildren<SpriteRenderer>().All(r=>r.gameObject.name!="Deception private badge outline"),"Private badge exposed a native outline that SetState had hidden");
                status.typeOutline.gameObject.SetActive(true);status.SetState(a.Unit.UnitState);
                Check(Badges(status).Single().ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name=="Deception private badge outline"));OwnedBadgeExpected();
                status.typeIconShadow.enabled=false;status.RefreshColor();
                Check(!status.typeIconShadow.enabled&&Badges(status).Single().ComponentsInChildren<SpriteRenderer>().All(r=>r.gameObject.name!="Deception private badge shadow"),"Private badge exposed a disabled native shadow");
                status.typeIconShadow.enabled=true;status.RefreshColor();
                Check(Badges(status).Single().ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name=="Deception private badge shadow"));
                // Native body opacity updates apply once; the separate native
                // type badge retains its own opacity and metadata.
                a.Unit.SetAlpha(0.25f);Check(a.Body.color.a==0);
                Near(Art(a)[0].color.a,0.25f);OwnedBadgeExpected();
                status.typeIcon.color=status.typeIcon.color with{a=0.45f};status.NativePlayerColor=new(0.3f,0.5f,0.8f,1);status.RefreshColor();
                nativeColors[3]=nativeColors[3] with{a=0.45f};nativeColors[0]=status.NativePlayerColor;
                layers=Badges(status).Single().ComponentsInChildren<SpriteRenderer>().ToArray();
                Near(layers.Single(r=>r.gameObject.name=="Deception private badge mask").color.a,0.45f);
                Check(layers.Single(r=>r.gameObject.name=="Deception private badge background").color==status.NativePlayerColor);
                OwnedBadgeExpected();
                // A native alpha equal to our previous projected value must
                // still become the new baseline, proving prefix restoration.
                status.NativePlayerColor=status.NativePlayerColor with{a=0.5f};status.RefreshColor();nativeColors[0]=status.NativePlayerColor;
                OwnedBadgeExpected();Near(status.typeBg.color.a,0.25f);
                for(int i=0;i<4;i++){MimicMapInspection.Tick();OwnedBadgeExpected();Near(Art(a)[0].color.a,0.25f);}
                MapRenderer.ProductionCallbacks=false;
                foreach(string loss in new[]{"unused","hidden","different-owner","missing"})
                {
                    if(loss=="unused")status.IsUsed=false;
                    if(loss=="hidden")status.typeContainer.gameObject.SetActive(false);
                    if(loss=="different-owner"){status.unit=other.Unit;status.typeBg.color=new(0.1f,0.9f,0.2f,0.8f);}
                    if(loss=="missing")a.Unit.statusDisplay=null;
                    MimicMapInspection.Refresh(a.Unit);Check(Badges(status).Length==0,"Private badge survived lost native status ownership: "+loss);
                    Check(Children(a).Count(g=>g.activeInHierarchy)==1,"Unavailable native badge incorrectly removed selected body");
                    if(loss!="different-owner")NativeBadgeExpected(false);
                    else Check(status.typeBg.color==new Color(0.1f,0.9f,0.2f,0.8f),"Cleanup recolored a badge already reassigned to another native unit");
                    status.IsUsed=true;status.typeContainer.gameObject.SetActive(true);status.unit=a.Unit;a.Unit.statusDisplay=status;
                    if(loss=="different-owner")
                    {for(int i=0;i<native.Length;i++)native[i].color=nativeColors[i];status.typeContainer.localPosition=containerPosition;} // Native reassignment repopulates its own display.
                    MimicMapInspection.Refresh(a.Unit);Check(Badges(status).Length==1);
                }
                badge=Badges(status).Single();MapRenderer.ProductionCallbacks=true;status.ReturnToPool();
                Check(badge.Destroyed&&badge.transform.parent is null&&!status.IsUsed&&status.unit==a.Unit&&status.transform.parent==MapRenderer.Pool,"Native status pooling left a private badge on a reusable object");
                Original(a,BodyColor with{a=0.25f});NativeBadgeExpected(false);
                status.SetUnit(other.Unit);other.Unit.statusDisplay=status;status.transform.SetParent(other.Unit.spriteContainer,false);status.Show();
                var nextSprite=new Sprite("Rider type badge");status.SetTypeSprite(nextSprite);status.NativePlayerColor=new(0.8f,0.2f,0.1f,1);status.RefreshColor();
                MimicMapInspection.Clear();Check(status.typeIcon.sprite==nextSprite&&status.typeIconShadow.sprite==nextSprite&&status.typeBg.color==status.NativePlayerColor,"Retired private badge rewrote the next native owner's icon");
                Reset();a=Create();status=AttachBadge(a);MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);badge=Badges(status).Single();
                GameManager.LocalPlayer.Id=2;MimicMapInspection.Refresh(a.Unit);Check(badge.Destroyed&&Badges(status).Length==0&&Children(a).Length==0);Original(a);
                Reset();a=Create();status=AttachBadge(a);var cleanColors=new[]{status.typeBg,status.typeOutline,status.typeIconShadow,status.typeIcon}.Select(r=>r.color).ToArray();
                MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);status.ThrowState=true;MapRenderer.ProductionCallbacks=true;
                bool failed=false;try{status.SetState(a.Unit.UnitState);}catch(InvalidOperationException){failed=true;}
                Check(failed&&Badges(status).Length==0&&Children(a).Length==0,"Failed native status operation retained private projection or suppression");
                Check(new[]{status.typeBg,status.typeOutline,status.typeIconShadow,status.typeIcon}.Select(r=>r.color).SequenceEqual(cleanColors));Original(a);
            });
            Case("UI-139-rear-small-native-glyph-remains-outside-foreground-disk",()=> {
                var a=Create();var status=AttachBadge(a);var origin=status.typeBg.bounds.center;
                status.typeIcon.bounds=new(origin,new(.16f,.16f,0));status.typeIconShadow.bounds=new(origin,new(.16f,.16f,0));
                var glyph=status.typeIcon.sprite;var nativeColor=status.typeIcon.color;bool actual=true;
                MimicMapInspection.Configure(_=>actual);
                for(int i=0;i<8;i++)
                {
                    actual=(i%2)==0;MimicMapInspection.Refresh(a.Unit);
                    var background=Badges(status).Single().ComponentsInChildren<SpriteRenderer>().Single(r=>r.gameObject.name=="Deception private badge background");
                    if(actual)
                    {
                        var c=status.typeIcon.bounds.center;var front=background.bounds.center;
                        float distance=(float)Math.Sqrt((c.x-front.x)*(c.x-front.x)+(c.y-front.y)*(c.y-front.y));
                        // A small white glyph needs more than a visible colored rim.
                        // Its entire central half must clear the foreground disk.
                        Check(distance>background.bounds.size.x/2+status.typeIcon.bounds.size.x/4,"Opaque foreground disk hides rear glyph core");
                        Check(status.typeIcon.color==(nativeColor with{a=nativeColor.a*.5f}));
                    }
                    Check(ReferenceEquals(status.typeIcon.sprite,glyph)&&status.typeIcon.enabled&&status.typeIcon.gameObject.activeInHierarchy);
                }
                MimicMapInspection.Clear();Check(status.typeIcon.color==nativeColor&&ReferenceEquals(status.typeIcon.sprite,glyph));
            });
            Case("UI-079-fixed-badge-slots-swap-identities",()=> {
                var a=Create();var status=AttachBadge(a);var group=status.typeContainer;
                status.transform.localPosition=new(-0.3f,0.2f,0);status.transform.localScale=new(0.75f,1.25f,1);
                group.localScale=new(1.5f,0.8f,1);
                status.typeBg.transform.localPosition=new(0.05f,-0.02f,0);
                status.typeIcon.transform.localPosition=new(0.02f,0.03f,0);
                status.typeIconShadow.transform.localPosition=new(0.01f,-0.02f,0.01f);
                var nested=new Transform{localPosition=new(0.025f,-0.03f,0),localScale=new(1.2f,0.7f,1)};
                nested.SetParent(group,false);status.typeOutline.transform.SetParent(nested,false);status.typeOutline.transform.localPosition=new(-0.015f,0.01f,0);
                var native=new[]{status.typeBg,status.typeOutline,status.typeIconShadow,status.typeIcon};
                var localPos=native.Select(r=>r.transform.localPosition).ToArray();var localScale=native.Select(r=>r.transform.localScale).ToArray();
                var centers=native.Select(r=>r.bounds.center).ToArray();var sizes=native.Select(r=>r.bounds.size).ToArray();var colors=native.Select(r=>r.color).ToArray();
                var groupOriginal=group.localPosition;var groupWorld=group.position;var groupScale=group.localScale;
                var low=group.InverseTransformPoint(status.typeBg.bounds.min);var high=group.InverseTransformPoint(status.typeBg.bounds.max);
                var rear=new Vector3(-(high.x-low.x)*0.65f,(high.y-low.y)*0.40f,0);
                var rearWorld=Subtract(group.TransformPoint(rear),group.TransformPoint(new(0,0,0)));
                var frontSlot=centers[0];var rearSlot=Add(frontSlot,rearWorld);
                var rearGroupLocal=group.parent!.InverseTransformPoint(Add(groupWorld,rearWorld));
                var factory=UIManager.Instance.SpriteDuplicator;var data=new UISpriteDuplicator.SpriteDuplicationData{source=a.Unit.transform};
                factory.RenderSprites(data);
                var copiedBaseline=data.container.CopiedImages.Select(c=>(c.Source,c.Color,c.Rect.rect,c.Rect.anchoredPosition)).ToArray();
                bool actual=false;MimicMapInspection.Configure(_=>actual);MapRenderer.ProductionCallbacks=true;
                void Slots()
                {
                    var badge=Badges(status).Single();var background=badge.ComponentsInChildren<SpriteRenderer>().Single(r=>r.gameObject.name=="Deception private badge background");
                    NearPoint(status.typeBg.bounds.center,actual?rearSlot:frontSlot,"Native identity changed its fixed slot");
                    NearPoint(background.bounds.center,actual?frontSlot:rearSlot,"Actual identity changed its fixed slot");
                    NearPoint(group.localPosition,actual?rearGroupLocal:groupOriginal,"Native group local position drifted");
                    NearPoint(badge.transform.localPosition,actual?new(-rear.x,-rear.y,-rear.z):rear,"Private group counter-offset drifted");
                    Check(group.localScale==groupScale);
                    for(int i=0;i<native.Length;i++)
                    {
                        Check(native[i].transform.localPosition==localPos[i]&&native[i].transform.localScale==localScale[i],"Badge swap changed a native role's internal geometry");
                        NearPoint(native[i].bounds.center,actual?Add(centers[i],rearWorld):centers[i],"Badge roles did not move as one group");
                        NearPoint(native[i].bounds.size,sizes[i],"Badge slot changed native role size");
                        Check(native[i].color==(colors[i] with{a=colors[i].a*(actual?0.5f:1)}));
                    }
                    Check(background.color==(colors[0] with{a=colors[0].a*(actual?1:0.5f)}));
                    Check(actual?background.sortingOrder>native.Max(r=>r.sortingOrder):background.sortingOrder<native.Min(r=>r.sortingOrder));
                    if(actual)Near(Art(a)[0].color.a,BodyColor.a);else Original(a);
                }
                foreach(bool mode in new[]{false,true,false,true,true,false})
                {
                    actual=mode;MimicMapInspection.Refresh(a.Unit);Slots();
                    for(int i=0;i<4;i++){MimicMapInspection.Tick();Slots();}
                    factory.RenderSprites(data);
                    Check(data.container.CopiedImages.Select(c=>(c.Source,c.Color,c.Rect.rect,c.Rect.anchoredPosition)).SequenceEqual(copiedBaseline),"Native UI copy inherited a badge slot offset or rear opacity");Slots();
                }
                actual=true;MimicMapInspection.Refresh(a.Unit);factory.ThrowRestore=true;bool threw=false;
                try{factory.RenderSprites(data);}catch(InvalidOperationException){threw=true;}finally{factory.ThrowRestore=false;}
                Check(threw);Slots();
                status.ReturnToPool();Check(Badges(status).Length==0&&Children(a).Length==0&&group.localPosition==groupOriginal);
                for(int i=0;i<native.Length;i++)Check(native[i].transform.localPosition==localPos[i]&&native[i].color==colors[i],"Pool cleanup failed to restore native badge geometry/color");
                var next=Create(id:4);status.SetUnit(next.Unit);status.transform.SetParent(next.Unit.spriteContainer,false);
                group.localPosition=new(5,6,0);var nextPosition=group.localPosition;MimicMapInspection.Clear();
                Check(group.localPosition==nextPosition,"Retired slot snapshot moved a new owner's native badge");
            });
        }
    }
}
