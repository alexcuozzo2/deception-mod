// Borrowed prefab contracts for the production map frame adapter. In native
// CreateUnit the cached Unit.skinVisuals is assigned AFTER Instantiate; the
// serialized SkinVisualsReference component is present on the prefab itself.
using System;
using System.Collections.Generic;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;

public static class PrefabManager
{
    public static readonly Dictionary<(UnitData.Type,TribeType,SkinType),Unit> Fixtures=new();
    public static int Calls;
    public static Func<UnitData.Type,TribeType,SkinType,Unit?>? Override;
    public static Unit GetPrefab(UnitData.Type type,TribeType tribe,SkinType skin)
    {
        Calls++;
        if(Override is not null)return Override(type,tribe,skin)!;
        if(Fixtures.TryGetValue((type,tribe,skin),out var found))return found;
        var unit=new Unit();unit.gameObject.name="Borrowed "+type;
        unit.skinVisuals=null!;
        if(type.ToString() is "dragonegg" or "babydragon" or "firedragon")
            unit.spriteContainer.localPosition=new(.005f,.2f,0);
        var reference=(SkinVisualsReference)unit.gameObject.AddComponent(new Il2CppSystem.Type(typeof(SkinVisualsReference)));
        var parts=new List<SkinVisualsReference.VisualPart>();
        void Part(string name,Vector3 position,Vector3 low,Vector3 high)
        {
            var sprite=new Sprite(name){FixtureBounds=new(new((low.x+high.x)/2,(low.y+high.y)/2,0),new(high.x-low.x,high.y-low.y,0))};
            var renderer=new SpriteRenderer{sprite=sprite};renderer.transform.SetParent(unit.spriteContainer,false);renderer.transform.localPosition=position;
            // Native prefab renderer bounds need not be initialized/usable.
            renderer.bounds=new(new(500,500,0),new(1000,1000,0));
            parts.Add(new(){renderer=new(){spriteRenderer=renderer}});
        }
        // Rect/PPU-derived synthetic bounds from pinned sharedassets0 sprite
        // metadata, not a claim that tight mesh bounds equal engine bounds.
        // Production reads the real Sprite.bounds; it never hardcodes these.
        Part("body",new(-.012f,.147f,0),new(-.099875708f,-.170454542f,0),new(.099875708f,.170454542f,0));
        Part("head",new(-.013f,.215f,0),new(-.071022727f,-.104166667f,0),new(.071022727f,.104166667f,0));
        Part("club",new(-.0426f,.1245f,0),new(-.156565656f,-.146780312f,0),new(.156565656f,.146780312f,0));
        var outline=new SpriteRenderer{sprite=new Sprite("Excluded outline"){FixtureBounds=new(new(0,0,0),new(50,50,0))}};
        outline.transform.SetParent(unit.spriteContainer,false);parts[0].outlineRenderer=new(){spriteRenderer=outline};
        var overlay=new SpriteRenderer{sprite=new Sprite("Excluded overlay"){FixtureBounds=new(new(0,0,0),new(99,99,0))}};overlay.transform.SetParent(unit.spriteContainer,false);
        reference.visualParts=new(parts.ToArray());
        Fixtures[(type,tribe,skin)]=unit;return unit;
    }
    public static void Reset(){Fixtures.Clear();Override=null;Calls=0;}
}
