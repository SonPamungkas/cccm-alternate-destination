using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public static class RedistributionSplitter
    {
        private static readonly FieldInfo shipTypeField =
            AccessTools.Field(AccessTools.TypeByName("ShipDefinition"), "shipType");
        private static readonly Dictionary<Unit, int> fleetKeyByShip = new Dictionary<Unit, int>();
        public static int GetGroupKey(Unit target)
        {
            if (target != null && fleetKeyByShip.TryGetValue(target, out int fleetKey)) return fleetKey;
            return target != null ? target.GetInstanceID() : 0;
        }
        public static bool IsEnemy(FactionHQ ownHQ, Unit u)
        {
            if (u == null || u.disabled || u is Missile) return false;
            if (u.gameObject == null || !u.gameObject.activeInHierarchy) return false;
            if (ownHQ != null)
                return u.NetworkHQ != null && u.NetworkHQ != ownHQ;
            return DynamicMap.GetFactionMode(u.NetworkHQ) == FactionMode.Enemy;
        }
        public static void ProcessCache(Dictionary<Unit, List<Missile>> cache, FactionHQ ownHQ)
        {
            if (cache.Count == 0) return;
            var navalMissiles  = new List<Missile>();
            var surfaceMissiles = new List<Missile>();
            Unit navalPrimary   = null;
            Unit surfacePrimary = null;
            int  navalMax = 0, surfaceMax = 0;
            foreach (var kvp in cache)
            {
                Unit   target   = kvp.Key;
                var    missiles = kvp.Value;
                if (target == null || target.disabled) continue;
                if (missiles == null || missiles.Count == 0) continue;
                if (target is Ship)
                {
                    navalMissiles.AddRange(missiles);
                    if (missiles.Count > navalMax) { navalMax = missiles.Count; navalPrimary = target; }
                }
                else
                {
                    surfaceMissiles.AddRange(missiles);
                    if (missiles.Count > surfaceMax) { surfaceMax = missiles.Count; surfacePrimary = target; }
                }
            }
            int minSalvo = AlteredDestinationPlugin.MinSalvoForRedistribution.Value;
            if (navalMissiles.Count >= minSalvo && navalPrimary != null)
                DistributePool(navalPrimary, navalMissiles, ownHQ, naval: true);
            if (surfaceMissiles.Count >= minSalvo && surfacePrimary != null)
                DistributePool(surfacePrimary, surfaceMissiles, ownHQ, naval: false);
        }
        private static void DistributePool(Unit primary, List<Missile> pool, FactionHQ ownHQ, bool naval)
        {
            GlobalPosition origin   = primary.GlobalPosition();
            float scanRadius        = naval
                ? AlteredDestinationPlugin.SeekerDeadTargetRadiusShip.Value
                : AlteredDestinationPlugin.SeekerDeadTargetRadiusSurface.Value;
            float scanRadiusSq = scanRadius * scanRadius;
            var candidates = new List<Unit>();
            var allUnits   = UnitRegistry.allUnits;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit u = allUnits[i];
                if (!IsEnemy(ownHQ, u)) continue;
                if (naval  && !(u is Ship)) continue;
                if (!naval && u is Ship)   continue;
                if ((u.GlobalPosition() - origin).sqrMagnitude > scanRadiusSq) continue;
                candidates.Add(u);
            }
            if (naval)
            {
                int fleetKey = primary.GetInstanceID();
                for (int i = 0; i < candidates.Count; i++)
                    fleetKeyByShip[candidates[i]] = fleetKey;
            }
            if (candidates.Count == 0)
            {
                if (AlteredDestinationPlugin.Verbose)
                    AlteredDestinationPlugin.Log($"[Redistribute] No fleet in {scanRadius:F0}m of '{primary.name}'. Skipping.");
                return;
            }
            if (naval)
            {
                RestrictToTierMode(candidates, AlteredDestinationPlugin.NavalTierDistributionMode.Value, 2);
            }
            candidates.Sort((a, b) => GetWeight(b).CompareTo(GetWeight(a)));
            pool.Sort((a, b) =>
            {
                float ta = (a != null && !a.disabled) ? a.timeSinceSpawn : 0f;
                float tb = (b != null && !b.disabled) ? b.timeSinceSpawn : 0f;
                return tb.CompareTo(ta); 
            });
            List<Unit> allocation = BuildAllocation(candidates, pool.Count);
            if (AlteredDestinationPlugin.Verbose)
            {
                AlteredDestinationPlugin.Log(
                    $"[Redistribute] Primary '{primary.name}': {pool.Count} missile(s) → {candidates.Count} target(s) in {scanRadius:F0}m.");
                foreach (Unit c in candidates)
                {
                    int cnt = 0;
                    foreach (Unit a in allocation) if (a == c) cnt++;
                    AlteredDestinationPlugin.Log($"  {c.name} (w={GetWeight(c)}) → {cnt}");
                }
            }
            float guardDist = AlteredDestinationPlugin.RedistributionGuardRange.Value;
            for (int i = 0; i < pool.Count && i < allocation.Count; i++)
            {
                Missile m = pool[i];
                if (m == null || m.disabled) continue;
                if (MissileUtil.IsBoosting(m)) continue;
                Unit desired = allocation[i];
                if (desired == null || desired.disabled) continue;
                Unit current = MissileUtil.GetTarget(m);
                if (current == desired) continue;
                GlobalPosition mPos = m.GlobalPosition();
                if (current != null && (mPos - current.GlobalPosition()).magnitude < guardDist) continue;
                if ((mPos - desired.GlobalPosition()).magnitude < guardDist) continue;
                MissileUtil.Retarget(m, desired);
            }
        }
        private static void RestrictToTierMode(List<Unit> candidates, NavalTierDistribution mode, int tierCount)
        {
            if (mode == NavalTierDistribution.Equal) return;
            var distinctWeights = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                int w = GetWeight(candidates[i]);
                if (!distinctWeights.Contains(w)) distinctWeights.Add(w);
            }
            if (distinctWeights.Count <= tierCount) return;
            distinctWeights.Sort((a, b) => mode == NavalTierDistribution.BottomTiers ? a.CompareTo(b) : b.CompareTo(a));
            distinctWeights.RemoveRange(tierCount, distinctWeights.Count - tierCount);
            if (AlteredDestinationPlugin.Verbose)
            {
                var excluded = new List<string>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!distinctWeights.Contains(GetWeight(candidates[i])))
                        excluded.Add($"{candidates[i].name} (w={GetWeight(candidates[i])})");
                }
                if (excluded.Count > 0)
                    AlteredDestinationPlugin.Log($"[Redistribute] Naval tier limit ({mode}): keeping weights [{string.Join(",", distinctWeights)}], excluding {excluded.Count} target(s): {string.Join(", ", excluded)}.");
            }
            for (int i = candidates.Count - 1; i >= 0; i--)
                if (!distinctWeights.Contains(GetWeight(candidates[i])))
                    candidates.RemoveAt(i);
        }
        public static int GetWeight(Unit u)
        {
            if (u == null)     return 1;
            if (u is Ship)     return GetNavalWeight(u);
            if (u is Building) return 1;
            return GetVehicleWeight(u);
        }
        public static string GetShipTypeName(Unit u)
        {
            if (u is Ship ship && ship.definition != null && shipTypeField != null)
            {
                try
                {
                    object stObj = shipTypeField.GetValue(ship.definition);
                    if (stObj != null) return stObj.ToString();
                }
                catch { }
            }
            return null;
        }
        private static int GetNavalWeight(Unit u)
        {
            switch (GetShipTypeName(u))
            {
                case "CV":
                case "LHA": return 12;
                case "DDG":
                case "FFG": return 8;
                case "LFD":
                case "FFL": return 4;
                default:    return 1; 
            }
        }
        private static int GetVehicleWeight(Unit u)
        {
            if (u == null) return 2;
            if (u.radar != null) return 3;
            if (u.weaponStations != null)
            {
                for (int i = 0; i < u.weaponStations.Count; i++)
                {
                    var ws = u.weaponStations[i];
                    if (ws?.Weapons == null) continue;
                    for (int w = 0; w < ws.Weapons.Count; w++)
                        if (ws.Weapons[w] is Gun || ws.Weapons[w] is MissileLauncher)
                            return 3;
                }
            }
            return 2;
        }
        private static List<Unit> BuildAllocation(List<Unit> targets, int count)
        {
            if (targets.Count == 0) return new List<Unit>();
            var weights = new int[targets.Count];
            int totalWeight = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                weights[i]   = GetWeight(targets[i]);
                totalWeight += weights[i];
            }
            var slots      = new int[targets.Count];
            var remainders = new float[targets.Count];
            int allocated  = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                float exact   = (float)count * weights[i] / totalWeight;
                slots[i]      = Mathf.FloorToInt(exact);
                remainders[i] = exact - slots[i];
                allocated    += slots[i];
            }
            while (allocated < count)
            {
                int best = -1; float bestR = -1f;
                for (int i = 0; i < targets.Count; i++)
                    if (remainders[i] > bestR) { bestR = remainders[i]; best = i; }
                if (best < 0) break;
                slots[best]++;
                remainders[best] = -1f;
                allocated++;
            }
            var list = new List<Unit>(count);
            for (int i = 0; i < targets.Count; i++)
                for (int s = 0; s < slots[i]; s++)
                    list.Add(targets[i]);
            return list;
        }
    }
}