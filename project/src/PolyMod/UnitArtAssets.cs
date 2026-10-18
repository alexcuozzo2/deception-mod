using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deception.PolyModAdapter
{
    // Original PNGs are loaded byte-for-byte from our mod file registry. Frames and part placement are
    // renderer metadata: no color key, resampling, or destructive PNG editing.
    internal static class UnitArtAssets
    {
        internal const float CanvasWidth=1000f,CanvasHeight=1000f;
        internal const float BoatVisibleWidth=653.3203125f,BoatVisibleHeight=438.4765625f,BoatFootBaseline=-194.3359375f;
        internal const float BoatVisibleCenterX=-30.76171875f,BoatMapScale=1.2f;
        internal sealed record Layer(object Sprite,float X,float Y,float Width,float Height,object Color,bool MirrorX=false,string? AssetName=null,bool IsHead=false,bool Tintable=false,bool RectMapped=false);
        sealed record Asset(int Width,int Height,string Sha,float X,float Y,float W,float H);
        static readonly Dictionary<string,Asset> Definitions=new(StringComparer.Ordinal)
        {
            ["fogger_fixed"]=new(1240,1240,"1aaaa647e794bdc3e748a04682ed0a24dcdd2446fc4b30fd6ba3130cbdd10b22",131,335,938,674),
            ["fogger_team"]=new(1240,1240,"a6b0178d2ef3fb39ef9ee557837afb32f0b0ab04ced55aa8f819ba828b9f5487",598,664,91,97),
            ["fogger_badge"]=new(1024,1024,"bb0ea720bcf2b9c3d69da8ea4f31d111bd3d09398a52d8d9f45b779f7d7f7d07",107,323,823,346),
            ["trade_secrets_fixed"]=new(512,512,"8694aa7eac5ae634fe20fdca0528f50f776f8b750ee143dec80688f59c967006",44,21,421,457),
            ["trade_secrets_team"]=new(512,512,"4b45abc85bbe64f8cf84fc81c1d0bb9c9df700fdf9b068a5d9f0d0ebcd9c3105",44,21,421,457),
            ["spymaid_team"]=new(1024,1024,"2263fe7d9c1c1d5cc1d8beab28e05e1d2a51f2ab84b3623452e5ad44f5298d04",0,0,1024,1024),
            ["spymaid_fixed"]=new(1024,1024,"f6ff411ee08765d2c0fa4f29b491a0f9780b8933a8c8e1b148eafbf29cd39687",0,0,1024,1024),
            ["mermimic_body_team"]=new(3870,3870,"7511011e2891c1690ec4cc165e9882e0966a54cbfc90647ec5d419210d74d0bb",0,0,3870,3870),
            ["mimic-body-fixed"]=new(1024,1024,"69a1bb6126855b63bf7cfb48e943a7a5bdb7e74a53bc67acbcb3bbe67be8f84b",0,0,1024,1024),
            ["mimic-body-team"]=new(3870,3870,"136c90209cac23fb618dfecc2818d70a1f5b7ca5303f69cea7e6fe741f38cc2d",0,0,3870,3870),
            ["mimic_mask"]=new(1024,1024,"79a09b5969ca98f3ca3d3f6a583896e1b644e5daa0597912743bc91b6e25bdef",242,86,590,790),
            ["mimic_icon"]=new(1024,1024,"ec12d61ca69595ecc8483887e03d4bf82d8024f780db1c555df7ba272ec576af",252,269,494,525),
            ["spy-fixed"]=new(1024,1024,"c3941c327a18ba46e84778883c7867e6a7e9c4f113c3decea1d45f755260cfc3",0,0,1024,1024),
            ["spy-team"]=new(1024,1024,"bd18538b8d1912577efeceac1fc5ab76382ad1fcb962ce058190b851d02d75c2",0,0,1024,1024),
            ["spy_icon"]=new(1254,1254,"19d066b6d7d01bfcc844f8525264dc02d82439eb6469fcee14353cf627a1f642",194,310,867,649),
            ["mimic_boat_fixed"]=new(1024,1024,"856d4d4adfaed5136bdcf08c32b1257518720bb6f68c10dc5230c32c3f3c1bb9",0,0,1024,1024),
            ["mimic_boat_team"]=new(1024,1024,"eea0d898efb3055baf0badae7e00709918a73b800d3451fad59dc9e331c15bab",0,0,1024,1024),
            ["propaganda_mask"]=new(1024,1024,"d72accaf9df50434638333e8870c2dff418ffed05c0f950604ac867a1bbd515c",204,134,576,775),
            ["disguise_mask"]=new(3870,3870,"5f611aad9b2a9a41bf15e2bfa853a67635edc6fa1be1879e82900e42d97ab028",773,509,2174,2926),
            ["intelligence_icon"]=new(3870,3870,"2c62c8751617f573c57748b07b6e5896cbf839e365eeadc8ca2c293ab63b8401",935,393,2001,3219),
            ["blackmail_fixed"]=new(512,512,"0fb96f5c97e4e73403043fc0defcf730bd7b6242d3277a5c795f7c2098290d23",44,21,421,457),
            ["blackmail_team"]=new(512,512,"0339e2e3d0a2d374758355be743b5168590e668a5d3f396d1f188fa6b8ca6bbd",44,21,421,457),
            ["disguise_icon"]=new(1024,1024,"e35408ab6a0199b7efc6cf9a3c5e44be2dee75f79153b97748f114fc4c795a3f",256,273,485,517)
        };
        static readonly Dictionary<string,object> Sprites=new(StringComparer.Ordinal);
        internal static object Get(string name)
        {
            if(Sprites.TryGetValue(name,out var cached)&&Native.UnityAlive(cached))
            {
                if(Native.UnityAlive(Native.Get(cached,"texture")))return cached;
                // A live Sprite with a destroyed texture cannot be used by an
                // existing widget. Retire only our unusable wrapper.
                Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,cached);
                Sprites.Remove(name);
            }
            var a=Definitions[name];
            var path="assets/unit-art/dcp_"+name+"__.png";
            var bytes=ModContentAssets.Read(path);
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(a.Sha,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unit artwork hash differs: "+name);
            var timer=Stopwatch.StartNew();
            var sprite=DecodeLoadedFrame(path,a.Width,a.Height,a.X,a.Y,a.W,a.H);
            Sprites[name]=sprite;
            Entry.Note("unit-art-loaded",name+" sha256="+a.Sha+" decodeMs="+timer.Elapsed.TotalMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));return sprite;
        }
        // The loader owns the PNG texture. Create only our cropped Sprite;
        // neither success nor failure may destroy or mutate the shared texture.
        internal static object DecodeLoadedFrame(string path,int width,int height,float x,float y,float w,float h)
        {
            var texture=ModContentAssets.LoadedTexture(path,width,height);
            var frame=Activator.CreateInstance(Native.T("UnityEngine.Rect"),x,y,w,h)!;
            var pivot=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f)!;
            return Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                texture,frame,pivot,100f,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))
                ??throw new InvalidOperationException("Loaded unit artwork frame failed.");
        }
        internal static object DecodeFrame(byte[] bytes,int width,int height,float x,float y,float w,float h)
        {
            // The installed PolyMod helper copies every decoded pixel through
            // managed Color wrappers and writes the unchanged pixels back. On
            // the 3870-square body this is nearly 15 million needless copies.
            // Use the same Unity PNG decoder/filter/mipmap policy directly.
            object? texture=null;bool published=false;var stages=new Stopwatch();
            try
            {
                texture=Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),1,1,
                    Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),true)
                    ??throw new InvalidOperationException("Unit artwork texture allocation failed.");
                var arrayType=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                var nativeBytes=Activator.CreateInstance(arrayType,new object[]{bytes})!;
                stages.Restart();
                bool loaded=(bool)Native.Invoke(Native.M("UnityEngine.ImageConversion","LoadImage","UnityEngine.Texture2D",arrayType.FullName!,"System.Boolean"),null,texture,nativeBytes,false)!;
                double decodedMs=stages.Elapsed.TotalMilliseconds;
                if(!loaded||Native.Int(Native.Get(texture,"width"))!=width||Native.Int(Native.Get(texture,"height"))!=height)
                    throw new InvalidOperationException("Unit artwork decode failed or dimensions differ.");
                Native.Set(texture,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Trilinear"));
                stages.Restart();
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),texture,true,false);
                double appliedMs=stages.Elapsed.TotalMilliseconds;
                var frame=Activator.CreateInstance(Native.T("UnityEngine.Rect"),x,y,w,h)!;
                var pivot=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),0.5f,0.5f)!;
                stages.Restart();
                var sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,texture,frame,pivot,100f,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))
                    ??throw new InvalidOperationException("Unit artwork frame failed.");
                Entry.Note("unit-art-decode-stages",FormattableString.Invariant($"texture={width}x{height} frame={w}x{h} decodeMs={decodedMs:F1} applyMs={appliedMs:F1} frameMs={stages.Elapsed.TotalMilliseconds:F1} mesh=FullRect"));
                published=true;return sprite;
            }
            finally
            {
                if(!published&&Native.UnityAlive(texture))
                    Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,texture);
            }
        }
        internal static object Icon(bool spy=false)=>Get(spy?"spy_icon":"mimic_icon");
        static object? BadgeMask;
        internal static object MimicBadgeIcon()
        {
            var source=Icon();var texture=Native.Get(source,"texture");
            if(Native.UnityAlive(BadgeMask)&&Native.Same(Native.Get(BadgeMask!,"texture"),texture))return BadgeMask!;
            if(Native.UnityAlive(BadgeMask))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,BadgeMask);
            // Original alpha occupies x256..740 inclusive. Keep the accepted
            // height/PPU and four transparent columns on either side; the
            // action icon's original frame and all PNG bytes stay untouched.
            BadgeMask=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                texture,Activator.CreateInstance(Native.T("UnityEngine.Rect"),252f,269f,493f,525f),
                Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),100f,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))
                ??throw new InvalidOperationException("Mimic badge frame was not created.");
            return BadgeMask;
        }
        internal static object DisguiseIcon()=>Get("disguise_icon");
        // Naval artwork is staged independently from transport mechanics. Keep
        // both original canvases and one common transform so the stripe aligns.
        internal static IReadOnlyList<Layer> BoatLayers(object player)
        {
            if(player is null)throw new InvalidOperationException("Boat artwork requires its explicit owner.");
            var white=Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f)!;
            var team=Native.Invoke(Native.M("ColorUtil","ColorFromInt","System.Int32"),null,Native.Int(Native.Get(player,"color")))!;
            return new[]{new Layer(Get("mimic_boat_fixed"),0,0,CanvasWidth,CanvasHeight,white,true,AssetName:"mimic_boat_fixed"),
                new Layer(Get("mimic_boat_team"),0,0,CanvasWidth,CanvasHeight,team,true,AssetName:"mimic_boat_team",Tintable:true)};
        }
        static readonly Dictionary<string,object> NativeIcons=new(StringComparer.Ordinal);
        static object? IconGame;
        internal static object? NativeIcon(string form)
        {
            var game=Native.Static("GameManager","GameState");if(game is null)return null;
            if(!Native.Same(game,IconGame)){NativeIcons.Clear();IconGame=game;}
            if(!NativeIcons.TryGetValue(form,out var handle))
            {
                if(NativeIcons.Count>=128)throw new InvalidOperationException("Native icon profile limit exceeded.");
                var address=Native.Invoke(Native.M("SpriteData","GetUnitIconAddress","Polytopia.Data.UnitData+Type"),null,
                    Native.EnumValue("Polytopia.Data.UnitData+Type",form));
                handle=Activator.CreateInstance(Native.T("SpriteHandle"))!;NativeIcons.Add(form,handle);
                try{Native.Invoke(Native.M("SpriteHandle","Request","SpriteAddress"),handle,address);}
                catch{NativeIcons.Remove(form);throw;}
            }
            return (bool)Native.Get(handle,"isCompleted")!&&Native.UnityAlive(Native.Get(handle,"sprite"))?Native.Get(handle,"sprite"):null;
        }
        sealed class HeadLoad
        {
            public object Handle=null!;
            public bool ReportedReady,ReportedFailure;
        }
        static string AddressArray=>Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1")
            .MakeGenericType(Native.T("SpriteAddress")).FullName!;
        static readonly Dictionary<(int Tribe,int Skin),HeadLoad> Heads=new();
        static object? HeadAtlas;
        internal static void ClearNativeHeads(){Heads.Clear();HeadAtlas=null;}
        internal static bool Prepare(object player)=>Head(player) is not null;
        internal static (int Tribe,int Skin) HeadProfile(object player)=>(
            Native.Int(Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,player)),
            Native.Int(Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player)));
        static object? Head(object player)
        {
            if(player is null)throw new InvalidOperationException("Unit artwork requires its explicit owner.");
            var game=Native.Static("GameManager","GameState");
            if(game is null)return null;
            // The installed address producer reads only the player's visual
            // tribe/skin, not GameState. Client/backend/recap siblings share an
            // atlas; changing between them must not restart identical loads.
            var atlas=Native.Invoke(Native.M("GameManager","GetSpriteAtlasManager"),null);
            if(atlas is null)return null;
            if(!Native.Same(HeadAtlas,atlas)){Heads.Clear();HeadAtlas=atlas;}
            var key=HeadProfile(player);
            if(Heads.TryGetValue(key,out var previous)&&previous.ReportedReady&&
                (!Native.UnityAlive(Native.Get(previous.Handle,"sprite"))||!Native.UnityAlive(Native.Get(Native.Get(previous.Handle,"sprite")!,"texture"))))
                Heads.Remove(key);
            if(!Heads.TryGetValue(key,out var load))
            {
                if(Heads.Count>=128)throw new InvalidOperationException("Native head profile limit exceeded.");
                var addresses=Native.Invoke(Native.M("SpriteData","GetHeadSpriteAddresses","GameState","PlayerState"),null,game,player)
                    ??throw new InvalidOperationException("Native head address list is absent.");
                var handle=Activator.CreateInstance(Native.T("SpriteHandle"))
                    ??throw new InvalidOperationException("Native head request was not created.");
                load=new HeadLoad{Handle=handle};Heads.Add(key,load);
                try{Native.Invoke(Native.M("SpriteHandle","Request",AddressArray),handle,addresses);}
                catch{Heads.Remove(key);throw;}
                Entry.Note("unit-head-request","Native skin/tribe address fallbacks; profile="+key);
            }
            if(!(bool)Native.Get(load.Handle,"isCompleted")!)return null;
            var head=Native.Get(load.Handle,"sprite");
            if(!Native.UnityAlive(head)||!Native.UnityAlive(Native.Get(head!,"texture")))
            {
                if(!load.ReportedFailure){load.ReportedFailure=true;Entry.Note("unit-head-load-failed","Native head request completed without a live sprite; profile="+key);}
                return null;
            }
            if(!load.ReportedReady){load.ReportedReady=true;Entry.Note("unit-head-ready","Native address load completed; profile="+key);}
            return head;
        }
        internal static void PreloadBodies(bool aquarion)
        {
            foreach(string name in aquarion?new[]{"mermimic_body_team","spymaid_fixed","spymaid_team"}:
                new[]{"mimic-body-fixed","mimic-body-team","spy-fixed","spy-team"})_=Get(name);
            _=Get("mimic_mask");_=Icon();_=Icon(true);
        }
        internal static IReadOnlyList<Layer> Layers(object player,bool spy=false,bool aquarion=false)
        {
            // Request exactly the same ordered skin/tribe address fallbacks as
            // the native portrait loader, rather than the world-part lookup.
            // No completion callback captures a popup, map unit or UI renderer.
            var head=Head(player);
            var white=Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f)!;
            if(head is null)
            {
                // A complete, recognizable private identity while the native
                // atlas loads. Callers retry only under their existing guards.
                var icon=Icon(spy);var iconRect=Native.Get(icon,"rect")!;
                float iw=Convert.ToSingle(Native.Get(iconRect,"width")),ih=Convert.ToSingle(Native.Get(iconRect,"height"));
                float scale=520f/Math.Max(iw,ih);
                return new[]{new Layer(icon,0,0,iw*scale,ih*scale,white,AssetName:spy?"spy_icon":"mimic_icon")};
            }
            var headRect=Native.Get(head!,"rect")!;
            float hw=Convert.ToSingle(Native.Get(headRect,"width")),hh=Convert.ToSingle(Native.Get(headRect,"height"));
            if(!float.IsFinite(hw)||!float.IsFinite(hh)||hw<=0||hh<=0)throw new InvalidOperationException("Invalid native head geometry.");
            var team=Native.Invoke(Native.M("ColorUtil","ColorFromInt","System.Int32"),null,Native.Int(Native.Get(player,"color")))!;
            float headWidth=500f,headHeight=Math.Min(470f,headWidth*hh/hw);
            headWidth=headHeight*hw/hh;
            if(spy)
            {
                // Spy's submitted shirt/pouch and fixed brown coat share their
                // original canvas; one common transform keeps all seams aligned.
                // Anchor the visible feet, not the padded canvas. The submitted
                // silhouette spans x293..663 and ends at y797 of 1024 pixels.
                const float bodySize=1393f*1.5f;
                float x=(512f-(aquarion?494f:478f))/1024f*bodySize;
                float y=FootBaseline+((aquarion?784f:797f)/1024f-0.5f)*bodySize;
                float neck=y+(512f-(aquarion?396f:382f))/1024f*bodySize;
                // Native heads have different transparent margins. Keep the
                // accepted base fit, with reviewed face/ornament calibration
                // before the shared collar anchor is calculated below.
                var (w,h)=SpyHeadSize(head!,hw,hh,headWidth*1.989f,headHeight*1.989f);
                float spyHeadY=HeadContactY(head!,w,h,0f,neck,true)-SpyHeadDrop;
                string fixedName=aquarion?"spymaid_fixed":"spy-fixed",teamName=aquarion?"spymaid_team":"spy-team";
                return new[]{new Layer(Get(fixedName),x,y,bodySize,bodySize,white,AssetName:fixedName),
                    new Layer(Get(teamName),x,y,bodySize,bodySize,team,AssetName:teamName,Tintable:true),
                    new Layer(head!,0,spyHeadY,w,h,white,IsHead:true,RectMapped:true)};
            }
            // Keep the original foot baseline (source y883 at design y-483).
            // Both PNG canvases stay aligned; the head joins the actual collar
            // through its borrowed mesh, whose rect may contain bottom padding.
            const float headScale=1.67578125f;
            float bodyY=FootBaseline+(883f/1024f-0.5f)*BodyHeight;
            float enlargedHeadWidth=headWidth*headScale,enlargedHeadHeight=headHeight*headScale;
            float headY=HeadContactY(head!,enlargedHeadWidth,enlargedHeadHeight);
            const float maskWidth=450f,maskX=89.375f,maskOffsetY=-82.8125f;
            if(aquarion)
            {
                // Preserve the established feet/collar/head geometry. The new
                // tail replaces the two land-body layers on its original canvas.
                const float size=(883f-354f)/1024f*BodyHeight*3870f/(2916f-1675f);
                float x=CollarX-(1970f/3870f-0.5f)*size;
                float y=FootBaseline+(2916f/3870f-0.5f)*size;
                return new[]{new Layer(Get("mermimic_body_team"),x,y,size,size,team,AssetName:"mermimic_body_team",Tintable:true),
                    new Layer(head!,0,headY,enlargedHeadWidth,enlargedHeadHeight,white,IsHead:true),
                    new Layer(Get("mimic_mask"),maskX,headY+maskOffsetY,maskWidth,maskWidth*790f/590f,white,AssetName:"mimic_mask")};
            }
            return new[]{new Layer(Get("mimic-body-fixed"),BodyX,bodyY,BodyWidth,BodyHeight,white,AssetName:"mimic-body-fixed"),
                new Layer(Get("mimic-body-team"),BodyX,bodyY,BodyWidth,BodyHeight,team,AssetName:"mimic-body-team",Tintable:true),
                new Layer(head!,0,headY,enlargedHeadWidth,enlargedHeadHeight,white,IsHead:true),
                new Layer(Get("mimic_mask"),maskX,headY+maskOffsetY,maskWidth,maskWidth*790f/590f,white,AssetName:"mimic_mask")};
        }
        internal const float SpyVisibleHeadHeight=341f*2.21f*0.9f,SpyHeadDrop=5f*2.21f*0.9f;
        sealed record HeadMeasure(object Source,float Width,float Height);
        static readonly Dictionary<long,HeadMeasure> SpyHeads=new();
        static float SpyHeadCalibration(object sprite,float rectWidth,float rectHeight,float visibleWidth,float visibleHeight)
        {
            // Initial per-skin calibration of the reported native heads and
            // the same broad core shape in the default Cymanti head.
            // DarkElf's horns inflate silhouette height while its face shrinks;
            // Cute's short, broad mask becomes oversized under that same fit.
            // Match the loaded asset, not the requested player skin: native
            // fallback may return a different head. Changed/unknown assets keep
            // the established fit. No shared Sprite or texture is resized.
            bool Shape(float rw,float rh,float vw,float vh)=>Math.Abs(rectWidth-rw)<0.01f&&Math.Abs(rectHeight-rh)<0.01f&&
                Math.Abs(visibleWidth-vw)<1f&&Math.Abs(visibleHeight-vh)<1f;
            return (string?)Native.Get(sprite,"name") switch
            {
                "head_darkelf" when Shape(455,600,358,411)=>1.20f,
                "head_cute" when Shape(418,512,313,286)=>0.84f,
                "head_cymanti" when Shape(350,460,323,303)=>0.88f,
                _=>1f
            };
        }
        static (float Width,float Height) SpyHeadSize(object sprite,float rectWidth,float rectHeight,float fallbackWidth,float fallbackHeight)
        {
            long key=Native.Pointer(sprite);
            if(SpyHeads.TryGetValue(key,out var old)&&Native.UnityAlive(old.Source)&&Native.Same(old.Source,sprite))return(old.Width,old.Height);
            // Bounds and UVs remain native; only the presentation transform changes.
            float width=fallbackWidth,height=fallbackHeight;
            try
            {
                var mesh=Native.Get(sprite,"vertices")!;int count=Native.Int(Native.Get(mesh,"Count"));
                if(count<3||count>512)throw new InvalidOperationException("Unsupported Spy head vertex count.");
                var vertices=Native.Items(mesh).Select(ContactVector).ToArray();
                float ppu=ContactNumber(Native.Get(sprite,"pixelsPerUnit"));
                float visible=(vertices.Max(v=>v.Y)-vertices.Min(v=>v.Y))*ppu;
                float visibleWidth=(vertices.Max(v=>v.X)-vertices.Min(v=>v.X))*ppu;
                if(ppu<=0||visible<=0||visible>rectHeight*1.001f||visibleWidth<=0||visibleWidth>rectWidth*1.001f)
                    throw new InvalidOperationException("Invalid visible Spy head geometry.");
                float scale=SpyVisibleHeadHeight/visible*SpyHeadCalibration(sprite,rectWidth,rectHeight,visibleWidth,visible);
                width=rectWidth*scale;height=rectHeight*scale;
            }
            catch(Exception e){Entry.Note("spy-head-size-fallback",e.Message);}
            if(SpyHeads.Count>=128)SpyHeads.Clear();
            SpyHeads[key]=new(sprite,width,height);return(width,height);
        }
        // Match Image's full sprite rectangle for native atlas heads. Map sprites
        // use the same coordinates even when Sprite.bounds is a tighter mesh box.
        internal static (float Width,float Height,float X,float Y) Frame(Layer layer)
        {
            if(layer.RectMapped)
            {
                var r=Native.Get(layer.Sprite,"rect")!;var pivot=ContactVector(Native.Get(layer.Sprite,"pivot")!);
                float ppu=ContactNumber(Native.Get(layer.Sprite,"pixelsPerUnit"));
                float w=ContactNumber(Native.Get(r,"width")),h=ContactNumber(Native.Get(r,"height"));
                if(ppu<=0||w<=0||h<=0)throw new InvalidOperationException("Invalid native art frame.");
                return(w/ppu,h/ppu,(w/2-pivot.X)/ppu,(h/2-pivot.Y)/ppu);
            }
            var bounds=Native.Get(layer.Sprite,"bounds")!;var size=ContactVector(Native.Get(bounds,"size")!);var center=ContactVector(Native.Get(bounds,"center")!);
            return(size.X,size.Y,center.X,center.Y);
        }
        // Reduce the body 8% relative to the accepted head/mask, preserving PNG aspect.
        internal const float FootBaseline=-483f;
        const float BodyX=20.41875f,BodyWidth=874.368f,BodyHeight=874.368f;
        const float CollarX=BodyX+(476f/1024f-0.5f)*BodyWidth;
        const float CollarY=FootBaseline+(883f-354f)/1024f*BodyHeight;
        const float ContactSpan=12f,ContactOverlap=16f;
        sealed record HeadContact(object Source,float Width,float Height,float CollarX,float CollarY,float Y,bool RectMapped);
        // Borrowed atlas identity only: never destroy or mutate these sprites.
        // Cache final placement, so selected-unit frames never rescan triangles.
        static readonly Dictionary<(long,float,float),HeadContact> Contacts=new();
        static bool ContactCapacityReported;
        static float HeadContactY(object source,float width,float height)
            =>HeadContactY(source,width,height,CollarX,CollarY);
        static float HeadContactY(object source,float width,float height,float collarX,float collarY,bool rectMapped=false)
        {
            float fallback=collarY-80f+height/2f;
            if(!Native.UnityAlive(source))return fallback;
            var key=(Native.Pointer(source),collarX,collarY);
            if(Contacts.TryGetValue(key,out var cached))
            {
                if(Native.UnityAlive(cached.Source)&&Native.Same(cached.Source,source)&&cached.Width==width&&cached.Height==height&&cached.RectMapped==rectMapped)return cached.Y;
                Contacts.Remove(key);
            }
            if(Contacts.Count>=128)
            {
                foreach(var dead in Contacts.Where(p=>!Native.UnityAlive(p.Value.Source)).Select(p=>p.Key).ToArray())Contacts.Remove(dead);
                if(Contacts.Count>=128)
                {
                    if(!ContactCapacityReported){ContactCapacityReported=true;Entry.Note("unit-head-contact-fallback","Head contact cache is full; using rectangle anchor for further sprites.");}
                    return fallback;
                }
            }
            float y=fallback;
            try{y=MeasureHeadContact(source,width,height,collarX,collarY,rectMapped);}
            catch(Exception error)
            {
                // Unsupported art remains visible. Remember this exact source
                // failure so it neither logs nor copies native arrays each frame.
                Entry.Note("unit-head-contact-fallback","sprite="+key+" "+error.Message);
            }
            Contacts[key]=new(source,width,height,collarX,collarY,y,rectMapped);return y;
        }
        readonly record struct ContactPoint(float X,float Y);
        static ContactPoint ContactVector(object value)
        {
            string type=value.GetType().FullName!;
            float Axis(int i)=>ContactNumber(Native.Invoke(Native.M(type,"get_Item","System.Int32"),value,i));
            return new(Axis(0),Axis(1));
        }
        static float ContactNumber(object? value)
        {
            float result=Convert.ToSingle(value);
            if(!float.IsFinite(result))throw new InvalidOperationException("Nonfinite head contact geometry.");
            return result;
        }
        static float MeasureHeadContact(object source,float width,float height)
            =>MeasureHeadContact(source,width,height,CollarX,CollarY);
        static float MeasureHeadContact(object source,float width,float height,float collarX,float collarY,bool rectMapped=false)
        {
            if(!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0)
                throw new InvalidOperationException("Invalid head contact dimensions.");
            var bounds=Native.Get(source,"bounds")!;
            var min=ContactVector(Native.Get(bounds,"min")!);var max=ContactVector(Native.Get(bounds,"max")!);
            if(rectMapped)
            {
                var frame=Frame(new Layer(source,0,0,width,height,new object(),RectMapped:true));
                min=new(frame.X-frame.Width/2,frame.Y-frame.Height/2);max=new(frame.X+frame.Width/2,frame.Y+frame.Height/2);
            }
            double sx=(double)max.X-min.X,sy=(double)max.Y-min.Y;
            if(!double.IsFinite(sx)||!double.IsFinite(sy)||sx<=0||sy<=0)
                throw new InvalidOperationException("Invalid head contact bounds.");
            double cx=((double)min.X+max.X)/2,cy=((double)min.Y+max.Y)/2;
            var nativeVertices=Native.Get(source,"vertices")!;var nativeIndices=Native.Get(source,"triangles")!;
            int vc=Native.Int(Native.Get(nativeVertices,"Count")),ic=Native.Int(Native.Get(nativeIndices,"Count"));
            if(vc<3||vc>512||ic<3||ic>1536||ic%3!=0)
                throw new InvalidOperationException("Head contact mesh exceeds supported counts.");
            var vertices=Native.Items(nativeVertices).Select(ContactVector).ToArray();
            var indices=Native.Items(nativeIndices).Select(Convert.ToInt32).ToArray();
            if(vertices.Length!=vc||indices.Length!=ic||indices.Any(i=>i<0||i>=vc))
                throw new InvalidOperationException("Invalid head contact indices.");
            double epsilon=Math.Max(sx,sy)*1e-5;
            if(vertices.Any(v=>v.X<min.X-epsilon||v.X>max.X+epsilon||v.Y<min.Y-epsilon||v.Y>max.Y+epsilon))
                throw new InvalidOperationException("Head contact vertices exceed source bounds.");
            var filled=new bool[ic/3];
            for(int i=0;i<ic;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                double area=((double)b.X-a.X)*(c.Y-a.Y)-((double)b.Y-a.Y)*(c.X-a.X);
                filled[i/3]=Math.Abs(area)>sx*sy*1e-10;
            }
            if(!filled.Any(v=>v))throw new InvalidOperationException("Head mesh has no filled triangles.");
            double contact=double.NegativeInfinity;
            foreach(float offset in new[]{-ContactSpan,0f,ContactSpan})
            {
                double x=cx+(collarX+offset)*sx/width;
                double bottom=double.PositiveInfinity;
                for(int i=0;i<ic;i+=3)
                {
                    if(!filled[i/3])continue;
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    if(x<Math.Min(a.X,Math.Min(b.X,c.X))||x>Math.Max(a.X,Math.Max(b.X,c.X)))continue;
                    void Edge(ContactPoint p,ContactPoint q)
                    {
                        if(x<Math.Min(p.X,q.X)||x>Math.Max(p.X,q.X))return;
                        if(p.X==q.X){bottom=Math.Min(bottom,Math.Min(p.Y,q.Y));return;}
                        double t=(x-p.X)/((double)q.X-p.X);
                        bottom=Math.Min(bottom,p.Y+t*((double)q.Y-p.Y));
                    }
                    Edge(a,b);Edge(b,c);Edge(c,a);
                }
                if(!double.IsFinite(bottom))throw new InvalidOperationException("Head mesh misses the collar contact span.");
                contact=Math.Max(contact,(bottom-cy)*height/sy);
            }
            double y=collarY-ContactOverlap-contact;
            if(!double.IsFinite(y)||Math.Abs(y)>CanvasHeight*2)
                throw new InvalidOperationException("Head contact placement exceeds supported range.");
            return (float)y;
        }
    }
}
