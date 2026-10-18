using System;
using System.Runtime.InteropServices;
namespace Deception.PolyModAdapter
{
    // Typed POD and contained callbacks only. No native library/load/install.
    internal static class NativeCommandOuterAbi
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Call
        {internal uint Size,Operation;internal int Version;internal uint Reserved;internal IntPtr Input,CommandOut,VersionOut,MethodInfo;}
        [StructLayout(LayoutKind.Sequential)] internal struct Value
        {internal IntPtr Result;internal int Version;internal uint Boolean;}
        [StructLayout(LayoutKind.Sequential)] internal struct Plan
        {internal ulong Cookie;internal Value Handled;}
        [StructLayout(LayoutKind.Sequential)] internal struct Config
        {internal uint Size,Version;internal IntPtr Write,Read,Prepare,Finish,Cleanup,Factory,Raise,Image,Namespace,Name;}
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int PrepareCallback(IntPtr call,IntPtr plan);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int FinishCallback(IntPtr call,IntPtr plan,IntPtr value,int originalRan);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CleanupCallback(IntPtr call,IntPtr plan,int outcome);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr WriteOriginal(IntPtr input,int version,IntPtr methodInfo);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate byte ReadOriginal(IntPtr input,IntPtr commandOut,IntPtr versionOut,IntPtr methodInfo);
        internal static readonly PrepareCallback PrepareRoot=Prepare;
        internal static readonly FinishCallback FinishRoot=Finish;
        internal static readonly CleanupCallback CleanupRoot=Cleanup;
        internal static long Prepared,Finished,Cleaned,Rejected;
        static void Validate(Call call)
        {if(call.Size!=48||call.Operation>1||call.Reserved!=0||call.Operation==0&&(call.CommandOut!=IntPtr.Zero||call.VersionOut!=IntPtr.Zero)||call.Operation==1&&call.Version!=0)throw new InvalidOperationException("Outer command POD call differs.");}
        static int Prepare(IntPtr cp,IntPtr pp)
        {
            Plan plan=default;int decision=2;
            try
            {
                if(cp==IntPtr.Zero||pp==IntPtr.Zero)throw new InvalidOperationException("Null outer POD.");
                Prepared++;var call=Marshal.PtrToStructure<Call>(cp);Validate(call);plan=Marshal.PtrToStructure<Plan>(pp);
                decision=NativeCommandOuterAdapter.Prepare(call,ref plan);
            }
            catch{Rejected++;decision=2;}
            // A cookie assigned before an allocation/read failure still
            // reaches native Cleanup and its exact outer journal.
            if(pp!=IntPtr.Zero)try{Marshal.StructureToPtr(plan,pp,false);}catch{Rejected++;decision=2;}
            return decision;
        }
        static int Finish(IntPtr cp,IntPtr pp,IntPtr vp,int ran)
        {
            try
            {
                if(cp==IntPtr.Zero||pp==IntPtr.Zero||vp==IntPtr.Zero||ran is <0 or >1)throw new InvalidOperationException("Invalid outer finish POD.");
                Finished++;var c=Marshal.PtrToStructure<Call>(cp);Validate(c);
                NativeCommandOuterAdapter.Finish(c,Marshal.PtrToStructure<Plan>(pp),Marshal.PtrToStructure<Value>(vp),ran);return 0;
            }
            catch{Rejected++;return 1;}
        }
        static int Cleanup(IntPtr cp,IntPtr pp,int outcome)
        {
            try
            {
                if(cp==IntPtr.Zero||pp==IntPtr.Zero||outcome is <0 or >5)throw new InvalidOperationException("Invalid outer cleanup POD.");
                Cleaned++;var c=Marshal.PtrToStructure<Call>(cp);Validate(c);
                NativeCommandOuterAdapter.Cleanup(c,Marshal.PtrToStructure<Plan>(pp),outcome);return 0;
            }
            catch{Rejected++;return 1;}
        }
    }
}
