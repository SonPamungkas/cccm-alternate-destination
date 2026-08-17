using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public static class SmartSwarm
    {
        private class DeadAssignment
        {
            public Unit threat;
            public Unit originalTarget;
            public Action<Unit> onThreatDisabled;
        }
        private static readonly ConditionalWeakTable<Missile, DeadAssignment> assignments = new ConditionalWeakTable<Missile, DeadAssignment>();
        private static readonly HashSet<FactionHQ> trackedHQs = new HashSet<FactionHQ>();
        private static readonly List<Missile> salvo = new List<Missile>();
        private static readonly HashSet<Unit> handledThreats = new HashSet<Unit>();
        private static readonly List<Missile> pool = new List<Missile>();
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, bool> terminalModeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, bool>("terminalMode");
        public static void RegisterHQ(FactionHQ hq)
        {
            if (hq != null) trackedHQs.Add(hq);
        }
        public static void RunThreatPass()
        {
            trackedHQs.RemoveWhere(hq => hq == null);
            foreach (FactionHQ hq in trackedHQs)
            {
                List<Missile> cruiseMissiles = hq.GetCruiseMissiles();
                if (cruiseMissiles == null || cruiseMissiles.Count == 0) continue;
                salvo.Clear();
                handledThreats.Clear();
                foreach (Missile m in cruiseMissiles)
                {
                    if (m == null || m.disabled || m.rb == null) continue;
                    if (!(MissileUtil.GetSeeker(m) is OpticalSeekerCruiseMissile)) continue;
                    if (assignments.TryGetValue(m, out var existing))
                    {
                        if (existing.threat == null || existing.threat.disabled) ReleaseAssignment(m, existing);
                        else handledThreats.Add(existing.threat);
                        continue;
                    }
                    if (MissileUtil.IsBoosting(m)) continue;
                    salvo.Add(m);
                }
                if (salvo.Count < AlteredDestinationPlugin.MinSalvoForDEAD.Value) continue;
                AssignThreats(hq, salvo, handledThreats);
            }
        }
        private static void AssignThreats(FactionHQ ourHQ, List<Missile> available, HashSet<Unit> alreadyHandled)
        {
            var allUnits = UnitRegistry.allUnits;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit candidate = allUnits[i];
                if (candidate == null || candidate.disabled) continue;
                if (candidate.NetworkHQ == ourHQ) continue;              
                if (candidate.weaponStations.Count == 0) continue;
                if (alreadyHandled.Contains(candidate)) continue;
                if (!IsShootingAtUs(candidate, ourHQ, out Missile engaged)) continue;
                Missile assignee = PickFromPool(available, engaged, candidate);
                if (assignee == null) continue;
                Assign(assignee, candidate);
                available.Remove(assignee);
                alreadyHandled.Add(candidate);
                if (available.Count < AlteredDestinationPlugin.MinSalvoForDEAD.Value) return;
            }
        }
        private static bool IsShootingAtUs(Unit candidate, FactionHQ ourHQ, out Missile engaged)
        {
            engaged = null;
            foreach (WeaponStation station in candidate.weaponStations)
            {
                if (station == null) continue;
                Unit tracked = station.GetStationTarget();
                if (!(tracked is Missile trackedMissile)) continue;
                if (trackedMissile.disabled || trackedMissile.NetworkHQ != ourHQ) continue;
                if (!(MissileUtil.GetSeeker(trackedMissile) is OpticalSeekerCruiseMissile)) continue;
                engaged = trackedMissile;
                return true;
            }
            return false;
        }
        private static Missile PickFromPool(List<Missile> available, Missile engaged, Unit threat)
        {
            string jsonKey = engaged.definition != null ? engaged.definition.jsonKey : null;
            Unit sharedTarget = MissileUtil.GetTarget(engaged);
            GlobalPosition threatPos = threat.GlobalPosition();
            float minRange = AlteredDestinationPlugin.DeadMinimumRange.Value;
            pool.Clear();
            foreach (Missile m in available)
            {
                if (m.definition == null || m.definition.jsonKey != jsonKey) continue;
                if (MissileUtil.GetTarget(m) != sharedTarget) continue;
                if (MissileUtil.GetSeeker(m) is OpticalSeekerCruiseMissile cs && terminalModeRef(cs)) continue;
                if (FastMath.InRange(m.GlobalPosition(), threatPos, minRange)) continue;
                pool.Add(m);
            }
            if (pool.Count == 0) return null;
            Missile best = pool[0];
            float bestSpeed = best.rb != null ? best.rb.velocity.sqrMagnitude : 0f;
            for (int i = 1; i < pool.Count; i++)
            {
                float speed = pool[i].rb != null ? pool[i].rb.velocity.sqrMagnitude : 0f;
                if (speed > bestSpeed)
                {
                    bestSpeed = speed;
                    best = pool[i];
                }
            }
            return best;
        }
        private static void Assign(Missile missile, Unit threat)
        {
            var assignment = new DeadAssignment
            {
                threat = threat,
                originalTarget = MissileUtil.GetTarget(missile)
            };
            assignment.onThreatDisabled = _ => ReleaseAssignment(missile, assignment);
            threat.onDisableUnit += assignment.onThreatDisabled;
            assignments.Remove(missile);
            assignments.Add(missile, assignment);
            MissileUtil.Retarget(missile, threat);
            if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"DEAD: missile retasked onto air defense '{threat.name}'.");
        }
        private static void ReleaseAssignment(Missile missile, DeadAssignment assignment)
        {
            if (assignment.threat != null && assignment.onThreatDisabled != null)
            {
                assignment.threat.onDisableUnit -= assignment.onThreatDisabled;
            }
            assignment.onThreatDisabled = null;
            assignments.Remove(missile);
            if (missile == null || missile.disabled) return;
            if (assignment.originalTarget != null && !assignment.originalTarget.disabled)
            {
                MissileUtil.Retarget(missile, assignment.originalTarget);
                if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"DEAD: threat destroyed, missile resumed onto '{assignment.originalTarget.name}'.");
            }
        }
    }
}