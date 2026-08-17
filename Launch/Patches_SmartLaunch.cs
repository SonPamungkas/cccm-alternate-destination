using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(CombatAI), "AnalyzeTarget")]
    public static class CombatAI_AnalyzeTarget_Patch
    {
        public static void Postfix(TrackingInfo trackingInfo, ref OpportunityThreat __result)
        {
            if (!AlteredDestinationPlugin.SmartLaunch.Value) return;
            if (__result.opportunity <= 0f) return;
            if (!trackingInfo.TryGetUnit(out Unit unit) || unit == null || unit.definition == null) return;
            float weight = AlteredDestinationPlugin.ValueWeight.Value;
            float reference = Mathf.Max(AlteredDestinationPlugin.ReferenceValue.Value, 1f);
            float valueRatio = Mathf.Max(unit.definition.value, 0f) / reference;
            float multiplier = Mathf.Lerp(1f, valueRatio, Mathf.Clamp01(weight));
            float attackers = Mathf.Max(trackingInfo.missileAttacks + trackingInfo.attackers, 0);
            float spreadPenalty = 1f + 2f * attackers;
            __result = new OpportunityThreat(
                __result.opportunity * Mathf.Max(multiplier, 0.0001f),
                __result.threat * spreadPenalty);
        }
    }
    [HarmonyPatch(typeof(WeaponInfo), "CalcAttacksNeeded")]
    public static class WeaponInfo_CalcAttacksNeeded_Patch
    {
        private const float Unlimited = 10000f;
        private static readonly ConditionalWeakTable<WeaponInfo, StrongBox<bool>> isCruiseWeapon = new ConditionalWeakTable<WeaponInfo, StrongBox<bool>>();
        public static void Postfix(WeaponInfo __instance, ref float __result)
        {
            if (!AlteredDestinationPlugin.SmartLaunch.Value || !AlteredDestinationPlugin.SpendFullStation.Value) return;
            if (!IsCruiseMissileWeapon(__instance)) return;
            __result = Unlimited;
        }
        private static bool IsCruiseMissileWeapon(WeaponInfo info)
        {
            if (info == null) return false;
            if (isCruiseWeapon.TryGetValue(info, out var cached)) return cached.Value;
            bool result = info.weaponPrefab != null
                && info.weaponPrefab.GetComponent<OpticalSeekerCruiseMissile>() != null;
            isCruiseWeapon.Add(info, new StrongBox<bool>(result));
            return result;
        }
    }
}