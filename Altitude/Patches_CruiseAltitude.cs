using HarmonyLib;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "Initialize")]
    public static class OpticalSeekerCruiseMissile_Initialize_Patch
    {
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> altitudeTargetRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("altitudeTarget");
        private static readonly AccessTools.FieldRef<MissileSeeker, Missile> missileRef = AccessTools.FieldRefAccess<MissileSeeker, Missile>("missile");
        public static void Postfix(OpticalSeekerCruiseMissile __instance)
        {
            Missile missile = missileRef(__instance);
            float vanillaAltitude = altitudeTargetRef(__instance);
            if (missile != null) CruiseAltitudeRegistry.TryBind(missile.definition, vanillaAltitude);
            altitudeTargetRef(__instance) = CruiseAltitudeRegistry.RegisterCruiseAltitude(missile, vanillaAltitude);
            if (missile != null) SmartSwarm.RegisterHQ(missile.NetworkHQ);
        }
    }
}