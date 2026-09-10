using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public class SeekerTargetMemory
    {
        public Unit firstTarget;
        public GlobalPosition firstTargetPos;
        public bool hasFirstTarget;
        public float lastScanTime;
    }
    [HarmonyPatch(typeof(Missile), "ServerFixedUpdate")]
    public static class Patches_SeekerAutoRetarget
    {
        private static readonly ConditionalWeakTable<Missile, SeekerTargetMemory> targetMemories =
            new ConditionalWeakTable<Missile, SeekerTargetMemory>();
        private static readonly AccessTools.FieldRef<Missile, GlobalPosition> missileAimPointRef =
            AccessTools.FieldRefAccess<Missile, GlobalPosition>("aimPoint");
        [HarmonyPrefix]
        public static void Prefix(Missile __instance)
        {
            if (!AlteredDestinationPlugin.SeekerAutoRetargetEnabled.Value) return;
            if (__instance == null || __instance.disabled || __instance.rb == null) return;
            if (MissileUtil.IsBoosting(__instance)) return;
            SeekerTargetMemory memory = targetMemories.GetOrCreateValue(__instance);
            Unit currentTarget = MissileUtil.GetTarget(__instance);
            if (currentTarget != null && !currentTarget.disabled)
            {
                if (!memory.hasFirstTarget)
                {
                    memory.firstTarget = currentTarget;
                    memory.firstTargetPos = currentTarget.GlobalPosition();
                    memory.hasFirstTarget = true;
                }
                else if (memory.firstTarget == currentTarget)
                {
                    memory.firstTargetPos = currentTarget.GlobalPosition();
                }
                return;
            }
            if (!memory.hasFirstTarget)
            {
                if (__instance.targetID.IsValid && UnitRegistry.TryGetUnit(__instance.targetID, out var initTarget))
                {
                    memory.firstTarget = initTarget;
                    memory.firstTargetPos = initTarget.GlobalPosition();
                    memory.hasFirstTarget = true;
                }
                else
                {
                    memory.firstTargetPos = missileAimPointRef(__instance);
                    memory.hasFirstTarget = true;
                }
            }
            if (Time.timeSinceLevelLoad - memory.lastScanTime < 0.2f) return;
            memory.lastScanTime = Time.timeSinceLevelLoad;
            bool hasActiveWaypoints = AlteredDestinationPlugin.MissileWaypoints.TryGetValue(__instance, out var wpData)
                                      && wpData.waypoints.Count > 0;
            GlobalPosition mPos = __instance.GlobalPosition();
            Vector3 mForward = __instance.transform.forward;
            Vector3 mPosLocal = __instance.transform.position;
            float maxFov = AlteredDestinationPlugin.SeekerAutoRetargetFov.Value;
            bool targetIsShip = Missile_SetAimpoint_Patch.IsShip(memory.firstTarget);
            float deadRadius = targetIsShip
                ? AlteredDestinationPlugin.SeekerDeadTargetRadiusShip.Value
                : AlteredDestinationPlugin.SeekerDeadTargetRadiusSurface.Value;
            float maxRange = AlteredDestinationPlugin.SeekerMaxSearchRange.Value;
            float maxRangeSq = maxRange * maxRange;
            float deadRadiusSq = deadRadius * deadRadius;
            MissileSeeker seeker = MissileUtil.GetSeeker(__instance);
            Unit bestFovProximity = null;
            float bestFovProxDistSq = float.MaxValue;
            Unit bestFov = null;
            float bestFovAngle = float.MaxValue;
            Unit bestProximity = null;
            float bestProxDistSq = float.MaxValue;
            var allUnits = UnitRegistry.allUnits;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit u = allUnits[i];
                if (u == null || u.disabled || u is Missile || u.gameObject == null || !u.gameObject.activeInHierarchy)
                    continue;
                if (!IsEnemy(__instance, u))
                    continue;
                if (!IsTargetCompatible(seeker, memory.firstTarget, u))
                    continue;
                GlobalPosition uPos = u.GlobalPosition();
                Vector3 toU = uPos - mPos;
                float distSq = toU.sqrMagnitude;
                if (distSq > maxRangeSq) continue;
                float distFromFirstSq = (uPos - memory.firstTargetPos).sqrMagnitude;
                bool inProximity = memory.hasFirstTarget && distFromFirstSq <= deadRadiusSq;
                if (inProximity && distFromFirstSq < bestProxDistSq)
                {
                    bestProxDistSq = distFromFirstSq;
                    bestProximity = u;
                }
                if (hasActiveWaypoints) continue;
                float angle = Vector3.Angle(mForward, toU);
                if (angle <= maxFov && u.LineOfSight(mPosLocal, 1000f))
                {
                    if (inProximity)
                    {
                        if (distFromFirstSq < bestFovProxDistSq)
                        {
                            bestFovProxDistSq = distFromFirstSq;
                            bestFovProximity = u;
                        }
                    }
                    else if (angle < bestFovAngle)
                    {
                        bestFovAngle = angle;
                        bestFov = u;
                    }
                }
            }
            Unit chosen = bestFovProximity ?? bestFov ?? bestProximity;
            if (chosen != null)
            {
                if (AlteredDestinationPlugin.Verbose)
                {
                    string reason = (chosen == bestFovProximity) ? "FOV + proximity" :
                                    (chosen == bestFov) ? "FOV" : $"proximity fallback ({deadRadius:F0}m)";
                    AlteredDestinationPlugin.Log($"[AutoRetarget] Missile redirected to {chosen.UniqueName ?? chosen.name} ({reason}).");
                }
                MissileUtil.Retarget(__instance, chosen);
            }
        }
        private static bool IsEnemy(Missile missile, Unit unit)
        {
            if (unit == null || unit.disabled || unit is Missile || unit.gameObject == null || !unit.gameObject.activeInHierarchy)
                return false;
            if (missile != null && missile.NetworkHQ != null)
            {
                return unit.NetworkHQ != null && unit.NetworkHQ != missile.NetworkHQ;
            }
            if (DynamicMap.i != null && DynamicMap.i.HQ != null)
            {
                return DynamicMap.GetFactionMode(unit.NetworkHQ) == FactionMode.Enemy;
            }
            return false;
        }
        private static bool IsTargetCompatible(MissileSeeker seeker, Unit firstTarget, Unit candidate)
        {
            if (candidate is Missile) return false;
            if (firstTarget != null)
            {
                if (firstTarget is Aircraft)
                {
                    if (!(candidate is Aircraft)) return false;
                }
                else
                {
                    if (candidate is Aircraft && candidate.rb != null && candidate.rb.velocity.magnitude > 30f)
                        return false;
                }
            }
            else
            {
                if (seeker is OpticalSeekerCruiseMissile || seeker is OpticalSeekerBomb || seeker is OpticalSeekerShell)
                {
                    if (candidate is Aircraft && candidate.rb != null && candidate.rb.velocity.magnitude > 30f)
                        return false;
                }
            }
            if (seeker is IRSeeker)
            {
                IRSource ir = candidate.GetIRSource();
                if (ir == null || ir.flare) return false;
            }
            else if (seeker is ARMSeeker)
            {
                if (candidate.radar == null) return false;
            }
            return true;
        }
    }
}