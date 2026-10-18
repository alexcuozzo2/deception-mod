using System;
using System.Collections.Generic;
namespace UnityEngine {public class Object:PointerObject {public bool Alive=true;public static implicit operator bool(Object? o)=>o?.Alive==true;}}
public sealed class Unit:UnityEngine.Object {public UnitState? UnitState{get;set;}public bool Visible;public float Alpha=0.35f;public void SetVisible(bool visible,MapRenderContext context){Visible=visible;if(visible&&!UnitState!.HasEffect(UnitEffect.Invisible))Alpha=1f;} }
public sealed class MapRenderContext:PointerObject {public MapData mapData{get;set;}=new();public PointerObject gld{get;set;}=new();public MapRenderer unitRegistry{get;set;}=new();public byte localPlayerId{get;set;}}
public sealed class MapRenderer:UnityEngine.Object {public static MapRenderer? Current{get;set;}public readonly Dictionary<uint,Unit> Units=new();public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);}
namespace Deception.PolyModAdapter {internal static class Entry {public static void Note(string kind,string message){} }}
