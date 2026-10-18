// Managed factory/ownership/geometry checks. Native rendering remains Alex's
// playtest responsibility; PNG pixels stay byte-identical in embedded sources.
using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Deception.Tests
{
    public static class BranchArtAdapterTests
    {
        static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
        static readonly (string Tech,bool Unlock,string Name,float W,float H)[] Routes={
            (TechTree.PropagandaId,false,"propaganda_mask",576,775),
            (TechTree.PropagandaId,true,"propaganda_mask",576,775),
            (TechTree.CounterintelligenceId,false,"fogger_fixed",938,674),
            (TechTree.CounterintelligenceId,true,"fogger_fixed",938,674),
            (TechTree.IntelligenceId,false,"intelligence_icon",2001,3219),
            (TechTree.IntelligenceId,true,"intelligence_icon",2001,3219)};
        public static void Run(Action<string,Action> test)
        {
            test("UI-145-branch-art-routes-and-normalizes-visible-frame",()=>{
                var reference=(Sprite)MimicControls.NativeSprite("UNKNOWN_ICON");
                float target=Math.Max(reference.rect.width,reference.rect.height)/reference.pixelsPerUnit;
                foreach(var r in Routes)
                {
                    var source=(Sprite)UnitArtAssets.Get(r.Name);var view=(Sprite)BranchArtAssets.Get(r.Tech,r.Unlock);
                    Check(BranchArtAssets.AssetName(r.Tech,r.Unlock)==r.Name,"Wrong tech/unlock artwork route");
                    bool glow=false;
                    Check(!ReferenceEquals(source,view)&&(glow?!ReferenceEquals(source.texture,view.texture):ReferenceEquals(source.texture,view.texture)),"Wrong normalized/glow artwork ownership");
                    Check(source.rect.width==r.W&&source.rect.height==r.H&&(glow||source.rect==view.rect),"User frame crop changed during normalization");
                    Check(Near(Math.Max(view.bounds.size.x,view.bounds.size.y),target),"Multi-thousand-pixel source escaped native icon bound");
                    Check((glow||Near(view.bounds.size.x/view.bounds.size.y,r.W/r.H))&&view.MeshType==SpriteMeshType.FullRect,"Icon aspect or native measurable geometry changed");
                    if(glow)Check(view.rect.width<=256&&view.rect.height<=256&&view.texture.MadeNonReadable,"Glow texture escaped its bounded immutable runtime frame");
                    var image=new UnityEngine.UI.Image{sprite=view};image.SetNativeSize();
                    Check(Near(Math.Max(image.rectTransform.rect.width,image.rectTransform.rect.height),target*100),"Image and native vertex fitting disagree");
                    UnityEngine.Object.Destroy(image.gameObject);Check(view&&view.texture&&source,"Retiring a widget damaged shared artwork");
                }
                Check(((Sprite)BranchArtAssets.Get(TechTree.EspionageId,false)).Name=="UNKNOWN_ICON","Unprovided technology lost native fallback");
            });
            test("UI-146-branch-art-cache-is-bounded-across-research-refreshes",()=>{
                var views=new object[Routes.Length];for(int i=0;i<Routes.Length;i++)views[i]=BranchArtAssets.Get(Routes[i].Tech,Routes[i].Unlock);
                int loads=ImageConversion.Loads,creates=Sprite.CreateModes.Count;
                for(int n=0;n<200;n++)for(int i=0;i<Routes.Length;i++)
                    Check(ReferenceEquals(views[i],BranchArtAssets.Get(Routes[i].Tech,Routes[i].Unlock)),"Idle refresh replaced cached artwork");
                Check(loads==ImageConversion.Loads&&creates==Sprite.CreateModes.Count,"Idle refresh decoded a texture or allocated a Sprite");
                Check(ReferenceEquals(views[0],views[1])&&ReferenceEquals(views[2],views[3])&&ReferenceEquals(views[4],views[5]),"Tree and unlock should share the three branch artwork views");
            });
            test("UI-149-disguise-preview-traced-glow-without-solid-backplate",()=>{
                UIBasicComponent result=null!;
                NativeBranchTechUi.PopupContent(new TechData{type=TechData.Type.dcp_propaganda},ref result);
                var button=result.gameObject.ComponentsInChildren<UIRoundButton_UI2>().Last();var background=button.BG;
                Check(background.transform.childCount==0,"Disguise tech unlock still has a solid white backdrop");
                Check(ReferenceEquals(button.Sprite,BranchArtAssets.DisguiseTech()),"Preview must use 3D mask, not flat action icon");
                var flat=(Sprite)BranchArtAssets.DisguiseAction();var source=(Sprite)UnitArtAssets.Get("disguise_icon");
                Check(ReferenceEquals(flat.texture,source.texture)&&!ReferenceEquals(flat,button.Sprite),"Flat mask is not independent action artwork");
                var circle=background.sprite!;UnityEngine.Object.Destroy(button.gameObject);
                Check(circle&&circle.texture&&flat&&button.Sprite!,"Button retirement damaged shared artwork");
                var action=new UIRoundButton_UI2();NativeButtonArt.WhiteBackground(action);
                Check(action.BG.transform.childCount==1,"Flat action's separately requested white backing was removed");
            });
            test("UI-150-white-backplate-rejects-foreign-background-before-allocation",()=>{
                var button=new UIRoundButton_UI2();var unrelated=new UIRoundButton_UI2();button.BG=unrelated.BG;
                int count=BranchTechFixture.Images.Count;bool denied=false;
                try{NativeButtonArt.WhiteBackground(button);}catch(InvalidOperationException){denied=true;}
                Check(denied&&count==BranchTechFixture.Images.Count&&unrelated.BG.transform.childCount==0,"Foreign background was changed or a partial Image leaked");
                UnityEngine.Object.Destroy(button.gameObject);Check(unrelated.BG,"Borrowed foreign button was destroyed");
            });
            test("UI-147-branch-art-source-retirement-and-texture-loss-are-safe",()=>{
                const string name="propaganda_mask";var source=(Sprite)UnitArtAssets.Get(name);var view=(Sprite)BranchArtAssets.Get(TechTree.PropagandaId,false);
                var texture=source.texture;var existing=new UnityEngine.UI.Image{sprite=view};
                UnityEngine.Object.Destroy(source);
                Check(view&&texture&&ReferenceEquals(BranchArtAssets.Get(TechTree.PropagandaId,true),view),"Source Sprite retirement destroyed a published independent view");
                var replacement=(Sprite)UnitArtAssets.Get(name);
                Check(!ReferenceEquals(source,replacement)&&ReferenceEquals(BranchArtAssets.Get(TechTree.PropagandaId,false),view)&&existing.sprite==view,
                    "Immutable source replacement invalidated a still-live published widget");
                UnityEngine.Object.Destroy(texture);var repaired=(Sprite)BranchArtAssets.Get(TechTree.PropagandaId,false);
                Check(!ReferenceEquals(view,repaired)&&view.DestroyCalls==1&&texture.DestroyCalls==1&&source.DestroyCalls==1,"Stale owned wrapper repair damaged borrowed assets");
                Check(repaired&&repaired.texture&&ReferenceEquals(repaired.texture,replacement.texture),"Dead texture was reused for the replacement view");
                UnityEngine.Object.Destroy(existing.gameObject);
            });
            test("UI-148-branch-art-invalid-fit-does-not-publish-and-recovers",()=>{
                var view=(Sprite)BranchArtAssets.Get(TechTree.IntelligenceId,false);UnityEngine.Object.Destroy(view);
                GameManager.Atlas.UnknownBounds=new(0,0);
                var result=new Il2CppReferenceArray<RectTransform>(0);var before=result;int creates=Sprite.CreateModes.Count;
                bool denied=false;try{NativeBranchTechUi.UnlockItems(new TechData{type=TechData.Type.dcp_intelligence},ref result);}catch(InvalidOperationException){denied=true;}
                Check(denied&&ReferenceEquals(before,result)&&Sprite.CreateModes.Count==creates,"Invalid normalization published partial output");
                Check(BranchTechFixture.Images[^1].gameObject.DestroyCalls==1,"Failed owned image leaked");
                GameManager.Atlas.UnknownBounds=new(20,20);
                NativeBranchTechUi.UnlockItems(new TechData{type=TechData.Type.dcp_intelligence},ref result);
                Check(result.Length==2&&result[0]&&result[1]&&result[0].gameObject.FindComponent<UnityEngine.UI.Image>()!.sprite!&&result[1].gameObject.FindComponent<UnityEngine.UI.Image>()!.sprite!,"Repaired Intelligence ability icons were empty");
                UnityEngine.Object.Destroy(result[0].gameObject);
            });
            test("UI-161-tech-glow-traces-alpha-preserves-face-and-transparent-corners",()=>{
                const int n=64;var pixels=new byte[n*n*4];
                // White RGB under transparent pixels must never seed a square.
                for(int i=0;i<n*n;i++){pixels[i*4]=pixels[i*4+1]=pixels[i*4+2]=255;}
                for(int y=8;y<56;y++)for(int x=8;x<56;x++)
                    if(Math.Abs(x-32)+Math.Abs(y-32)<22)
                    {int i=(y*n+x)*4;pixels[i]=24;pixels[i+1]=48;pixels[i+2]=72;pixels[i+3]=255;}
                var original=(byte[])pixels.Clone();var output=TechIconGlow.Compose(pixels,n,n,n,n);int w=n+2*TechIconGlow.Padding;
                int center=((32+TechIconGlow.Padding)*w+32+TechIconGlow.Padding)*4;
                Check(output.Skip(center).Take(4).SequenceEqual(new byte[]{24,48,72,255}),"Mask face was whitened or filled");
                int ring=((32+TechIconGlow.Padding)*w+55+TechIconGlow.Padding)*4;
                Check(output[ring]==255&&output[ring+1]==255&&output[ring+2]==255&&output[ring+3]>0,"White glow does not follow the diamond edge");
                Check(output[0+3]==0&&output[(TechIconGlow.Padding*w+TechIconGlow.Padding)*4+3]==0,
                    "Transparent frame/canvas became a white square");
                Check(Enumerable.Range(0,w).All(i=>output[i*4+3]==0&&output[((w-1)*w+i)*4+3]==0&&output[(i*w)*4+3]==0&&output[(i*w+w-1)*4+3]==0),"Glow clipped at output bounds");
                Check(pixels.SequenceEqual(original),"Runtime glow changed source pixels");
                var linear=TechIconGlow.Compose(pixels,n,n,n,n,true);
                Check(linear[center]>output[center]&&linear[center+2]>output[center+2]&&linear[center+3]==255,"Linear readback was not encoded for the sRGB output texture");
                bool denied=false;try{TechIconGlow.Compose(new byte[4],1,1,1,1);}catch(InvalidOperationException){denied=true;}
                Check(denied,"Empty alpha produced a solid fallback background");
            });
            test("UI-162-tech-glow-cache-owns-one-result-and-failure-is-bounded",()=>{
                var source=new Sprite("tech glow isolated"){rect=new(32,48),texture=new Texture2D(32,48,TextureFormat.RGBA32,false)};
                var input=source.texture;var beforeTarget=new RenderTexture();RenderTexture.active=beforeTarget;
                var first=(Sprite)TechIconGlow.Get(source);int reads=Texture2D.Readbacks.Count,allocations=Texture2D.Allocations.Count,creates=Sprite.CreateModes.Count;
                for(int n=0;n<200;n++)Check(ReferenceEquals(first,TechIconGlow.Get(source)),"Cached tech glow identity changed");
                Check(reads==Texture2D.Readbacks.Count&&allocations==Texture2D.Allocations.Count&&creates==Sprite.CreateModes.Count,"Idle tech refresh read GPU or allocated textures");
                Check(RenderTexture.active==beforeTarget&&source&&input,"GPU crop changed prior target/source ownership");
                var secondSource=new Sprite("tech glow replacement"){rect=new(32,48),texture=new Texture2D(32,48,TextureFormat.RGBA32,false)};
                var output=first.texture;var second=(Sprite)TechIconGlow.Get(secondSource);
                Check(first.DestroyCalls==1&&output.DestroyCalls==1&&source&&input&&second&&second.texture,"Replacing cache accumulated owned textures or destroyed source art");
                var failed=new Sprite("tech glow failed"){rect=new(32,48),texture=new Texture2D(32,48,TextureFormat.RGBA32,false)};
                int logged=Entry.Notes.Count(n=>n.Kind=="tech-mask-glow-unavailable");
                Graphics.Failure="read";object fallback;try{fallback=TechIconGlow.Get(failed);}finally{Graphics.Failure=null;}
                int readFailures=Texture2D.Readbacks.Count;
                for(int n=0;n<20;n++)Check(ReferenceEquals(TechIconGlow.Get(failed),failed),"Failed source did not retain the original icon fallback");
                Check(ReferenceEquals(fallback,failed)&&readFailures==Texture2D.Readbacks.Count&&RenderTexture.active==beforeTarget&&failed&&failed.texture&&
                    Entry.Notes.Count(n=>n.Kind=="tech-mask-glow-unavailable")==logged+1,"Failed generation retried GPU/logged, lost target or destroyed source");
                // A new actual source may recover. The production caller has only
                // one fixed Camouflage source; idle failures stay latched.
                var restored=(Sprite)BranchArtAssets.Get(TechTree.CamouflageId,false);Check(restored&&restored.texture,"New source could not recover from failure");
            });
            test("UI-163-tech-glow-diffuses-beyond-former-outline-without-bright-band",()=>{
                const int n=64;var pixels=new byte[n*n*4];
                for(int y=0;y<n;y++)for(int x=0;x<32;x++)pixels[(y*n+x)*4+3]=255;
                var output=TechIconGlow.Compose(pixels,n,n,n,n);int w=n+TechIconGlow.Padding*2;
                byte Alpha(int distance)=>output[((32+TechIconGlow.Padding)*w+31+distance+TechIconGlow.Padding)*4+3];
                Check(Alpha(0)==255&&output[((32+TechIconGlow.Padding)*w+31+TechIconGlow.Padding)*4]==0,"Black face lost its color or opacity");
                Check(Alpha(1)<=180&&Alpha(1)>Alpha(2)&&Alpha(2)>Alpha(5),"Glow starts with a constant-bright narrow outline");
                Check(Alpha(12)>=65&&Alpha(20)>10&&Alpha(27)>0&&Alpha(29)==0,"Glow lacks the broad smooth outer falloff");
                byte prior=Alpha(1);for(int d=2;d<=30;d++){Check(Alpha(d)<=prior,"Glow brightened away from the silhouette");prior=Alpha(d);}
                Check(output[3]==0&&output[^1]==0,"Glow has an opaque frame corner");
            });
        }
    }
}
