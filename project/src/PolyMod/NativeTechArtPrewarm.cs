using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Unity decoding/readback stays on the native loading thread, after atlas
    // completion and before OnGameReady renders the map or resumes commands.
    // Only real enrolled player profiles are read. No UI or game data is built.
    internal static class NativeTechArtPrewarm
    {
        internal static Func<double> Clock=()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        sealed record Player(object Value,byte Id,bool Aquarion,(int Tribe,int Skin) Profile);
        sealed class Job
        {
            internal object Manager=null!,Client=null!,State=null!,Atlas=null!;
            internal Player[] Players=Array.Empty<Player>();
            internal readonly HashSet<byte> Finished=new();
            internal int Thread,Polls,Generation;
            internal double Deadline;
        }
        static Job? Pending;
        static object? AttemptClient,AttemptAtlas;
        static bool Running,Noted,Skipped;
        static int Generation;
        internal static void Install(Harmony h)
        {
            _=Native.M("GameManager","get_isLevelLoaded");
            Native.Hook(h,Native.M("GameManager","OnLevelLoaded"),typeof(NativeTechArtPrewarm),nameof(BeforeLevel));
            Native.Hook(h,Native.M("GameManager","OnGameReady"),typeof(NativeTechArtPrewarm),nameof(BeforeReady));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeTechArtPrewarm),null,nameof(AfterUpdate));
        }
        internal static void Clear()
        {Generation++;Pending=null;AttemptClient=null;AttemptAtlas=null;Noted=false;Skipped=false;UnitArtAssets.ClearNativeHeads();}
        static void Note(Exception e)
        {if(!Noted){Noted=true;Entry.Note("tech-art-prewarm-unavailable",e.Message);}}
        public static void BeforeLevel(object __instance)
        {
            try{if(Native.Same(__instance,Native.Static("GameManager","instance")))Clear();}
            catch(Exception e){Note(e);}
        }
        static bool Context(object manager,out object? state,out object? client,out object? atlas)
        {
            state=null;client=null;atlas=null;
            if(!RecruitmentHooks.Registered||!Native.Same(manager,Native.Static("GameManager","instance"))||
                Native.Get(manager,"fakeSession") is not null||!(bool)Native.Get(manager,"isLevelLoaded")!)return false;
            state=Native.Static("GameManager","GameState");client=Native.Static("GameManager","Client");
            if(state is null||client is null||!Native.Same(Native.Get(client,"GameState"),state)||
                !(bool)Native.Get(client,"IsReady")!||(bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!)return false;
            var kind=Native.Get(client,"clientType");
            if(!Equals(kind,Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))&&
                !Equals(kind,Enum.Parse(Native.T("ClientBase+ClientType"),"PassAndPlay")))return false;
            if(!NativeRegroupSession.Ready(state))
            {if(!Skipped){Skipped=true;Entry.Note("tech-art-prewarm-skipped","Loaded native local client has no enrolled current state.");}return false;}
            atlas=Native.Invoke(Native.M("GameManager","GetSpriteAtlasManager"),null);
            return atlas is not null;
        }
        public static void BeforeReady(object __instance)
        {
            if(Running)return;
            try
            {
                if(!Context(__instance,out var state,out var client,out var atlas))return;
                if(Native.Same(AttemptClient,client)&&Native.Same(AttemptAtlas,atlas))return;
                // Take a bounded native roster, including the second hotseat
                // player even when they have never been the active viewer.
                var players=Roster(state!);
                AttemptClient=client;AttemptAtlas=atlas;Running=true;
                var watch=Stopwatch.StartNew();
                var job=new Job{Manager=__instance,Client=client!,State=state!,Atlas=atlas!,Players=players,
                    Thread=Environment.CurrentManagedThreadId,Generation=Generation,Deadline=Clock()+10};
                // Start all native asynchronous requests first. The exact same
                // profile cache is then consumed by either player's tech tree.
                foreach(var player in players)_=UnitArtAssets.Prepare(player.Value);
                foreach(bool aquarion in players.Select(p=>p.Aquarion).Distinct())UnitArtAssets.PreloadBodies(aquarion);
                foreach(string tech in new[]{TechTree.PropagandaId,TechTree.CamouflageId,TechTree.IntelligenceId})_=BranchArtAssets.Get(tech,false);
                _=BlackmailArtAssets.Sprite();_=BlackmailArtAssets.Sprite(true);
                _=TradeSecretsArtAssets.Sprite();_=TradeSecretsArtAssets.Sprite(true);
                _=FoggerArtAssets.Icon();_=FoggerArtAssets.Team();_=FoggerArtAssets.Badge();_=BranchArtAssets.DisguiseTech();_=BranchArtAssets.Reconnaissance();
                if(!Current(job,__instance))return;
                FinishHeads(job);
                if(job.Finished.Count!=players.Length)Pending=job;
                Entry.Note("tech-art-prewarmed",FormattableString.Invariant($"players={players.Length} readyHeads={job.Finished.Count} loadingWorkMs={watch.Elapsed.TotalMilliseconds:F1}"));
            }
            catch(Exception e){Pending=null;Note(e);}
            finally{Running=false;}
        }
        static void FinishHeads(Job job)
        {
            foreach(var player in job.Players)
            {
                if(job.Finished.Contains(player.Id)||!UnitArtAssets.Prepare(player.Value))continue;
                // Cache native head mesh geometry for both custom portraits.
                _=UnitArtAssets.Layers(player.Value,false,player.Aquarion);
                _=UnitArtAssets.Layers(player.Value,true,player.Aquarion);
                job.Finished.Add(player.Id);
            }
        }
        static Player[] Roster(object state)
        {
            // Defensive bound includes native neutral/system entries as well
            // as the playable roster; no assumption of exactly two players.
            var roster=Native.Items(Native.Get(state,"PlayerStates")!).Take(33).ToArray();
            if(roster.Length is <1 or >32)throw new InvalidOperationException("Tech prewarm player roster exceeds its bound.");
            var players=roster.Select(p=>new Player(p,Convert.ToByte(Native.Get(p,"Id")),NativeAquarionUnits.IsAquarion(p),UnitArtAssets.HeadProfile(p)))
                .Where(p=>p.Id is not (0 or 255)).OrderBy(p=>p.Id).ToArray();
            if(players.Length==0||players.Select(p=>p.Id).Distinct().Count()!=players.Length)
                throw new InvalidOperationException("Tech prewarm player roster is invalid.");
            return players;
        }
        static bool Current(Job job,object manager)
        {
            if(job.Generation!=Generation||job.Thread!=Environment.CurrentManagedThreadId||!Native.Same(job.Manager,manager)||
                !Context(manager,out var state,out var client,out var atlas)||!Native.Same(job.Client,client)||!Native.Same(job.Atlas,atlas))return false;
            if(!Native.Same(job.State,state))
            {
                // A separately enrolled sibling can finish the same borrowed
                // atlas requests only when every actual profile still matches.
                var players=Roster(state!);
                if(!players.Select(p=>(p.Id,p.Aquarion,p.Profile)).SequenceEqual(job.Players.Select(p=>(p.Id,p.Aquarion,p.Profile))))return false;
                job.State=state!;job.Players=players;
            }
            return true;
        }
        public static void AfterUpdate(object __instance)
        {
            var job=Pending;if(job is null||Running)return;
            try
            {
                if(++job.Polls>300||Clock()>job.Deadline||!Current(job,__instance))
                {Pending=null;return;}
                Running=true;FinishHeads(job);
                if(job.Finished.Count==job.Players.Length){Pending=null;Entry.Note("tech-art-heads-prewarmed","players="+job.Players.Length);}
            }
            catch(Exception e){Pending=null;Note(e);}
            finally{Running=false;}
        }
    }
}
