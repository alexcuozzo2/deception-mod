using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Unit operations use the existing framed Build command/action journal.
    // Income settlement belongs to the native EndTurn command's final marker:
    // StartTurn itself only QUEUES the native IncreaseCurrencyAction children.
    internal static class NativeBlackmail
    {
        internal const int BaseCost=2;
        const int MaxActions=16384;
        static long Generation;
        sealed record ReadView(Guid Epoch,LedgerBlackmail[] Rows,Dictionary<ulong,LedgerUnit> Units,Dictionary<uint,LedgerUnit> NativeUnits,Dictionary<(int,int),LedgerBlackmail[]> Cities,Dictionary<ulong,LedgerBlackmail[]> Targets);
        static ConditionalWeakTable<NativeGameStateEnvelope.Snapshot,ReadView> Views=new();
        static ReadView Read(NativeGameStateEnvelope.Snapshot snapshot)=>Views.GetValue(snapshot,key=>
        {var ledger=NativeIdentityLedgerCodec.Decode(key.Bytes(),key.Revision);return new(ledger.Epoch,ledger.Blackmails??Array.Empty<LedgerBlackmail>(),ledger.Units.ToDictionary(r=>r.LogicalId),ledger.Units.Where(r=>r.Alive).ToDictionary(r=>r.NativeId),
            (ledger.Blackmails??Array.Empty<LedgerBlackmail>()).Where(r=>r.UnitId>0).GroupBy(r=>(r.X,r.Y)).ToDictionary(g=>g.Key,g=>g.ToArray()),
            (ledger.Blackmails??Array.Empty<LedgerBlackmail>()).Where(r=>r.UnitId>0).GroupBy(r=>r.UnitId).ToDictionary(g=>g.Key,g=>g.ToArray()));});
        [ThreadStatic] static bool Writing;
        internal sealed class ActionScope
        {
            internal readonly object State,Map,Action;
            internal readonly Guid Epoch;
            internal readonly uint Turn;
            internal readonly byte Player;
            internal readonly long Generation;
            internal readonly bool Settlement;
            internal bool Ran,Closed;
            internal ActionScope(object state,object action,Guid epoch,bool settlement,long generation)
            {State=state;Map=Native.Get(state,"Map")!;Action=action;Epoch=epoch;Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
             Player=Convert.ToByte(Native.Get(state,"CurrentPlayer"));Settlement=settlement;Generation=generation;}
        }
        internal static void Clear(){Generation++;Writing=false;Views=new();}
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("EndCommandAction","Execute","GameState"),typeof(NativeBlackmail),nameof(BeforeSettlement),nameof(AfterNative),nameof(Finished));
            foreach(string type in new[]{"CaptureCityAction","DestroyImprovementAction","WipePlayerAction","PeaceRequestResponseAction"})
                Native.Hook(h,Native.M(type,"Execute","GameState"),typeof(NativeBlackmail),nameof(BeforeCleanup),nameof(AfterNative),nameof(Finished));
        }
        static void Require(bool yes,string why){if(!yes)throw new InvalidDataException(why);}
        static bool Ready(object state)=>NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(state)&&RecruitmentHooks.Registered&&
            Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"));
        static object? Player(object state,byte id)
        {
            if(id is 0 or 255)return null;
            object?[] args={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is object p&&
                Convert.ToByte(Native.Get(p,"Id"))==id?p:null;
        }
        static bool Alive(object state,object? player)=>player is not null&&
            (bool)Native.Invoke(Native.M("PlayerExtensions","IsAlive","PlayerState","GameState"),null,player,state)!;
        static object? Tile(object state,int x,int y)
        {
            if(x is <0 or >=4096||y is <0 or >=4096)return null;
            object at=Activator.CreateInstance(Native.T("WorldCoordinates"),new object[]{x,y})!;
            return Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        }
        static (int X,int Y) AtPosition(object tile)
        {var at=Native.Get(tile,"coordinates")!;return(Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));}
        static bool City(object? tile)=>tile is not null&&(bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,
            Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
        [ThreadStatic] static bool ReadingIncome;
        internal static bool ReadingGrossIncome=>ReadingIncome;
        static object? Find(object state,uint id)
        {object?[] found={id,null};return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,found)!?found[1]:null;}
        static object? Root(object state,LedgerUnit row)
        {
            if(!row.Alive)return null;
            var unit=Find(state,row.NativeId);
            if(unit is null&&row.Transport is LedgerTransport cargo)unit=Find(state,cargo.PassengerNativeId);
            if(unit is null)return null;
            var at=AtPosition(unit);var tile=Tile(state,at.X,at.Y);var root=tile is null?null:Native.Get(tile,"unit");
            if(root is null)return null;
            if(!Native.Same(root,unit))
            {
                // Native boarding can precede reconciliation at the command
                // boundary. Follow only this exact passenger into its root.
                if(!Native.Same(Native.Get(root,"passengerUnit"),unit))return null;
            }
            return Native.Int(Native.Get(root,"health"))>0?root:null;
        }
        static (int X,int Y) Home(object unit)
        {var at=Native.Get(Native.Get(unit,"passengerUnit")??unit,"home")!;return(Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));}
        static byte Owner(object unit,LedgerUnit row)=>row.Rogue?.Owner??Convert.ToByte(Native.Get(unit,"owner"));
        internal static object? ResolveUnit(object state,LedgerBlackmail mark)
        {
            if(mark.UnitId==0||NativeGameStateEnvelope.Get(state) is not { } snapshot||!Read(snapshot).Units.TryGetValue(mark.UnitId,out var row))return null;
            var unit=Root(state,row);return unit is not null&&Owner(unit,row)==mark.Victim&&Home(unit)==(mark.X,mark.Y)?unit:null;
        }
        internal static int GrossIncome(object state,object city)
        {
            bool previous=ReadingIncome;ReadingIncome=true;
            try{return Math.Max(0,Native.Int(Native.Invoke(Native.M("TileDataExtensions","CalculateWork","TileData","GameState"),null,city,state)));}
            finally{ReadingIncome=previous;}
        }
        static LedgerBlackmail[] Current(object state,LedgerBlackmail[] records,Dictionary<ulong,LedgerUnit> units)
        {
            var alive=new Dictionary<byte,bool>();var peace=new Dictionary<(byte,byte),bool>();var bodies=new Dictionary<ulong,object?>();
            bool HasPlayer(byte p){if(!alive.TryGetValue(p,out bool yes))alive[p]=yes=Alive(state,Player(state,p));return yes;}
            bool Peace(byte a,byte b){if(!peace.TryGetValue((a,b),out bool yes))peace[(a,b)]=yes=NativeBlackmailHostility.IsPeace(state,a,b);return yes;}
            bool Target(LedgerBlackmail r)
            {
                if(r.UnitId==0||!units.TryGetValue(r.UnitId,out var row))return false;
                if(!bodies.TryGetValue(r.UnitId,out var body))bodies[r.UnitId]=body=Root(state,row);
                return body is not null&&Owner(body,row)==r.Victim&&Home(body)==(r.X,r.Y);
            }
            return records.Where(r=>Target(r)&&HasPlayer(r.Blackmailer)&&HasPlayer(r.Victim)&&!Peace(r.Blackmailer,r.Victim)&&
                Tile(state,r.X,r.Y) is object t&&City(t)&&Native.Int(Native.Get(t,"owner"))==r.Victim).ToArray();
        }
        static LedgerBlackmail[] Effective(object state,LedgerBlackmail[] records)
        {
            var result=new List<LedgerBlackmail>();
            foreach(var group in records.GroupBy(r=>(r.X,r.Y)))
            {
                var city=Tile(state,group.Key.X,group.Key.Y);int remaining=city is null?0:GrossIncome(state,city);
                // A production loss pauses excess layers; it never manufactures
                // income, erases a paid contract or unlocks another same-turn buy.
                foreach(var row in group.OrderBy(r=>r.CreatedTurn).ThenBy(r=>r.Blackmailer).ThenBy(r=>r.UnitId))
                {int take=Math.Min(remaining,row.Layers);remaining-=take;if(take>0)result.Add(row with{Layers=take});}
            }
            return result.OrderBy(r=>r.X).ThenBy(r=>r.Y).ThenBy(r=>r.Blackmailer).ThenBy(r=>r.UnitId).ToArray();
        }
        internal static LedgerBlackmail[] Contracts(object state)
        {
            if(!Ready(state)||NativeGameStateEnvelope.Get(state) is not { } snapshot)return Array.Empty<LedgerBlackmail>();
            var view=Read(snapshot);return Current(state,view.Rows,view.Units);
        }
        internal static LedgerBlackmail[] All(object state)=>Effective(state,Contracts(state));
        internal static LedgerBlackmail[] ContractsAt(object state,object tile)
        {
            var p=AtPosition(tile);if(!Native.Same(Tile(state,p.X,p.Y),tile))return Array.Empty<LedgerBlackmail>();
            if(!Ready(state)||NativeGameStateEnvelope.Get(state) is not { } snapshot)return Array.Empty<LedgerBlackmail>();
            var view=Read(snapshot);return view.Cities.TryGetValue((p.X,p.Y),out var rows)?Current(state,rows,view.Units):Array.Empty<LedgerBlackmail>();
        }
        internal static LedgerBlackmail[] At(object state,object tile)=>Effective(state,ContractsAt(state,tile));
        internal static LedgerBlackmail[] ForUnit(object state,object unit)
        {
            if(NativeGameStateEnvelope.Get(state) is not { } snapshot)return Array.Empty<LedgerBlackmail>();
            var view=Read(snapshot);uint id=Convert.ToUInt32(Native.Get(unit,"id"));
            if(!view.NativeUnits.TryGetValue(id,out var row)||!Native.Same(Root(state,row),unit))return Array.Empty<LedgerBlackmail>();
            if(!Ready(state)||!view.Targets.TryGetValue(row.LogicalId,out var targets)||targets.Length==0)return Array.Empty<LedgerBlackmail>();
            var city=targets[0];return Effective(state,Current(state,view.Cities[(city.X,city.Y)],view.Units)).Where(r=>r.UnitId==row.LogicalId).ToArray();
        }
        static byte[] Fingerprint(byte[] payload,object target,byte victim,int currency,int income)
        {
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            writer.Write("DCP.BLACKMAIL.UNIT.1");writer.Write(payload.Length);writer.Write(payload);writer.Write(victim);writer.Write(currency);writer.Write(income);
            var at=AtPosition(target);var home=Home(target);writer.Write(Convert.ToUInt32(Native.Get(target,"id")));writer.Write(at.X);writer.Write(at.Y);writer.Write(home.X);writer.Write(home.Y);
            writer.Flush();return SHA256.HashData(stream.ToArray());
        }
        internal static bool TryOffer(object state,byte actor,object unit,out NativeExchangeIntent? intent,out string reason)
            =>TryOffer(state,actor,unit,out intent,out _,out reason);
        internal static bool TryOffer(object state,byte actor,object unit,out NativeExchangeIntent? intent,out int cost,out string reason)
        {
            intent=null;cost=0;reason="";
            try
            {
                Require(Ready(state),"Blackmail needs an active match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Blackmail ledger is absent.");
                var original=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(original,snapshot.Revision);
                uint id=Convert.ToUInt32(Native.Get(unit,"id"));var row=ledger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id);
                Require(row is not null&&Native.Same(Root(state,row),unit),"The selected unit changed.");
                var home=Home(unit);var city=Tile(state,home.X,home.Y);Require(City(city),"This unit has no supporting city.");
                var player=Player(state,actor)??throw new InvalidDataException("Blackmailer is absent.");
                var p=AtPosition(unit);var target=new NativeExchangeUnit(row!.LogicalId,id,p.X,p.Y);
                var request=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),actor,
                    NativeExchangeKind.Blackmail,false,NativePartnerReference.Existing,ledger.NextLogicalId,target,target,
                    Fingerprint(original,unit,Owner(unit,row),Native.Int(Native.Get(player,"Currency")),GrossIncome(state,city!)));
                if(!TryPlan(state,request,out var plan,out reason))return false;intent=request;cost=plan!.StarCost;return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            plan=null;reason="";
            try
            {
                Require(intent.Kind==NativeExchangeKind.Blackmail,"Invalid Blackmail intent.");
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                Require(Ready(state),"Blackmail needs an active match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Blackmail ledger is absent.");
                var original=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(original,snapshot.Revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,snapshot.Revision,request))
                {plan=new(state,request,true,snapshot.Revision,snapshot.Revision,original,original);return true;}
                Require(request.Initiator.LogicalId>0&&request.PartnerReference==NativePartnerReference.Existing,"Blackmail needs a live unit identity.");
                Require(Native.Int(Native.Get(state,"CurrentPlayer"))==request.Actor&&Convert.ToUInt32(Native.Get(state,"CurrentTurn"))==request.Turn,"Blackmail turn changed.");
                object player=Player(state,request.Actor)??throw new InvalidDataException("Blackmailer is absent.");
                Require(Alive(state,player),"Blackmailer is defeated.");
                Require((bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,player,Native.EnumValue(RecruitmentHooks.TT,TechTree.EspionageId))!,"Research Espionage first.");
                var row=ledger.Units.SingleOrDefault(r=>r.LogicalId==request.Initiator.LogicalId);
                Require(row is not null&&row.Alive&&row.NativeId==request.Initiator.NativeId,"Blackmail target identity changed.");
                var unit=Root(state,row!);Require(unit is not null&&AtPosition(unit)==(request.Initiator.X,request.Initiator.Y),"Blackmail target moved or disappeared.");
                byte victim=Owner(unit!,row!);Require(victim!=request.Actor&&Alive(state,Player(state,victim)),"Blackmail needs another player's unit.");
                Require(!NativeBlackmailHostility.IsPeace(state,request.Actor,victim),"Blackmail is hostile. End the peace treaty first.");
                var tile=Tile(state,request.Initiator.X,request.Initiator.Y)!;
                Require((bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,request.Actor)!&&
                    Native.Same(Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),tile,state,request.Actor,false),unit),"The unit is hidden.");
                var home=Home(unit!);var city=Tile(state,home.X,home.Y);
                Require(City(city)&&Native.Int(Native.Get(city!,"owner"))==victim,"The unit has no owned supporting city.");
                int currency=Native.Int(Native.Get(player,"Currency"));
                int income=GrossIncome(state,city!);
                Require(Fingerprint(original,unit!,victim,currency,income).SequenceEqual(request.SourceAppearanceHash),"Blackmail target, income or treasury changed.");
                NativeLedgerState.Validate(state,snapshot.Revision,original);
                var records=Current(state,ledger.Blackmails??Array.Empty<LedgerBlackmail>(),ledger.Units.ToDictionary(r=>r.LogicalId));
                var existing=records.SingleOrDefault(r=>r.Blackmailer==request.Actor&&r.UnitId==row!.LogicalId);
                // Every purchase has the same setup price; existing layers
                // continue to determine income and shared city capacity only.
                int cost=BaseCost;
                Require(currency>=cost,$"Blackmail costs {cost} stars.");
                Require(existing is null||existing.LastAppliedTurn<request.Turn,"This unit was already blackmailed this turn.");
                Require(records.Where(r=>r.X==home.X&&r.Y==home.Y).Sum(r=>(long)r.Layers)<income,"The supporting city has no income left to siphon.");
                Require(existing is null||existing.Layers<65535,"This unit's Blackmail capacity is full.");
                var added=existing is null?new LedgerBlackmail(request.Actor,victim,home.X,home.Y,request.Turn,UnitId:row!.LogicalId,LastAppliedTurn:request.Turn):
                    existing with{Layers=checked(existing.Layers+1),LastAppliedTurn=request.Turn};
                records=records.Where(r=>r!=existing).Append(added).OrderBy(r=>r.X).ThenBy(r=>r.Y).ThenBy(r=>r.Blackmailer).ThenBy(r=>r.UnitId).ToArray();
                long after=checked(snapshot.Revision+1);
                var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),snapshot.Revision,after,new[]{row!.LogicalId});
                var candidate=ledger with{NextSequence=checked(ledger.NextSequence+1),Blackmails=records,Receipts=ledger.Receipts.Append(receipt).ToArray()};
                plan=new(state,request,false,snapshot.Revision,after,original,NativeIdentityLedgerCodec.Encode(candidate,after),a:unit,b:unit,tileA:tile,tileB:city,starCost:cost);return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        static void Publish(object state,long beforeRevision,byte[] before,long afterRevision,byte[] after,Dictionary<byte,int> money,NativeBlackmailContacts.Pending? contacts=null,NativeBlackmailHostility.Pending? hostility=null)
        {
            Require(!Writing,"Reentrant Blackmail economy.");Require(contacts is null||hostility is null,"Blackmail cannot combine unrelated native stages.");
            // Native queries can return new managed wrappers for the same
            // player. Aggregate by authoritative ID, then resolve once.
            var players=money.Keys.ToDictionary(id=>id,id=>Player(state,id)??throw new InvalidDataException("Blackmail participant disappeared."));
            var old=players.ToDictionary(p=>p.Key,p=>Native.Int(Native.Get(p.Value,"Currency")));bool changed=false;
            Writing=true;
            try
            {
                NativeLedgerState.Validate(state,afterRevision,after);changed=true;
                foreach(var pair in money){Require(pair.Value>=0,"Blackmail cannot overdraw a treasury.");Native.Set(players[pair.Key],"Currency",pair.Value);}
                foreach(var pair in money)Require(Native.Int(Native.Get(players[pair.Key],"Currency"))==pair.Value,"Native Blackmail currency differs from its plan.");
                NativeGameStateEnvelope.Set(state,afterRevision,after);
                contacts?.Attach();hostility?.Attach();contacts?.Accept();hostility?.Accept();
            }
            catch(Exception error)
            {
                if(changed)try{hostility?.Rollback();contacts?.Rollback();foreach(var pair in old)Native.Set(players[pair.Key],"Currency",pair.Value);NativeGameStateEnvelope.Set(state,beforeRevision,before);}
                    catch(Exception rollback){NativeRegroupSession.Fault("Blackmail rollback failed: "+rollback.Message);throw new AggregateException(error,rollback);}
                throw;
            }
            finally{Writing=false;}
        }
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason="";
            try
            {
                Require(NativeExchangeCommands.LiveExecution(state),"No authorized Blackmail action.");
                Require(TryPlan(state,intent,out var plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                object player=Player(state,intent.Actor)!;int before=Native.Int(Native.Get(player,"Currency"));
                Require(TryPlan(state,intent,out var fresh,out why)&&fresh is not null&&Native.Same(fresh.TileA,plan.TileA)&&fresh.StarCost==plan.StarCost&&fresh.CandidatePayload.SequenceEqual(plan.CandidatePayload),"Blackmail changed before commitment: "+why);
                var hostility=NativeBlackmailHostility.Prepare(state,intent.Actor,plan.CandidateLedger.Blackmails!.Single(r=>r.Blackmailer==intent.Actor&&r.UnitId==intent.Initiator.LogicalId).Victim);
                Publish(state,plan.BeforeRevision,plan.OriginalPayload,plan.AfterRevision,plan.CandidatePayload,new(){[intent.Actor]=checked(before-plan.StarCost)},hostility:hostility);
                result=new(false,plan.BeforeRevision,plan.AfterRevision,plan);return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        static bool Queued(object state,object action)
        {
            var stack=Native.Get(state,"ActionStack")!;int count=Native.Int(Native.Get(stack,"Count"));
            return count is >0 and <=MaxActions&&Native.Items(stack).Any(a=>Native.Same(a,action));
        }
        static void Begin(object action,object state,bool settlement,out ActionScope? scope)
        {
            scope=null;if(Writing||!Ready(state)||NativeGameStateEnvelope.Get(state) is not { } snapshot)return;
            var view=Read(snapshot);
            if(view.Rows.Length==0||!Queued(state,action)||!(bool)Native.Invoke(Native.M("ActionBase","IsValid","GameState"),action,state)!)return;
            scope=new(state,action,view.Epoch,settlement,Generation);
        }
        public static void BeforeSettlement(object __instance,object __0,out ActionScope? __state)
        {
            __state=null;
            bool settlement=Equals(Native.Get(__instance,"commandType"),Enum.Parse(Native.T("CommandType"),"EndTurn"));
            Begin(__instance,__0,settlement,out __state);
        }
        public static void BeforeCleanup(object __instance,object __0,out ActionScope? __state)=>Begin(__instance,__0,false,out __state);
        public static void AfterNative(bool __runOriginal,ActionScope? __state){if(__state is not null)__state.Ran=__runOriginal;}
        public static Exception? Finished(Exception? __exception,ActionScope? __state)
        {
            if(__state is not ActionScope s||s.Closed)return __exception;s.Closed=true;
            if(__exception is not null||!s.Ran||s.Generation!=Generation||!Ready(s.State)||!Native.Same(s.Map,Native.Get(s.State,"Map"))||
                Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"))!=s.Turn||Native.Int(Native.Get(s.State,"CurrentPlayer"))!=s.Player||!Queued(s.State,s.Action))return __exception;
            try
            {
                var pending=NativeGameStateEnvelope.Get(s.State);
                if(pending is null||Read(pending).Epoch!=s.Epoch)return __exception;
                // Native kills, disbands and transport transitions may have
                // completed since the last framed write. Reconcile that real
                // action boundary before validating/publishing lien cleanup.
                NativeRegroupSession.Prepare(s.State);
                var snapshot=NativeGameStateEnvelope.Get(s.State)??throw new InvalidDataException("Blackmail event lost its ledger.");
                var original=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(original,snapshot.Revision);
                if(ledger.Epoch!=s.Epoch)return __exception;
                var records=Current(s.State,ledger.Blackmails??Array.Empty<LedgerBlackmail>(),ledger.Units.ToDictionary(r=>r.LogicalId));var money=new Dictionary<byte,int>();
                var effective=Effective(s.State,records).ToDictionary(r=>(r.Blackmailer,r.UnitId),r=>r.Layers);
                if(s.Settlement&&Player(s.State,s.Player) is object victim&&Alive(s.State,victim))
                {
                    int originalCurrency=Native.Int(Native.Get(victim,"Currency"));Require(originalCurrency>=0,"Native treasury is negative.");
                    long delta=0;
                    for(int i=0;i<records.Length;i++)
                    {
                        var row=records[i];if(row.CreatedTurn>s.Turn)continue;
                        if(row.Victim==s.Player&&row.LastPaidTurn<s.Turn)
                        {row=row with{LastPaidTurn=s.Turn};delta-=effective.GetValueOrDefault((row.Blackmailer,row.UnitId));}
                        if(row.Blackmailer==s.Player&&row.CreatedTurn<s.Turn&&row.LastCollectedTurn<s.Turn)
                        {row=row with{LastCollectedTurn=s.Turn};delta+=effective.GetValueOrDefault((row.Blackmailer,row.UnitId));}
                        records[i]=row;
                    }
                    // Like embassy income, each participant resolves their own
                    // income on their own turn. No mid-opponent-turn credit.
                    int next=(int)Math.Clamp((long)originalCurrency+delta,0,int.MaxValue);
                    if(next!=originalCurrency)money[s.Player]=next;
                }
                var contacts=s.Settlement?NativeBlackmailContacts.Prepare(s.State,s.Player,records):null;
                if(records.SequenceEqual(ledger.Blackmails??Array.Empty<LedgerBlackmail>())&&money.Count==0&&contacts is null)return __exception;
                var payload=NativeIdentityLedgerCodec.Encode(ledger with{Blackmails=records},snapshot.Revision);
                Publish(s.State,snapshot.Revision,original,snapshot.Revision,payload,money,contacts);
            }
            catch(Exception e){NativeRegroupSession.Fault("Blackmail turn/city event: "+e.Message);return e;}
            return __exception;
        }
    }
}
