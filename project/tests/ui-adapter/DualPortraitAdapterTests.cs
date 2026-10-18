// Production dual-portrait code with copied Image/atlas-mesh fixtures. These
// checks establish geometry/ownership behavior, not native-screen acceptance.
using System;
using System.Collections.Generic;
using System.Linq;
using Deception.PolyModAdapter;
using Deception.Core;
using Polytopia.Data;
using UnityEngine;
using Image=UnityEngine.UI.Image;

namespace Deception.Tests
{
    public static class DualPortraitAdapterTests
    {
        static void Check(bool value,string message="Dual portrait assertion failed")
        {if(!value)throw new Exception(message);}
        static bool Near(float a,float b)=>Math.Abs(a-b)<0.002f;
        static void Reject(Action work)
        {bool rejected=false;try{work();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Invalid portrait was accepted");}
        readonly record struct Frame(float L,float B,float R,float T)
        {public float Width=>R-L;public float Height=>T-B;public float X=>(L+R)/2;public float Y=>(B+T)/2;}
        static Sprite Tight(string name,float width,float height,float l,float b,float r,float t)
        {
            var sprite=new Sprite(name){rect=new(width,height),PixelsPerUnit=100,BoundsCenter=new(.13f,-.21f,0)};
            var bounds=sprite.bounds;
            sprite.FixtureVertices=new[]{new Vector2(bounds.min.x+l*bounds.size.x,bounds.min.y+b*bounds.size.y),
                new Vector2(bounds.min.x+r*bounds.size.x,bounds.min.y+b*bounds.size.y),
                new Vector2(bounds.min.x+r*bounds.size.x,bounds.min.y+t*bounds.size.y),
                new Vector2(bounds.min.x+l*bounds.size.x,bounds.min.y+t*bounds.size.y)};
            return sprite;
        }
        sealed class Scene:IDisposable
        {
            public readonly Unit Unit=new(){UnitState=new(){owner=GameManager.LocalPlayer.Id}};
            public readonly UIDuplicatedSprites Container=new();
            public readonly UISpriteDuplicator.SpriteDuplicationData Data;
            public readonly Sprite Body=Tight("Native body",400,600,.2f,.15f,.8f,.85f);
            public readonly Sprite Head=Tight("Native head",330,470,.08f,.2f,.9f,.9f);
            public readonly Sprite Overlay=new("Native outline / unrelated decoration"){rect=new(5000,8000)};
            public readonly List<Image> Copies=new();
            public bool AddOverlay=true;public bool BadMesh;public Action? DuringCopy;
            public MimicProjection Projection=new(1,1,"dcp_mimic_form_warrior","warrior","Warrior",1,1,10,10,new UnitData(UnitData.Type.dcp_mimic_form_warrior),new UnitData(UnitData.Type.warrior),GameManager.GameState);
            public Material PreviewMaterial=new(){shader=new(){name="Polytopia/Sprite"}};
            public PlayerState? PreviewPlayer; public PolytopiaBackendBase.Common.SkinType PreviewSkin;
            public Scene()
            {
                Unit.transform.localPosition=new(7,8,9);Unit.spriteContainer.localScale=new(2,3,4);
                var body=new SpriteRenderer{sprite=Body,color=new(.6f,.4f,.2f,.35f)};body.transform.SetParent(Unit.spriteContainer,false);
                var head=new SpriteRenderer{sprite=Head,color=new(.2f,.5f,.8f,.9f)};head.transform.SetParent(Unit.spriteContainer,false);
                var outline=new SpriteRenderer{sprite=Overlay};outline.transform.SetParent(Unit.spriteContainer,false);
                Unit.skinVisuals.visualParts=new(new[]{new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=body},outlineRenderer=new(){spriteRenderer=outline}},
                    new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=head}}});
                var destination=Container.rectTransform;destination.rect=new(-80,-50,160,100);destination.localScale=new(.25f,.25f,.25f);
                Data=new(){source=Unit.spriteContainer,container=Container,offset=new(23,-45),scale=.002f};
                void Populate(UISpriteDuplicator.SpriteDuplicationData d,bool preview)
                {
                    Copies.Clear();
                    Image Add(Sprite s,Rect area,Vector2 position,Vector3 scale,Color color)
                    {
                        var rect=new RectTransform{rect=area,anchoredPosition=position,localScale=scale};d.container.rectTransform.Append(rect);
                        var image=new Image{gameObject=rect.gameObject,sprite=s,color=color,material=PreviewMaterial};Copies.Add(image);return image;
                    }
                    Add(Body,new(-200,-300,400,600),new(0,-120),new(1.4f,.8f,1),body.color);
                    Add(Head,new(-165,-235,330,470),new(20,300),new(-1,1,1),head.color);
                    if(!preview&&AddOverlay)Add(Overlay,new(-2500,-4000,5000,8000),new(3000,-2000),Vector3.one,new(1,0,0,.4f));
                    if(BadMesh)Body.FixtureVertices![0]=new(float.NaN,0);
                    if(preview)DuringCopy?.Invoke();
                }
                UIManager.Instance.SpriteDuplicator.RenderOverride=d=>Populate(d,false);
                UIUtils.PreviewOverride=(renderer,data,player,skin)=>
                {PreviewPlayer=player;PreviewSkin=skin;Populate(renderer.spriteDuplicationData,true);};

            }
            public void Apply(bool actual)=>MimicDualPortrait.Apply(Container,Data,actual,Unit.Owner,Unit,Projection);
            public void Dispose()
            {
                UIUtils.PreviewOverride=null;var d=UIManager.Instance.SpriteDuplicator;d.RenderOverride=null;d.ExtraFactoryChild=false;d.FailAtFactoryCall=-1;
                UnityEngine.Object.Destroy(Container.gameObject);UnityEngine.Object.Destroy(Unit.gameObject);
                UnityEngine.Object.Destroy(Body);UnityEngine.Object.Destroy(Head);UnityEngine.Object.Destroy(Overlay);
            }
        }
        static Image[] Actual(Scene s)=>Enumerable.Range(0,s.Container.rectTransform.childCount)
            .Select(i=>s.Container.rectTransform.GetChild(i).gameObject.FindComponent<Image>())
            .Where(i=>i is not null&&!s.Copies.Contains(i)).Cast<Image>().ToArray();
        static Frame Visible(IEnumerable<Image> images)
        {
            float left=float.PositiveInfinity,bottom=float.PositiveInfinity,right=float.NegativeInfinity,top=float.NegativeInfinity;
            foreach(var image in images)
            {
                var sprite=image.sprite!;var rect=(RectTransform)image.transform;var area=rect.rect;
                float l,b,r,t;
                // Independent pinned source-pixel bounds, same source facts used
                // for actual UI fitting. No fixture PNG decoder claims alpha.
                if(ReferenceEquals(sprite,UnitArtAssets.Get("mimic-body-fixed"))){l=204f/1024;b=142f/1024;r=812f/1024;t=556f/1024;}
                else if(ReferenceEquals(sprite,UnitArtAssets.Get("mimic-body-team"))){l=633f/3870;b=1160f/3870;r=3101f/3870;t=3004f/3870;}
                else if(ReferenceEquals(sprite,UnitArtAssets.Get("mimic_mask"))){l=8f/590;b=10f/790;r=584f/590;t=785f/790;}
                else if(ReferenceEquals(sprite,UnitArtAssets.Icon())){l=5f/494;b=5f/525;r=488f/494;t=520f/525;}
                else
                {
                    var vertices=sprite.FixtureVertices??new[]{new Vector2(sprite.bounds.min.x,sprite.bounds.min.y),new Vector2(sprite.bounds.max.x,sprite.bounds.max.y)};
                    l=(vertices.Min(v=>v.x)*sprite.pixelsPerUnit+sprite.pivot.x)/sprite.rect.width;r=(vertices.Max(v=>v.x)*sprite.pixelsPerUnit+sprite.pivot.x)/sprite.rect.width;
                    b=(vertices.Min(v=>v.y)*sprite.pixelsPerUnit+sprite.pivot.y)/sprite.rect.height;t=(vertices.Max(v=>v.y)*sprite.pixelsPerUnit+sprite.pivot.y)/sprite.rect.height;
                }
                foreach(float x in new[]{l,r})foreach(float y in new[]{b,t})
                {
                    float px=rect.anchoredPosition.x+(area.x+x*area.width)*rect.localScale.x;
                    float py=rect.anchoredPosition.y+(area.y+y*area.height)*rect.localScale.y;
                    left=Math.Min(left,px);right=Math.Max(right,px);bottom=Math.Min(bottom,py);top=Math.Max(top,py);
                }
            }
            return new(left,bottom,right,top);
        }
        static string Geometry(IEnumerable<Image> images)=>string.Join(";",images.Select(i=>{
            var r=(RectTransform)i.transform;return $"{r.rect.x:R},{r.rect.y:R},{r.rect.width:R},{r.rect.height:R}|{r.anchoredPosition.x:R},{r.anchoredPosition.y:R}|{r.localScale.x:R},{r.localScale.y:R},{r.localScale.z:R}|{i.color}";}));
        static void Layout(Scene s,bool? focus=null)
        {
            var a=Visible(Actual(s));var d=Visible(s.Copies.Take(2));var r=s.Container.rectTransform.rect;
            Check(Near(a.Height,d.Height),"Actual/disguise visible heights differ");
            Check(a.Height<=r.height*.68f+.002f&&d.Height>0,"Portrait exceeds bounded target height");
            var left=a.X<d.X?a:d;var right=a.X<d.X?d:a;
            Check(Near(right.L-left.R,r.width*.06f),"Portraits overlap or gap is inconsistent");
            Check(Near((left.L+right.R)/2,r.x+r.width/2)&&Near((a.Y+d.Y)/2,r.y+r.height/2),"Visible pair is not centered");
            Check(Near(left.Y-right.Y,r.height*.08f),"Focused portrait is not slightly lower");
            Check(left.L>=r.x&&right.R<=r.x+r.width&&a.B>=r.y&&a.T<=r.y+r.height,"Visible portrait exceeds its native slot");
            if(focus is bool actual)Check((a.X>d.X)==actual,"Focused identity did not occupy the front/right position");
            bool actualFocus=a.X>d.X;
            Check(Actual(s).All(i=>Near(i.color.a,actualFocus?1:.55f)&&!i.raycastTarget),"Actual UI focus alpha differs");
            Check(s.Copies.Take(2).All(i=>Near(i.color.a,actualFocus?198f/255:1f)&&!i.raycastTarget),"Disguise UI encoded focus alpha differs");
        }
        public static void Run(Action<string,Action> test)
        {
            test("UI-077-dual-portrait-copies-fit-order-and-native-restoration",()=>{
                MimicDualPortrait.Validate();using var s=new Scene();var layouts=new Dictionary<bool,(string Native,string Actual)>();float? actualHeight=null;
                foreach(bool focus in new[]{false,true,false,true})
                {
                    s.Apply(focus);Layout(s,focus);var actual=Actual(s);var root=s.Container.rectTransform;
                    Check(root.childCount==s.Copies.Count+actual.Length,"Repeated focus accumulated children");
                    Check(ReferenceEquals(root.GetChild(focus?0:actual.Length),s.Copies[0].transform),"Native body order changed");
                    var layers=UnitArtAssets.Layers(s.Unit.Owner);
                    Check(actual.Select(i=>i.sprite).SequenceEqual(layers.Select(l=>(Sprite)l.Sprite)),"Actual layer order changed");
                    var current=(Geometry(s.Copies.Take(2)),Geometry(actual));
                    if(layouts.TryGetValue(focus,out var prior))Check(prior==current,"Repeated focus changed layout");
                    layouts[focus]=current;actualHeight??=Visible(actual).Height;
                    Check(Near(actualHeight.Value,Visible(actual).Height),"Focus changed portrait height");
                    Check(s.Copies.Count==2,"Map outline participated in genuine preview");
                    Check(UIUtils.Previews.Last().gameObject.Destroyed&&s.Copies.All(i=>!i.gameObject.Destroyed),"Temporary preview destruction affected transferred images");
                    Check(root.localScale==new Vector3(.25f,.25f,.25f)&&root.SizeWrites==0&&s.Unit.transform.localPosition==new Vector3(7,8,9)&&
                        s.Unit.spriteContainer.localScale==new Vector3(2,3,4)&&s.Data.offset==new Vector2(23,-45)&&s.Data.scale==.002f,"Source/container/data mutated");
                    Check(s.Unit.skinVisuals.visualParts[0].renderer.spriteRenderer!.color==new Color(.6f,.4f,.2f,.35f),"Source native alpha changed");
                }
                var duplicator=UIManager.Instance.SpriteDuplicator;
                duplicator.ExtraFactoryChild=true;Reject(()=>s.Apply(true));
                Check(s.Container.rectTransform.childCount==3&&s.Copies[0].transform.localScale==new Vector3(1.4f,.8f,1)&&s.Copies[2].gameObject.activeSelf,"Failed projection did not restore native copies");
                duplicator.ExtraFactoryChild=false;duplicator.FailAtFactoryCall=duplicator.FactoryCalls+3;int start=duplicator.FactoryImages.Count;
                Reject(()=>s.Apply(false));Check(s.Container.rectTransform.childCount==3,"Partial actual image survived factory failure");
                Check(duplicator.FactoryImages.Skip(start).All(i=>i.gameObject.Destroyed));
                Check(s.Body.DestroyCalls==0&&s.Head.DestroyCalls==0&&s.Overlay.DestroyCalls==0,"Borrowed native sprites were destroyed");
            });
            test("UI-127-dual-visible-padding-overlay-exclusion-and-wide-fit",()=>{
                using var s=new Scene();s.Apply(false);Layout(s);string native=Geometry(s.Copies.Take(2)),actual=Geometry(Actual(s));
                s.Body.FixtureBounds=new(new(10,20,0),new(.01f,.02f,0));
                s.Head.FixtureBounds=new(new(-7,9,0),new(.02f,.01f,0));
                s.AddOverlay=false;s.Apply(false);Layout(s);
                Check(native==Geometry(s.Copies)&&actual==Geometry(Actual(s)),"Outline or Sprite.bounds tightness affected body fit");
                // Very wide copy must reduce both heights together, not distort
                // one body or overflow; a non-centered native rect stays valid.
                s.Container.rectTransform.rect=new(10,20,90,140);
                s.DuringCopy=()=>((RectTransform)s.Copies[0].transform).localScale=new(10,.8f,1);
                s.Apply(true);Layout(s);Check(Visible(Actual(s)).Height<90*.68f,"Wide disguise did not constrain common fit");
            });
            test("UI-128-dual-pending-head-completion-keeps-opacity-and-focus-stable",()=>{
                GameManager.GameState=new();SpriteHandle.Delay=true;using var s=new Scene();s.Apply(false);Layout(s);
                Check(Actual(s).Length==1,"Pending native head did not use one complete identity glyph");
                float pendingHeight=Visible(Actual(s)).Height;s.Apply(true);Layout(s,true);Check(Near(pendingHeight,Visible(Actual(s)).Height),"Pending glyph resized on focus");
                SpriteHandle.Requested.Last().Complete(Tight("Ready padded native head",330,470,.1f,.2f,.9f,.9f));
                s.Apply(true);Layout(s);Check(Actual(s).Length==4,"Head completion did not replace glyph with complete layers");
                float readyHeight=Visible(Actual(s)).Height;s.Apply(false);Layout(s,false);Check(Near(readyHeight,Visible(Actual(s)).Height),"Ready composition resized on focus");
            });
            test("UI-140-dual-native-preview-restores-full-color-and-copied-profile",()=>{
                using var s=new Scene();var owner=s.Unit.Owner;var oldColor=owner.color;var oldTribe=owner.tribe;
                s.Unit.UnitState.moved=s.Unit.UnitState.attacked=true;
                var source=s.Unit.skinVisuals.visualParts[0].renderer.spriteRenderer!;source.Properties.SetFloat("_OverlayStrength",.4f);
                s.Projection=s.Projection with{Appearance=new("warrior",new("Bardur","TestSkin"),new("Bardur","TestSkin"),new("Imperius","Default"),
                    0x123456,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>())};
                foreach(bool focus in new[]{false,true,false})
                {
                    s.Apply(focus);Layout(s,focus);
                    Check(!ReferenceEquals(s.PreviewPlayer,owner)&&s.PreviewPlayer!.color==0x123456&&s.PreviewPlayer.tribe==PolytopiaBackendBase.Common.TribeType.Bardur&&
                        s.PreviewSkin==PolytopiaBackendBase.Common.SkinType.TestSkin,"Recorded preview profile was lost");
                    foreach(var image in s.Copies)
                    {
                        int packed=(int)Math.Round(image.color.a*255f);
                        Check((packed&128)!=0&&Math.Abs((packed&127)/127f-(focus?.55f:1))<.005f,"Native preview remains washed out or lost its focus opacity");
                        Check(image.material.OverlayStrength==0&&ReferenceEquals(image.material,s.PreviewMaterial),"Preview readiness overlay or borrowed material changed");
                    }
                    Check(owner.color==oldColor&&owner.tribe==oldTribe&&owner.Deception,"Live owner was changed");
                    Check(source.Properties.GetFloat("_OverlayStrength")==.4f&&s.Unit.UnitState.moved&&s.Unit.UnitState.attacked,"Map readiness was changed while clearing UI washout");
                    Check(UIUtils.Previews.Last().spriteDuplicationData.source.gameObject.Destroyed,"Fixture retained native temporary Unit source");
                }
                s.PreviewMaterial.shader.name="UI/Default";s.Apply(true);
                Check(s.Copies.All(i=>Near(i.color.a,.55f)),"Ordinary UI shader received encoded alpha");
                Il2CppSystem.Object.AliasClone=true;int count=UIUtils.Previews.Count;
                try{Reject(()=>s.Apply(false));Check(UIUtils.Previews.Count==count,"Aliased owner clone reached native allocation");}
                finally{Il2CppSystem.Object.AliasClone=false;}
            });
            test("UI-144-dual-cosmetic-enum-members-are-distinct-from-registry-ids",()=>{
                using var s=new Scene();var owner=s.Unit.Owner;var originalTribe=owner.tribe;var originalColor=owner.color;
                LedgerAppearance Profile(string tribe,string skin)=>new("warrior",new(tribe,skin),new(tribe,skin),new(tribe,skin),
                    0x123456,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
                // A native registry lookup must not accidentally stand in for
                // exact CLR enum names captured by the appearance ledger.
                Reject(()=>Native.EnumValue("PolytopiaBackendBase.Common.TribeType","Elyrion"));
                foreach(var tribe in Enum.GetValues<PolytopiaBackendBase.Common.TribeType>())
                foreach(var skin in Enum.GetValues<PolytopiaBackendBase.Common.SkinType>())
                {
                    s.Projection=s.Projection with{Appearance=Profile(tribe.ToString(),skin.ToString())};
                    s.Apply(false);Layout(s,false);
                    Check(s.PreviewPlayer!.tribe==tribe&&s.PreviewSkin==skin&&!ReferenceEquals(s.PreviewPlayer,owner));
                    Check(UIUtils.Previews.Last().gameObject.Destroyed&&s.Copies.All(i=>!i.gameObject.Destroyed));
                }
                int count=UIUtils.Previews.Count;string existing=Geometry(s.Copies)+Geometry(Actual(s));
                foreach(var pair in new[]{("elyrion","Default"),("1","Default"),("NoTribe","Default"),
                    ("Elyrion","default"),("Elyrion","0"),("Elyrion","NoSkin")})
                {
                    s.Projection=s.Projection with{Appearance=Profile(pair.Item1,pair.Item2)};
                    Reject(()=>s.Apply(true));
                    Check(UIUtils.Previews.Count==count,"Invalid cosmetic allocated a native preview");
                    Check(existing==Geometry(s.Copies)+Geometry(Actual(s)),"Invalid cosmetic disturbed the displayed pair");
                    Check(owner.tribe==originalTribe&&owner.color==originalColor,"Invalid cosmetic changed live ownership visuals");
                }
            });
            test("UI-129-dual-rejects-stale-unit-and-invalid-mesh-before-half-publication",()=>{
                using var s=new Scene();s.Apply(false);var other=new Unit();
                Reject(()=>MimicDualPortrait.Apply(s.Container,s.Data,true,s.Unit.Owner,other,s.Projection));
                var original=s.Body.FixtureVertices!.ToArray();s.BadMesh=true;Reject(()=>s.Apply(true));
                Check(UIUtils.Previews.Last().gameObject.Destroyed,"Invalid mesh leaked temporary preview");
                s.BadMesh=false;s.Body.FixtureVertices=original;
                s.DuringCopy=()=>s.Data.source=new Transform();
                Reject(()=>s.Apply(true));Check(UIUtils.Previews.Last().gameObject.Destroyed,"Replaced bar source leaked temporary preview");
                UnityEngine.Object.Destroy(other.gameObject);
            });
        }
    }
}
