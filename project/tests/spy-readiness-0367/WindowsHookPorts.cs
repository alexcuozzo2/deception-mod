#if DECEPTION_WINDOWS_PORT_CANDIDATE
using System;
using System.Reflection;
namespace Deception.PolyModAdapter
{
    internal static class NativeWindowsHooks
    {
        internal static bool Enabled;
        internal static bool Prepare(ref MethodInfo target,Type patch,string? pre,string? post,string? final)=>true;
    }
}
#endif
