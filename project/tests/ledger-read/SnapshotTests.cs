using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Snapshot=Deception.PolyModAdapter.NativeGameStateEnvelope.Snapshot;

namespace Deception.Tests
{
    public static class SnapshotTests
    {
        static int Assertions;
        static void Check(bool value){Assertions++;if(!value)throw new Exception("Snapshot assertion "+Assertions);}
        static readonly Guid Epoch=Guid.Parse("d2150197-6174-4095-a2e3-983953cb9a57");
        static LedgerAppearance Appearance(string form)=>new(form,new("imperius","default"),new("imperius","default"),new("imperius","default"),0,100,100,false,ApparentConcealment.Visible,-1,new[]{"Poison"});
        static byte[] Bytes(string form="warrior",int extra=0)
        {
            var units=new[]{new LedgerUnit(1,3,true,LedgerActualKind.Mimic,true,Appearance(form),PaidInvestmentKind.Known,3)}
                .Concat(Enumerable.Range(0,extra).Select(n=>new LedgerUnit((ulong)n+2,(uint)n+4,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0))).ToArray();
            return NativeIdentityLedgerCodec.Encode(new(Epoch,(ulong)units.Length+1,1,units,Array.Empty<LedgerReceipt>()),0);
        }
        static long ReadCost(Snapshot snapshot,int count)
        {
            _=snapshot.Epoch;_=snapshot.ReadAppearance(3);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<count;i++){_=snapshot.Epoch;_=snapshot.ReadAppearance(3);}
            return GC.GetAllocatedBytesForCurrentThread()-before;
        }
        public static int Run()
        {
            try
            {
                byte[] bytes=Bytes();var snapshot=new Snapshot(0,bytes);bytes[0]=0;
                Check(snapshot.Revision==0&&snapshot.Epoch==Epoch);
                byte[] copy=snapshot.Bytes();copy[0]=0;
                Check(snapshot.Bytes()[0]!=0&&snapshot.ReadAppearance(3)!.Form=="warrior");
                Check(snapshot.ReadAppearance(4) is null);
                var appearance=snapshot.ReadAppearance(3)!;appearance.Effects[0]="Invisible";
                Check(snapshot.ReadAppearance(3)!.Effects.Single()=="Poison");
                var replacement=new Snapshot(0,Bytes("rider"));
                Check(replacement.ReadAppearance(3)!.Form=="rider"&&snapshot.ReadAppearance(3)!.Form=="warrior");
                bool rejected=false;try{_=new Snapshot(0,new byte[]{0,0,0,0}).Epoch;}catch(InvalidDataException){rejected=true;}
                Check(rejected);
                rejected=false;try{_=new Snapshot(-1,Bytes()).Epoch;}catch(InvalidDataException){rejected=true;}
                Check(rejected);
                var large=new Snapshot(0,Bytes(extra:4096));
                long smallCost=ReadCost(snapshot,10000),largeCost=ReadCost(large,10000);
                Check(smallCost<2_000_000&&largeCost<2_000_000);
                Check(Math.Abs(smallCost-largeCost)<=1024);
                Check(large.ReadAppearance(4099) is null&&large.Epoch==Epoch);
                var old=NativeIdentityLedgerCodec.Decode(large.Bytes(),0);
                Check(old.Units.Length==4097&&old.Units[0].Appearance!.Form=="warrior");
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,assertions=Assertions,smallReadBytes=smallCost,largeReadBytes=largeCost,reads=10000,largeUnits=4097}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
