using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public static class JamToggleRegistry
    {
        private static readonly Dictionary<UnitDefinition, ConfigEntry<bool>> jamToggles = new Dictionary<UnitDefinition, ConfigEntry<bool>>();
        private static readonly ConditionalWeakTable<Missile, ConfigEntry<bool>> toggleByMissile = new ConditionalWeakTable<Missile, ConfigEntry<bool>>();
        public static void Scan()
        {
            var definitions = Resources.FindObjectsOfTypeAll<MissileDefinition>();
            foreach (MissileDefinition def in definitions)
            {
                if (def == null || def.unitPrefab == null) continue;
                if (def.unitPrefab.GetComponent<OpticalSeekerCruiseMissile>() == null) continue;
                TryBind(def);
            }
        }
        public static void TryBind(UnitDefinition def)
        {
            if (def == null || jamToggles.ContainsKey(def)) return;
            string label = SanitizeConfigKey(DisplayName(def));
            try
            {
                jamToggles[def] = AlteredDestinationPlugin.Instance.Config.Bind(
                    "Cruise Jamming Toggles",
                    label,
                    true,
                    new ConfigDescription(
                        "Allow this cruise missile type to directly jam its target or an incoming ARH threat. Off, this type never jams regardless of the Cruise Jamming section.",
                        null,
                        new ConfigurationManagerAttributes { Order = -jamToggles.Count }));
                if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"Registered jam toggle: {label}");
            }
            catch (Exception e)
            {
                AlteredDestinationPlugin.LogError($"Could not register jam toggle for '{label}': {e.Message}");
            }
        }
        private static string DisplayName(UnitDefinition def)
        {
            if (def == null) return "None";
            string key = string.IsNullOrEmpty(def.jsonKey) ? (def.unitPrefab != null ? def.unitPrefab.name : def.name) : def.jsonKey;
            string raw = string.IsNullOrEmpty(def.unitName) ? key : $"{def.unitName} ({key})";
            return raw.Length > 40 ? raw.Substring(0, 37) + "..." : raw;
        }
        private static string SanitizeConfigKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unknown";
            s = s.Replace("[", "(").Replace("]", ")")
                 .Replace("=", "-").Replace("\\", "/")
                 .Replace("'", "").Replace("\"", "")
                 .Replace("\n", " ").Replace("\t", " ");
            return s.Trim();
        }
        public static void RegisterMissile(Missile missile)
        {
            if (missile == null || missile.definition == null) return;
            jamToggles.TryGetValue(missile.definition, out var entry);
            toggleByMissile.Remove(missile);
            toggleByMissile.Add(missile, entry);
        }
        public static bool IsJamEnabled(Missile missile)
        {
            return !toggleByMissile.TryGetValue(missile, out var entry) || entry == null || entry.Value;
        }
    }
}