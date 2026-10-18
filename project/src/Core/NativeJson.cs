using System;
using System.Reflection;
namespace Deception.Core
{
    // IL2CPP proxy classes can declare a NEW virtual ToString slot instead of
    // overriding System.Object.ToString. Calling object.ToString would then yield
    // the wrapper type's name rather than JSON. Dispatch the proxy method explicitly.
    public static class NativeJson
    {
        public static string Read(object proxy)
        {
            if(proxy is null) throw new RuleViolation("Missing native JSON object");
            for(Type? type=proxy.GetType();type is not null && type!=typeof(object);type=type.BaseType)
            {
                var method=type.GetMethod("ToString",BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly,
                    null,Type.EmptyTypes,null);
                if(method is not null && method.ReturnType==typeof(string))
                    return method.Invoke(proxy,null) as string ?? throw new RuleViolation("Native JSON ToString returned null");
            }
            throw new RuleViolation("Native JSON ToString dispatch not found; refusing System.Object fallback");
        }
    }
}
