using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;
using WeakIdentity=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;

namespace Deception.PolyModAdapter
{
    internal static partial class NativeRegroupSession
    {
        // Native ConvertStateOnFileToSummary reads three saved states, then
        // serializes the current one to the native GameStateSummary parser.
        // These are detached menu data, never gameplay siblings of Current.
        [ThreadStatic] static SummaryRead? SummaryScope;
        static readonly Dictionary<long,WeakIdentity> SummaryStates=new();
        internal sealed class SummaryRead
        {
            internal readonly SummaryRead? Previous;
            internal readonly Guid Epoch;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly List<(object State,bool Complete)> Reads=new();
            internal bool Closed;
            internal SummaryRead(Guid epoch){Epoch=epoch;Previous=SummaryScope;}
            internal void Check(object state)
            {
                Require(!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(SummaryScope,this),"Saved-match summary scope ended or changed.");
                Require(Reads.Any(x=>Native.Same(x.State,state)),"Summary state lacks its exact read witness.");
            }
            internal void Complete(object state)
            {
                Check(state);int index=Reads.FindIndex(x=>Native.Same(x.State,state));
                var frame=NativeGameStateEnvelope.Get(state);
                if(frame is not null)Require(frame.Epoch==Epoch,"Summary decoded a different saved match.");
                // A summary needs native public fields only. Do not emit a
                // custom frame into the native, unextended summary parser.
                NativeGameStateEnvelope.Forget(state);Reads[index]=(state,true);
            }
        }
        static void InstallSummaryReads(Harmony harmony)=>Native.Hook(harmony,
            Native.M("MultiplayerScreen","ConvertStateOnFileToSummary","System.String"),typeof(NativeRegroupSession),nameof(BeforeSummary),null,nameof(SummaryFinished));
        public static void BeforeSummary(string __0,out object? __state)
        {
            __state=null;if(!Enabled)return;
            Require(Buying is null,"Summary cannot run inside an unfinished paid action.");
            Require(Path.GetExtension(__0)==".state"&&Guid.TryParseExact(Path.GetFileNameWithoutExtension(__0),"D",out _),"Saved summary filename lacks its native match Guid.");
            Guid epoch=Guid.ParseExact(Path.GetFileNameWithoutExtension(__0),"D");Require(epoch!=Guid.Empty,"Saved summary has empty match Guid.");
            int depth=0;for(var s=SummaryScope;s is not null;s=s.Previous)depth++;
            Require(depth<8,"Saved summary nesting bound exceeded.");
            var scope=new SummaryRead(epoch);SummaryScope=scope;__state=scope;
        }
        public static Exception? SummaryFinished(Exception? __exception,object? __state)
        {
            if(__state is not SummaryRead scope)return __exception;
            try
            {
                Require(ReferenceEquals(SummaryScope,scope)&&scope.Thread==Environment.CurrentManagedThreadId,"Summary finalizer scope mismatch.");
                foreach(var item in scope.Reads)NativeGameStateEnvelope.Forget(item.State);
            }
            finally{scope.Closed=true;SummaryScope=scope.Previous;scope.Reads.Clear();}
            return __exception;
        }
        static bool WasSummary(object state)
        {
            if(!SummaryStates.TryGetValue(Native.Pointer(state),out var weak))return false;
            if(weak.Matches(state))return true;SummaryStates.Remove(Native.Pointer(state));return false;
        }
        static void RejectSummaryEscape(object state)=>Require(!WasSummary(state),"Detached saved summary cannot become a gameplay transport state.");
        static Transport SummaryTransport(SummaryRead scope,object state,bool reading,bool enrolled)
        {
            Require(!enrolled&&NativeGameStateEnvelope.Get(state) is null,"Saved summary cannot access an enrolled or framed gameplay state.");
            if(reading)
            {
                Require(!WasSummary(state),"Summary read receiver was already used.");
                Require(scope.Reads.Count<16,"Saved summary state count exceeded.");
                foreach(var key in SummaryStates.Where(x=>x.Value.Target() is null).Select(x=>x.Key).ToArray())SummaryStates.Remove(key);
                Require(SummaryStates.Count<MaxOwned,"Too many live saved summary states.");
                SummaryStates.Add(Native.Pointer(state),new WeakIdentity(state));scope.Reads.Add((state,false));
            }
            else Require(scope.Reads.Any(x=>x.Complete&&Native.Same(x.State,state)),"Summary writer lacks a completed detached read.");
            scope.Check(state);return new Transport(scope,state,reading);
        }
        internal static void TransportFault(string reason)
        {
            // A bad file may be omitted by the native menu without disabling
            // a different active match. The operation itself still rejects.
            if(SummaryScope is null)Fault(reason);
        }
    }
}
