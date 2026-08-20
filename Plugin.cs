using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using InputFramework;
using UnityEngine;
namespace AlteredDestination
{
    public class OverrideData
    {
        public GlobalPosition staticPos;
        public Unit targetUnit;
    }
    public class MissileWaypointData
    {
        public List<OverrideData> waypoints = new List<OverrideData>();
        public int colorIndex = -1;
    }
    [BepInPlugin("neutral.checkpointcharlie.cruisemissile", "Checkpoint Charlie's Cruise Missile (Alternate destination)", "1.3.1")]
    public class AlteredDestinationPlugin : BaseUnityPlugin
    {
        public const string WaypointAction = "MissileWaypoint";
        private const string S_GENERAL = "General";
        private const string S_WAYPOINTS = "Waypoints";
        private const string S_SMART_LAUNCH = "SmartLaunch";
        private const string S_SMART_SWARM = "SmartSwarm";
        private const string S_SYNC_LAUNCH = "SynchronizedLaunch";
        public static ConditionalWeakTable<Missile, MissileWaypointData> MissileWaypoints = new ConditionalWeakTable<Missile, MissileWaypointData>();
        public static AlteredDestinationPlugin Instance;
        public static ConfigEntry<bool> DirectNaval;
        public static ConfigEntry<bool> VerboseLogging;
        public static ConfigEntry<float> WaypointRadius;
        public static ConfigEntry<float> LineThickness;
        public static ConfigEntry<bool> SmartLaunch;
        public static ConfigEntry<float> ValueWeight;
        public static ConfigEntry<float> ReferenceValue;
        public static ConfigEntry<bool> SpendFullStation;
        public static ConfigEntry<bool> SmartSwarmEnabled;
        public static ConfigEntry<int> MinSalvoForDEAD;
        public static ConfigEntry<float> DeadMinimumRange;
        public static ConfigEntry<float> DeadScanInterval;
        public static ConfigEntry<bool> SynchronizedLaunch;
        private void Awake()
        {
            Instance = this;
            BindConfigs();
            ExtraInputManager.RegisterAction(WaypointAction, Rewired.InputActionType.Button);
            var harmony = new Harmony("com.checkpointcharlie.cruisemissile");
            harmony.PatchAll();
            StartCoroutine(DeadScanLoop());
            StartCoroutine(AltitudeScanLoop());
            if (Verbose) Logger.LogInfo("Checkpoint Charlie's Cruise Missile Mod Loaded!");
        }
        private void BindConfigs()
        {
            DirectNaval = Config.Bind(S_GENERAL, "DirectNavalAttack", false,
                "Off (default) = the missile performs its built-in pop-up before diving on a ship. On = flat, level run-in into the hull.");
            VerboseLogging = Config.Bind(S_GENERAL, "VerboseLogging", false,
                "Print what the mod is doing to the BepInEx console. Errors are always reported regardless of this setting.");
            WaypointRadius = Config.Bind(S_WAYPOINTS, "AcceptanceRadius", 1500f, new ConfigDescription(
                "How close (meters) a missile must get to a waypoint before moving on to the next leg. Values below ~900 risk the missile circling a waypoint it cannot turn tightly enough to reach.",
                new AcceptableValueRange<float>(500f, 5000f)));
            LineThickness = Config.Bind(S_WAYPOINTS, "LineThickness", 0.6f, new ConfigDescription(
                "Thickness of the route line drawn on the map. 0 hides the line entirely.",
                new AcceptableValueRange<float>(0f, 20f)));
            SmartLaunch = Config.Bind(S_SMART_LAUNCH, "Enabled", true,
                "AI missile stations concentrate on the most valuable target in range instead of spreading fire across everything they can reach.");
            ValueWeight = Config.Bind(S_SMART_LAUNCH, "ValueWeight", 1f, new ConfigDescription(
                "How hard unit value dominates target selection. 0 = vanilla scoring, 1 = fully proportional to value.",
                new AcceptableValueRange<float>(0f, 1f)));
            ReferenceValue = Config.Bind(S_SMART_LAUNCH, "ReferenceValue", 10f, new ConfigDescription(
                "Unit value treated as the baseline when normalising. Targets above this are favoured, below are penalised.",
                new AcceptableValueRange<float>(1f, 1000f)));
            SpendFullStation = Config.Bind(S_SMART_LAUNCH, "SpendFullStation", true,
                "Empty the whole weapon station into the chosen target rather than stopping once the AI judges it has fired 'enough'. Cruise missile stations only.");
            SmartSwarmEnabled = Config.Bind(S_SMART_SWARM, "Enabled", true,
                "When something starts shooting at the salvo, one cruise missile breaks off to destroy the shooter.");
            MinSalvoForDEAD = Config.Bind(S_SMART_SWARM, "MinimumSalvoSize", 4, new ConfigDescription(
                "Only peel a missile off when at least this many are still alive against the primary target, so a small strike never sacrifices one.",
                new AcceptableValueRange<int>(2, 32)));
            DeadMinimumRange = Config.Bind(S_SMART_SWARM, "MinimumRange", 5000f, new ConfigDescription(
                "A missile is only retasked onto a threat at least this far away (meters). Closer than this there is no room to turn onto it, and the missile drives into the ground instead.",
                new AcceptableValueRange<float>(500f, 30000f)));
            DeadScanInterval = Config.Bind(S_SMART_SWARM, "ScanInterval", 1f, new ConfigDescription(
                "How often the threat scan runs, in seconds.",
                new AcceptableValueRange<float>(0.25f, 10f)));
            SynchronizedLaunch = Config.Bind(S_SYNC_LAUNCH, "Enabled", true,
                "When a ship fires a cruise missile, sister ships of the same class that can also reach the target are forced to launch at it as well, regardless of their distance or bearing.");
        }
        private IEnumerator DeadScanLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(DeadScanInterval.Value);
                if (!SmartSwarmEnabled.Value) continue;
                try { SmartSwarm.RunThreatPass(); }
                catch (Exception e) { Logger.LogError("DEAD threat pass error: " + e); }
            }
        }
        private IEnumerator AltitudeScanLoop()
        {
            while (true)
            {
                try { CruiseAltitudeRegistry.Scan(); }
                catch (Exception e) { Logger.LogError("Cruise altitude scan failed: " + e); }
                yield return new WaitForSeconds(10f);
            }
        }
        public static bool Verbose => VerboseLogging.Value;
        public static void Log(string message)
        {
            Instance.Logger.LogInfo(message);
        }
        public static void LogError(string message)
        {
            Instance.Logger.LogError(message);
        }
    }
}