using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
namespace AlteredDestination
{
    public static class RedistributionScanner
    {
        public static readonly Dictionary<Unit, List<Missile>> IncomingCache =
            new Dictionary<Unit, List<Missile>>();
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef =
            AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        public static IEnumerator ScanLoop()
        {
            yield return new UnityEngine.WaitForSeconds(
                AlteredDestinationPlugin.RedistributionScanInterval.Value);
            while (true)
            {
                if (AlteredDestinationPlugin.MidFlightRedistributionEnabled.Value)
                {
                    try { RunScan(); }
                    catch (Exception e)
                    {
                        AlteredDestinationPlugin.LogError("[Redistribution] Scan error: " + e);
                    }
                }
                yield return new UnityEngine.WaitForSeconds(
                    AlteredDestinationPlugin.RedistributionScanInterval.Value);
            }
        }
        private static void RunScan()
        {
            IncomingCache.Clear();
            var allUnits = UnitRegistry.allUnits;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit u = allUnits[i];
                if (!(u is Missile m)) continue;
                if (m.disabled || m.gameObject == null || !m.gameObject.activeInHierarchy) continue;
                MissileSeeker seeker = seekerRef(m);
                if (!(seeker is OpticalSeekerCruiseMissile)) continue;
                Unit target = MissileUtil.GetTarget(m);
                if (target == null || target.disabled) continue;
                if (!IncomingCache.TryGetValue(target, out var list))
                {
                    list = new List<Missile>();
                    IncomingCache[target] = list;
                }
                list.Add(m);
            }
            if (AlteredDestinationPlugin.Verbose && IncomingCache.Count > 0)
                AlteredDestinationPlugin.Log($"[Redistribution] Scan: {IncomingCache.Count} active target group(s).");
            FactionHQ ownHQ = ResolveOwnHQ();
            if (AlteredDestinationPlugin.Verbose)
                DumpEnemyNavalFleet(ownHQ);
            RedistributionSplitter.ProcessCache(IncomingCache, ownHQ);
        }
        private static FactionHQ ResolveOwnHQ()
        {
            foreach (var missiles in IncomingCache.Values)
            {
                for (int i = 0; i < missiles.Count; i++)
                {
                    Missile m = missiles[i];
                    if (m != null && !m.disabled && m.NetworkHQ != null)
                        return m.NetworkHQ;
                }
            }
            return null;
        }
        private static void DumpEnemyNavalFleet(FactionHQ ownHQ)
        {
            var allUnits = UnitRegistry.allUnits;
            int dumped = 0;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit u = allUnits[i];
                if (!(u is Ship ship) || ship.disabled) continue;
                if (ship.gameObject == null || !ship.gameObject.activeInHierarchy) continue;
                if (!RedistributionSplitter.IsEnemy(ownHQ, ship)) continue;
                GlobalPosition pos = ship.GlobalPosition();
                string shipTypeName = RedistributionSplitter.GetShipTypeName(ship);
                int tier = RedistributionSplitter.GetWeight(ship);
                if (dumped == 0)
                    AlteredDestinationPlugin.Log("[Redistribute] Enemy naval fleet:");
                AlteredDestinationPlugin.Log($"  {ship.name} @ ({pos.x:F0}, {pos.z:F0}) type={shipTypeName} tier={tier}");
                dumped++;
            }
        }
    }
}