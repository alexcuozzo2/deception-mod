using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // DRAFT: normalize only the probe's supplied protected census. This does
    // not change shared WeakIdentity, omit roots, clone native objects or grant
    // private authority. Call before constructing the first context weak root.
    internal static class NativeProtectedCensus
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Protected census: "+why);}
        internal static object[] Normalize(IReadOnlyList<object> values)
        {
            ArgumentNullException.ThrowIfNull(values);Require(values.Count<8192,"Census bound exceeded");
            object[] original=values.ToArray();var converted=new object[original.Length];var pointers=new long[original.Length];
            try
            {
                var nativeObject=Native.T("Il2CppSystem.Object");
                for(int i=0;i<original.Length;i++)
                {
                    object value=original[i]??throw new InvalidDataException("Protected census: null source root");
                    pointers[i]=Native.Pointer(value);Require(pointers[i]!=0,"Null source pointer");
                    object target=Native.As(value,"Il2CppSystem.Object");
                    Require(target is not null&&nativeObject.IsInstanceOfType(target)&&Native.Same(value,target),"Native object cast changed identity");
                    converted[i]=target!;
                }
                Require(values.Count==original.Length&&values.Select(Native.Pointer).SequenceEqual(pointers)&&
                    converted.Select(Native.Pointer).SequenceEqual(pointers),"Census count/order/identity changed");
                return converted;
            }
            finally{GC.KeepAlive(original);GC.KeepAlive(values);}
        }
    }
}
