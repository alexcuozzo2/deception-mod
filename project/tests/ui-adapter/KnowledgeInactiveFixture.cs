namespace Deception.PolyModAdapter
{
    // The existing UI suite exercises Mimic inspection. The Knowledge branch
    // is tested with its real controller and hold adapter in intelligence-0361.
    internal static class NativeIntelligenceOverlay
    {
        internal readonly record struct ControlContext(long Client,long State,long Map,long Interaction,long Unit,long Tile,uint Turn,byte Viewer,byte Owner);
        internal static bool TryControlContext(object interaction,out ControlContext context){context=default;return false;}
        internal static bool TryToggleControl(object interaction,ControlContext expected)=>false;
        internal readonly record struct KnowledgeGestureContext(long Client,long State,long Map,long Interaction,long Unit,long NativeUnit,long Tile,uint Turn,byte Viewer,byte Owner);
        internal static bool TryGestureContext(object interaction,object unit,out KnowledgeGestureContext context){context=default;return false;}
        internal static bool TryToggle(object interaction,object unit,KnowledgeGestureContext expected)=>throw new System.InvalidOperationException("Inactive Knowledge fixture was dispatched");
    }
}
