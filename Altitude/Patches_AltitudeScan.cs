using System;
using HarmonyLib;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(FactionHQ), "OnMissionLoad")]
    public static class FactionHQ_OnMissionLoad_Patch
    {
        public static void Postfix()
        {
            if (AlteredDestinationPlugin.Instance == null) return;
            try { CruiseAltitudeRegistry.Scan(); }
            catch (Exception e) { AlteredDestinationPlugin.LogError("Cruise altitude scan failed: " + e); }
            try { FlareToggleRegistry.Scan(); }
            catch (Exception e) { AlteredDestinationPlugin.LogError("Flare toggle scan failed: " + e); }
            try { JamToggleRegistry.Scan(); }
            catch (Exception e) { AlteredDestinationPlugin.LogError("Jam toggle scan failed: " + e); }
        }
    }
}