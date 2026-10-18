using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal sealed record SmuggleHarvest(string Id,string NativeHarvestId,string ResourceId);

    // A private Build command carries a real unit ID. Its native Build action is
    // the transaction boundary; the UI never changes currency, tiles or units.
    internal static class MimicSmuggleActions
    {
        internal static bool NativeTransport;
        internal const uint PayloadMagic=0x534D5547;
        internal const byte PayloadVersion=1;
        const string IT="Polytopia.Data.ImprovementData+Type", RT="Polytopia.Data.ResourceData+Type";
        const string ID="Polytopia.Data.ImprovementData", GLD="Polytopia.Data.GameLogicData";
        const string Writer="Il2CppSystem.IO.BinaryWriter", Reader="Il2CppSystem.IO.BinaryReader";
        static readonly Dictionary<string,SmuggleHarvest> Harvests=new(StringComparer.Ordinal);
        // Native history owns record lifetime. A weak native identity prevents
        // pointer reuse from inheriting a payload without rooting discarded
        // command/action copies or the game states which executed them.
        sealed class Payload
        {
            internal NativeLedgerOwnership.WeakIdentity Identity=null!;
            internal uint UnitId;
        }
        static readonly Dictionary<long,Payload> Payloads=new();
        static readonly Dictionary<long,NativeLedgerOwnership.WeakIdentity> PopulationActions=new();
        static readonly Dictionary<long,NativeLedgerOwnership.WeakIdentity> ResourceActions=new();
        static uint Publications;
        static Payload? Find(object value)
        {
            long key=Native.Pointer(value);if(!Payloads.TryGetValue(key,out var payload))return null;
            if(payload.Identity.Matches(value))return payload;
            Payloads.Remove(key);return null;
        }
        internal static void PruneDead()
        {
            foreach(long key in Payloads.Where(p=>p.Value.Identity.Target() is null).Select(p=>p.Key).ToArray())Payloads.Remove(key);
            foreach(long key in PopulationActions.Where(p=>p.Value.Target() is null).Select(p=>p.Key).ToArray())PopulationActions.Remove(key);
            foreach(long key in ResourceActions.Where(p=>p.Value.Target() is null).Select(p=>p.Key).ToArray())ResourceActions.Remove(key);
        }
        static void BeforePublication()
        {if((++Publications&127)==0||Payloads.Count>=8192)PruneDead();}
        internal sealed class CommandScope
        {
            internal object State=null!,Command=null!;
            internal uint UnitId;
            internal HashSet<long> Existing=new();
        }
        sealed class CommitContext
        {
            internal object Unit=null!,Player=null!,Tile=null!,Home=null!,HomeCoordinates=null!;
            internal int Population;
        }
        [ThreadStatic] static int Committing;
        internal sealed class ResourceScope
        {
            internal object State=null!,Tile=null!;
            internal ResourceScope? Parent;
            internal bool Deferred,Replaying;
        }
        [ThreadStatic] static ResourceScope? RemovingResource;
        static string ActionListType=>Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("ActionBase")).FullName!;
        public static void Configure(IEnumerable<SmuggleHarvest> descriptors)
        {
            var staged=new Dictionary<string,SmuggleHarvest>(StringComparer.Ordinal);
            foreach(var d in descriptors)
            {
                if(!d.Id.StartsWith("dcp_smuggle_",StringComparison.Ordinal)||
                    !new[]{"fruit","fish","game"}.Contains(d.ResourceId,StringComparer.Ordinal))
                    throw new InvalidOperationException("Invalid Smuggle descriptor.");
                staged.Add(d.Id,d);
            }
            // Entry prepares this catalog before native ProcessGameLogicData
            // registers custom enums. Resolve IDs only when offering a command.
            Harvests.Clear();foreach(var p in staged)Harvests.Add(p.Key,p.Value);
        }
        public static void Clear(){foreach(var p in Payloads.Values)if(p.Identity.Target() is object value)NativeBuildRecordJournalDraft.BeforeMutation(value);Payloads.Clear();PopulationActions.Clear();ResourceActions.Clear();RemovingResource=null;}
        // The UI calls this only for an offer that was never submitted. Native
        // command history retains its bindings for later save/replay encoding.
        public static void ForgetOffer(object command){NativeBuildRecordJournalDraft.BeforeMutation(command);Payloads.Remove(Native.Pointer(command));}
        public static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("BuildCommand","IsValid","GameState","System.String&"),typeof(MimicSmuggleActions),nameof(GuardValidity));
            if((Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXECUTION_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_PAID_BIRTH_PROBE")!="1"))
            Native.Hook(h,Native.M("BuildCommand","Execute","GameState"),typeof(MimicSmuggleActions),nameof(GuardCommand),nameof(CommandReturned),nameof(CommandFinished));
            foreach(var method in new[]{"Execute","ExecuteDefault"})
                if(method!="Execute"||(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXECUTION_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_PAID_BIRTH_PROBE")!="1"))
                Native.Hook(h,Native.M("BuildAction",method,"GameState"),typeof(MimicSmuggleActions),nameof(ExecuteAction));
            if(!NativeTransport&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BUILD_LEAF_PROBE")!="1")
            foreach(var owner in new[]{"BuildCommand","BuildAction"})
            {
                Native.Hook(h,Native.M(owner,"Serialize",Writer,"System.Int32"),typeof(MimicSmuggleActions),null,nameof(WritePayload));
                Native.Hook(h,Native.M(owner,"Deserialize",Reader,"System.Int32"),typeof(MimicSmuggleActions),null,nameof(ReadPayload));
            }
            Native.Hook(h,Native.M("IncreasePopulationReaction","Execute","Il2CppSystem.Action"),typeof(MimicSmuggleActions),nameof(PopulationReaction));
            Native.Hook(h,Native.M("DestroyResourceAction","Execute","GameState"),typeof(MimicSmuggleActions),
                nameof(ResourceRemoving),nameof(ResourceRemoved),nameof(ResourceFinished));
            Native.Hook(h,Native.M("ActionUtils","CheckSurroundingArea","GameState","System.Byte","TileData"),typeof(MimicSmuggleActions),nameof(DeferResourceArea));
            Native.Hook(h,Native.M("ActionUtils","UpdateImprovementLevel","GameState","System.Byte","TileData"),typeof(MimicSmuggleActions),nameof(ResourceImprovementOwner));
            // Resolve all operation boundaries at installation, before a command
            // can be offered. The metadata gate separately pins these signatures.
            foreach(var m in new[]{
                Native.M("CommandBase","PassesBasicValidation","GameState","System.String&"),
                Native.M(GLD,"GetActionableImprovementForResource",RT,"PlayerState"),
                Native.M("ResourceDataUtils","GetPopulationReward",ID),
                Native.M("UnitState","MakeExhauseted","GameState"),
                Native.M("UnitState","HasEffect","UnitEffect"),
                Native.M("UnitState","HasLeader"),Native.M("UnitState","HasFollower"),
                Native.M("TileData","GetExplored","System.Byte"),
                Native.M("ActionBase","CommitSubActionsToStack",ActionListType),
                Native.M("PlayerDiplomacyExtensions","HasPeaceWith","PlayerState","System.Byte"),
                Native.M(Writer,"Write","System.UInt32"),Native.M(Writer,"Write","System.Byte"),
                Native.M(Reader,"ReadUInt32"),Native.M(Reader,"ReadByte")}){_=m;}
        }
        static string? IdOf(object value)
        {
            try{return Native.EnumName(Native.Get(value,"Type")!);}catch{}
            // Native lists expose base wrappers even when the instance is a
            // concrete Build command/action. Cast only for reading its own tag.
            foreach(var type in new[]{"BuildCommand","BuildAction"})
                try{return Native.EnumName(Native.Get(Native.As(value,type),"Type")!);}catch{}
            return null;
        }
        public static bool IsSmuggle(object value)
        {
            var id=IdOf(value);return id is not null&&id.StartsWith("dcp_smuggle_",StringComparison.Ordinal);
        }
        public static string Describe(object command)=>IsSmuggle(command)?"Smuggle":"";
        static void Bind(object value,uint unitId)
        {
            NativeBuildRecordJournalDraft.BeforeMutation(value);long key=Native.Pointer(value);_=Find(value);BeforePublication();
            if(!Payloads.ContainsKey(key)&&Payloads.Count>=8192)throw new InvalidDataException("Smuggle attachment bound exceeded.");
            Payloads[key]=new Payload{Identity=new(value),UnitId=unitId};
        }
        internal static (object Token,byte[] Bytes)? CapturePrivateJournal(object value)
        {
            var p=Find(value);if(p is null)return null;
            using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);
            writer.Write(PayloadMagic);writer.Write(PayloadVersion);writer.Write(p.UnitId);writer.Flush();return(p,bytes.ToArray());
        }
        internal static object PublishPrivateJournal(object value,uint unitId)
        {
            NativeBuildRecordJournalDraft.BeforeMutation(value);long key=Native.Pointer(value);_=Find(value);BeforePublication();
            if(!IsSmuggle(value)||Payloads.ContainsKey(key)||Payloads.Count>=8192)throw new InvalidDataException("Smuggle private publication requires correct tag/absent binding/capacity.");
            var p=new Payload{Identity=new(value),UnitId=unitId};Payloads.Add(key,p);return p;
        }
        internal static void ForgetPrivateJournal(object value,object token)
        {
            NativeBuildRecordJournalDraft.BeforeMutation(value);long key=Native.Pointer(value);
            var p=Find(value);if(p is null)return;
            if(!ReferenceEquals(p,token))throw new InvalidDataException("Smuggle exact rollback token changed.");Payloads.Remove(key);
        }
        internal static bool HasPayload(object value)=>PayloadFor(value,out _);
        static bool PayloadFor(object value,out uint unitId)
        {
            unitId=0;
            var p=Find(value);if(p is null)return false;
            unitId=p.UnitId;return true;
        }
        static object? Player(object state,byte owner)
        {
            object?[] args={owner,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!?args[1]:null;
        }
        static object? Tile(object state,object coordinates)=>Native.Invoke(
            Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinates);
        static bool CoordinatesEqual(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&
            Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static bool Reject(string message,out string reason){reason=message;return false;}
        public static bool TryCreate(object state,object player,object tile,out object? command,out string reason)
        {
            command=null;reason="Smuggle is not available.";
            try
            {
                var resource=Native.Get(tile,"resource");var unit=Native.Get(tile,"unit");
                if(resource is null){reason="The source tile has no native resource.";return false;}
                if(unit is null){reason="The source tile has no native unit.";return false;}
                string resourceId=Native.EnumName(Native.Get(resource,"type")!);
                var d=Harvests.Values.SingleOrDefault(x=>x.ResourceId==resourceId);
                if(d is null){reason="Smuggle does not support native resource "+resourceId+".";return false;}
                object candidate=Activator.CreateInstance(Native.T("BuildCommand"),new[]{
                    (object)(byte)Native.Int(Native.Get(player,"Id")),Native.EnumValue(IT,d.Id),Native.Get(tile,"coordinates")!})!;
                Bind(candidate,Convert.ToUInt32(Native.Get(unit,"id")));
                if(!Validate(state,candidate,true,out _,out reason)){Payloads.Remove(Native.Pointer(candidate));return false;}
                command=candidate;return true;
            }
            catch(Exception e){reason="Smuggle eligibility could not be verified.";Entry.Note("smuggle-create-error",e.ToString());return false;}
        }
        static bool Validate(object state,object value,bool command,out CommitContext? context,out string reason)
        {
            context=null;reason="";
            string? id=IdOf(value);
            if(id is null||!Harvests.TryGetValue(id,out var harvest)||!RecruitmentHooks.Registered)
                return Reject("Smuggle is not registered.",out reason);
            if(!PayloadFor(value,out uint unitId))return Reject("Smuggle source identity is missing.",out reason);
            byte owner=(byte)Native.Int(Native.Get(value,"PlayerId"));
            if(Native.Int(Native.Get(state,"CurrentPlayer"))!=owner)return Reject("The commanding player changed.",out reason);
            if(command)
            {
                object?[] args={state,null};
                if(!(bool)Native.Invoke(Native.M("CommandBase","PassesBasicValidation","GameState","System.String&"),value,args)!)
                    return Reject((string?)args[1]??"A native command is pending.",out reason);
            }
            var player=Player(state,owner);var coordinates=Native.Get(value,"Coordinates")!;var tile=Tile(state,coordinates);
            var unit=tile is null?null:Native.Get(tile,"unit");
            if(player is null||tile is null||unit is null||Convert.ToUInt32(Native.Get(unit,"id"))!=unitId||
                Native.Int(Native.Get(unit,"owner"))!=owner||!CoordinatesEqual(Native.Get(unit,"coordinates")!,coordinates)||
                !RecruitmentHooks.Forms.ContainsKey(Native.EnumName(Native.Get(unit,"type")!))||Native.Int(Native.Get(unit,"health"))<=0)
                return Reject("The original Mimic is no longer on this tile.",out reason);
            if(!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,owner)!)
                return Reject("The Mimic's tile is not accessible to its commander.",out reason);
            object?[] lookup={unitId,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,lookup)!||!Native.Same(unit,lookup[1]))
                return Reject("The Mimic's native identity changed.",out reason);
            if((bool)Native.Get(unit,"attacked")!)
                return Reject("Smuggle needs an unused action.",out reason);
            if((bool)Native.Invoke(Native.M("UnitState","HasEffect","UnitEffect"),unit,Enum.Parse(Native.T("UnitEffect"),"Frozen"))!)
                return Reject("A frozen Mimic cannot Smuggle.",out reason);
            if((bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!||(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!)
                return Reject("Smuggle requires an independent Mimic body.",out reason);
            var resource=Native.Get(tile,"resource");
            if(resource is null||Native.EnumName(Native.Get(resource,"type")!)!=harvest.ResourceId)
                return Reject("The resource beneath the Mimic changed.",out reason);
            var gld=Native.Get(state,"GameLogicData")!;
            var nativeHarvest=Native.Invoke(Native.M(GLD,"GetActionableImprovementForResource",RT,"PlayerState"),gld,Native.Get(resource,"type"),player);
            // This native mapper walks the commanding player's researched
            // technologies, skips null unlocks, applies tribe replacements and
            // excludes hidden improvements. Its exact result is the real gate.
            // IsUnlocked(ImprovementType,PlayerState) is not an equivalent check:
            // the pinned native overload returns false at an earlier null entry.
            if(nativeHarvest is null)
                return Reject("The native harvesting technology is required for "+harvest.ResourceId+".",out reason);
            string nativeHarvestId=Native.EnumName(Native.Get(nativeHarvest,"type")!);
            if(nativeHarvestId!=harvest.NativeHarvestId)
                return Reject("Native harvest mapping mismatch: returned "+nativeHarvestId+"; expected "+harvest.NativeHarvestId+".",out reason);
            int population=checked((int)Convert.ToUInt32(Native.Invoke(Native.M("ResourceDataUtils","GetPopulationReward",ID),null,nativeHarvest)));
            if(population<=0||population>32)return Reject("The native harvesting reward is unavailable.",out reason);
            byte territory=(byte)Native.Int(Native.Get(tile,"owner"));
            if(!NativeRogueRules.IsRogue(state,unit)&&territory!=owner&&territory!=byte.MaxValue&&
                (bool)Native.Invoke(Native.M("PlayerDiplomacyExtensions","HasPeaceWith","PlayerState","System.Byte"),null,player,territory)!)
                return Reject("A treaty protects this resource.",out reason);
            var homeCoordinates=Native.Get(unit,"home")!;var home=Tile(state,homeCoordinates);
            if(home is null||!(bool)Native.Invoke(Native.M("TileData","HasImprovement",IT),home,Native.EnumValue(IT,"city"))!||
                Player(state,(byte)Native.Int(Native.Get(home,"owner"))) is null)
                return Reject("The Mimic has no valid native home city.",out reason);
            if(Native.Int(Native.Get(player,"Currency"))<2)return Reject("Smuggle costs 2 stars.",out reason);
            if(!command&&!(bool)Native.Get(value,"DeductCost")!)return Reject("Smuggle requires its paid native action.",out reason);
            context=new CommitContext{Unit=unit,Player=player,Tile=tile,Home=home,HomeCoordinates=homeCoordinates,Population=population};
            return true;
        }
        public static bool GuardValidity(object __instance,object __0,ref string __1,ref bool __result)
        {
            if(!IsSmuggle(__instance))return true;
            __result=false;
            try{__result=Validate(__0,__instance,true,out _,out __1);}
            catch(Exception e){__1="Smuggle eligibility could not be verified.";Entry.Note("smuggle-validity-error",e.ToString());}
            return false;
        }
        public static bool GuardCommand(object __instance,object __0,out CommandScope? __state)
        {
            __state=null;if(!IsSmuggle(__instance))return true;
            if(!Validate(__0,__instance,true,out _,out var why))
            {Entry.Note("smuggle-command-rejected",why);return false;}
            PayloadFor(__instance,out uint id);
            __state=new CommandScope{Command=__instance,State=__0,UnitId=id,
                Existing=Native.Items(Native.Get(__0,"ActionStack")!).Select(Native.Pointer).ToHashSet()};
            return true;
        }
        public static void CommandReturned(object __instance,object __0,CommandScope? __state)
        {
            if(__state is null)return;
            var added=Native.Items(Native.Get(__0,"ActionStack")!).Where(a=>!__state.Existing.Contains(Native.Pointer(a))).ToArray();
            var matching=new List<object>();
            foreach(var a in added)
            {
                object build;
                try{build=Native.As(a,"BuildAction");}catch{continue;}
                if(IdOf(build)==IdOf(__instance)&&Native.Int(Native.Get(build,"PlayerId"))==Native.Int(Native.Get(__instance,"PlayerId"))&&
                    CoordinatesEqual(Native.Get(build,"Coordinates")!,Native.Get(__instance,"Coordinates")!))matching.Add(build);
            }
            if(matching.Count!=1)throw new InvalidOperationException("Native Smuggle command did not produce exactly one matching Build action.");
            Bind(matching[0],__state.UnitId);
            Entry.Note("smuggle-command-queued","Pinned source unit; native Build action and command scaffold retained.");
        }
        public static Exception? CommandFinished(Exception? __exception,CommandScope? __state)
        {
            if(__exception is not null&&__state is not null)
            {
                // Audited native BuildCommand.Execute only appends command
                // scaffold and a BuildAction. No child has executed yet. Remove
                // this invocation's additions if binding/native creation failed.
                try
                {
                    var stack=Native.Get(__state.State,"ActionStack")!;var entries=Native.Items(stack).ToArray();
                    var remove=Native.M(stack.GetType(),"RemoveAt","System.Int32");
                    for(int i=entries.Length-1;i>=0;i--)
                        if(!__state.Existing.Contains(Native.Pointer(entries[i])))
                        {Payloads.Remove(Native.Pointer(entries[i]));Native.Invoke(remove,stack,i);}
                }
                catch(Exception cleanup){Entry.Note("smuggle-command-cleanup-error",cleanup.Message);}
            }
            return __exception;
        }
        public static void WritePayload(object __instance,object __0,int __1)
        {
            if(!IsSmuggle(__instance))return;
            if(!PayloadFor(__instance,out uint id))throw new InvalidDataException("Cannot save Smuggle without its source unit ID.");
            Native.Invoke(Native.M(Writer,"Write","System.UInt32"),__0,PayloadMagic);
            Native.Invoke(Native.M(Writer,"Write","System.Byte"),__0,PayloadVersion);
            Native.Invoke(Native.M(Writer,"Write","System.UInt32"),__0,id);
        }
        public static void ReadPayload(object __instance,object __0,int __1)
        {
            if(!IsSmuggle(__instance))return;
            NativeBuildRecordJournalDraft.BeforeMutation(__instance);Payloads.Remove(Native.Pointer(__instance));
            uint magic=Convert.ToUInt32(Native.Invoke(Native.M(Reader,"ReadUInt32"),__0));
            byte version=Convert.ToByte(Native.Invoke(Native.M(Reader,"ReadByte"),__0));
            if(magic!=PayloadMagic||version!=PayloadVersion)throw new InvalidDataException("Unsupported Smuggle command/action payload.");
            Bind(__instance,Convert.ToUInt32(Native.Invoke(Native.M(Reader,"ReadUInt32"),__0)));
        }
        public static bool ExecuteAction(object __instance,object __0)
        {
            if(!IsSmuggle(__instance))return true;
            if(Committing!=0)throw new InvalidOperationException("Reentrant Smuggle commit.");
            if(!Validate(__0,__instance,false,out var c,out var why))
            {Entry.Note("smuggle-action-rejected",why);return false;}
            var context=c!;var source=Native.Get(__instance,"Coordinates")!;
            var actions=new List<object>();
            var removal=Activator.CreateInstance(Native.T("DestroyResourceAction"),new[]{Native.Get(__instance,"PlayerId")!,source})!;
            ResourceActions[Native.Pointer(removal)]=new(removal);actions.Add(removal);
            for(int i=0;i<context.Population;i++)
            {
                var population=Activator.CreateInstance(Native.T("IncreasePopulationAction"),new[]{
                    (object)(byte)Native.Int(Native.Get(context.Home,"owner")),source,context.HomeCoordinates,(object)60})!;
                PopulationActions[Native.Pointer(population)]=new(population);actions.Add(population);
            }
            var prepared=Native.NewList("ActionBase",actions);var stack=Native.Get(__0,"ActionStack")!;
            int originalCount=Native.Int(Native.Get(stack,"Count"));int currency=Native.Int(Native.Get(context.Player,"Currency"));
            var previousSubActions=Native.Get(__instance,"subActions");
            bool moved=(bool)Native.Get(context.Unit,"moved")!,attacked=(bool)Native.Get(context.Unit,"attacked")!;
            // Prepare constructors/lists and every eligibility check before the
            // commit. Roll back this synchronous transaction if native dispatch
            // throws; no resource/population subaction has run at this point.
            var effects=Native.Get(context.Unit,"effects")!;
            var savedEffects=Native.NewList("UnitEffect",Native.Items(effects));
            try
            {
                Committing++;
                if(!Validate(__0,__instance,false,out _,out why))throw new InvalidOperationException(why);
                Native.Set(__instance,"subActions",prepared);
                Native.Set(context.Player,"Currency",currency-2);
                Native.Invoke(Native.M("UnitState","MakeExhauseted","GameState"),context.Unit,__0);
                if(!(bool)Native.Get(context.Unit,"moved")!||!(bool)Native.Get(context.Unit,"attacked")!)
                    throw new InvalidOperationException("Native exhaustion did not spend both allowances.");
                Native.Invoke(Native.M("ActionBase","CommitSubActionsToStack",ActionListType),__instance,stack);
                Entry.Note("smuggle-action-committed","Cost=2; movement/action spent; native resource removal and "+context.Population+" home population actions queued.");
            }
            catch
            {
                var remove=Native.M(stack.GetType(),"RemoveAt","System.Int32");
                while(Native.Int(Native.Get(stack,"Count"))>originalCount)
                    Native.Invoke(remove,stack,Native.Int(Native.Get(stack,"Count"))-1);
                Native.Set(context.Player,"Currency",currency);
                Native.Set(context.Unit,"moved",moved);Native.Set(context.Unit,"attacked",attacked);
                Native.Set(context.Unit,"effects",savedEffects);
                Native.Set(__instance,"subActions",previousSubActions);
                throw;
            }
            finally{Committing--;}
            return false;
        }
        static bool SmuggleResource(object action,object state,object tile)
        {
            if(ResourceActions.TryGetValue(Native.Pointer(action),out var tagged))
            {
                if(tagged.Matches(action))return true;
                ResourceActions.Remove(Native.Pointer(action));
            }
            // Flattened native child actions have no private Smuggle bytes.
            // Recover only the exhausted actual Mimic with its still-pending
            // source-to-home population child. An ordinary native harvest under
            // that exhausted Mimic has identical child fields and receives
            // the same post-removal adjacency correction. No gameplay field
            // is a marker.
            var unit=Native.Get(tile,"unit");
            if(unit is null||!RecruitmentHooks.Registered||
                !RecruitmentHooks.Forms.ContainsKey(Native.EnumName(Native.Get(unit,"type")!))||
                !(bool)Native.Get(unit,"moved")!||!(bool)Native.Get(unit,"attacked")!||
                !CoordinatesEqual(Native.Get(unit,"coordinates")!,Native.Get(action,"Coordinates")!))return false;
            var home=Tile(state,Native.Get(unit,"home")!);if(home is null)return false;
            foreach(var queued in Native.Items(Native.Get(state,"ActionStack")!))
            {
                object population;
                try{population=Native.As(queued,"IncreasePopulationAction");}catch{continue;}
                if(CoordinatesEqual(Native.Get(population,"Source")!,Native.Get(action,"Coordinates")!)&&
                    CoordinatesEqual(Native.Get(population,"Target")!,Native.Get(unit,"home")!)&&
                    Native.Int(Native.Get(population,"PlayerId"))==Native.Int(Native.Get(home,"owner")))return true;
            }
            return false;
        }
        public static void ResourceRemoving(object __instance,object __0,out ResourceScope? __state)
        {
            __state=null;var tile=Tile(__0,Native.Get(__instance,"Coordinates")!);
            if(tile is null||Native.Get(tile,"resource") is null||!SmuggleResource(__instance,__0,tile))return;
            __state=new ResourceScope{State=__0,Tile=tile,Parent=RemovingResource};RemovingResource=__state;
        }
        public static bool DeferResourceArea(object __0,byte __1,object __2)
        {
            var scope=RemovingResource;
            if(scope is null||scope.Replaying||!Native.Same(scope.State,__0)||!Native.Same(scope.Tile,__2))return true;
            // Pinned native DestroyResourceAction checks adjacency before it
            // clears the animal. Defer that one check, rather than adding a
            // second check that could enqueue duplicate level adjustments.
            scope.Deferred=true;return false;
        }
        public static void ResourceRemoved(object __instance,object __0,ResourceScope? __state)
        {
            if(__state is null||!ReferenceEquals(RemovingResource,__state)||!__state.Deferred||
                Native.Get(__state.Tile,"resource") is not null)return;
            __state.Deferred=false;__state.Replaying=true;
            try
            {
                Native.Invoke(Native.M("ActionUtils","CheckSurroundingArea","GameState","System.Byte","TileData"),null,
                    __0,Native.Get(__instance,"PlayerId"),__state.Tile);
            }
            finally{__state.Replaying=false;}
        }
        public static void ResourceImprovementOwner(object __0,ref byte __1,object __2)
        {
            var scope=RemovingResource;
            if(scope is not null&&scope.Replaying&&Native.Same(scope.State,__0))
                __1=checked((byte)Native.Int(Native.Get(__2,"owner")));
        }
        public static Exception? ResourceFinished(Exception? __exception,ResourceScope? __state)
        {
            if(__state is not null&&ReferenceEquals(RemovingResource,__state))RemovingResource=__state.Parent;
            return __exception;
        }
        static bool SmugglePopulation(object action)
        {
            if(PopulationActions.TryGetValue(Native.Pointer(action),out var tagged))
            {if(tagged.Matches(action))return true;PopulationActions.Remove(Native.Pointer(action));}
            // Native child actions flatten into ActionStack and serialize only
            // their ordinary fields. On Resume, a pending Smuggle's source is
            // still its exhausted Mimic: command processing cannot accept new
            // moves before these reactions finish. Recover only that narrow
            // source/home/recipient pattern. No gameplay field stores a marker.
            var state=Native.Static("GameManager","GameState");if(state is null)return false;
            var source=Tile(state,Native.Get(action,"Source")!);var unit=source is null?null:Native.Get(source,"unit");
            if(unit is null||!RecruitmentHooks.Registered||
                !RecruitmentHooks.Forms.ContainsKey(Native.EnumName(Native.Get(unit,"type")!))||
                !(bool)Native.Get(unit,"moved")!||!(bool)Native.Get(unit,"attacked")!||
                !CoordinatesEqual(Native.Get(unit,"home")!,Native.Get(action,"Target")!))return false;
            var home=Tile(state,Native.Get(action,"Target")!);
            return home is not null&&Native.Int(Native.Get(home,"owner"))==Native.Int(Native.Get(action,"PlayerId"));
        }
        public static bool PopulationReaction(object __instance,object __0)
        {
            object action=Native.Get(__instance,"action")!;
            if(!SmugglePopulation(action))return true;
            PopulationActions.Remove(Native.Pointer(action));
            try
            {
                var map=Native.Static("MapRenderer","instance")!;
                var get=Native.M("MapRenderer","GetTileInstance","WorldCoordinates");
                var source=Native.Invoke(get,map,Native.Get(action,"Source"));
                var target=Native.Invoke(get,map,Native.Get(action,"Target"));
                if(source is not null&&target is not null&&
                    !(bool)Native.Get(source,"IsHidden")!&&!(bool)Native.Get(target,"IsHidden")!)return true;
            }
            catch(Exception e){Entry.Note("population-visibility-error",e.Message);}
            Native.Invoke(Native.M("Il2CppSystem.Action","Invoke"),__0);
            return false;
        }
    }
}
