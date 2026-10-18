using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Bind by full managed signatures; no guessed native addresses / raw memory access.
    internal static class Native
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static readonly Dictionary<string,Type> Types=new();
        static readonly Dictionary<string,MethodInfo> Methods=new();
        static readonly Dictionary<string,PropertyInfo> Properties=new();
        public static Type T(string name)
        {
            if(Types.TryGetValue(name,out var t))return t;
            var types=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name,false)).Where(x=>x is not null).Cast<Type>().ToArray();
            if(types.Length!=1)throw new TypeLoadException("Expected one native wrapper type: "+name+"; got "+types.Length);
            return Types[name]=types[0];
        }
        public static MethodInfo M(string owner,string name,params string[] args)=>M(T(owner),name,args);
        public static MethodInfo M(Type owner,string name,params string[] args)
        {
            string key=owner.AssemblyQualifiedName+"::"+name+"("+string.Join(",",args)+")";
            if(Methods.TryGetValue(key,out var m))return m;
            var found=owner.GetMethods(Flags).Where(x=>x.Name==name&&!x.IsGenericMethodDefinition&&
                x.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(args)).ToArray();
            if(found.Length!=1)throw new MissingMethodException("Native signature mismatch: "+key);
            return Methods[key]=found[0];
        }
        public static object? Invoke(MethodInfo m,object? target,params object?[] args)
        {
            try{return m.Invoke(target,args);}catch(TargetInvocationException e) when(e.InnerException is not null)
            {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
        }
        static PropertyInfo P(Type t,string name)
        {
            string key=t.AssemblyQualifiedName+"::"+name;
            if(Properties.TryGetValue(key,out var p))return p;
            // Closest declaration wins for native wrappers that hide inherited properties.
            for(Type? level=t;level is not null;level=level.BaseType)
            {
                p=level.GetProperty(name,Flags|BindingFlags.DeclaredOnly);
                if(p is not null)return Properties[key]=p;
            }
            throw new MissingMemberException(t.FullName,name);
        }
        public static object As(object value,string wrapperType)
        {
            var target=T(wrapperType);
            if(target.IsInstanceOfType(value))return value;
            var cast=value.GetType().GetMethods(Flags).Single(m=>m.Name=="Cast"&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1&&m.GetParameters().Length==0);
            return Invoke(cast.MakeGenericMethod(target),value)!;
        }
        public static object DetachedClone(object source,string wrapperType)
        {
            // A managed MemberwiseClone would alias the SAME native object.
            // Invoke the exact IL2CPP Object clone and check native identity
            // before any display-only scalar is written.
            Type owner=T("Il2CppSystem.Object");
            var method=owner.GetMethods(Flags|BindingFlags.DeclaredOnly).Single(m=>
                m.Name=="MemberwiseClone"&&m.GetParameters().Length==0&&m.ReturnType==owner);
            object clone=As(Invoke(method,source)??throw new InvalidOperationException("Native display clone is null"),wrapperType);
            if(ReferenceEquals(source,clone)||Same(source,clone))
                throw new InvalidOperationException("Native display clone aliases authoritative source");
            return clone;
        }
        public static object? Get(object o,string p)=>P(o.GetType(),p).GetValue(o);
        public static object? Static(string type,string p)=>P(T(type),p).GetValue(null);
        public static void Set(object o,string p,object? value)=>P(o.GetType(),p).SetValue(o,value);
        public static int Int(object? x)=>Convert.ToInt32(x,System.Globalization.CultureInfo.InvariantCulture);
        public static long Pointer(object o)=>((IntPtr)(Get(o,"Pointer") ?? IntPtr.Zero)).ToInt64();
        public static bool Same(object? a,object? b)=>a is not null&&b is not null&&Pointer(a)==Pointer(b);
        public static bool UnityAlive(object? value)=>value is not null&&
            (bool)Invoke(M("UnityEngine.Object","op_Implicit","UnityEngine.Object"),null,value)!;
        public static IEnumerable<object> Items(object list)
        {
            int n=Int(Get(list,"Count"));var indexer=list.GetType().GetProperties(Flags)
                .Single(p=>p.Name=="Item"&&p.GetIndexParameters().Length==1&&p.GetIndexParameters()[0].ParameterType==typeof(int));
            for(int i=0;i<n;i++)if(indexer.GetValue(list,new object[]{i}) is object value)yield return value;
        }
        public static object EnumValue(string enumName,string id)
        {
            Type cache=T("EnumCache`1").MakeGenericType(T(enumName));
            object value=Invoke(M(cache,"GetType","System.String"),null,id) ?? throw new InvalidOperationException("Missing enum mapping "+id);
            if(EnumName(value)!=id.ToLowerInvariant())throw new InvalidOperationException("Native enum mapping did not round-trip: "+id);
            return value;
        }
        public static string EnumName(object value)
        {
            Type cache=T("EnumCache`1").MakeGenericType(value.GetType());
            return ((string)(Invoke(M(cache,"GetName",value.GetType().FullName!),null,value) ?? "")).ToLowerInvariant();
        }
        public static object Callback(Action action)
        {
            return Invoke(M("Il2CppSystem.Action","op_Implicit","System.Action"),null,action)!;
        }
        public static object NewList(string elementName,IEnumerable<object> items)
        {
            var type=T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(T(elementName));
            var list=Activator.CreateInstance(type)??throw new InvalidOperationException("Cannot create native list");
            var add=M(type,"Add",elementName);
            foreach(var item in items)Invoke(add,list,item);
            return list;
        }
        // Use the generated native ButtonAction conversion, not an unmanaged function
        // pointer with an invented event signature. The caller roots this delegate until
        // all native buttons have been cleared.
        public static object ButtonCallback(Action action)
        {
            var make=typeof(Native).GetMethod(nameof(ButtonCallbackTyped),BindingFlags.Static|BindingFlags.NonPublic)!;
            return Invoke(make.MakeGenericMethod(T("UnityEngine.EventSystems.BaseEventData")),null,action)!;
        }
        static object ButtonCallbackTyped<TEvent>(Action action)
        {
            Action<int,TEvent> callback=(_,_)=>action();
            return Invoke(M("UIButtonBase+ButtonAction","op_Implicit",typeof(Action<int,TEvent>).FullName!),null,callback)!;
        }
        // Generated wrappers for nonblittable IL2CPP value types are managed
        // reference classes. The installed 64-bit trampoline maps those to a
        // pointer, which is not their native by-value ABI. In particular,
        // PathFinderSettings is a 56-byte stack value on this Intel Mac path.
        // Reflected calls remain valid; only installing such a detour is denied.
        internal static void RequireHookAbi(MethodInfo target)
        {
            static bool WrappedValue(Type type)
            {
                if(type.IsByRef)type=type.GetElementType()!;
                for(Type? current=type;current is not null;current=current.BaseType)
                    if(current.FullName=="Il2CppSystem.ValueType")return true;
                return false;
            }
            if(WrappedValue(target.ReturnType)||target.GetParameters().Any(p=>WrappedValue(p.ParameterType))||
                (!target.IsStatic&&target.DeclaringType is Type receiver&&WrappedValue(receiver)))
                throw new NotSupportedException("Native hook requires an explicit value-type ABI adapter: "+target);
        }
        public static void Hook(Harmony h,MethodInfo target,Type patch,string? pre=null,string? post=null,string? final=null)
        {
            RequireHookAbi(target);
#if DECEPTION_WINDOWS_PORT_CANDIDATE
            if(!NativeWindowsHooks.Prepare(ref target,patch,pre,post,final))return;
#endif
            HarmonyMethod? H(string? n)=>n is null?null:new HarmonyMethod(patch.GetMethod(n,Flags)??throw new MissingMethodException(patch.FullName,n));
            h.Patch(target,H(pre),H(post),null,H(final),null);
        }
        public static void RequireReturn(MethodInfo method,Type expected)
        {if(method.ReturnType!=expected)throw new MissingMethodException("Unexpected return signature: "+method);}
    }
}
