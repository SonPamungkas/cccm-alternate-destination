using System;
using HarmonyLib;
using UnityEngine;
using System.Runtime.CompilerServices;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(Missile), "SetAimpoint")]
    public static class Missile_SetAimpoint_Patch
    {
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef = AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, bool> terminalModeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, bool>("terminalMode");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> altitudeTargetRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("altitudeTarget");
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef = AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef = AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, Transform> targetPartRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, Transform>("targetPart");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, TopAttack> topAttackRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, TopAttack>("topAttack");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, JinkEvasion> jinkRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, JinkEvasion>("jinkEvasion");
        private static Type shipType = AccessTools.TypeByName("Ship");
        private static ConditionalWeakTable<Unit, StrongBox<bool>> isShipCache = new ConditionalWeakTable<Unit, StrongBox<bool>>();
        private static ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>> neuteredSeekersCache = new ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>>();
        private static ConditionalWeakTable<Missile, StrongBox<float>> failsafeTimers = new ConditionalWeakTable<Missile, StrongBox<float>>();
        public static bool IsShip(Unit targetUnit)
        {
            if (targetUnit == null) return false;
            if (isShipCache.TryGetValue(targetUnit, out var cachedResult)) return cachedResult.Value;
            string name = targetUnit.name;
            bool isShipFallback = name.IndexOf("ship", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("corvette", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("carrier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("cruiser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("destroyer", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isShip = (shipType != null && (targetUnit.GetComponentInParent(shipType) != null || targetUnit.GetComponentInChildren(shipType) != null)) || isShipFallback;
            isShipCache.Add(targetUnit, new StrongBox<bool>(isShip));
            return isShip;
        }
        private static void ApplyCounterPitch(Missile missile)
        {
            if (missile.rb == null) return;
            float currentTime = Time.time;
            bool inEmergency = false;
            if (failsafeTimers.TryGetValue(missile, out var timerBox))
            {
                if (currentTime < timerBox.Value)
                {
                    inEmergency = true;
                }
                else if (missile.GlobalPosition().y < 1.0)
                {
                    timerBox.Value = currentTime + 1.0f;
                    inEmergency = true;
                }
            }
            else if (missile.GlobalPosition().y < 1.0)
            {
                failsafeTimers.Add(missile, new StrongBox<float>(currentTime + 1.0f));
                inEmergency = true;
            }
            else
            {
                failsafeTimers.Add(missile, new StrongBox<float>(0f));
            }
            Vector3 vel = missile.rb.velocity;
            bool needsVelUpdate = false;
            if (inEmergency)
            {
                if (vel.y < 0.5f)
                {
                    vel.y = 0.5f;
                    needsVelUpdate = true;
                }
                Vector3 desiredUp = (Vector3.up + missile.transform.forward * 0.05f).normalized;
                Vector3 emergencyAxis = Vector3.Cross(missile.transform.up, desiredUp);
                missile.rb.AddTorque(emergencyAxis * 50f, ForceMode.Acceleration);
            }
            else
            {
                if (Mathf.Abs(vel.y) > 0.1f)
                {
                    vel.y = 0f;
                    needsVelUpdate = true;
                }
                Vector3 tiltAxis = Vector3.Cross(missile.transform.up, Vector3.up);
                if (tiltAxis.sqrMagnitude > 0.0001f)
                {
                    missile.rb.AddTorque(tiltAxis * 50f, ForceMode.Acceleration);
                }
            }
            if (needsVelUpdate) missile.rb.velocity = vel;
        }
        public static bool Prefix(Missile __instance, ref GlobalPosition aimPoint, ref Vector3 targetVel)
        {
            OpticalSeekerCruiseMissile cSeeker = seekerRef(__instance) as OpticalSeekerCruiseMissile;
            if (cSeeker != null)
            {
                bool isTerminal = terminalModeRef(cSeeker);
                Unit earlyTarget = seekerTargetRef(cSeeker) ?? missileTargetRef(__instance);
                bool isShip = IsShip(earlyTarget);
                bool directNaval = AlteredDestinationPlugin.DirectNaval.Value && isShip;
                if (isTerminal && directNaval && !neuteredSeekersCache.TryGetValue(cSeeker, out _))
                {
                    TopAttack top = topAttackRef(cSeeker);
                    if (top != null)
                    {
                        top.Amount = 0f;
                        top.Active = false;
                    }
                    JinkEvasion jink = jinkRef(cSeeker);
                    if (jink != null) jink.amount = 0f;
                    neuteredSeekersCache.Add(cSeeker, new StrongBox<bool>(true));
                }
                float cruiseAltitude = CruiseAltitudeRegistry.GetCruiseAltitude(__instance, altitudeTargetRef(cSeeker));
                altitudeTargetRef(cSeeker) = cruiseAltitude;
                Unit targetUnit = earlyTarget;
                if (targetPartRef(cSeeker) == null && targetUnit != null)
                {
                    targetPartRef(cSeeker) = targetUnit.GetRandomPart();
                }
                if (directNaval && isTerminal && targetUnit != null)
                {
                    GlobalPosition tPos = targetUnit.GlobalPosition();
                    aimPoint.x = tPos.x;
                    aimPoint.z = tPos.z;
                    aimPoint.y = __instance.GlobalPosition().y;
                    if (targetUnit.rb != null)
                    {
                        targetVel = targetUnit.rb.velocity;
                        targetVel.y = 0f;
                    }
                    ApplyCounterPitch(__instance);
                }
                return true;
            }
            ApplyRouteToPlainMissile(__instance, ref aimPoint, ref targetVel);
            return true;
        }
        private static void ApplyRouteToPlainMissile(Missile missile, ref GlobalPosition aimPoint, ref Vector3 targetVel)
        {
            if (!AlteredDestinationPlugin.MissileWaypoints.TryGetValue(missile, out var waypointData)) return;
            GlobalPosition pos = missile.GlobalPosition();
            while (waypointData.waypoints.Count > 1)
            {
                OverrideData head = waypointData.waypoints[0];
                GlobalPosition leg = ResolveWaypoint(head, out _);
                if (!FastMath.InRange(pos, leg, 1500f)) break;
                waypointData.waypoints.RemoveAt(0);
            }
            if (waypointData.waypoints.Count == 0) return;
            GlobalPosition dest = ResolveWaypoint(waypointData.waypoints[0], out Vector3 destVel);
            aimPoint.x = dest.x;
            aimPoint.z = dest.z;
            if (FastMath.InRange(pos, dest, 3000f))
            {
                targetVel = destVel;
            }
            else
            {
                targetVel = Vector3.zero;
            }
        }
        private static GlobalPosition ResolveWaypoint(OverrideData data, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (data.targetUnit != null && !data.targetUnit.disabled && data.targetUnit.gameObject.activeInHierarchy)
            {
                if (data.targetUnit.rb != null)
                {
                    velocity = data.targetUnit.rb.velocity;
                    velocity.y = 0f;
                }
                return data.targetUnit.GlobalPosition();
            }
            return data.staticPos;
        }
    }
}