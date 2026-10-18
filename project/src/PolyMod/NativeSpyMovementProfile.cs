using System;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Detached appearance checks also use native Hide/Invisible predicates.
    // A Spy mask must obey normal occupancy and ZOC even when its real body
    // is a Cloak. This scope never changes either native unit or shared data.
    internal static class NativeSpyMovementProfile
    {
        [ThreadStatic] static Scope? Current;
        static long Generation;
        internal static void Clear(){Generation++;Current=null;}
        static bool At(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        internal sealed class Scope:IDisposable
        {
            readonly Scope? Previous;
            internal readonly object State,Source,Profile,Data,Map,Frame,Origin;
            readonly byte Owner;readonly uint Turn;readonly long Epoch;bool Closed;
            internal Scope(object state,object source,object profile,object frame)
            {State=state;Source=source;Profile=profile;Data=Native.Get(profile,"UnitData")!;Map=Native.Get(state,"Map")!;Frame=frame;Origin=Native.Get(source,"coordinates")!;
             Owner=Convert.ToByte(Native.Get(source,"owner"));Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));Epoch=Generation;Previous=Current;Current=this;}
            internal bool Valid=>!Closed&&Epoch==Generation&&NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(State)&&
                ReferenceEquals(Frame,NativeGameStateEnvelope.Get(State))&&Native.Same(Map,Native.Get(State,"Map"))&&
                Owner==Native.Int(Native.Get(Source,"owner"))&&Turn==Convert.ToUInt32(Native.Get(State,"CurrentTurn"))&&
                At(Origin,Native.Get(Source,"coordinates")!)&&Tile(State,Origin) is object tile&&Native.Same(Source,Native.Get(tile,"unit"))&&
                Native.Same(Data,Native.Get(Profile,"UnitData"));
            public void Dispose(){if(Closed)return;Closed=true;if(ReferenceEquals(Current,this))Current=Epoch==Generation?Previous:null;}
        }
        internal static Scope? Begin(object state,object source,object profile)
        {
            if(!SpyCatalog.IsSpyId(Native.EnumName(Native.Get(profile,"type")!))||Native.Same(source,profile)||
                !NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state)||NativeGameStateEnvelope.Get(state) is not object frame||
                Tile(state,Native.Get(source,"coordinates")!) is not object tile||!Native.Same(source,Native.Get(tile,"unit")))return null;
            return new(state,source,profile,frame);
        }
        internal static bool Suppress(object value,bool data=false)=>Current is Scope s&&s.Valid&&Native.Same(value,data?s.Data:s.Profile);
    }
}
