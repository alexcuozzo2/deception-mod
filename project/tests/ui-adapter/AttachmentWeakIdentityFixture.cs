// Synthetic native lifetime for the UI adapter suite. The production identity
// uses an IL2CPP WeakReference; managed GC is only a fixture substitute here.
using System;
namespace Deception.PolyModAdapter
{
    internal static class NativeLedgerOwnership
    {
        internal sealed class WeakIdentity
        {
            readonly WeakReference<object> Weak;
            internal WeakIdentity(object value){Weak=new(value);}
            internal object? Target()=>Weak.TryGetTarget(out var value)?value:null;
            internal bool Matches(object value)=>Target() is object target&&Native.Same(target,value);
        }
    }
}
