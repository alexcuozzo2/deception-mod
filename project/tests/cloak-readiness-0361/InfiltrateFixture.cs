using System;
using System.Linq;
using Deception.PolyModAdapter;
using A=Polytopia.Data.UnitAbility.Type;

// Narrow native-shaped completion port. Native AttackCommand.IsValid uses
// CanAttack and real attack options, not the public disguise or an Invisible
// prerequisite. Its Execute selects ability35 and appends InfiltrateAction.
public sealed class AttackCommand
{
 public uint UnitId{get;set;}=1;public byte PlayerId{get;set;}=1;
 public WorldCoordinates Origin{get;set;}=new(0,0);public WorldCoordinates Target{get;set;}=new(1,0);
 public bool IsValid(GameState s,out string reason)
 {
  reason="native invalid";var u=MapDataExtensions.GetTile(s.Map,Origin)?.unit;var target=MapDataExtensions.GetTile(s.Map,Target);
  return s.CurrentPlayer==PlayerId&&u is not null&&u.id==UnitId&&u.owner==PlayerId&&u.health>0&&u.CanAttack()&&
   UnitDataExtensions.HasAbility(u,A.Infiltrate)&&target is {City:true,Explored:true,BeingCaptured:false,Infiltrated:false}&&target.owner!=0&&target.owner!=PlayerId&&
   !s.Players[PlayerId].HasPeaceWith(target.owner)&&Math.Max(Math.Abs(Origin.X-Target.X),Math.Abs(Origin.Y-Target.Y))<=1;
 }
 public void Execute(GameState s)
 {
  if(!IsValid(s,out _))return;
  NativeDisguiseLifecycle.BeforeActive(this,s,out bool scope);
  try
  {
   // A second real predicate read proves shedding did not consume readiness.
   if(!IsValid(s,out _))return;
   var u=MapDataExtensions.GetTile(s.Map,Origin)!.unit!;
   if(UnitDataExtensions.HasAbility(u,A.Infiltrate))s.ActionStack.Add(new InfiltrateAction(PlayerId,Origin,Target));
   u.attacked=true;
  }
  catch(Exception e){NativeDisguiseLifecycle.Finished(e,scope);throw;}
  NativeDisguiseLifecycle.Finished(null,scope);
 }
}
public sealed class InfiltrateAction
{
 public byte PlayerId;public WorldCoordinates Origin,Target;
 public InfiltrateAction(byte owner,WorldCoordinates origin,WorldCoordinates target){PlayerId=owner;Origin=origin;Target=target;}
 public void Execute(GameState s)
 {
  // The real current-version action consumes the source at 0x663629, then
  // queues the native city consequence. Do not replace it with a MoveAction.
  var origin=MapDataExtensions.GetTile(s.Map,Origin)!;if(origin.unit is null)return;
  origin.unit=null;s.Infiltrations++;s.InfiltrationOwner=PlayerId;
  MapDataExtensions.GetTile(s.Map,Target)!.Infiltrated=true;
 }
}
