using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Deception.PolyModAdapter
{
    // Original artwork remains byte-identical. Sprite rectangles trim only the
    // transparent canvas at rendering time. Audio is locally decoded PCM.
    internal static class SmokeGrenadeAssets
    {
        static object? Menu,ProjectileSprite,Clip;
        static bool AudioFailed;
        static byte[] Read(string name,string sha)
        {
            var bytes=ModContentAssets.Read("assets/spy-effects/"+(name.EndsWith(".png",StringComparison.Ordinal)?"dcp_"+name.Substring(0,name.Length-4)+"__.png":name));
            if(bytes.Length>2_000_000)throw new InvalidDataException("Spy effect asset exceeds bound.");
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Spy effect asset hash changed: "+name);
            return bytes;
        }
        static object Image(bool icon)
        {
            var old=icon?Menu:ProjectileSprite;
            if(Native.UnityAlive(old)&&Native.UnityAlive(Native.Get(old!,"texture")))return old!;
            if(Native.UnityAlive(old))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,old);
            var bytes=Read(icon?"smoke_grenade_icon.png":"smoke_grenade_projectile.png",icon?"f74c08e7d64580c33c3126b62a2c230978517f0603a450b9bc0cd52e8790e206":"04c68d7a7aaef6c0676acbabd804b266cc5edb8b4e73b3f9304b827b8adb0aa9");
            object sprite=UnitArtAssets.DecodeLoadedFrame("assets/spy-effects/dcp_"+(icon?"smoke_grenade_icon":"smoke_grenade_projectile")+"__.png",3870,3870,icon?590:1203,icon?710:916,icon?2829:1499,icon?2670:1940);
            var texture=Native.Get(sprite,"texture")!;
            if(icon)
            {
                object? replacement=null;
                try
                {
                    var reference=MimicControls.NativeSprite("UNKNOWN_ICON");var frame=Native.Get(reference,"rect")!;
                    float size=Math.Max(Convert.ToSingle(Native.Get(frame,"width")),Convert.ToSingle(Native.Get(frame,"height")));
                    float ppu=2829*Convert.ToSingle(Native.Get(reference,"pixelsPerUnit"))/size;
                    if(!float.IsFinite(ppu)||ppu<=0)throw new InvalidDataException("Invalid native icon scale.");
                    replacement=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                        texture,Native.Get(sprite,"rect"),Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),ppu,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"));
                    if(!Native.UnityAlive(replacement))throw new InvalidDataException("Smoke icon was not created.");
                }
                catch{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,sprite);throw;}
                Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,sprite);sprite=replacement!;Menu=sprite;
            }
            else ProjectileSprite=sprite;
            return sprite;
        }
        internal static object Icon()=>Image(true);
        internal static object Projectile()=>Image(false);
        internal sealed record Pcm(int Channels,int Rate,float[] Samples);
        internal static Pcm DecodeWave(byte[] bytes)
        {
            using var stream=new MemoryStream(bytes,false);using var r=new BinaryReader(stream);
            string Tag()=>System.Text.Encoding.ASCII.GetString(r.ReadBytes(4));
            if(bytes.Length<44||Tag()!="RIFF"||r.ReadUInt32()!=bytes.Length-8||Tag()!="WAVE")throw new InvalidDataException("Invalid smoke WAV header.");
            int channels=0,rate=0,align=0;byte[]? data=null;
            while(stream.Position+8<=stream.Length)
            {
                var tag=Tag();uint length=r.ReadUInt32();long end=stream.Position+length;
                if(end>stream.Length)throw new InvalidDataException("Truncated smoke WAV chunk.");
                if(tag=="fmt ")
                {
                    if(channels!=0||length<16||r.ReadUInt16()!=1)throw new InvalidDataException("Smoke WAV must be PCM16.");
                    channels=r.ReadUInt16();rate=r.ReadInt32();int bytesPerSecond=r.ReadInt32();align=r.ReadUInt16();int bits=r.ReadUInt16();
                    if(channels is <1 or >2||rate is <8000 or >96000||bits!=16||align!=channels*2||bytesPerSecond!=rate*align)throw new InvalidDataException("Invalid smoke WAV format.");
                }
                else if(tag=="data"){if(data is not null||length>2_000_000)throw new InvalidDataException("Invalid smoke WAV data.");data=r.ReadBytes((int)length);}
                stream.Position=end+(length&1);
            }
            if(data is null||channels==0||data.Length==0||data.Length%align!=0||data.Length/align>rate*10)throw new InvalidDataException("Invalid smoke WAV sample count.");
            var samples=new float[data.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(data[2*i]|data[2*i+1]<<8)/32768f;
            return new(channels,rate,samples);
        }
        // These installed Unity facades use missing Il2Cpp ReadOnlySpan members:
        // AudioClip.Create/SetData and DownloadHandlerAudioClip.Create. The exact
        // generated *_Injected methods instead invoke existing Unity delegates.
        // Supply their audited CLR span value directly while its managed buffer
        // is pinned, only for the duration of each synchronous native copy.
        // Installed interop uses ExplicitLayout (TypeDef0x100111), not the
        // sequential layout of an ordinary hand-written span-shaped struct.
        internal static void RequireSpanLayout(Type type)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var fields=type.GetFields(flags);
            if(IntPtr.Size!=8||!type.IsValueType||!type.IsExplicitLayout||fields.Length!=2||
               type.GetField("begin",flags)?.FieldType!=typeof(IntPtr)||type.GetField("length",flags)?.FieldType!=typeof(int)||
               Marshal.SizeOf(type)!=16||Marshal.OffsetOf(type,"begin")!=IntPtr.Zero||Marshal.OffsetOf(type,"length")!=new IntPtr(8))
                throw new NotSupportedException("Unexpected Unity ManagedSpanWrapper layout.");
        }
        internal sealed class PinnedBuffer:IDisposable
        {
            GCHandle Handle;readonly FieldInfo Begin,Length;
            internal object Span{get;}
            internal PinnedBuffer(Array buffer)
            {
                if(buffer is not char[]&&buffer is not float[])throw new ArgumentException("Unsupported audio buffer.");
                if(buffer.Length==0)throw new ArgumentException("Empty audio buffer.");
                var type=Native.T("UnityEngine.Bindings.ManagedSpanWrapper");RequireSpanLayout(type);
                const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
                Begin=type.GetField("begin",flags)!;Length=type.GetField("length",flags)!;
                Span=Activator.CreateInstance(type)??throw new InvalidOperationException("Cannot create Unity span value.");
                try{Handle=GCHandle.Alloc(buffer,GCHandleType.Pinned);Begin.SetValue(Span,Handle.AddrOfPinnedObject());Length.SetValue(Span,buffer.Length);}
                catch{Dispose();throw;}
            }
            public void Dispose()
            {
                try{Begin.SetValue(Span,IntPtr.Zero);Length.SetValue(Span,0);}
                finally{if(Handle.IsAllocated)Handle.Free();}
            }
        }
        internal static void PrepareSound()
        {
            if(AudioFailed||Native.UnityAlive(Clip))return;
            object? candidate=null;
            try
            {
                var bytes=Read("ninja_smoke.wav","64de332f5839795a8969e4d567921044cc86c88ca36dd0cb56b4743df148353f");
                var pcm=DecodeWave(bytes);
                using var name=new PinnedBuffer("Deception smoke grenade".ToCharArray());
                using var samples=new PinnedBuffer(pcm.Samples);
                candidate=Native.Invoke(Native.M("UnityEngine.AudioClip","Construct_Internal"),null);
                if(!Native.UnityAlive(candidate))throw new InvalidDataException("Smoke audio clip was not constructed.");
                // This is the Unity engine object pointer, distinct from the
                // generated wrapper's Il2Cpp managed-object Pointer property.
                var nativeClip=(IntPtr)Native.Get(candidate!,"m_CachedPtr")!;
                if(nativeClip==IntPtr.Zero)throw new InvalidDataException("Smoke audio clip has no native object.");
                Native.Invoke(Native.M("UnityEngine.AudioClip","CreateUserSound_Injected","System.IntPtr","UnityEngine.Bindings.ManagedSpanWrapper&","System.Int32","System.Int32","System.Int32","System.Boolean"),null,
                    nativeClip,name.Span,pcm.Samples.Length/pcm.Channels,pcm.Channels,pcm.Rate,false);
                if(!(bool)Native.Invoke(Native.M("UnityEngine.AudioClip","SetData_Injected","System.IntPtr","UnityEngine.Bindings.ManagedSpanWrapper&","System.Int32"),null,nativeClip,samples.Span,0)!)
                    throw new InvalidDataException("Smoke audio samples were not accepted.");
                if(!Native.UnityAlive(candidate)||Native.Int(Native.Get(candidate!,"samples"))!=pcm.Samples.Length/pcm.Channels||
                    Native.Int(Native.Get(candidate!,"channels"))!=pcm.Channels||Native.Int(Native.Get(candidate!,"frequency"))!=pcm.Rate)
                    throw new InvalidDataException("Smoke audio clip format changed during creation.");
                Clip=candidate;candidate=null;
            }
            catch(Exception e)
            {
                AudioFailed=true;
                if(candidate is not null)try{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,candidate);}catch(Exception cleanup){Entry.Note("spy-audio-cleanup",cleanup.Message);}
                Entry.Note("spy-audio-load-error",e.ToString());
            }
        }
        // Retained for the existing effect lifecycle; preparation is synchronous
        // and cached, so the render callback performs no decoder work.
        internal static void PollSound(){}
        static object Sound()
        {
            PrepareSound();PollSound();
            return Native.UnityAlive(Clip)?Clip!:throw new InvalidDataException("Smoke audio was unavailable at impact.");
        }
        internal static void ImpactSound()
        {
            if(!Native.UnityAlive(Native.Static("AudioManager","instance"))||!(bool)Native.Invoke(Native.M("AudioManager","ShouldPlaySoundEffects"),null)!)return;
            // Borrow only the native SFX bus. Do not alter its clip, volume,
            // pan, mixer or playback, so the user's mute/volume settings apply.
            var source=Native.Invoke(Native.M("AudioManager","GetAudioSource","AudioManager+AudioSourceTypes"),null,Enum.Parse(Native.T("AudioManager+AudioSourceTypes"),"SFX"));
            if(Native.UnityAlive(source))Native.Invoke(Native.M("UnityEngine.AudioSource","PlayOneShot","UnityEngine.AudioClip","System.Single"),source,Sound(),1f);
        }
    }
}
