using System;
using Deception.Core;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    public static partial class CloakMovementTests
    {
        static (GameState,UnitState) Spy(string form,bool disguised)
        {
            var(s,u,_)=Make(kind:LedgerActualKind.Genuine);
            u.type=Enum.Parse<UnitType>(form);
            if(!disguised)s.Row=s.Row! with{Independent=false,Appearance=null};
            return(s,u);
        }
        static bool Hide(UnitState u)
        {bool result=true;NativeCloakMovement.MovementAbility(u,Polytopia.Data.UnitAbility.Type.Hide,ref result);return result;}
        static bool DataHide(Polytopia.Data.UnitData d)
        {bool result=true;NativeCloakMovement.MovementDataAbility(d,Polytopia.Data.UnitAbility.Type.Hide,ref result);return result;}
        static bool HiddenMovement(UnitState u)
        {bool result=true;NativeCloakMovement.MovementEffect(u,UnitEffect.Invisible,ref result);return result;}
        static void CheckNormalMovement(UnitState u)
        {
            Check(!Hide(u),"no occupied-tile traversal from Spy Hide");
            Check(!DataHide(u.UnitData),"direct UnitData Hide does not bypass ZOC");
            Check(!HiddenMovement(u),"native invisibility does not bypass ZOC");
        }
        static void CheckUnscoped(UnitState u)
        {Check(Hide(u)&&DataHide(u.UnitData)&&HiddenMovement(u),"native concealment remains intact outside movement queries");}
        static void StealthMovementCases()
        {
            foreach(string form in new[]{SpyCatalog.Id,SpyCatalog.AquarionId})
            foreach(bool disguised in new[]{false,true})
            foreach(bool options in new[]{false,true})
                Test(form+" keeps movement "+disguised+" options="+options,()=>
                {
                    NativeCloakMovement.Clear();var(s,u)=Spy(form,disguised);var data=u.UnitData;var row=s.Row;
                    NativeCloakMovement.ActualQuery? scope;
                    if(options)NativeCloakMovement.BeforeOptions(s,u.coordinates,u,out scope);
                    else NativeCloakMovement.BeforePath(s,u.coordinates,u,out scope);
                    Check(scope is not null,"actual Spy is guarded even with no independent disguise");
                    try
                    {
                        CheckNormalMovement(u);
                        bool dash=true,frozen=true;
                        NativeCloakMovement.MovementDataAbility(data,Polytopia.Data.UnitAbility.Type.Dash,ref dash);
                        NativeCloakMovement.MovementEffect(u,UnitEffect.Frozen,ref frozen);
                        Check(dash&&frozen,"unrelated ability and effect are untouched");
                        Check(DataHide(new Polytopia.Data.UnitData()),"foreign unit definition unchanged");
                        var other=new UnitState();Check(Hide(other)&&HiddenMovement(other),"foreign native body unchanged");
                    }
                    finally{NativeCloakMovement.QueryFinished(null,scope);}
                    CheckUnscoped(u);
                    Check(ReferenceEquals(data,u.UnitData)&&ReferenceEquals(row,s.Row),"query does not edit shared data or ledger");
                });
            foreach(string form in new[]{SpyCatalog.Id,SpyCatalog.AquarionId})
                Test("Mimic "+form+" inherits normal Spy movement",()=>
                {
                    NativeCloakMovement.Clear();var(s,u,_)=Make(form);
                    NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                    try{Check(scope is not null,"Spy mimic has its own guard");CheckNormalMovement(u);}
                    finally{NativeCloakMovement.QueryFinished(null,scope);}
                    CheckUnscoped(u);
                });
            foreach(string form in new[]{"cloak","mermaidcloak","cloak_boat"})
                Test("Mimic "+form+" retains genuine inherited traversal",()=>
                {
                    NativeCloakMovement.Clear();var(s,u,_)=Make(form);
                    NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                    try{Check(scope is {InheritedCloak:true},"Cloak inheritance recognized");CheckUnscoped(u);}
                    finally{NativeCloakMovement.QueryFinished(null,scope);}
                });
            foreach(string form in new[]{"cloak","mermaidcloak","cloak_boat"})
                Test("genuine "+form+" bypasses Spy guards",()=>
                {
                    NativeCloakMovement.Clear();var(s,u,_)=Make(kind:LedgerActualKind.Genuine);u.type=Enum.Parse<UnitType>(form);
                    NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                    try{Check(scope is null,"genuine Cloak stays native");CheckUnscoped(u);}
                    finally{NativeCloakMovement.QueryFinished(null,scope);}
                });
            Test("genuine Warrior wearing Spy remains normal",()=>
            {
                NativeCloakMovement.Clear();var(s,u,_)=Make(SpyCatalog.Id,LedgerActualKind.Genuine);
                NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                try{CheckNormalMovement(u);}finally{NativeCloakMovement.QueryFinished(null,scope);}
            });
            foreach(string mutation in new[]{"frame","turn","owner","position","root","generation"})
                Test("Spy query invalidation "+mutation,()=>
                {
                    NativeCloakMovement.Clear();var(s,u)=Spy(SpyCatalog.Id,false);
                    NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                    switch(mutation)
                    {
                        case "frame":NativeGameStateEnvelope.Frame=new();break;
                        case "turn":s.CurrentTurn++;break;
                        case "owner":u.owner=2;break;
                        case "position":u.coordinates=new(9,9);break;
                        case "root":s.Map.Tiles[(0,0)].unit=new UnitState();break;
                        case "generation":NativeCloakMovement.Clear();break;
                    }
                    try
                    {
                        CheckUnscoped(u);
                        object? result=Offered((1,0));NativeCloakMovement.AfterQuery(scope,ref result);
                        Check(((Il2CppSystem.Collections.Generic.List<WorldCoordinates>)result!).Count==0,"stale query cannot publish a route");
                    }
                    finally{NativeCloakMovement.QueryFinished(null,scope);}
                });
            Test("nested ordinary query masks Spy scope",()=>
            {
                NativeCloakMovement.Clear();var(s,u)=Spy(SpyCatalog.Id,false);
                NativeCloakMovement.BeforePath(s,u.coordinates,u,out var outer);
                var other=new UnitState{type=UnitType.warrior};
                NativeCloakMovement.BeforePath(s,other.coordinates,other,out var inner);
                try{CheckUnscoped(u);}finally{NativeCloakMovement.QueryFinished(null,inner);}
                try{CheckNormalMovement(u);}finally{NativeCloakMovement.QueryFinished(null,outer);}
                CheckUnscoped(u);
            });
            Test("exception finalizer clears Spy movement scope",()=>
            {
                NativeCloakMovement.Clear();var(s,u)=Spy(SpyCatalog.AquarionId,true);
                NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                var error=new Exception("native path failure");
                Check(ReferenceEquals(error,NativeCloakMovement.QueryFinished(error,scope)),"native exception preserved");
                CheckUnscoped(u);
            });
            foreach(string mutation in new[]{"disabled","unready","missing","dead","wrong-carrier"})
                Test("Spy boundary refuses "+mutation,()=>
                {
                    NativeCloakMovement.Clear();var(s,u)=Spy(SpyCatalog.Id,false);
                    switch(mutation)
                    {
                        case "disabled":NativeRegroupSession.Enabled=false;break;
                        case "unready":s.Owned=false;break;
                        case "missing":s.Row=null;break;
                        case "dead":s.Row=s.Row! with{Alive=false};break;
                        case "wrong-carrier":u.type=UnitType.dcp_mimic_form_warrior;s.Row=s.Row! with{Kind=LedgerActualKind.Mimic,Independent=true,Appearance=Make(SpyCatalog.Id).Item1.Row!.Appearance};break;
                    }
                    NativeCloakMovement.BeforePath(s,u.coordinates,u,out var scope);
                    try{Check(scope is null,"unbound/stale body not reinterpreted");CheckUnscoped(u);}
                    finally{NativeCloakMovement.QueryFinished(null,scope);NativeRegroupSession.Enabled=true;}
                });
        }
    }
}
