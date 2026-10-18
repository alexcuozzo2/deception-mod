using System;
using System.IO;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Tribe backgrounds deserialize packaged maps through the ordinary native
    // serializer. They are UI previews, never siblings of an open Local match.
    // Scope only the observed synchronous producer, not arbitrary unbound reads.
    internal static class NativeMenuPreviewScope
    {
        [ThreadStatic] static int Depth;
        internal static void Install(Harmony harmony)=>Native.Hook(harmony,
            Native.M("SelectTribePopup","SetBackground"),typeof(NativeMenuPreviewScope),nameof(Before),null,nameof(Finished));
        public static void Before(out int __state){__state=Depth;Depth=checked(Depth+1);}
        public static Exception? Finished(Exception? __exception,int __state)
        {Depth=__state;return __exception;}
        internal static bool Unbound(object state,bool enrolled)
        {
            if(Depth==0)return false;
            if(enrolled||NativeGameStateEnvelope.Get(state) is not null)
                throw new InvalidDataException("A tribe preview cannot serialize an enrolled or framed match state.");
            // Framed input still passes through NativeRegroupSession.BeforeFrame
            // and is rejected without a production transport token.
            return true;
        }
    }
}
