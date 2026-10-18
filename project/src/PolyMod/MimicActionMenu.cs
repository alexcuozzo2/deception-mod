using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // A control's native object, not its changing caption, determines its slot.
    // The list drives keyboard navigation; sibling order drives Unity layout.
    internal static class MimicActionMenu
    {
        internal static void Install()
        {
            _=Native.M("UnityEngine.Transform","GetSiblingIndex");
            _=Native.M("UnityEngine.Transform","SetSiblingIndex","System.Int32");
            var list=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("UIRoundButton"));
            _=Native.M(list,"set_Item","System.Int32","UIRoundButton");
        }
        internal static int Rank(object button,object? inspect,object? smuggle,object? regroup,object? disguise=null)=>
            Native.Same(button,inspect)?0:Native.Same(button,smuggle)?2:Native.Same(button,regroup)?3:Native.Same(button,disguise)?4:1;
        internal static void Normalize(object bar)
        {
            if(!Native.UnityAlive(bar))return;
            NativeActionBarScroll.Observe(bar);
            object? inspect=MimicInspectionControls.OwnedButton(bar),smuggle=MimicSmuggleUi.OwnedButton(bar),regroup=MimicRegroupUi.OwnedButton(bar);
            object? disguise=MimicRegroupUi.OwnedButton(bar,Deception.Core.NativeExchangeKind.Disguise);
            var surveillance=NativeSurveillanceUi.OwnedButton(bar);
            var upgrades=NativeApparentUpgradeUi.OwnedButtons(bar).ToArray();
            var special=NativeSpecialActionUi.OwnedButtons(bar).ToArray();
            if(inspect is null&&smuggle is null&&regroup is null&&disguise is null&&upgrades.Length==0&&special.Length==0&&surveillance is null)return;
            var list=Native.Get(bar,"buttons");if(list is null)return;
            var before=Native.Items(list).ToArray();
            if(before.Length!=Native.Int(Native.Get(list,"Count"))||before.Any(x=>!Native.UnityAlive(x))||
                before.Select(Native.Pointer).Distinct().Count()!=before.Length)return;
            int Order(object button)
            {
                if(Native.Same(button,surveillance))return 200;
                int extra=Array.FindIndex(special,x=>Native.Same(x,button));if(extra>=0)return 100+extra;
                int upgrade=Array.FindIndex(upgrades,x=>Native.Same(x,button));
                return upgrade<0?Rank(button,inspect,smuggle,regroup,disguise):5+upgrade;
            }
            // Native Cost<0 hides the entire ResourceWidget (number and star).
            // Only normalize controls whose pool ownership still matches;
            // a later paid native use reactivates the widget via set_Cost.
            foreach(var button in before)
                if(Order(button)!=1&&Convert.ToSingle(Native.Get(button,"Cost"))==0f)Native.Set(button,"Cost",-1f);
            var ordered=before.OrderBy(Order).ToArray();
            bool changed=false;
            var setter=Native.M(list.GetType(),"set_Item","System.Int32","UIRoundButton");
            for(int i=0;i<ordered.Length;i++)
                if(!Native.Same(before[i],ordered[i])){Native.Invoke(setter,list,i,ordered[i]);changed=true;}
            // Keep non-button decorations in their existing sibling slots and
            // never reparent a top-row or quick-action widget into this row.
            var transforms=ordered.Select(x=>Native.Get(x,"transform")!).ToArray();
            foreach(var group in transforms.Where(Native.UnityAlive).GroupBy(x=>Native.Get(x,"parent") is object p?Native.Pointer(p):0))
            {
                if(group.Key==0)continue;
                var targets=group.ToArray();
                var parent=Native.Get(targets[0],"parent")!;int count=Native.Int(Native.Get(parent,"childCount"));
                if(count<targets.Length||count>128)continue;
                var children=Enumerable.Range(0,count).Select(i=>Native.Invoke(Native.M("UnityEngine.Transform","GetChild","System.Int32"),parent,i)!).ToArray();
                var ids=targets.Select(Native.Pointer).ToHashSet();
                int next=0;
                var wanted=children.Select(x=>ids.Contains(Native.Pointer(x))?targets[next++]:x).ToArray();
                if(next!=targets.Length)continue;
                // Moving one button shifts intervening decorations. Replay the
                // entire desired sibling sequence so those decorations retain
                // their exact original final indices as well.
                for(int i=0;i<wanted.Length;i++)
                    if(Native.Int(Native.Invoke(Native.M("UnityEngine.Transform","GetSiblingIndex"),wanted[i]))!=i)
                    {Native.Invoke(Native.M("UnityEngine.Transform","SetSiblingIndex","System.Int32"),wanted[i],i);changed=true;}
            }
            if(changed)Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);
        }
    }
}
