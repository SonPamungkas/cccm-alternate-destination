using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(OpticalSeekerShell))]
    public static class Patches_ShellGuidance
    {
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef =
            AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        private static readonly AccessTools.FieldRef<OpticalSeekerShell, Transform> targetTransformRef =
            AccessTools.FieldRefAccess<OpticalSeekerShell, Transform>("targetTransform");
        private static readonly AccessTools.FieldRef<OpticalSeekerShell, GlobalPosition> knownPosRef =
            AccessTools.FieldRefAccess<OpticalSeekerShell, GlobalPosition>("knownPos");
        [HarmonyPatch("Initialize")]
        [HarmonyPostfix]
        public static void Initialize_Postfix(OpticalSeekerShell __instance, Unit target)
        {
            if (!AlteredDestinationPlugin.ShellCenterOfMassEnabled.Value) return;
            Unit targetUnit = seekerTargetRef(__instance) ?? target;
            if (targetUnit != null && !(targetUnit is Ship))
            {
                targetTransformRef(__instance) = targetUnit.transform;
                knownPosRef(__instance) = targetUnit.GlobalPosition();
            }
        }
        [HarmonyPatch("GetTargetParameters")]
        [HarmonyPrefix]
        public static void GetTargetParameters_Prefix(OpticalSeekerShell __instance)
        {
            if (!AlteredDestinationPlugin.ShellCenterOfMassEnabled.Value) return;
            Unit targetUnit = seekerTargetRef(__instance);
            if (targetUnit != null && !(targetUnit is Ship))
            {
                Transform current = targetTransformRef(__instance);
                if (current == null || current != targetUnit.transform)
                {
                    targetTransformRef(__instance) = targetUnit.transform;
                }
            }
        }
    }
}