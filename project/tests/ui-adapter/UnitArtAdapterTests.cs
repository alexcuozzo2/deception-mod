// Real provider and UI adapter; native sprites and renderer calls are stand-ins.
// Embedded hashes/IHDR dimensions are checked, not native PNG decoding or pixels.
using System;
using System.Linq;
using Deception.PolyModAdapter;
using PolytopiaBackendBase.Common;
using UnityEngine;

namespace Deception.Tests
{
    public static class UnitArtAdapterTests
    {
        static void Check(bool value,string reason="Unit art assertion failed")
        {if(!value)throw new Exception(reason);}
        static void Reject(Action work)
        {bool rejected=false;try{work();}catch(InvalidOperationException){rejected=true;}Check(rejected);}
        public static void Run(Action<string,Action> test)
        {
            test("UI-121-outline-render-target-cleanup-cache-and-failures",()=>{
                var prior=new RenderTexture();RenderTexture.active=prior;var source=new Sprite("Outline source"){rect=new(30,50)};
                int blits=Graphics.Blits;var value=UnitArtOutline.Get(source);
                Check(value is not null&&value.WidthScale>1&&value.HeightScale>1);
                Check(RenderTexture.active==prior&&RenderTexture.Allocated.Last().Releases==1&&Texture2D.Readbacks.Last().Destroyed);
                Check(ReferenceEquals(value,UnitArtOutline.Get(source))&&Graphics.Blits==blits+1);
                Check(source.DestroyCalls==0&&source.texture.DestroyCalls==0&&!((Sprite)value!.Sprite).Destroyed);
                foreach(string failure in new[]{"blit","read","load"})
                {
                    var failed=new Sprite("Failure "+failure){rect=new(20,30)};int created=Texture2D.Allocations.Count;
                    Graphics.Failure=failure;
                    try
                    {
                        Check(UnitArtOutline.Get(failed)==null&&RenderTexture.active==prior&&RenderTexture.Allocated.Last().Releases==1);
                        Check(Texture2D.Allocations.Skip(created).All(t=>t.DestroyCalls==1),"Unpublished outline texture survived failure "+failure);
                        int before=Graphics.Blits;Check(UnitArtOutline.Get(failed)==null&&Graphics.Blits==before,"Failed outline retried every frame");
                        Check(failed.DestroyCalls==0&&failed.texture.DestroyCalls==0&&prior.Releases==0);
                    }
                    finally{Graphics.Failure="";}
                }
                var malformed=new Sprite("Zero width outline"){rect=new(0,50)};
                int priorBlits=Graphics.Blits,priorTemps=RenderTexture.Allocated.Count;
                Check(UnitArtOutline.Get(malformed,"fixture-zero-width")==null);
                var note=Entry.Notes.Last(n=>n.Kind=="unit-art-outline-failed").Message;
                Check(note.Contains("label=fixture-zero-width")&&note.Contains("texture=")&&note.Contains("rect=")&&
                      note.Contains("vertices=4 uv=4 indices=6")&&note.Contains("stage=mesh-validation")&&note.Contains("bounds are nonfinite or empty"));
                Check(Graphics.Blits==priorBlits&&RenderTexture.Allocated.Count==priorTemps&&RenderTexture.active==prior);
                Check(malformed.DestroyCalls==0&&malformed.texture.DestroyCalls==0);
                RenderTexture.active=null;
            });
            test("UI-119-mimic-proportions-anchor-head-to-shrunken-body",()=>{
                var layers=UnitArtAssets.Layers(GameManager.LocalPlayer);var body=layers[1];var head=layers[2];var mask=layers[3];
                Check(body.Width==874.368f&&body.Height==874.368f&&layers[0].Y==body.Y&&body.X==20.41875f);
                float collar=body.Y+(0.5f-354f/1024f)*body.Height;
                // This default fixture head has a full rectangle mesh.
                Check(Math.Abs((head.Y-head.Height/2)-(collar-16))<0.01f,"Head mesh is floating above the tunic collar");
                Check(Math.Abs(body.Y+(0.5f-883f/1024f)*body.Height+483)<0.001f,"Body foot baseline moved");
                Check(Math.Abs(mask.Width-450)<0.01f&&mask.X==89.375f&&Math.Abs(mask.Y-head.Y+82.8125f)<0.01f);
                Check(Math.Abs(mask.Height-450f*790/590)<0.01f);
                Check(head.Height<=470*1.67578125f+0.01f&&head.Width<=500*1.67578125f+0.01f);
                // Packed heads can have transparent full-rect top beyond500;
                // the native category fitter bounds the complete composition.
                Check(float.IsFinite(head.Y)&&body.Y-body.Height*(883f/1024f-0.5f)>=-UnitArtAssets.CanvasHeight/2);
            });
            test("UI-126-native-head-mesh-contact-cache-fallback-and-fixed-mask",()=>{
                // Source-pinned head_imperius: Unity6000.3.17f1, rect330x470,
                // PPU2112,28vertices/78indices; read-only bundle receipt nearby.
                var vertices=new Vector2[]{new(0.05681818351f,0.03125f),
                    new(0.05681818351f,-0.0279356055f),
                    new(0.05681818351f,0.008996211924f),
                    new(0.05634469539f,-0.03125f),
                    new(0.03267045319f,0.0539772734f),
                    new(0.06013257429f,0.03693181649f),
                    new(0.02935606055f,0.06486742198f),
                    new(0.03219696879f,0.0629734844f),
                    new(0.01325757615f,-0.0582386367f),
                    new(-0.003314394038f,-0.06581439078f),
                    new(0.01325757615f,-0.07433712482f),
                    new(-0.008522727527f,0.08712121099f),
                    new(-0.0118371211f,0.08712121099f),
                    new(-0.03409091011f,0.08096590638f),
                    new(-0.0511363633f,-0.04166666791f),
                    new(-0.0553977266f,0.0696022734f),
                    new(-0.05776515231f,0.05634469539f),
                    new(-0.0587121211f,0.04592803121f),
                    new(-0.06581439078f,0.04024621099f),
                    new(-0.06723485142f,0.006155303214f),
                    new(-0.06770833582f,-0.002367424313f),
                    new(-0.06770833582f,-0.0317234844f),
                    new(0.07717803121f,-0.03740530461f),
                    new(0.07528409362f,-0.04166666791f),
                    new(0.07717803121f,-0.04119318351f),
                    new(0.06912878901f,-0.0151515156f),
                    new(0.015625f,-0.07433712482f),
                    new(0.0160984844f,-0.07149621099f)};
                var indices=new ushort[]{10,8,26,27,26,8,1,2,23,25,23,2,22,23,25,24,23,22,21,20,14,19,14,20,18,14,19,17,14,18,16,14,17,15,14,16,13,14,15,9,14,13,12,9,13,11,9,12,6,9,11,8,9,6,10,9,8,3,8,6,4,3,6,7,4,6,0,3,4,5,0,4,1,3,0,2,1,0};
                var owned=new System.Collections.Generic.List<Sprite>();
                Sprite Head(string name){var result=new Sprite(name){rect=new(330,470),PixelsPerUnit=2112,
                    FixtureVertices=vertices.ToArray(),FixtureTriangles=indices.ToArray()};owned.Add(result);return result;}
                System.Collections.Generic.IReadOnlyList<UnitArtAssets.Layer> Place(Sprite head)
                {GameManager.GameState=new();GameManager.Atlas=new(){Head=head};return UnitArtAssets.Layers(GameManager.LocalPlayer);}
                try
                {
                var source=Head("Native Imperius contact");var layers=Place(source);
                var body=layers[1];var headLayer=layers[2];var mask=layers[3];
                Check(Math.Abs(headLayer.Y-180.2858909f)<0.002f,"Rect padding was used instead of native head mesh contact: "+headLayer.Y+" "+string.Join(";",Entry.Notes.Select(n=>n.Kind+":"+n.Message)));
                Check(Math.Abs(body.Y+(0.5f-883f/1024f)*body.Height+483)<0.001f);
                Check(mask.Width==450&&Math.Abs(mask.Height-602.54237f)<0.001f&&Math.Abs(mask.Y-headLayer.Y+82.8125f)<0.001f);
                int vr=source.VertexReads,tr=source.TriangleReads;
                for(int i=0;i<5;i++)Check(UnitArtAssets.Layers(GameManager.LocalPlayer)[2].Y==headLayer.Y);
                Check(source.VertexReads==vr&&source.TriangleReads==tr,"Cached source mesh was read again");
                Check(source.FixtureVertices!.SequenceEqual(vertices)&&source.FixtureTriangles!.SequenceEqual(indices)&&source.DestroyCalls==0&&source.texture.DestroyCalls==0);
                // Noncentral Sprite pivot/Bounds center must not move its contact.
                var shifted=Head("Shifted native pivot");shifted.BoundsCenter=new(.02f,-.04f,0);
                shifted.FixtureVertices=vertices.Select(v=>new Vector2(v.x+.02f,v.y-.04f)).ToArray();
                Check(Math.Abs(Place(shifted)[2].Y-headLayer.Y)<0.002f);
                foreach(string invalid in new[]{"index","nonfinite","count","span","degenerate","bounds"})
                {
                    var bad=Head("Unsupported contact "+invalid);
                    if(invalid=="index")bad.FixtureTriangles=new ushort[]{0,1,500};
                    if(invalid=="nonfinite")bad.FixtureVertices![0]=new(float.NaN,0);
                    if(invalid=="count")bad.FixtureVertices=new Vector2[513];
                    if(invalid=="span")bad.FixtureVertices=vertices.Select(v=>new Vector2(.06f+v.x*.1f,v.y)).ToArray();
                    if(invalid=="degenerate")bad.FixtureTriangles=new ushort[]{0,0,0};
                    if(invalid=="bounds")bad.BoundsCenter=new(float.NaN,0,0);
                    int logs=Entry.Notes.Count(n=>n.Kind=="unit-head-contact-fallback");var failed=Place(bad);
                    float collar=failed[1].Y+(0.5f-354f/1024f)*failed[1].Height;
                    Check(Math.Abs(failed[2].Y-(collar-80+failed[2].Height/2))<.002f,"Unsupported mesh blanked or changed rect fallback");
                    int reads=bad.VertexReads;_=UnitArtAssets.Layers(GameManager.LocalPlayer);
                    Check(bad.VertexReads==reads&&Entry.Notes.Count(n=>n.Kind=="unit-head-contact-fallback")==logs+1);
                    Check(bad.DestroyCalls==0&&bad.texture.DestroyCalls==0);
                }
                // Saturation does not rescan uncached meshes or grow the cache.
                for(int i=0;i<130;i++)_=Place(Head("Contact capacity "+i));
                var overflow=Head("Contact beyond capacity");_=Place(overflow);_=UnitArtAssets.Layers(GameManager.LocalPlayer);
                Check(overflow.VertexReads==0&&overflow.TriangleReads==0);
                // Dead cached wrappers can be pruned without touching borrowed assets.
                UnityEngine.Object.Destroy(source);var reclaimed=Head("Reclaimed dead contact slot");_=Place(reclaimed);
                Check(reclaimed.VertexReads==1&&reclaimed.DestroyCalls==0&&source.DestroyCalls==1);
                }
                finally{foreach(var value in owned)if(value)UnityEngine.Object.Destroy(value);}
            });
            test("UI-110-direct-png-decode-retains-pixels-filter-and-one-frame",()=>{
                using var stream=typeof(UnitArtAssets).Assembly.GetManifestResourceStream("Deception.UnitArt.mimic-body-team.png")!;
                using var buffer=new System.IO.MemoryStream();stream.CopyTo(buffer);var bytes=buffer.ToArray();
                int builds=PolyMod.Managers.Visual.Builds,loads=ImageConversion.Loads;
                var sprite=(Sprite)UnitArtAssets.DecodeFrame(bytes,3870,3870,0,0,3870,3870);
                Check(PolyMod.Managers.Visual.Builds==builds&&ImageConversion.Loads==loads+1,"Slow framework pixel-copy path was invoked");
                Check(sprite.rect==new Rect(0,0,3870,3870)&&sprite.texture.width==3870&&sprite.texture.height==3870);
                Check(sprite.texture.MipChain&&sprite.texture.AppliedMips&&!sprite.texture.MadeNonReadable&&sprite.texture.filterMode==FilterMode.Trilinear);
                Check(sprite.PixelsPerUnit==100&&sprite.texture.DestroyCalls==0&&sprite.MeshType==SpriteMeshType.FullRect);
            });
            test("UI-111-decode-failure-retires-only-unpublished-texture",()=>{
                using var stream=typeof(UnitArtAssets).Assembly.GetManifestResourceStream("Deception.UnitArt.mimic_icon.png")!;
                using var buffer=new System.IO.MemoryStream();stream.CopyTo(buffer);var bytes=buffer.ToArray();
                var shared=(Sprite)UnitArtAssets.Icon();
                foreach(bool fail in new[]{true,false})
                {
                    ImageConversion.Fail=fail;ImageConversion.WrongSize=!fail;
                    Reject(()=>UnitArtAssets.DecodeFrame(bytes,1024,1024,252,269,494,525));
                    Check(ImageConversion.LastTexture!.Destroyed&&ImageConversion.LastTexture.DestroyCalls==1);
                    Check(shared.DestroyCalls==0&&shared.texture.DestroyCalls==0);
                }
                ImageConversion.Fail=false;ImageConversion.WrongSize=false;
            });
            test("UI-099-seven-user-assets-retain-verified-resources-and-cache",()=>{
                var names=new[]{"mimic-body-fixed","mimic-body-team","mimic_mask","mimic_icon","spy-fixed","spy-team","spy_icon"};
                var sprites=names.Select(n=>(Sprite)UnitArtAssets.Get(n)).ToArray();
                int builds=PolyMod.Managers.Visual.Builds;
                for(int i=0;i<names.Length;i++)Check(ReferenceEquals(sprites[i],UnitArtAssets.Get(names[i])));
                Check(PolyMod.Managers.Visual.Builds==builds&&sprites.Select(s=>s.Pointer).Distinct().Count()==7);
                Check(sprites[1].rect==new Rect(0,0,3870,3870)&&sprites[4].rect==sprites[5].rect);
                Check(sprites[0].rect==new Rect(0,0,1024,1024)&&sprites[4].rect==new Rect(0,0,1024,1024));
                Check(ReferenceEquals(UnitArtAssets.Icon(),sprites[3])&&ReferenceEquals(UnitArtAssets.Icon(true),sprites[6]));
                Check(!ReferenceEquals(sprites[3].texture,sprites[2].texture),"Type glyph still crops the old physical mask/body texture");
                Check(sprites.All(s=>s&&s.texture&&s.rect.width>0&&s.rect.height>0));
                for(int i=0;i<names.Length;i++)
                {
                    var loaded=(Sprite)PolyMod.Registry.sprites["dcp_"+names[i]+"__"];
                    Check(ReferenceEquals(sprites[i].texture,loaded.texture),"Artwork duplicated the loader texture");
                    Check(loaded.DestroyCalls==0&&loaded.texture.DestroyCalls==0,"Borrowed loader art was destroyed");
                }
                int loads=ImageConversion.Loads;
                var extra=(Sprite)UnitArtAssets.DecodeLoadedFrame("assets/unit-art/dcp_mimic_icon__.png",1024,1024,252,269,494,525);
                Check(ImageConversion.Loads==loads&&ReferenceEquals(extra.texture,sprites[3].texture),"Loaded frame decoded another PNG");
                UnityEngine.Object.Destroy(extra);
                Check(sprites[3].texture.DestroyCalls==0,"Owned frame retirement destroyed borrowed texture");
            });
            test("UI-100-owner-color-and-native-profile-independent-of-viewer",()=>{
                var owner=new PlayerState{Id=2,tribe=TribeType.Bardur,skinType=SkinType.TestSkin,color=0x123456};
                GameManager.LocalPlayer.color=0xFEDCBA;var originalViewer=GameManager.LocalPlayer;
                var first=UnitArtAssets.Layers(owner);var head=GameManager.Atlas.Head!;
                Check(first.Count==4&&ReferenceEquals(first[2].Sprite,head));
                Check((Color)first[1].Color==new Color(18/255f,52/255f,86/255f,1));
                Check(new[]{0,2,3}.All(i=>(Color)first[i].Color==new Color(1,1,1,1)));
                owner.color=0xABCDEF;GameManager.LocalPlayer=new(){color=0};
                var second=UnitArtAssets.Layers(owner);
                Check((Color)second[1].Color==new Color(171/255f,205/255f,239/255f,1));
                for(int i=0;i<first.Count;i++)Check(ReferenceEquals(first[i].Sprite,second[i].Sprite)&&first[i].X==second[i].X&&first[i].Y==second[i].Y);
                Check(GameManager.Atlas.Lookups.Count==0,"Art still uses the failing world-part lookup");
                Check(SpriteHandle.Requested.Count==1&&SpriteHandle.Requested[0].Addresses!.Length==2);
                var address=SpriteHandle.Requested[0].Addresses!;
                Check(address[0].atlas=="skin"&&address[1].atlas=="tribe"&&address[0].Tribe==TribeType.Bardur&&address[0].Skin==SkinType.TestSkin);
                Check(originalViewer.color==0xFEDCBA&&owner.color==0xABCDEF&&head.DestroyCalls==0);
            });
            test("UI-101-body-pairs-share-canvas-and-spy-has-no-mimic-mask",()=>{
                foreach(bool spy in new[]{false,true})
                {
                    var layers=UnitArtAssets.Layers(GameManager.LocalPlayer,spy);
                    Check(layers.Count==(spy?3:4)&&layers[0].X==layers[1].X&&layers[0].Y==layers[1].Y&&
                        layers[0].Width==layers[1].Width&&layers[0].Height==layers[1].Height,
                        "Matching fixed/team files acquired independent placement or scaling");
                    Check(ReferenceEquals(layers[0].Sprite,UnitArtAssets.Get(spy?"spy-fixed":"mimic-body-fixed"))&&
                        ReferenceEquals(layers[1].Sprite,UnitArtAssets.Get(spy?"spy-team":"mimic-body-team")));
                    Check(ReferenceEquals(layers[2].Sprite,GameManager.Atlas.Head));
                    if(spy)Check(layers.All(x=>!ReferenceEquals(x.Sprite,UnitArtAssets.Get("mimic_mask"))));
                }
            });
            test("UI-102-missing-head-preserves-ui-and-later-factory-failure-restores",()=>{
                var source=UISpriteDuplicator.NativePreviewSource();var container=new UIDuplicatedSprites();
                var portraitUnit=new Unit{UnitState=new(){owner=GameManager.LocalPlayer.Id}};source.SetParent(portraitUnit.spriteContainer,false);
                var data=new UISpriteDuplicator.SpriteDuplicationData{source=source,container=container};
                var factory=UIManager.Instance.SpriteDuplicator;factory.RenderSprites(data);
                var old=container.rectTransform.GetChild(0);int clears=container.ClearCalls;var head=GameManager.Atlas.Head!;
                try
                {
                    SpriteHandle.Delay=true;
                    Check(!UnitArtAssets.Prepare(GameManager.LocalPlayer));
                    var pending=UnitArtAssets.Layers(GameManager.LocalPlayer);
                    Check(pending.Count==1&&ReferenceEquals(pending[0].Sprite,UnitArtAssets.Icon()),"Pending head must show a complete mask identity, not Warrior or a partial body");
                    Check(container.ClearCalls==clears&&ReferenceEquals(container.rectTransform.GetChild(0),old));
                    SpriteHandle.Requested.Last().Complete(head);SpriteHandle.Delay=false;
                    Check(UnitArtAssets.Prepare(GameManager.LocalPlayer));
                    var oldRect=head.rect;head.rect=new(0,30);
                    try{Reject(()=>MimicDualPortrait.Apply(container,data,true,GameManager.LocalPlayer,portraitUnit,new(1,1,"dcp_mimic_form_warrior","warrior","Warrior",1,1,10,10,new Polytopia.Data.UnitData(Polytopia.Data.UnitData.Type.dcp_mimic_form_warrior),new Polytopia.Data.UnitData(Polytopia.Data.UnitData.Type.warrior),GameManager.GameState)));}
                    finally{head.rect=oldRect;}
                    Check(container.ClearCalls==clears&&ReferenceEquals(container.rectTransform.GetChild(0),old));
                    factory.FailAtFactoryCall=factory.FactoryCalls+3;int first=factory.FactoryImages.Count;
                    Reject(()=>MimicInspectionControls.ApplyActualPortrait(container,data,GameManager.LocalPlayer));
                    Check(container.rectTransform.childCount==1&&container.CopiedImages.Count==1&&ReferenceEquals(container.CopiedImages[0].Source,source.gameObject.ComponentsInChildren<SpriteRenderer>().Single()));
                    Check(factory.FactoryImages.Skip(first).All(x=>x.gameObject.Destroyed)&&head.DestroyCalls==0&&head.texture.DestroyCalls==0);
                }
                finally{GameManager.Atlas.Head=head;factory.FailAtFactoryCall=-1;SpriteHandle.Delay=false;}
            });
        }
    }
}
