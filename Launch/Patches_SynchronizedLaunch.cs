using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(MissileLauncher), "Fire")]
    public static class MissileLauncher_Fire_Patch
    {
        private static bool syncing;
        private static readonly List<Unit> sisters = new List<Unit>();
        public static void Postfix(MissileLauncher __instance, Unit owner, Unit target, Vector3 inheritedVelocity, WeaponStation weaponStation, GlobalPosition aimpoint)
        {
            if (syncing) return;
            if (!AlteredDestinationPlugin.SynchronizedLaunch.Value) return;
            if (!(owner is Ship) || target == null || target.disabled) return;
            if (weaponStation == null || weaponStation.WeaponInfo == null) return;
            if (!IsCruiseMissileWeapon(weaponStation.WeaponInfo)) return;
            if (owner.definition == null || owner.NetworkHQ == null) return;
            CollectSisterShips(owner);
            if (sisters.Count == 0) return;
            syncing = true;
            try
            {
                int launched = 0;
                foreach (Unit sister in sisters)
                {
                    if (TryForceLaunch(sister, target, weaponStation.WeaponInfo)) launched++;
                }
                if (launched > 0 && AlteredDestinationPlugin.Verbose)
                {
                    AlteredDestinationPlugin.Log($"Synchronized launch: {launched} sister ship(s) fired on '{target.name}'.");
                }
            }
            finally
            {
                syncing = false;
            }
        }
        private static void CollectSisterShips(Unit firingShip)
        {
            sisters.Clear();
            FactionHQ hq = firingShip.NetworkHQ;
            string jsonKey = firingShip.definition.jsonKey;
            foreach (PersistentID id in hq.factionUnits)
            {
                if (!UnitRegistry.TryGetUnit(id, out Unit unit)) continue;
                if (unit == null || unit == firingShip || unit.disabled) continue;
                if (!(unit is Ship) || unit.definition == null) continue;
                if (unit.definition.jsonKey != jsonKey) continue;
                sisters.Add(unit);
            }
        }
        private static bool TryForceLaunch(Unit sister, Unit target, WeaponInfo firingWeapon)
        {
            float range = firingWeapon.targetRequirements.maxRange;
            float distance = FastMath.Distance(sister.GlobalPosition(), target.GlobalPosition());
            if (distance > range) return false;
            foreach (WeaponStation station in sister.weaponStations)
            {
                if (station == null || station.WeaponInfo != firingWeapon) continue;
                if (!station.Ready()) continue;
                Vector3 inherited = sister.rb != null ? sister.rb.velocity : Vector3.zero;
                foreach (Weapon weapon in station.Weapons)
                {
                    if (weapon == null) continue;
                    weapon.Fire(sister, target, inherited, station, target.GlobalPosition());
                    return true;
                }
            }
            return false;
        }
        private static bool IsCruiseMissileWeapon(WeaponInfo info)
        {
            return info.weaponPrefab != null && info.weaponPrefab.GetComponent<OpticalSeekerCruiseMissile>() != null;
        }
    }
}