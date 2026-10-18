using System;
using System.Collections.Generic;
using Deception.Core;
namespace Il2CppSystem
{
    public sealed class Action:Object
    {
        readonly System.Action Handler;public Action(System.Action action){Handler=action;}
        public static Action op_Implicit(System.Action action)=>new(action);
        public void Invoke()=>Handler();
    }
}
namespace UnityEngine
{
    public class Object:Il2CppSystem.Object
    {
        public bool Alive{get;set;}=true;
        public static bool op_Implicit(Object value)=>value is not null&&value.Alive;
        public static void Destroy(Object value){value.Alive=false;}
    }
}
public sealed partial class TileData
{
    public bool Invisible{get;set;}public int GetUnitCalls{get;set;}public bool? IncludeHidden{get;set;}
    public UnitState? GetUnit(GameState state,byte viewer,bool includeHidden)
    {GetUnitCalls++;IncludeHidden=includeHidden;return Invisible&&!includeHidden?null:unit;}
}
public sealed class Tile:UnityEngine.Object{public TileData Data{get;set;}=new();public bool IsHidden{get;set;}}
public sealed class MapRenderer:UnityEngine.Object
{
    public static MapRenderer? Current{get;set;}public Tile? Visual{get;set;}
    public Tile? GetTileInstance(WorldCoordinates at)=>Visual;
}
public static class GameManager
{public static GameState? GameState{get;set;}public static PlayerState? LocalPlayer{get;set;}public static ClientBase? Client{get;set;}}
public sealed class ClientBase:Il2CppSystem.Object{public int Sent;public CommandBase? Last;public void SendCommand(CommandBase command){Sent++;Last=command;}}
public class CommandBase:Il2CppSystem.Object{public NativeExchangeIntent? Intent;}
public sealed class BuildAction:Il2CppSystem.Object{public NativeExchangeIntent? Intent{get;set;}public bool Valid{get;set;}=true;}
public sealed class BuildReaction:Il2CppSystem.Object{public BuildAction action{get;set;}=new();}
public sealed class UpgradeAction:Il2CppSystem.Object
{
    public byte PlayerId{get;}public Polytopia.Data.UnitData.Type Type{get;}public WorldCoordinates Coordinates{get;}public int Cost{get;}
    public static int Executions;
    public UpgradeAction(byte actor,Polytopia.Data.UnitData.Type type,WorldCoordinates coordinates,int cost){PlayerId=actor;Type=type;Coordinates=coordinates;Cost=cost;}
    public void Execute(GameState state){Executions++;throw new Exception("Never execute a genuine upgrade for an apparent change");}
}
public sealed class UpgradeReaction:Il2CppSystem.Object
{
    public UpgradeAction action{get;set;}public static int Calls;public static string Mode="delay";public static Il2CppSystem.Action? Pending;public static UpgradeAction? Last;
    public UpgradeReaction(UpgradeAction value){action=value;}
    public void Execute(Il2CppSystem.Action callback)
    {Calls++;Last=action;Pending=callback;if(Mode=="throw")throw new Exception("Native reaction failure");if(Mode=="twice"||Mode=="callback-throw"){callback.Invoke();callback.Invoke();if(Mode=="callback-throw")throw new Exception("Native failure after completion");}}
}
namespace Deception.PolyModAdapter
{
    internal static class MimicRegroupUi{internal static bool Choosing;}
    internal static class MimicPresentationUi{internal static bool Allowed=true;internal static bool CanSmuggleView(object unit)=>Allowed;internal static void ReflowBar(object bar){}}
    internal static partial class NativeExchangeCommands
    {
        internal static bool IsExchange(object action)=>action is BuildAction;
        internal static NativeExchangeIntent? Get(object action)=>((BuildAction)action).Intent;
        internal static void ValidateBase(object action,NativeExchangeIntent intent){NativeExchangeIntentCodec.Validate(intent);if(!((BuildAction)action).Valid)throw new Exception("Native carrier differs from full intent");}
        internal static object Create(NativeExchangeIntent intent)=>new CommandBase{Intent=intent};
        internal static void Forget(object command){}
    }
}
