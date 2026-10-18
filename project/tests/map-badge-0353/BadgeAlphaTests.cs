using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.PolyModAdapter;
using UnityEngine;

namespace Deception.Tests
{
    // Production map adapter with synthetic Unity objects. Pixel interpretation
    // below follows the pinned native Metal shader, not the implementation's
    // helper. No Unity renderer or GPU/native visual acceptance is claimed.
    public static class BadgeAlphaTests
    {
        static int Assertions;
        static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
        static int Byte(float value)=>(int)MathF.Round(Math.Clamp(value,0f,1f)*255f);
        static float ShaderOpacity(float stored)
        {
            float vertex=Byte(stored)/255f;
            return (vertex*255f-(vertex>=128/255f?128:0))/127f;
        }
        static object? Call(Type type,string name,params object?[] args)=>type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
        static void Reset(){Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");}
        static GameObject Badge(UnitStatusDisplay status)=>Enumerable.Range(0,status.typeContainer.childCount)
            .Select(i=>status.typeContainer.GetChild(i).gameObject).Single(g=>g.name=="Deception private Mimic badge"&&!g.Destroyed);
        public static int Run()
        {
            try
            {
                Check(ShaderOpacity(1f*.5f)==0,"Fixture does not reproduce native half-alpha disappearance");
                foreach(int original in Enumerable.Range(0,256))foreach(float factor in new[]{0f,.25f,.5f,.75f,1f})
                {
                    float changed=MimicMapInspection.FadedBadgeAlpha(original/255f,true,factor);
                    Check((Byte(changed)&128)==(original&128),"Packed fading changed saturation bit");
                    Check(Math.Abs(ShaderOpacity(changed)-ShaderOpacity(original/255f)*factor)<=.5f/127f+.00001f,"Packed fade changed intended visual opacity");
                    Check(Math.Abs(MimicMapInspection.BadgeOpacity(original/255f,true)-ShaderOpacity(original/255f))<.00001f,"Native opacity read is not shader equivalent");
                }
                Check(MimicMapInspection.FadedBadgeAlpha(.6f,false,.5f)==.3f,"Linear shader must retain ordinary multiplication");
                Check(MimicMapInspection.BadgeOpacity(.6f,false)==.6f,"Linear opacity must not be decoded");
                Reset();MimicMapInspection.Install(new HarmonyLib.Harmony());
                object actor=Call(typeof(MapAdapterTests),"Create",true,3u)!;
                var unit=(Unit)actor.GetType().GetProperty("Unit")!.GetValue(actor)!;
                var status=(UnitStatusDisplay)Call(typeof(MapAdapterTests),"AttachBadge",actor)!;
                var native=new[]{status.typeBg,status.typeOutline,status.typeIconShadow,status.typeIcon};
                foreach(var renderer in native)renderer.sharedMaterial=new(){shader=new(){name="Polytopia/Sprite"}};
                native[0].color=new(.1f,.2f,.8f,1);native[1].color=new(1,1,1,1);
                native[2].color=new(0,0,0,160/255f);native[3].color=new(1,1,1,1);
                var baseline=native.Select(r=>r.color).ToArray();var materials=native.Select(r=>r.sharedMaterial).ToArray();
                var sprites=native.Select(r=>r.sprite).ToArray();var originalPosition=status.typeContainer.localPosition;
                bool actual=false;MimicMapInspection.Configure(_=>actual);
                for(int round=0;round<20;round++)
                {
                    actual=round%2==0;MimicMapInspection.Refresh(unit);MimicMapInspection.Tick();
                    for(int i=0;i<native.Length;i++)
                    {
                        var color=native[i].color;
                        Check(color.r==baseline[i].r&&color.g==baseline[i].g&&color.b==baseline[i].b,"Badge fade recolored native RGB");
                        Check((Byte(color.a)&128)==(Byte(baseline[i].a)&128),"Refresh changed native saturation bit");
                        Check(Math.Abs(ShaderOpacity(color.a)-ShaderOpacity(baseline[i].a)*(actual?0.5f:1f))<=.5f/127f+.00001f,"Native badge alpha drifted with refresh/toggle");
                        Check(ReferenceEquals(native[i].sprite,sprites[i])&&ReferenceEquals(native[i].sharedMaterial,materials[i]),"Projection replaced borrowed native asset/material");
                    }
                    var copied=Badge(status).ComponentsInChildren<SpriteRenderer>().ToArray();
                    var mask=copied.Single(r=>r.gameObject.name=="Deception private badge mask");
                    var shadow=copied.Single(r=>r.gameObject.name=="Deception private badge shadow");
                    Check(mask.color==new Color(1,1,1,actual?1:.5f),"Owned linear-shader mask opacity changed");
                    Check(Math.Abs(shadow.color.a-ShaderOpacity(baseline[2].a)*(actual?1:.5f))<.00001f,"Owned shadow received packed byte as ordinary alpha");
                    Check(copied.All(r=>r.sharedMaterial!.shader.name=="Sprites/Default"),"Owned badge borrowed packed shader unexpectedly");
                }
                actual=true;MimicMapInspection.Refresh(unit);status.SetTypeSprite(new Sprite("Updated rider glyph"));
                MimicMapInspection.Refresh(unit);Check(ShaderOpacity(status.typeIcon.color.a)>.49f,"Async glyph replacement was transparent");
                MapRenderer.ProductionCallbacks=true;status.ReturnToPool();
                Check(native.Select(r=>r.color).SequenceEqual(baseline),"Pool cleanup failed to restore exact packed colors");
                Check(status.typeContainer.localPosition==originalPosition,"Pool cleanup failed to restore native anchor");
                Check(!status.typeIcon.sprite!.Destroyed&&materials.All(m=>m is not null&&!m.Destroyed),"Cleanup destroyed borrowed native assets");
                MimicMapInspection.Clear();
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,assertions=Assertions,packedInputs=256,factors=5,refreshToggleRounds=20}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
