// Managed compiler metadata only. The final adapter combines the bundled .NET
// references with generated IL2CPP references; that reference set failed nullable
// attribute synthesis (CS0656), although the managed-only fixtures compiled.
// Supply the exact managed constructors locally, without changing any runtime
// library or weakening nullable checking. These types are private to this DLL.
// The native build audits both their shape and their actual use before packaging.
#nullable disable
namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.All, AllowMultiple=false, Inherited=false)]
    internal sealed class NullableAttribute : global::System.Attribute
    {
        public readonly byte[] NullableFlags;
        public NullableAttribute(byte flag) { NullableFlags=new[]{flag}; }
        public NullableAttribute(byte[] flags) { NullableFlags=flags; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.All, AllowMultiple=false, Inherited=false)]
    internal sealed class NullableContextAttribute : global::System.Attribute
    {
        public readonly byte Flag;
        public NullableContextAttribute(byte flag) { Flag=flag; }
    }
}
