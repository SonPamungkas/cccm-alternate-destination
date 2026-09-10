using HarmonyLib;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(FireControl), "FireControl_OnInitialize")]
    public static class FireControl_OnInitialize_Patch
    {
        private static readonly AccessTools.FieldRef<FireControl, float> planningTimePerFireRef =
            AccessTools.FieldRefAccess<FireControl, float>("planningTimePerFire");
        public static void Postfix(FireControl __instance)
        {
            if (!AlteredDestinationPlugin.FasterSalvoPlanning.Value) return;
            planningTimePerFireRef(__instance) *= AlteredDestinationPlugin.PlanningTimeMultiplier.Value;
        }
    }
}