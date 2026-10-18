using System;
using System.Collections.Generic;

namespace Deception.PolyModAdapter
{
    // Older presentation fixtures do not exercise upgrade offers or commands.
    // The real upgrade UI/transaction has its own fixture. An explicit owned
    // button list permits testing the shared action menu's identity ranking.
    internal static class NativeApparentUpgradeUi
    {
        internal static object? FixtureBar;
        internal static object[] FixtureButtons=Array.Empty<object>();
        internal static IEnumerable<object> OwnedButtons(object bar)=>
            ReferenceEquals(bar,FixtureBar)?FixtureButtons:Array.Empty<object>();
        internal static void Sync(object bar,object? unit,bool allowed){}
        internal static void Remove(object bar,bool reflow=true)
        {if(ReferenceEquals(bar,FixtureBar))Reset();}
        internal static void Reset(){FixtureBar=null;FixtureButtons=Array.Empty<object>();}
    }
}
