using System;
using HarmonyLib;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    public static class DynamicMap_Update_Patch
    {
        public static void Postfix()
        {
            try
            {
                MapRouteDisplay.NoteKeyHeld(DynamicMap_MapControls_Patch.IsAppendHeld());
                MapRouteDisplay.Redraw();
            }
            catch (Exception e) { AlteredDestinationPlugin.LogError("Route display error: " + e); }
        }
    }
    [HarmonyPatch(typeof(DynamicMap), "OnEnable")]
    public static class DynamicMap_OnEnable_Patch
    {
        public static void Postfix() => MapRouteDisplay.MapExists = true;
    }
    [HarmonyPatch(typeof(DynamicMap), "OnDestroy")]
    public static class DynamicMap_OnDestroy_Patch
    {
        public static void Postfix()
        {
            MapRouteDisplay.MapExists = false;
            MapRouteDisplay.Forget();
        }
    }
    [HarmonyPatch(typeof(UnitMapIcon), "OnRemoveIcon")]
    public static class UnitMapIcon_OnRemoveIcon_Patch
    {
        public static void Prefix(UnitMapIcon __instance)
        {
            if (__instance != null && __instance.unit is Missile missile) MapRouteDisplay.Release(missile);
        }
    }
}