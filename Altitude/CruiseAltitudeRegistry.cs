using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public static class CruiseAltitudeRegistry
    {
        private static readonly Dictionary<UnitDefinition, ConfigEntry<float>> cruiseAltitudes = new Dictionary<UnitDefinition, ConfigEntry<float>>();
        private class AltitudeSource
        {
            public ConfigEntry<float> entry;
            public float vanilla;
            public float Value => entry != null ? entry.Value : vanilla;
        }
        private static readonly ConditionalWeakTable<Missile, AltitudeSource> altitudeByMissile = new ConditionalWeakTable<Missile, AltitudeSource>();
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> prefabAltitudeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("altitudeTarget");
        public static void Scan()
        {
            var definitions = Resources.FindObjectsOfTypeAll<MissileDefinition>();
            int cruiseCount = 0;
            foreach (MissileDefinition def in definitions)
            {
                if (def == null || def.unitPrefab == null) continue;
                var prefabSeeker = def.unitPrefab.GetComponent<OpticalSeekerCruiseMissile>();
                if (prefabSeeker == null) continue;
                cruiseCount++;
                TryBind(def, prefabAltitudeRef(prefabSeeker));
            }
            if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"Cruise altitude scan: scanned {definitions.Length} definitions, {cruiseCount} cruise, {cruiseAltitudes.Count} registered.");
        }
        public static void TryBind(UnitDefinition def, float vanillaAltitude)
        {
            if (def == null || cruiseAltitudes.ContainsKey(def)) return;
            string label = SanitizeConfigKey(DisplayName(def));
            try
            {
                cruiseAltitudes[def] = AlteredDestinationPlugin.Instance.Config.Bind(
                    "Cruise Altitude",
                    label,
                    vanillaAltitude,
                    new ConfigDescription(
                        "Target radar altitude in meters. Give each type a different value so a mixed salvo does not fly at one shared height. Lower increases the risk of terrain collision.",
                        new AcceptableValueRange<float>(1f, 500f),
                        new ConfigurationManagerAttributes { Order = -cruiseAltitudes.Count }));
                if (AlteredDestinationPlugin.Verbose) AlteredDestinationPlugin.Log($"Registered cruise altitude: {label} (default {vanillaAltitude}m)");
            }
            catch (Exception e)
            {
                AlteredDestinationPlugin.LogError($"Could not register cruise altitude for '{label}': {e.Message}");
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
        public static float RegisterCruiseAltitude(Missile missile, float vanillaAltitude)
        {
            var source = new AltitudeSource { vanilla = vanillaAltitude };
            if (missile != null && missile.definition != null)
            {
                cruiseAltitudes.TryGetValue(missile.definition, out source.entry);
                altitudeByMissile.Remove(missile);
                altitudeByMissile.Add(missile, source);
            }
            return source.Value;
        }
        public static float GetCruiseAltitude(Missile missile, float fallback)
        {
            return altitudeByMissile.TryGetValue(missile, out var source) ? source.Value : fallback;
        }
    }
}