using System;
using HarmonyLib;
using UnityEngine;
using System.Runtime.CompilerServices;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(Missile), "SetAimpoint")]
    public static class Missile_SetAimpoint_Patch
    {
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef =
            AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, bool> terminalModeRef =
            AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, bool>("terminalMode");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> altitudeTargetRef =
            AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("altitudeTarget");
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef =
            AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef =
            AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, Transform> targetPartRef =
            AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, Transform>("targetPart");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, TopAttack> topAttackRef =
            AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, TopAttack>("topAttack");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, JinkEvasion> jinkRef =
            AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, JinkEvasion>("jinkEvasion");
        private static readonly Type shipType = AccessTools.TypeByName("Ship");
        private static readonly ConditionalWeakTable<Unit, StrongBox<bool>> isShipCache =
            new ConditionalWeakTable<Unit, StrongBox<bool>>();
        private static readonly ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>> neuteredSeekersCache =
            new ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>>();
        private static readonly ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>> loggedTerminalModeCache =
            new ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>>();
        private sealed class ShipSplitEntry
        {
            public Unit target;
            public TerminalMode mode;
        }
        private static readonly ConditionalWeakTable<OpticalSeekerCruiseMissile, ShipSplitEntry> shipSplitModeCache =
            new ConditionalWeakTable<OpticalSeekerCruiseMissile, ShipSplitEntry>();
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
        public static TerminalMode ResolveSmartTerminalMode(Unit target, OpticalSeekerCruiseMissile cSeeker, SalvoMemberState salvoState)
        {
            if (target == null) return TerminalMode.Vanilla;
            if (IsShip(target))
            {
                if (cSeeker != null && shipSplitModeCache.TryGetValue(cSeeker, out var cached) && cached.target == target)
                {
                    return cached.mode;
                }
                TerminalMode mode = (salvoState != null && salvoState.salvoIndex % 2 == 1)
                    ? AlteredDestinationPlugin.ShipTerminalModeB.Value
                    : AlteredDestinationPlugin.ShipTerminalModeA.Value;
                if (cSeeker != null)
                {
                    shipSplitModeCache.Remove(cSeeker);
                    shipSplitModeCache.Add(cSeeker, new ShipSplitEntry { target = target, mode = mode });
                }
                return mode;
            }
            if (target is Building || (target.definition != null && target.definition.mass > 500000f))
            {
                string bName = target.name;
                if (!IsShip(target))
                {
                    return AlteredDestinationPlugin.BuildingTerminalMode.Value;
                }
            }
            string tName = target.name ?? "";
            string defName = target.definition != null ? (target.definition.unitName ?? target.definition.name ?? "") : "";
            string combined = (tName + " " + defName).ToLowerInvariant();
            bool hasGun = false;
            bool hasMissile = false;
            if (target.weaponStations != null)
            {
                for (int i = 0; i < target.weaponStations.Count; i++)
                {
                    var ws = target.weaponStations[i];
                    if (ws == null || ws.Weapons == null) continue;
                    for (int w = 0; w < ws.Weapons.Count; w++)
                    {
                        var weapon = ws.Weapons[w];
                        if (weapon is Gun) hasGun = true;
                        else if (weapon is MissileLauncher) hasMissile = true;
                    }
                }
            }
            bool isSAM = hasMissile ||
                         target.radar != null ||
                         combined.Contains("sam") ||
                         combined.Contains("irsam") ||
                         combined.Contains("rsam") ||
                         combined.Contains("radar") ||
                         combined.Contains("missile");
            if (isSAM)
            {
                return AlteredDestinationPlugin.SAMTerminalMode.Value;
            }
            bool isAAA = hasGun ||
                         combined.Contains("aaa") ||
                         combined.Contains("flak") ||
                         combined.Contains("spaag") ||
                         combined.Contains("shilka") ||
                         combined.Contains("tunguska") ||
                         combined.Contains("gepard") ||
                         combined.Contains("vulcan") ||
                         combined.Contains("cannon") ||
                         combined.Contains("gun");
            if (isAAA)
            {
                return AlteredDestinationPlugin.AAATerminalMode.Value;
            }
            if (target is GroundVehicle || combined.Contains("vehicle") || combined.Contains("tank"))
            {
                return AlteredDestinationPlugin.SurfaceTerminalMode.Value;
            }
            if (combined.Contains("building") || combined.Contains("hangar") || combined.Contains("bunker") || combined.Contains("factory") || combined.Contains("depot") || combined.Contains("bridge") || combined.Contains("runway"))
            {
                return AlteredDestinationPlugin.BuildingTerminalMode.Value;
            }
            return TerminalMode.Vanilla;
        }
        public static bool Prefix(Missile __instance, ref GlobalPosition aimPoint, ref Vector3 targetVel)
        {
            if (__instance == null || __instance.disabled || __instance.rb == null) return true;
            OpticalSeekerCruiseMissile cSeeker = seekerRef(__instance) as OpticalSeekerCruiseMissile;
            if (cSeeker != null)
            {
                Unit targetUnit = seekerTargetRef(cSeeker) ?? missileTargetRef(__instance);
                GlobalPosition targetPos = (targetUnit != null && !targetUnit.disabled) ? targetUnit.GlobalPosition() : aimPoint;
                float distance = (targetPos - __instance.GlobalPosition()).magnitude;
                SalvoMemberState salvoState = SalvoCoordinator.GetOrCreateState(__instance, cSeeker, targetUnit, targetPos);
                bool isTerminal = terminalModeRef(cSeeker);
                bool hasActiveWaypoints = AlteredDestinationPlugin.MissileWaypoints.TryGetValue(__instance, out var wpData)
                                          && wpData.waypoints.Count > 0;
                if (isTerminal && !hasActiveWaypoints)
                {
                    TerminalMode configMode = AlteredDestinationPlugin.TerminalModeDefault.Value;
                    TerminalMode effectiveMode = AlteredDestinationPlugin.SmartTerminalModeEnabled.Value
                        ? ResolveSmartTerminalMode(targetUnit, cSeeker, salvoState)
                        : configMode;
                    if (AlteredDestinationPlugin.Verbose && !loggedTerminalModeCache.TryGetValue(cSeeker, out _))
                    {
                        loggedTerminalModeCache.Add(cSeeker, new StrongBox<bool>(true));
                        string targetName = targetUnit != null ? targetUnit.unitName : "unknown";
                        AlteredDestinationPlugin.Log($"[TerminalMode] {__instance.unitName} vs {targetName}: config={configMode}, resolved={effectiveMode}.");
                    }
                    if (effectiveMode != TerminalMode.Vanilla && !neuteredSeekersCache.TryGetValue(cSeeker, out _))
                    {
                        TopAttack top = topAttackRef(cSeeker);
                        if (top != null)
                        {
                            top.Amount = 0f;
                            top.Active = false;
                        }
                        JinkEvasion jink = jinkRef(cSeeker);
                        if (jink != null)
                        {
                            jink.amount = 0f;
                        }
                        neuteredSeekersCache.Add(cSeeker, new StrongBox<bool>(true));
                    }
                    if (targetUnit != null)
                    {
                        if (targetPartRef(cSeeker) == null)
                        {
                            targetPartRef(cSeeker) = targetUnit.transform;
                        }
                        if (targetUnit.rb != null)
                        {
                            targetVel = targetUnit.rb.velocity;
                            targetVel.y = 0f;
                        }
                    }
                    aimPoint.x = targetPos.x;
                    aimPoint.z = targetPos.z;
                    TerminalGuidance.ApplyTerminalEvasion(__instance, salvoState, targetPos, distance, ref aimPoint, effectiveMode, IsShip(targetUnit));
                    TerminalGuidance.ApplyFloorGuard(__instance, distance, ref aimPoint);
                    TerminalGuidance.ApplyFinalHeadingAlignment(__instance, targetPos, distance);
                    FlareScreenDispenser.UpdateTerminalFlares(salvoState, __instance, distance);
                    MissileJammer.UpdateJamming(salvoState, __instance, targetUnit);
                    return true;
                }
                float cruiseAltitude = CruiseAltitudeRegistry.GetCruiseAltitude(__instance, altitudeTargetRef(cSeeker));
                altitudeTargetRef(cSeeker) = cruiseAltitude;
                SalvoCoordinator.UpdateThrottle(__instance, salvoState, targetPos);
                FlareScreenDispenser.UpdateTerminalFlares(salvoState, __instance, distance);
                MissileJammer.UpdateJamming(salvoState, __instance, targetUnit);
                if (!MissileUtil.IsBoosting(__instance))
                {
                    TerminalGuidance.ApplyFloorGuard(__instance, distance, ref aimPoint);
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