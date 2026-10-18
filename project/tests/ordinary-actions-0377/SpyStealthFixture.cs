using System;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
public sealed class EmbarkAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}
 public override void Execute(GameState s){}
}
public sealed class RecoverCommand:CommandBase
{
 public WorldCoordinates Coordinates{get;set;}public bool Throw;
 public override bool IsValid(GameState s,out string why)
 {return base.IsValid(s,out why)&&MapDataExtensions.GetTile(s.Map,Coordinates)?.unit is UnitState u&&!u.moved&&!u.attacked&&!u.HasEffect(UnitEffect.Frozen)&&u.health<100;}
 public override void Execute(GameState s)
 {base.Execute(s);if(Throw)throw new InvalidOperationException("native Recover failure");s.ActionStack.Add(new RecoverAction{PlayerId=PlayerId,Coordinates=Coordinates});s.ActionStack.Add(new HealAction{PlayerId=PlayerId,Coordinates=Coordinates});}
}
public sealed class RecoverAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}
 public override void Execute(GameState s)=>MapDataExtensions.GetTile(s.Map,Coordinates)!.unit!.MakeExhauseted(s);
}
public sealed class HealAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}public ushort FixedHealAmount{get;set;}
 public override void Execute(GameState s){var u=MapDataExtensions.GetTile(s.Map,Coordinates)!.unit!;u.health=Math.Min(100,u.health+20);}
}
public sealed class RevealAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}public bool Show;
 public RevealAction(byte actor,WorldCoordinates at,bool popup){PlayerId=actor;Coordinates=at;Show=popup;}
 public override void Execute(GameState s)=>MapDataExtensions.GetTile(s.Map,Coordinates)!.unit!.effects.Remove(UnitEffect.Invisible);
}
