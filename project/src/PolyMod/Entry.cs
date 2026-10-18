using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx.Logging;
using HarmonyLib;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    public static class Entry
    {
        static ManualLogSource? Log;
        static Harmony? Patches;
        static bool Applied;
        static int Notes,CriticalNotes;
        const string HarmonyId="alex.deception.m0";
        // Preserve the established Mac log location; use the platform's user-local
        // application-data directory elsewhere instead of creating a Library tree.
        static readonly string EvidenceDir=RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Logs","Polytopia-Deception")
            :Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Polytopia-Deception","Logs");
        public static void Load(ManualLogSource logger)
        {
            Log=logger;
            if(Patches is not null)throw new InvalidOperationException("Duplicate Deception PolyScript");
            var poly=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="PolyMod");
            var loader=poly.GetType("PolyMod.Loader",true)!;
            var target=loader.GetMethod("ProcessGameLogicData",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            if(target is null||target.ReturnType!=typeof(void)||target.GetParameters().Length!=2||
                target.GetParameters()[0].ParameterType.FullName!="Polytopia.Data.GameLogicData"||
                target.GetParameters()[1].ParameterType.FullName!="Newtonsoft.Json.Linq.JObject")throw new InvalidOperationException("PolyMod managed GLD hook contract changed");
            Directory.CreateDirectory(EvidenceDir);Notes=0;CriticalNotes=0;
            // Capture the managed exception before runtime shutdown, without
            // touching Unity or changing the fatal exception's behavior.
            FatalDiagnostics.Install(EvidenceDir);
            Write("events.jsonl","");
            Write("startup.json",JsonSerializer.Serialize(new{milestone="M1E",handoff="v1.5 + current UI-A27",gameBuildExpected="2.17.3.16375",
                nativeMimicRecruitment="M1C USER-TESTED BASELINE; M1E REGRESSION TEST REQUIRED",deceptivePublicPresentation="M1E CANDIDATE: ONE MAP SPRITE + TWO FIXED TYPE-BADGE SLOTS WITH REAR-ONLY FADE + DUAL MENU PORTRAIT + NATIVE INFO ONLY; MAC VERIFICATION REQUIRED",
                ordinaryActions="SCOPED NATIVE READINESS CANDIDATE; NATIVE PREDICATE RESULTS RETAINED",smuggle="SOURCE-BOUND NATIVE BUILD COMMAND + NATIVE POPULATION; PLAYTEST REQUIRED",transport="SEPARATE ACTUAL/APPARENT CARGO AND IDENTITY-SAFE TRANSPORT IMPLEMENTED; NAVAL MOVEMENT RETEST REQUIRED",
                regroup="LOCAL NORMAL-PLAY CANDIDATE: PERSISTENT COPY + OPTIONAL SWAP + SHARED PUFFS; ALEX PLAYTEST REQUIRED",higherTechnologies="DISINFORMATION, DISGUISE, KNOWLEDGE AND SPY ACTIONS IMPLEMENTED; NATIVE EDGE-CASE ACCEPTANCE PENDING",rootPolicy="Original Basic idx=0; all playable tribes; no starting research changes",
                hook=target.ToString(),timestamp=DateTimeOffset.Now},Codec.Options));
            if(File.Exists(Path.Combine(EvidenceDir,"adapter-error.txt")))File.Move(Path.Combine(EvidenceDir,"adapter-error.txt"),Path.Combine(EvidenceDir,"adapter-error.txt.previous"),true);
            Patches=new Harmony(HarmonyId);
            try
            {
#if DECEPTION_WINDOWS_PORT_CANDIDATE
                NativeWindowsHooks.Initialize();
#endif
                NativeTrainLeafProbeTrigger.ValidateMode();
                NativeExecutionTrigger.ValidateMode();
                Note("playtest-mode",JsonSerializer.Serialize(new{
                    privateSerializerDiagnostics=NativeTrainLeafProbeTrigger.Requested,
                    privateExecutionDiagnostics=NativeExecutionTrigger.Requested,
                    buildVersion=typeof(Entry).Assembly.GetName().Version?.ToString(),
                    diagnosticEnvironment=Environment.GetEnvironmentVariables().Keys.Cast<string>()
                        .Where(k=>k.StartsWith("DECEPTION_NATIVE_",StringComparison.Ordinal)&&k.EndsWith("_PROBE",StringComparison.Ordinal)&&Environment.GetEnvironmentVariable(k)=="1")
                        .OrderBy(k=>k,StringComparer.Ordinal).ToArray()}));
                if(NativeCleanBetaCompatibility.Install(Patches))Note("clean-beta-compatibility","Unmodified beta admitted; exact dormant reader hook filter installed.");
                NativeRegistryOrder.Install(Patches,loader);
                ResearchAudio.Install(Patches);
                NativeRegroupSession.Install(Patches);
                NativeOnlineSession.Install(Patches);
                NativeMenuPreviewScope.Install(Patches);
                NativeDisguiseGrowth.Install(Patches);
                NativeDisguiseGrowthAudio.Install(Patches);
                NativeMimicTransport.Install(Patches);
                NativeDisguiseLifecycle.Install(Patches);
                NativeDisguiseConcealment.Install(Patches);
                NativeDisguiseConcealmentVisual.Install(Patches);
                NativeCloakMovement.Install(Patches);
                NativeMimicWaterMovement.Install(Patches);
                NativeDisguiseMovement.Install(Patches);
                NativeMimicMovement.Install(Patches);
                NativeApparentTerrain.PendingDisguiseShed=NativeDisguiseMovement.IsShedding;
                NativeBlackmail.Install(Patches);
                NativeRogueRules.Install(Patches);
                NativeRogueVisuals.Install(Patches);
                NativeRogueTactics.Install(Patches);
                NativeDisguisePathPreference.Install(Patches);
                NativeRogueAccounting.Install(Patches);
                NativeRogueUpgrade.Install(Patches);
                NativeApparentTerrain.Install(Patches);
                NativeApparentSplashPresentation.Install(Patches);
                NativeInvestment.Install(Patches);
                NativeFeignSupport.Install(Patches);
                NativeFeignSupportReaction.Install(Patches);
                NativeRegroupSession.OnSessionEnded=()=>{MimicRegroupUi.Clear();MimicRegroupAnimation.Clear();NativeShedPresentation.Clear();NativeDisguiseRefresh.Clear();NativeDisguiseGrowthAudio.Clear();NativeDisguiseConcealment.Clear();NativeDisguiseConcealmentVisual.Clear();NativeApparentUpgradeUi.Clear();NativeSpecialActionUi.Clear();NativeActionBarScroll.Clear();NativeRogueRules.Clear();NativeInvestment.Clear();OrdinaryActionEligibility.Clear();NativeDisbandPresentation.Clear();NativeRogueVisuals.Clear();NativeSurveillanceUi.Clear();NativeRogueUpgrade.Clear();NativeApparentTerrain.Clear();NativeMimicWaterMovement.Clear();NativeDisguiseMovement.Clear();NativeCloakMovement.Clear();NativeMimicMovement.Clear();NativeDisguisePathPreference.Clear();NativeApparentSplashPresentation.Clear();NativeFeignSupport.Clear();NativeFeignSupportReaction.Clear();NativeSpyEffectPresentation.Clear();NativeBlackout.Clear();NativeRogueSight.Clear();NativeSpyReadinessPresentation.Clear();NativeSpyActions.Clear();NativeCombatPreview.Clear();NativeTechArtPrewarm.Clear();NativeBlackmail.Clear();NativeBlackmailPresentation.Clear();NativeBlackmailIncomeUi.Clear();NativeBlackmailEmbassyUi.Clear();NativeBlackmailNotices.Clear();NativeBranchTechUi.ClearLabels();NativeTechnologyIntelligence.Clear();NativeTradeSecrets.Clear();};
                MimicPresentationState.ReadAppearance=NativeRegroupSession.ReadAppearance;
                MimicPresentationState.ReadIdentity=NativeRegroupSession.ReadIdentity;
                NativeSpyPresentation.Ready=NativeRegroupSession.Ready;
                NativeBlackout.Install(Patches);
                NativeRogueSight.Install(Patches);
                RecruitmentHooks.Install(Patches);
                NativeSpyActions.Install(Patches);
                NativeSpyPresentation.Install(Patches);
                NativeFoggerPresentation.Install(Patches);
                NativeBranchTechUi.Install(Patches);
                NativeTechArtPrewarm.Install(Patches);
                OrdinaryActionEligibility.Install(Patches);
                NativeOrdinaryCommands.Install(Patches);
                NativeDisbandPresentation.Install(Patches);
                MimicSmuggleActions.NativeTransport=NativeRegroupSession.Enabled;
                MimicSmuggleActions.Install(Patches);
                if(NativeRegroupSession.Enabled)NativeExchangeCommands.InstallLive(Patches);
                else NativeExchangeCommands.Install(Patches);
                if(NativeRecruitmentProbe.Configure(Note))
                {
                    Native.Hook(Patches,Native.M("TrainAction","Execute","GameState"),typeof(NativeRecruitmentProbe),
                        nameof(NativeRecruitmentProbe.BeforeTrain),nameof(NativeRecruitmentProbe.AfterTrain),nameof(NativeRecruitmentProbe.TrainFinished));
                    Native.Hook(Patches,Native.M("ActionUtils","TrainUnit","GameState","PlayerState","TileData","Polytopia.Data.UnitData"),
                        typeof(NativeRecruitmentProbe),null,nameof(NativeRecruitmentProbe.Spawned));
                }
                MimicSmuggleUi.Install(Patches);
                MimicRegroupUi.Install(Patches);
                MimicRegroupAnimation.Install(Patches);
                NativeShedPresentation.Install();
                NativeApparentUpgradeUi.Install(Patches);
                NativeSpecialActionUi.Install(Patches);
                NativeActionBarScroll.Install(Patches);
                MimicDualPortrait.Validate();
                MimicMapInspection.Configure(MimicPresentationUi.MapView);
                MimicMapInspection.Install(Patches);
                NativeSpyReadinessPresentation.Install(Patches);
                NativeCombatPreview.Install(Patches);
                NativeRogueSight.ResolvePresentationBody=NativeCombatPreview.ActualBody;
                NativeDisguiseRefresh.Install(Patches);
                NativeMimicPrefabLookup.Install(Patches);
                MimicPresentationUi.Install(Patches);
                MimicControls.Install(Patches);
                MimicInspectionControls.Install(Patches);
                MimicHoldInput.Install(Patches);
                NativeIntelligenceOverlay.Install(Patches);
                NativeTradeSecrets.Install(Patches);
                NativeTechnologyIntelligence.Install(Patches);
                NativeSurveillanceUi.Install(Patches);
                NativeBlackmailPresentation.Install(Patches);
                NativeSpyEffectPresentation.Install(Patches);
                NativeBlackout.Presentation=NativeSpyEffectPresentation.Record;
                NativeBlackmailIncomeUi.Install(Patches);
                NativeBlackmailEmbassyUi.Install(Patches);
                NativeBlackmailNotices.Install(Patches);
                NativeFreshSession.Install(Patches);
                NativeBehaviorSession.Install(Patches);
                NativeTrainLeafProbeTrigger.Install(Patches);
                NativeGameStateEnvelope.Install(NativeLedgerState.ValidateTransportPayload,NativeLedgerState.ValidateTransportState);
                NativeStateProbeTrigger.Install(Patches,Note);
                NativeExecutionTrigger.Install(Patches);
                Patches.Patch(target,new HarmonyMethod(typeof(Entry).GetMethod(nameof(BeforeProcess))!),new HarmonyMethod(typeof(Entry).GetMethod(nameof(AfterProcess))!),null,null,null);
            }
            catch
            {
                try{try{NativeExecutionBridge.RollbackSetup();}finally{NativeStateBridge.RollbackSetup();}}
                finally{Patches.UnpatchSelf();Patches=null;}throw;
            }
            Log.LogInfo((object)("Deception "+typeof(Entry).Assembly.GetName().Version+" candidate armed; exact matching preview/build required. Native visual and two-Mac online acceptance remain pending. Further gaps are recorded in DEVELOPMENT-STATUS.txt."));
        }
        public static void BeforeProcess(object __1)
        {
            NativeRegistryPublication? publication=null;
            try
            {
                string before=NativeJson.Read(__1);var source=JsonNode.Parse(before)!.AsObject();
                if(source["techData"]?[TechTree.DeceptionId] is not null&&Applied)
                {RecruitmentCatalog.ValidateReentry(before);SmuggleCatalog.ValidateReentry(before);NativeExchangeCatalog.ValidateReentry(before);NativeExchangeDiagnosticResearch.ValidateReentry(before);RecruitmentHooks.ValidateRosterReentry(before);NativeRegistryOrder.Arm(__1);Note("registry-reentry","Validated original Basic root and native prototype invariants.");return;}
                var profile=new JsonObject();
                foreach(string key in new[]{"tribeData","techData","unitData","resourceData","unitAbilityData","improvementData"})
                    if(source[key] is JsonNode n)profile[key]=JsonNode.Parse(n.ToJsonString());
                Write("native-registry-before.json",profile.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
                var rosters=NativeRecruitmentRosters.AllResearchRosters(before);
                var tree=TechTree.AddDeception(before);
                if(!RecruitmentHooks.Ready)throw new InvalidOperationException("Recruitment hooks were not armed; refusing dangling city menu.");
                var result=RecruitmentCatalog.Add(tree.Json);
                string candidateJson=NativeExchangeDiagnosticResearch.Add(NativeExchangeCatalog.Add(SmuggleCatalog.Add(result.Json)));
                Type jt=__1.GetType();
                var parse=jt.GetMethod("Parse",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(string)},null)??throw new MissingMethodException("JObject.Parse(string)");
                object candidate=parse.Invoke(null,new object[]{candidateJson})??throw new InvalidOperationException("Null parsed JSON");
                publication=new NativeRegistryPublication(__1,candidate);
                publication.Apply();
                // Aquarion replacements must reach the live native parser too.
                // Native Dragon Egg recruitment remains unchanged (A60).
                RecruitmentCatalog.ValidateReentry(NativeJson.Read(__1));
                RecruitmentHooks.Register(result.Forms,rosters);
                MimicSmuggleActions.Configure(SmuggleCatalog.Harvests.Select(h=>new SmuggleHarvest(h.Id,h.NativeHarvest,h.Resource)));
                Write("tree-registration.json",JsonSerializer.Serialize(new{milestone="M1E",scope="ALL PLAYABLE TRIBES",rootId="basic",rootIndex=0,
                    newRootIds=System.Array.Empty<string>(),tree.PlayableTribes,tree.ModifiedRoots,tree.OriginalBranches,tree.TribeAudit,
                    addedResearch=TechTree.Branch.Select(n=>new{n.Id,n.Parent,n.Tier}),startingResearchChanged=false,nativeResearchPurchase="M1C USER-REPORTED PASS; M1E RETEST NEEDED"},Codec.Options));
                Write("recruitment-registration.json",JsonSerializer.Serialize(new{milestone="M1E",menu=RecruitmentCatalog.MenuId,
                    result.Forms,result.DeferredForms,nativeHealth=RecruitmentCatalog.NativeHealth,displayHealth=1,cost=RecruitmentCatalog.Cost,
                    identity="Distinct custom native types; no real ordinary-unit spawn followed by relabelling",
                    pricing="Native TrainCommand/TrainAction; no manual star debit",publicPresentation="Native display clones; private actual inspection portrait; candidate awaiting Mac verification",
                    scope="Supported own-roster city forms; native research or tier-2 Propaganda. Super units excluded. Deferred forms remain gaps.",appearanceRosters=rosters,nativeAcceptance="NOT RUN"},Codec.Options));
                Write("smuggle-registration.json",JsonSerializer.Serialize(new {cost=SmuggleCatalog.Cost,harvests=SmuggleCatalog.Harvests.Select(h=>new{id=h.Id,native=h.NativeHarvest,resource=h.Resource}),nativeAcceptance="NOT RUN"},Codec.Options));
                var after=JsonNode.Parse(NativeJson.Read(__1))!;
                Write("tree-after.json",new JsonObject{["techData"]=JsonNode.Parse(after["techData"]!.ToJsonString()),["tribeData"]=JsonNode.Parse(after["tribeData"]!.ToJsonString())}.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
                NativeRegistryOrder.Arm(__1);
                Applied=true;Note("registry-ready","Preserved native Basic root; added Deception + recruitment prototype for "+tree.PlayableTribes.Length+" playable tribes.");
            }
            catch(Exception e)
            {
                NativeRegistryOrder.RegistrationFailed(e);
                if(publication is not null)
                {
                    try{publication.Rollback();}
                    catch(Exception rollback){Log!.LogError((object)("DECEPTION ROLLBACK FAILED: quit without saving. "+rollback));throw;}
                }
                Applied=false;RecruitmentHooks.Clear();MimicSmuggleActions.Clear();MimicSmuggleUi.Clear();MimicRegroupUi.Clear();MimicRegroupAnimation.Clear();NativeShedPresentation.Clear();NativeDisguiseRefresh.Clear();NativeDisguiseGrowthAudio.Clear();NativeDisguiseConcealment.Clear();NativeDisguiseConcealmentVisual.Clear();NativeApparentUpgradeUi.Clear();NativeSpecialActionUi.Clear();NativeActionBarScroll.Clear();NativeRogueRules.Clear();NativeInvestment.Clear();OrdinaryActionEligibility.Clear();NativeDisbandPresentation.Clear();NativeRogueVisuals.Clear();NativeSurveillanceUi.Clear();NativeRogueUpgrade.Clear();NativeApparentTerrain.Clear();NativeMimicWaterMovement.Clear();NativeDisguiseMovement.Clear();NativeCloakMovement.Clear();NativeMimicMovement.Clear();NativeDisguisePathPreference.Clear();NativeApparentSplashPresentation.Clear();NativeFeignSupport.Clear();NativeFeignSupportReaction.Clear();NativeSpyEffectPresentation.Clear();NativeBlackout.Clear();NativeRogueSight.Clear();NativeSpyReadinessPresentation.Clear();NativeSpyActions.Clear();NativeCombatPreview.Clear();NativeTechArtPrewarm.Clear();NativeBlackmail.Clear();NativeBlackmailPresentation.Clear();NativeBlackmailIncomeUi.Clear();NativeBlackmailEmbassyUi.Clear();NativeBlackmailNotices.Clear();NativeBranchTechUi.ClearLabels();NativeTechnologyIntelligence.Clear();NativeTradeSecrets.Clear();
                try{Write("adapter-error.txt",e.ToString());}catch{}
                Log!.LogError((object)("DECEPTION NOT ACTIVATED: "+e));
            }
        }
        public static void AfterProcess(object __1)=>NativeRegistryOrder.Complete(__1);
        internal static void Note(string kind,string message)
        {
            Log?.LogInfo((object)("DECEPTION "+kind+": "+message));
            if(Notes++>=256)
            {
                bool important=kind.Contains("regroup",StringComparison.Ordinal)||kind.Contains("error",StringComparison.Ordinal)||kind.Contains("fault",StringComparison.Ordinal);
                if(!important||CriticalNotes++>=128)return;
            }
            try{File.AppendAllText(Path.Combine(EvidenceDir,"events.jsonl"),JsonSerializer.Serialize(new{timestamp=DateTimeOffset.Now,kind,message})+"\n");}catch{}
        }
        static void Write(string name,string value)
        {
            string path=Path.Combine(EvidenceDir,name),temp=path+".tmp";
            if(File.Exists(temp))File.Delete(temp);
            File.WriteAllText(temp,value);if(File.Exists(path))File.Copy(path,path+".previous",true);File.Move(temp,path,true);
        }
    }
}
