using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "PreTerminalMode")]
    public static class OpticalSeekerCruiseMissile_PreTerminalMode_Patch
    {
        private static readonly AccessTools.FieldRef<MissileSeeker, Missile> missileRef = AccessTools.FieldRefAccess<MissileSeeker, Missile>("missile");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef = AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef = AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, GlobalPosition> aimPosRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, GlobalPosition>("aimPos");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, GlobalPosition> knownPosRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> lastTerminalCheckRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("lastTerminalCheck");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> terminalRangeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("terminalRange");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, bool> terminalModeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, bool>("terminalMode");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, Transform> targetPartRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, Transform>("targetPart");
        private static readonly AccessTools.FieldRef<Missile, float> throttleRef = AccessTools.FieldRefAccess<Missile, float>("throttle");
        public static bool Prefix(OpticalSeekerCruiseMissile __instance, out bool __state)
        {
            __state = false;
            Missile missile = missileRef(__instance);
            if (missile == null) return true;
            if (Time.timeSinceLevelLoad - lastTerminalCheckRef(__instance) < 0.5f) return true;
            __state = true;
            if (MissileUtil.IsBoosting(missile)) return true;
            GlobalPosition leg;
            if (TryGetCurrentLeg(missile, out leg, out bool justFinished))
            {
                RunGuidanceUpdate(__instance, missile, leg);
                return false;
            }
            if (justFinished) aimPosRef(__instance) = knownPosRef(__instance);
            return true;
        }
        private static void RunGuidanceUpdate(OpticalSeekerCruiseMissile seeker, Missile missile, GlobalPosition leg)
        {
            lastTerminalCheckRef(seeker) = Time.timeSinceLevelLoad;
            aimPosRef(seeker) = leg;
            missile.SetAimpoint(seeker.TerrainWaypoint(leg), Vector3.zero);
            GlobalPosition knownPos = knownPosRef(seeker);
            if (missile.timeSinceSpawn <= 6f || !FastMath.InRange(seeker.transform.GlobalPosition(), knownPos, terminalRangeRef(seeker))) return;
            Unit targetUnit = seekerTargetRef(seeker);
            if (targetUnit != null && !targetUnit.disabled)
            {
                if (AlteredDestinationPlugin.MissileWaypoints.TryGetValue(missile, out var waypointData) && waypointData.waypoints.Count > 0)
                {
                    if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"Missile entered terminal phase, discarding {waypointData.waypoints.Count} remaining waypoint(s).");
                    waypointData.waypoints.Clear();
                }
                targetPartRef(seeker) = targetUnit.GetRandomPart();
                terminalModeRef(seeker) = true;
                missile.Arm();
            }
            else
            {
                missile.Detonate(missile.rb.velocity, hitArmor: false, hitTerrain: false);
            }
        }
        private static bool TryGetCurrentLeg(Missile missile, out GlobalPosition leg, out bool justFinished)
        {
            leg = default(GlobalPosition);
            justFinished = false;
            if (!AlteredDestinationPlugin.MissileWaypoints.TryGetValue(missile, out var waypointData)) return false;
            if (waypointData.waypoints.Count == 0) return false;
            while (waypointData.waypoints.Count > 0)
            {
                GlobalPosition candidate = ResolveWaypoint(waypointData.waypoints[0]);
                if (!Reached(missile, candidate))
                {
                    leg = candidate;
                    return true;
                }
                waypointData.waypoints.RemoveAt(0);
                if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"Missile reached waypoint, {waypointData.waypoints.Count} leg(s) remaining.");
            }
            justFinished = true;
            return false;
        }
        private static GlobalPosition ResolveWaypoint(OverrideData data)
        {
            if (data.targetUnit != null && !data.targetUnit.disabled && data.targetUnit.gameObject.activeInHierarchy)
            {
                return data.targetUnit.GlobalPosition();
            }
            return data.staticPos;
        }
        private static bool Reached(Missile missile, GlobalPosition leg)
        {
            float radius = AlteredDestinationPlugin.WaypointRadius.Value;
            GlobalPosition pos = missile.GlobalPosition();
            if (FastMath.InRange(pos, leg, radius)) return true;
            if (missile.rb == null) return false;
            return FastMath.InRange(pos, leg, radius * 3f) && Vector3.Dot(leg - pos, missile.rb.velocity) < 0f;
        }
    }
}