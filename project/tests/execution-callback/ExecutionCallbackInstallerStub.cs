using System;
using System.Reflection;
using System.Threading;

namespace Deception.PolyModAdapter
{
    // Callback-only harness: none of these installer/native-access methods may run.
    // Harmony is an exact existing compiler reference; no Harmony API is invoked.
    internal static class Native
    {
        internal static int Calls;
        static Exception Forbidden(){Interlocked.Increment(ref Calls);return new InvalidOperationException("Native installer/game access is forbidden in callback tests.");}
        internal static Type T(string name)=>throw Forbidden();
        internal static MethodInfo M(string owner,string name,params string[] arguments)=>throw Forbidden();
        internal static object? Static(string type,string member)=>throw Forbidden();
        internal static object? Get(object value,string member)=>throw Forbidden();
        internal static object? Invoke(MethodInfo method,object? instance,params object?[] arguments)=>throw Forbidden();
    }
}
