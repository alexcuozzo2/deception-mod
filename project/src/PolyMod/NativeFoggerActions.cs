using System;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // DoubleReady is the native DoubleAttack allowance, not mod persistence:
    // EndTurn restores it and native serialization already saves it. Consume
    // it exactly as AttackCommand does, while Blackout never deals damage.
    internal static class NativeFoggerActions
    {
        internal static bool IsFogger(object? unit)=>unit is not null&&RecruitmentHooks.Registered&&
            FoggerCatalog.IsFoggerId(Native.EnumName(Native.Get(unit,"type")!));
        internal static bool Ready(object state,object unit,byte actor)=>IsFogger(unit)&&
            Native.Int(Native.Get(unit,"health"))>0&&NativeRogueRules.CanCommand(state,unit,actor)&&
            !(bool)Native.Get(unit,"attacked")!&&
            !Native.Items(Native.Get(unit,"effects")!).Any(e=>Native.Int(e)==0);
        internal static void SpendBlackout(object state,object unit)
        {
            if(!Ready(state,unit,Convert.ToByte(Native.Get(state,"CurrentPlayer"))))
                throw new InvalidDataException("Fogger has no Blackout action.");
            // Read actual effects directly. Presentation may temporarily answer
            // HasEffect using a disguise; it cannot grant a real second shot.
            bool first=Native.Items(Native.Get(unit,"effects")!).Any(e=>Native.Int(e)==7);
            Native.Invoke(Native.M("UnitState","MakeExhauseted","GameState"),unit,state);
            if(!(bool)Native.Get(unit,"moved")!||!(bool)Native.Get(unit,"attacked")!||
                Native.Items(Native.Get(unit,"effects")!).Any(e=>Native.Int(e)==7))
                throw new InvalidDataException("Native Blackout did not consume its action allowance.");
            if(first)Native.Set(unit,"attacked",false);
            // Keep moved=true even when firing in place: Dash grants movement
            // before shooting, never another move between the two Blackouts.
        }
    }
}
