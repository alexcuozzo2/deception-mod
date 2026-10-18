// Actual category adapter + provider; synthetic native factory/vertex surface.
// This proves managed ownership and scale invariants, not in-game readability.
using System;
using System.Linq;
using Deception.PolyModAdapter;
using Polytopia.Data;
using UnityEngine;

namespace Deception.Tests
{
    public static class TechArtAdapterTests
    {
        static void Check(bool value,string detail="Category artwork assertion failed")
        {if(!value)throw new Exception(detail);}
        static void Near(float a,float b)=>Check(Math.Abs(a-b)<0.001f,$"Geometry differs: {a} vs {b}");
        static Transform[] Children(UIUnitRenderer r)=>Enumerable.Range(0,r.duplicatedSpritesContainer.rectTransform.childCount)
            .Select(i=>r.duplicatedSpritesContainer.rectTransform.GetChild(i)).ToArray();
        static UnityEngine.UI.Image[] Images(UIUnitRenderer r)=>Children(r).Select(t=>t.gameObject.FindComponent<UnityEngine.UI.Image>()!).ToArray();
        static UIUnitRenderer Make(UnitData.Type type=UnitData.Type.dcp_mimic)
        {
            UIUtils.CategoryCallbacks=true;
            try{var result=UIUtils.GetUIUnitRenderer(new(type),GameManager.LocalPlayer,GameManager.LocalPlayer.GetVisualSkin());NativeFit(result);return result;}
            finally{UIUtils.CategoryCallbacks=false;}
        }
        static void NativeFit(UIUnitRenderer r)
        {
            var b=UIUtils.GetImageVerticesBounds(r.rectTransform);float fit=60/Math.Max(b.size.x,b.size.y);
            r.rectTransform.localScale=new(fit,fit,1);r.rectTransform.anchoredPosition=new(-b.center.x*fit,-b.center.y*fit);
        }
        static void SameNativeExtent(UIUnitRenderer r)
        {
            var b=UIUtils.GetImageVerticesBounds(r.rectTransform);float fit=r.rectTransform.localScale.x;
            Near(Math.Max(b.size.x,b.size.y)*fit,60);
            Near(r.rectTransform.anchoredPosition.x+b.center.x*fit,0);Near(r.rectTransform.anchoredPosition.y+b.center.y*fit,0);
            Check(Children(r).All(c=>c.localScale==Vector3.one),"Category still applies the invisible-to-native-fitter child scaling");
            // Model rendered rectangles independently of the native fitter. The
            // old code passes its own bounds but draws under one pixel here.
            var body=Images(r)[0];float drawn=body.rectTransform.rect.width*fit;
            Check(drawn>12&&drawn<=80,$"The fitted body is effectively blank or oversized: {drawn}px");
        }
        static Bounds RenderedCanvasBounds(UIUnitRenderer r)
        {
            // Independent drawing envelope. The actual atlas head remains in
            // Image.activeSprite, and is drawn within its configured Image rect.
            var images=Images(r);float left=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            foreach(var image in images)
            {
                Check(image.activeSprite is not null&&image.color.a>0&&image.gameObject.activeSelf);
                var rect=image.rectTransform;left=Math.Min(left,rect.anchoredPosition.x-rect.rect.width/2);right=Math.Max(right,rect.anchoredPosition.x+rect.rect.width/2);
                bottom=Math.Min(bottom,rect.anchoredPosition.y-rect.rect.height/2);top=Math.Max(top,rect.anchoredPosition.y+rect.rect.height/2);
            }
            return new(new((left+right)/2,(bottom+top)/2,0),new(right-left,top-bottom,0));
        }
        static void HeadFits(UIUnitRenderer r)
        {
            var measured=UIUtils.GetImageVerticesBounds(r.rectTransform);var drawn=RenderedCanvasBounds(r);
            Near(measured.size.x,drawn.size.x);Near(measured.size.y,drawn.size.y);Near(measured.center.x,drawn.center.x);Near(measured.center.y,drawn.center.y);
            float scale=r.rectTransform.localScale.x;Near(Math.Max(drawn.size.x,drawn.size.y)*scale,60);
            Near(r.rectTransform.anchoredPosition.y+drawn.center.y*scale,0);
            var head=Images(r)[2];Check(head.sprite!.MeshType==SpriteMeshType.FullRect);
            Check(ReferenceEquals(head.activeSprite,GameManager.Atlas.Head)&&ReferenceEquals(head.mainTexture,GameManager.Atlas.Head!.texture));
            Check(!ReferenceEquals(head.sprite.texture,head.mainTexture),"Layout-only pixels became the rendered head texture");
        }
        public static void Run(Action<string,Action> test)
        {
            test("UI-124-category-png-views-use-fullrect-and-reuse-cache",()=>{
                // Warm the provider before this measurement: this case isolates
                // category PPU views rather than making a timing claim.
                var layers=UnitArtAssets.Layers(GameManager.LocalPlayer);_=UnitArtAssets.Icon();
                var sources=layers.Select(l=>(Sprite)l.Sprite).ToArray();
                var rectangles=sources.Select(x=>x.rect).ToArray();var ppus=sources.Select(x=>x.PixelsPerUnit).ToArray();
                float prior=UIConstants.UI_PIXELS_PER_UNIT;
                try
                {
                    UIConstants.UI_PIXELS_PER_UNIT=313;int count=Sprite.CreateModes.Count;
                    var first=Make();var views=Images(first);
                    Check(Sprite.CreateModes.Count-count==4&&Sprite.CreateModes.Skip(count).All(m=>m==SpriteMeshType.FullRect),
                        "Category regenerated a Tight contour or allocated an unexpected view");
                    Check(new[]{0,1,3}.All(i=>views[i].sprite!.MeshType==SpriteMeshType.FullRect));
                    SameNativeExtent(first);HeadFits(first);
                    count=Sprite.CreateModes.Count;var second=Make();
                    Check(Sprite.CreateModes.Count==count,"Unchanged category repeated sprite construction");
                    Check(Images(second).Select(x=>x.sprite).SequenceEqual(views.Select(x=>x.sprite)));
                    Check(sources.Select(x=>x.rect).SequenceEqual(rectangles)&&sources.Select(x=>x.PixelsPerUnit).SequenceEqual(ppus));
                    Check(sources.All(x=>x.DestroyCalls==0&&x.texture.DestroyCalls==0));
                }
                finally{UIConstants.UI_PIXELS_PER_UNIT=prior;}
            });
            test("UI-125-category-fullrect-pending-upgrade-preserves-fit-and-head",()=>{
                SpriteHandle.Delay=true;float prior=UIConstants.UI_PIXELS_PER_UNIT;
                try
                {
                    UIConstants.UI_PIXELS_PER_UNIT=317;var renderer=Make();var pending=Images(renderer).Single();
                    Check(pending.sprite!.MeshType==SpriteMeshType.FullRect);SameNativeExtent(renderer);
                    int count=Sprite.CreateModes.Count;var head=GameManager.Atlas.Head!;
                    var rect=head.rect;float ppu=head.PixelsPerUnit;
                    SpriteHandle.Requested.Last().Complete(head);MimicControls.Tick();
                    Check(Sprite.CreateModes.Count-count==4&&Sprite.CreateModes.Skip(count).All(m=>m==SpriteMeshType.FullRect));
                    SameNativeExtent(renderer);HeadFits(renderer);
                    Check(pending.gameObject.Destroyed&&Images(renderer)[2].activeSprite==head&&head.rect==rect&&head.PixelsPerUnit==ppu);
                    Check(head.DestroyCalls==0&&head.texture.DestroyCalls==0&&MimicControls.PendingPreviewCount==0);
                }
                finally{UIConstants.UI_PIXELS_PER_UNIT=prior;}
            });
            test("UI-106-category-factory-replaces-body-at-native-unit-scale",()=>{
                var hooks=new HarmonyLib.Harmony();MimicControls.Install(hooks);
                Check(hooks.Patched.Contains("UIUtils::GetUIUnitRenderer"));
                var ordinary=Make(UnitData.Type.warrior);Check(Images(ordinary).Single().sprite!.Name=="Warrior body");
                var genuine=Images(ordinary).Single();var renderer=Make();
                var images=Images(renderer);var layers=UnitArtAssets.Layers(GameManager.LocalPlayer);
                Check(images.Length==4&&images.Select(i=>i.mainTexture).SequenceEqual(layers.Select(l=>((Sprite)l.Sprite).texture)));
                Check(renderer.duplicatedSpritesContainer.CopiedImages.Single().Rect.gameObject.Destroyed);
                Check(genuine&&!genuine.gameObject.Destroyed&&genuine.sprite!.Name=="Warrior body");
                SameNativeExtent(renderer);HeadFits(renderer);
                var button=new UIRoundButton();button.SetUnitRenderer(renderer);MimicControls.RendererAssigned(button,renderer);
                Check(ReferenceEquals(button.Icon,renderer)&&renderer.gameObject.activeSelf&&button.Text=="Mimic"&&button.SpriteCalls==0);
                var reward=new UIRoundButton_UI2();var originalSprite=new Sprite("Native hidden image");reward.Sprite=originalSprite;reward.SpriteScale=2.3f;
                renderer.transform.SetParent(reward.IconContainer,false);var parent=renderer.transform.parent;
                MimicControls.TechCategory(reward,new UnitData(UnitData.Type.dcp_mimic));
                Check(reward.Text=="Mimic"&&ReferenceEquals(reward.Sprite,originalSprite)&&reward.SpriteScale==2.3f&&ReferenceEquals(parent,renderer.transform.parent));
                Check(GameManager.Atlas.Head!.DestroyCalls==0&&GameManager.LocalPlayer.Currency==15);
            });
            test("UI-107-pending-category-mask-upgrades-without-native-resize",()=>{
                SpriteHandle.Delay=true;var renderer=Make();var before=Children(renderer);var source=renderer.spriteDuplicationData.source;
                Check(Images(renderer).Length==1&&ReferenceEquals(Images(renderer)[0].sprite!.texture,((Sprite)UnitArtAssets.Icon()).texture)&&MimicControls.PendingPreviewCount==1);
                SameNativeExtent(renderer);MimicControls.Tick();Check(Children(renderer).SequenceEqual(before));
                SpriteHandle.Requested.Last().Complete(GameManager.Atlas.Head);MimicControls.Tick();
                Check(Images(renderer).Length==4&&MimicControls.PendingPreviewCount==0&&before.All(x=>x.gameObject.Destroyed));
                SameNativeExtent(renderer);HeadFits(renderer);var now=Children(renderer);MimicControls.Tick();Check(Children(renderer).SequenceEqual(now));
                Check(ReferenceEquals(source,renderer.spriteDuplicationData.source)&&source.gameObject.Destroyed,"Native preview retained its discarded source");
                Check(GameManager.Atlas.Head!.DestroyCalls==0);
            });
            test("UI-108-pending-category-rejects-reuse-owner-and-child-changes",()=>{
                foreach(string change in new[]{"type","owner-color","skin","world","container","child","destroy"})
                {
                    MimicControls.Clear();SpriteHandle.Reset();UnitArtAssets.ClearNativeHeads();GameManager.GameState=new();GameManager.LocalPlayer=new();
                    SpriteHandle.Delay=true;var renderer=Make();var old=Children(renderer);int factory=UIManager.Instance.SpriteDuplicator.FactoryCalls;
                    switch(change)
                    {
                        case "type":renderer.unitType=UnitData.Type.warrior;break;
                        case "owner-color":GameManager.LocalPlayer.color=0x123456;break;
                        case "skin":GameManager.LocalPlayer.skinType=PolytopiaBackendBase.Common.SkinType.TestSkin;break;
                        case "world":GameManager.GameState=new();break;
                        case "container":renderer.duplicatedSpritesContainer=new();break;
                        case "child":renderer.duplicatedSpritesContainer.rectTransform.Append(new RectTransform());break;
                        case "destroy":UnityEngine.Object.Destroy(renderer.gameObject);break;
                    }
                    SpriteHandle.Requested.Last().Complete(GameManager.Atlas.Head);MimicControls.Tick();
                    Check(MimicControls.PendingPreviewCount==0&&UIManager.Instance.SpriteDuplicator.FactoryCalls==factory,
                        "Late head completion rewrote a reused category: "+change);
                    if(change!="destroy")Check(old.All(c=>!c.gameObject.Destroyed));
                    Check(GameManager.Atlas.Head!.DestroyCalls==0);
                }
                MimicControls.Clear();SpriteHandle.Reset();
            });
            test("UI-109-category-stage-failure-retains-native-source-and-retires-partials",()=>{
                var factory=UIManager.Instance.SpriteDuplicator;factory.FailAtFactoryCall=factory.FactoryCalls+3;
                int start=factory.FactoryImages.Count,roots=BranchTechFixture.Images.Count,calls=factory.FactoryCalls;
                var renderer=Make();factory.FailAtFactoryCall=-1;
                Check(Images(renderer).Length==1&&Images(renderer)[0].sprite!.Name=="Warrior body");
                Check(!renderer.duplicatedSpritesContainer.CopiedImages.Single().Rect.gameObject.Destroyed);
                Check(factory.FactoryCalls==calls+3&&factory.FactoryImages.Count==start+3&&BranchTechFixture.Images.Count==roots+1,
                    "The requested later-layer failure boundary was not reached");
                Check(factory.FactoryImages.Skip(start).All(i=>i.gameObject.Destroyed)&&BranchTechFixture.Images.Skip(roots).All(i=>i.gameObject.Destroyed));
                Check(MimicControls.PendingPreviewCount==0&&Entry.Notes.Any(n=>n.Kind=="mimic-category-art-error"&&n.Message.Contains("Injected partial image factory failure")));
                Check(GameManager.Atlas.Head!.DestroyCalls==0&&GameManager.Atlas.Head.texture.DestroyCalls==0);
                // A completion failure after temporary reparenting must restore
                // the previously visible glyph, not leave the destination blank.
                GameManager.GameState=new();SpriteHandle.Reset();UnitArtAssets.ClearNativeHeads();SpriteHandle.Delay=true;
                var pending=Make();var old=Children(pending);var scale=pending.rectTransform.localScale;var position=pending.rectTransform.anchoredPosition;
                UIUtils.FailBoundsRoot=pending.rectTransform;UIUtils.FailBoundsImageCount=4;
                int created=factory.FactoryImages.Count;
                try{SpriteHandle.Requested.Last().Complete(GameManager.Atlas.Head);MimicControls.Tick();}
                finally{UIUtils.FailBoundsRoot=null;UIUtils.FailBoundsImageCount=0;}
                Check(Children(pending).SequenceEqual(old)&&old.All(x=>!x.gameObject.Destroyed),"Final-fit rejection lost the previous visible glyph");
                Check(pending.rectTransform.localScale==scale&&pending.rectTransform.anchoredPosition==position);
                Check(factory.FactoryImages.Skip(created).Count()==4&&factory.FactoryImages.Skip(created).All(x=>x.gameObject.Destroyed));
                Check(Entry.Notes.Any(n=>n.Message.Contains("Injected final category fit failure"))&&MimicControls.PendingPreviewCount==0);
            });
            test("UI-112-native-vertex-fitter-rejects-old-double-shrink-assumption",()=>{
                var root=new RectTransform();var sprite=new Sprite("large team"){rect=new(3870,3870),PixelsPerUnit=100};
                var child=new RectTransform{rect=new(1024,1024),localScale=new(60f/3870,60f/3870,1)};root.Append(child);
                _=new UnityEngine.UI.Image{gameObject=child.gameObject,sprite=sprite};
                var b=UIUtils.GetImageVerticesBounds(root);Near(b.size.x,3870);float fit=60/b.size.x;
                Check(1024*child.localScale.x*fit<1,"Regression no longer reaches the actual double-shrink boundary");
                child.localScale=Vector3.one;child.rect=new(10,10);Near(UIUtils.GetImageVerticesBounds(root).size.x,3870);
                sprite.PixelsPerUnit=100f*3870/1024;Near(UIUtils.GetImageVerticesBounds(root).size.x,1024);
            });
            test("UI-113-category-views-preserve-sources-and-key-native-ppu",()=>{
                var source=(Sprite)UnitArtAssets.Get("mimic-body-team");float sourcePpu=source.PixelsPerUnit;var head=GameManager.Atlas.Head!;
                var a=Make();var first=Images(a)[1].sprite!;
                Check(!ReferenceEquals(first,source)&&ReferenceEquals(first.texture,source.texture));
                Near(first.PixelsPerUnit,100f*3870/Images(a)[1].rectTransform.rect.width);Near(source.PixelsPerUnit,sourcePpu);
                Check(ReferenceEquals(Images(a)[2].activeSprite,head)&&!ReferenceEquals(Images(a)[2].sprite,head)&&head.DestroyCalls==0);
                try{UIConstants.UI_PIXELS_PER_UNIT=200;var b=Make();var second=Images(b)[1].sprite!;
                    Check(!ReferenceEquals(first,second));Near(second.PixelsPerUnit,200f*3870/Images(b)[1].rectTransform.rect.width);SameNativeExtent(b);}
                finally{UIConstants.UI_PIXELS_PER_UNIT=100;}
                Check(source.DestroyCalls==0&&source.texture.DestroyCalls==0&&head.DestroyCalls==0);
            });
            test("UI-114-category-head-fit-uses-rendered-size-not-atlas-ppu",()=>{
                var original=GameManager.Atlas.Head;
                try
                {
                    foreach(var shape in new[]{(64f,64f,2112f),(512f,256f,10f),(37f,100f,100f)})
                    {
                        GameManager.GameState=new();SpriteHandle.Reset();UnitArtAssets.ClearNativeHeads();MimicControls.Clear();
                        var head=new Sprite("Packed native head"){rect=new(shape.Item1,shape.Item2),PixelsPerUnit=shape.Item3,texture=new(){SourceName="Borrowed head atlas"}};
                        GameManager.Atlas.Head=head;var beforeRect=head.rect;var beforePpu=head.PixelsPerUnit;
                        var renderer=Make();HeadFits(renderer);
                        var image=Images(renderer)[2];var guide=image.sprite!;
                        // Reproduce the prior missing-head-envelope error without
                        // changing the source: only the owned image binding moves.
                        image.sprite=head;var wrong=UIUtils.GetImageVerticesBounds(renderer.rectTransform);image.sprite=guide;
                        var correct=UIUtils.GetImageVerticesBounds(renderer.rectTransform);
                        Check(Math.Abs(wrong.size.y-correct.size.y)>1||Math.Abs(wrong.size.x-correct.size.x)>1);
                        Check(head.rect==beforeRect&&head.PixelsPerUnit==beforePpu&&head.DestroyCalls==0&&head.texture.DestroyCalls==0);
                        var ordinary=Make(UnitData.Type.warrior);Check(Images(ordinary)[0].activeSprite!.Name=="Warrior body");
                    }
                }
                finally{GameManager.Atlas.Head=original;GameManager.GameState=new();SpriteHandle.Reset();UnitArtAssets.ClearNativeHeads();MimicControls.Clear();}
            });
            test("UI-115-category-head-override-failure-preserves-pending-glyph",()=>{
                SpriteHandle.Delay=true;var renderer=Make();var before=Children(renderer);var scale=renderer.rectTransform.localScale;
                int created=UIManager.Instance.SpriteDuplicator.FactoryImages.Count;
                UnityEngine.UI.Image.FailOverrideSprite=true;
                try{SpriteHandle.Requested.Last().Complete(GameManager.Atlas.Head);MimicControls.Tick();}
                finally{UnityEngine.UI.Image.FailOverrideSprite=false;}
                Check(Children(renderer).SequenceEqual(before)&&before.All(x=>!x.gameObject.Destroyed));
                Check(renderer.rectTransform.localScale==scale&&MimicControls.PendingPreviewCount==0);
                var parts=UIManager.Instance.SpriteDuplicator.FactoryImages.Skip(created).ToArray();
                Check(parts.Length==4&&parts.All(x=>x.gameObject.Destroyed));
                Check(GameManager.Atlas.Head!.DestroyCalls==0&&GameManager.Atlas.Head.texture.DestroyCalls==0);
                Check(Entry.Notes.Any(n=>n.Message.Contains("Injected head override failure")));
            });
        }
    }
}
