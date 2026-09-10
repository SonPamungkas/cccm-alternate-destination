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
    [BepInPlugin("neutral.checkpointcharlie.cruisemissile", "Checkpoint Charlie's Cruise Missile (Alternate destination)", "1.4.0")]
    public class AlteredDestinationPlugin : BaseUnityPlugin
    {
        public const string WaypointAction = "MissileWaypoint";
        private const string S_GENERAL = "General";
        private const string S_WAYPOINTS = "Waypoints";
        private const string S_SMART_LAUNCH = "SmartLaunch";
        private const string S_FIRE_CONTROL = "FireControl";
        private const string S_SMART_SWARM = "SmartSwarm";
        private const string S_SYNC_LAUNCH = "SynchronizedLaunch";
        private const string S_SHELL_GUIDANCE = "GuidedShells";
        private const string S_SEEKER_RETARGET = "SeekerRetarget";
        private const string S_REDISTRIBUTION = "MidFlightRedistribution";
        private const string S_TERMINAL_FLARES = "TerminalFlares";
        private const string S_CRUISE_JAMMING = "Cruise Jamming";
        private const string S_FORWARD_FLARE_SCREEN = "ForwardFlareScreen";
        private const string S_TERMINAL = "TerminalGuidance";
        private const string S_SMART_TERMINAL = "SmartTerminalModes";
        private const string S_TOT_SPEED = "TimeOnTarget";
        public static ConditionalWeakTable<Missile, MissileWaypointData> MissileWaypoints = new ConditionalWeakTable<Missile, MissileWaypointData>();
        public static AlteredDestinationPlugin Instance;
        public static ConfigEntry<bool> VerboseLogging;
        public static ConfigEntry<TerminalMode> TerminalModeDefault;
        public static ConfigEntry<bool> SmartTerminalModeEnabled;
        public static ConfigEntry<TerminalMode> ShipTerminalModeA;
        public static ConfigEntry<TerminalMode> ShipTerminalModeB;
        public static ConfigEntry<TerminalMode> AAATerminalMode;
        public static ConfigEntry<TerminalMode> SAMTerminalMode;
        public static ConfigEntry<TerminalMode> BuildingTerminalMode;
        public static ConfigEntry<TerminalMode> SurfaceTerminalMode;
        public static ConfigEntry<float> TerminalEmergencyFloor;
        public static ConfigEntry<float> TerminalFloorGuard;
        public static ConfigEntry<float> TerminalFloorRecoveryHeight;
        public static ConfigEntry<float> TerminalHardCeiling;
        public static ConfigEntry<float> FinalCommitDistance;
        public static ConfigEntry<float> TerminalImpactSpreadMeters;
        public static ConfigEntry<float> ConvergenceStartDistance;
        public static ConfigEntry<float> AltitudeLookAheadSeconds;
        public static ConfigEntry<float> EmergencyRecoveryClimbSpeed;
        public static ConfigEntry<float> WeavePeriodSeconds;
        public static ConfigEntry<float> WeaveAmplitudeMeters;
        public static ConfigEntry<float> CorkscrewPeriodSeconds;
        public static ConfigEntry<float> CorkscrewLateralAmplitude;
        public static ConfigEntry<float> CorkscrewVerticalAmplitude;
        public static ConfigEntry<float> PopUpStartDistance;
        public static ConfigEntry<float> PopUpApexAltitude;
        public static ConfigEntry<bool> TotSpeedSyncEnabled;
        public static ConfigEntry<float> MinimumSyncThrottle;
        public static ConfigEntry<float> SyncReleaseDistance;
        public static ConfigEntry<float> WaypointRadius;
        public static ConfigEntry<float> LineThickness;
        public static ConfigEntry<bool> SmartLaunch;
        public static ConfigEntry<float> ValueWeight;
        public static ConfigEntry<float> ReferenceValue;
        public static ConfigEntry<bool> SpendFullStation;
        public static ConfigEntry<bool> SaturationCappingEnabled;
        public static ConfigEntry<int> SuperCapitalCap;
        public static ConfigEntry<int> HeavyShipCap;
        public static ConfigEntry<int> LightShipCap;
        public static ConfigEntry<int> GroundTargetCap;
        public static ConfigEntry<bool> FasterSalvoPlanning;
        public static ConfigEntry<float> PlanningTimeMultiplier;
        public static ConfigEntry<bool> SmartSwarmEnabled;
        public static ConfigEntry<int> MinSalvoForDEAD;
        public static ConfigEntry<float> DeadMinimumRange;
        public static ConfigEntry<float> DeadScanInterval;
        public static ConfigEntry<float> SwarmMaxAspectAngle;
        public static float SwarmMinAspectCos { get; private set; } = 0.2588f;
        public static ConfigEntry<bool> SynchronizedLaunch;
        public static ConfigEntry<bool> ShellCenterOfMassEnabled;
        public static ConfigEntry<bool> SeekerAutoRetargetEnabled;
        public static ConfigEntry<float> SeekerAutoRetargetFov;
        public static ConfigEntry<float> SeekerDeadTargetRadiusShip;
        public static ConfigEntry<float> SeekerDeadTargetRadiusSurface;
        public static ConfigEntry<float> SeekerMaxSearchRange;
        public static ConfigEntry<bool> MidFlightRedistributionEnabled;
        public static ConfigEntry<int> MinSalvoForRedistribution;
        public static ConfigEntry<float> RedistributionScanInterval;
        public static ConfigEntry<float> RedistributionGuardRange;
        public static ConfigEntry<NavalTierDistribution> NavalTierDistributionMode;
        public static ConfigEntry<bool>  EnableTerminalJamming;
        public static ConfigEntry<float> JamActiveDuration;
        public static ConfigEntry<float> JamCooldownDuration;
        public static ConfigEntry<float> JamPulseInterval;
        public static ConfigEntry<float> JammingIntensity;
        public static ConfigEntry<bool> EnableTerminalFlares;
        public static ConfigEntry<float> TerminalFlareStartDistance;
        public static ConfigEntry<int> TerminalFlaresPerMissile;
        public static ConfigEntry<int> TerminalFlaresPerBurst;
        public static ConfigEntry<float> TerminalFlareBurstInterval;
        public static ConfigEntry<float> TerminalFlareRearwardSpeed;
        public static ConfigEntry<float> TerminalFlareSideSpeed;
        public static ConfigEntry<float> TerminalFlareUpwardSpeed;
        public static ConfigEntry<float> TerminalFlareSideOffset;
        public static ConfigEntry<float> TerminalFlareUpOffset;
        public static ConfigEntry<float> TerminalFlareRearOffset;
        public static ConfigEntry<float> TerminalFlareLifetime;
        public static ConfigEntry<int> ForwardFlareScreenFlaresPerMissile;
        public static ConfigEntry<float> ForwardFlareScreenStartDistance;
        public static ConfigEntry<float> ForwardFlareScreenBurstInterval;
        private void Awake()
        {
            Instance = this;
            BindConfigs();
            ExtraInputManager.RegisterAction(WaypointAction, Rewired.InputActionType.Button);
            var harmony = new Harmony("neutral.checkpointcharlie.cruisemissile");
            harmony.PatchAll();
            StartCoroutine(DeadScanLoop());
            StartCoroutine(AltitudeScanLoop());
            StartCoroutine(RedistributionScanner.ScanLoop());
            if (Verbose) Logger.LogInfo("Checkpoint Charlie's Cruise Missile Mod Loaded!");
        }
        private void BindConfigs()
        {
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
            SaturationCappingEnabled = Config.Bind(S_SMART_LAUNCH, "SaturationCapping", true,
                "Cap maximum cruise missiles inbound against a single target. Once saturated, surplus launch capacity overflows to the next most valuable target in range.");
            SuperCapitalCap = Config.Bind(S_SMART_LAUNCH, "SuperCapitalCap", 6, new ConfigDescription(
                "Max incoming missiles for aircraft carriers and super capital ships before overflowing to next target.",
                new AcceptableValueRange<int>(1, 24)));
            HeavyShipCap = Config.Bind(S_SMART_LAUNCH, "HeavyShipCap", 4, new ConfigDescription(
                "Max incoming missiles for destroyers and cruisers before overflowing.",
                new AcceptableValueRange<int>(1, 16)));
            LightShipCap = Config.Bind(S_SMART_LAUNCH, "LightShipCap", 2, new ConfigDescription(
                "Max incoming missiles for corvettes and light combatants before overflowing.",
                new AcceptableValueRange<int>(1, 8)));
            GroundTargetCap = Config.Bind(S_SMART_LAUNCH, "GroundTargetCap", 2, new ConfigDescription(
                "Max incoming missiles for standard ground installations and radar/SAM units before overflowing.",
                new AcceptableValueRange<int>(1, 8)));
            FasterSalvoPlanning = Config.Bind(S_FIRE_CONTROL, "Enabled", true,
                "Shrink the wait between a datalink fire-control station finishing salvo planning and actually launching. Vanilla waits planningTimePerFire * shotsQueued seconds (minutes for a full AGM-99 salvo) before firing starts.");
            PlanningTimeMultiplier = Config.Bind(S_FIRE_CONTROL, "PlanningTimeMultiplier", 0.2f, new ConfigDescription(
                "Multiplier applied to planningTimePerFire after vanilla's per-ship skill division. 1.0 = vanilla delay, lower = faster salvo launch.",
                new AcceptableValueRange<float>(0.05f, 1f)));
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
            SwarmMaxAspectAngle = Config.Bind(S_SMART_SWARM, "MaxAspectAngle", 75f, new ConfigDescription(
                "Maximum off-boresight angle (degrees) a threat can be from the missile's forward direction to be eligible for DEAD. Prevents suicidal 180-degree reverse turns.",
                new AcceptableValueRange<float>(15f, 180f)));
            UpdateAspectCos();
            SwarmMaxAspectAngle.SettingChanged += (_, _) => UpdateAspectCos();
            SynchronizedLaunch = Config.Bind(S_SYNC_LAUNCH, "Enabled", true,
                "When a ship fires a cruise missile, sister ships of the same class that can also reach the target are forced to launch at it as well, regardless of their distance or bearing.");
            ShellCenterOfMassEnabled = Config.Bind(S_SHELL_GUIDANCE, "NonShipCenterOfMass", true,
                "For guided artillery/mortar shells (OpticalSeekerShell), target the unit's primary transform/center-of-mass on non-ship targets rather than random weapon stations or antennae.");
            SeekerAutoRetargetEnabled = Config.Bind(S_SEEKER_RETARGET, "Enabled", true,
                "If the missile's current target is null or destroyed, automatically redirect to a new target within FOV or fallback to adjacent enemy units near the dead target.");
            SeekerAutoRetargetFov = Config.Bind(S_SEEKER_RETARGET, "MaxFovAngle", 60f, new ConfigDescription(
                "Field of view cone half-angle (degrees) from missile forward for autonomous target acquisition.",
                new AcceptableValueRange<float>(15f, 180f)));
            SeekerDeadTargetRadiusShip = Config.Bind(S_SEEKER_RETARGET, "DeadTargetRadiusShip", 10000f, new ConfigDescription(
                "Proximity fallback and redistribution radius (meters) around a destroyed naval target for adjacent enemy ships (0 to 10km, default 10km).",
                new AcceptableValueRange<float>(0f, 10000f)));
            SeekerDeadTargetRadiusSurface = Config.Bind(S_SEEKER_RETARGET, "DeadTargetRadiusSurface", 500f, new ConfigDescription(
                "Proximity fallback and redistribution radius (meters) around a destroyed surface/ground target for adjacent enemy ground units (0 to 10km, default 500m).",
                new AcceptableValueRange<float>(0f, 10000f)));
            SeekerMaxSearchRange = Config.Bind(S_SEEKER_RETARGET, "MaxSearchRange", 15000f, new ConfigDescription(
                "Maximum distance (meters) for acquiring targets within FOV.",
                new AcceptableValueRange<float>(1000f, 50000f)));
            MidFlightRedistributionEnabled = Config.Bind(S_REDISTRIBUTION, "Enabled", true,
                "On launch, scan the enemy fleet around the primary target once and proportionally distribute all strikers. Ratios: CV/LHA=12, LFD/DDG=8, FFG/FFL=4, LC/PB=1; SAM/AAA=3, other vehicle=2; building=1.");
            MinSalvoForRedistribution = Config.Bind(S_REDISTRIBUTION, "MinSalvoForRedistribution", 4, new ConfigDescription(
                "Minimum number of cruise missiles targeting the same unit before redistribution triggers.",
                new AcceptableValueRange<int>(2, 64)));
            NavalTierDistributionMode = Config.Bind(S_REDISTRIBUTION, "NavalTierDistributionMode", NavalTierDistribution.TopTiers,
                "Which 2 weight tiers naval redistribution targets, out of the tiers actually present in the fleet (CV/LHA=12, DDG/FFG=8, LFD/FFL=4, LC/PB=1): TopTiers (highest-value ships), BottomTiers (lowest-value ships), or Equal (no restriction, proportional spread across all tiers present).");
            RedistributionScanInterval = Config.Bind(S_REDISTRIBUTION, "RedistributionScanIntervalSec", 5f, new ConfigDescription(
                "How often (seconds) the redistribution scanner sweeps active cruise missiles to detect launch groups.",
                new AcceptableValueRange<float>(1f, 30f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            RedistributionGuardRange = Config.Bind(S_REDISTRIBUTION, "RedistributionGuardRangeMeters", 20000f, new ConfigDescription(
                "Missiles within this distance (meters) of their current OR desired target will not be retargeted by redistribution (prevents disrupting terminal-phase missiles).",
                new AcceptableValueRange<float>(1000f, 50000f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            EnableTerminalJamming = Config.Bind(S_CRUISE_JAMMING, "Enabled", true,
                "Allow supported cruise missiles to directly jam their target's radar/tracking during terminal approach, duty-cycled active/cooldown.");
            JamActiveDuration = Config.Bind(S_CRUISE_JAMMING, "JamActiveDurationSeconds", 5f, new ConfigDescription(
                "How long (seconds) each missile actively jams its target before cooling down.",
                new AcceptableValueRange<float>(0.5f, 60f)));
            JamCooldownDuration = Config.Bind(S_CRUISE_JAMMING, "JamCooldownDurationSeconds", 5f, new ConfigDescription(
                "How long (seconds) each missile waits between jamming bursts.",
                new AcceptableValueRange<float>(0.5f, 60f)));
            JamPulseInterval = Config.Bind(S_CRUISE_JAMMING, "JamPulseIntervalSeconds", 0.2f, new ConfigDescription(
                "Seconds between Unit.Jam pulses while actively jamming (mirrors vanilla JammingPod's own cadence).",
                new AcceptableValueRange<float>(0.05f, 2f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            JammingIntensity = Config.Bind(S_CRUISE_JAMMING, "JammingIntensity", 1f, new ConfigDescription(
                "Jam amount applied per pulse (Unit.JamEventArgs.jamAmount).",
                new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            EnableTerminalFlares = Config.Bind(S_TERMINAL_FLARES, "Enabled", true,
                "Allow supported cruise missiles to dispense real IR-decoy flares during terminal approach.");
            TerminalFlaresPerMissile = Config.Bind(S_TERMINAL_FLARES, "FlaresPerMissile", 100, new ConfigDescription(
                "Total flares carried by each normal/non-forward-screen cruise missile.",
                new AcceptableValueRange<int>(0, 100)));
            TerminalFlareStartDistance = Config.Bind(S_TERMINAL_FLARES, "StartDistanceMeters", 14816f, new ConfigDescription(
                "Flares begin within this distance (meters) of the target.",
                new AcceptableValueRange<float>(900f, 37000f)));
            TerminalFlareLifetime = Config.Bind(S_TERMINAL_FLARES, "FlareLifetimeSeconds", 4f, new ConfigDescription(
                "How long (seconds) a dispensed flare burns before disappearing. Force-set on every spawn (mirrors AryxWeaponsPack's AryxMissileFlare.Launch) rather than trusting the scavenged prefab's own serialized burn time, which may be 0 for a prefab never meant to be spawned standalone.",
                new AcceptableValueRange<float>(0.5f, 15f)));
            TerminalFlareBurstInterval = Config.Bind(S_TERMINAL_FLARES, "BurstIntervalSec", 1f, new ConfigDescription(
                "Seconds between flare bursts for normal missiles.",
                new AcceptableValueRange<float>(0.1f, 10f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlaresPerBurst = Config.Bind(S_TERMINAL_FLARES, "FlaresPerBurst", 2, new ConfigDescription(
                "Number of flares dispensed each burst.",
                new AcceptableValueRange<int>(1, 10), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlareRearwardSpeed = Config.Bind(S_TERMINAL_FLARES, "RearwardSpeed", 32f, new ConfigDescription(
                "Rearward speed (m/s) added relative to the missile so flares separate behind it.",
                new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlareSideSpeed = Config.Bind(S_TERMINAL_FLARES, "SideSpeed", 18f, new ConfigDescription(
                "Sideways speed (m/s) used to split each flare pair left and right.",
                new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlareUpwardSpeed = Config.Bind(S_TERMINAL_FLARES, "UpwardSpeed", 18f, new ConfigDescription(
                "Upward speed (m/s) added to the flare so it rises above the sea-skimming missile.",
                new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlareSideOffset = Config.Bind(S_TERMINAL_FLARES, "SpawnSideOffset", 0.85f, new ConfigDescription(
                "Left/right spawn offset (meters) from the missile center.",
                new AcceptableValueRange<float>(0f, 10f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlareUpOffset = Config.Bind(S_TERMINAL_FLARES, "SpawnUpOffset", 0.35f, new ConfigDescription(
                "Vertical spawn offset (meters) from the missile center.",
                new AcceptableValueRange<float>(0f, 10f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            TerminalFlareRearOffset = Config.Bind(S_TERMINAL_FLARES, "SpawnRearOffset", 1.25f, new ConfigDescription(
                "Rearward spawn offset (meters) from the missile center.",
                new AcceptableValueRange<float>(0f, 10f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            ForwardFlareScreenFlaresPerMissile = Config.Bind(S_FORWARD_FLARE_SCREEN, "FlaresPerMissile", 20, new ConfigDescription(
                "Total flares carried by each missile assigned to the forward flare screen. Independent from TerminalFlares > FlaresPerMissile.",
                new AcceptableValueRange<int>(0, 100)));
            ForwardFlareScreenStartDistance = Config.Bind(S_FORWARD_FLARE_SCREEN, "StartDistanceMeters", 14816f, new ConfigDescription(
                "Forward flare-screen missiles begin dispensing at this distance (meters). Other missiles use TerminalFlares > StartDistanceMeters.",
                new AcceptableValueRange<float>(900f, 37000f)));
            ForwardFlareScreenBurstInterval = Config.Bind(S_FORWARD_FLARE_SCREEN, "BurstIntervalSec", 1f, new ConfigDescription(
                "Seconds between flare bursts for forward flare-screen missiles.",
                new AcceptableValueRange<float>(0.1f, 10f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            SmartTerminalModeEnabled = Config.Bind(S_TERMINAL, "SmartTerminalModeEnabled", true,
                "When on, terminal flight mode auto-adapts per target category (see SmartTerminalModes section). When off, every missile uses TerminalMode below regardless of target type.");
            TerminalModeDefault = Config.Bind(S_TERMINAL, "TerminalMode", TerminalMode.Direct,
                "Terminal flight mode used uniformly for every target when SmartTerminalModeEnabled is off: Vanilla, Direct, Weave, Corkscrew, PopUp.");
            ShipTerminalModeA = Config.Bind(S_SMART_TERMINAL, "ShipModeA", TerminalMode.Direct,
                "Smart mode vs. ships: mode for even-indexed missiles in a salvo (~half the salvo).");
            ShipTerminalModeB = Config.Bind(S_SMART_TERMINAL, "ShipModeB", TerminalMode.Vanilla,
                "Smart mode vs. ships: mode for odd-indexed missiles in a salvo (~the other half), so point-defense faces a mixed threat instead of a uniform attack.");
            AAATerminalMode = Config.Bind(S_SMART_TERMINAL, "AAAMode", TerminalMode.Corkscrew,
                "Smart mode vs. anti-aircraft artillery (guns, flak, SPAAG).");
            SAMTerminalMode = Config.Bind(S_SMART_TERMINAL, "SAMMode", TerminalMode.Weave,
                "Smart mode vs. radar/IR-guided SAM sites.");
            BuildingTerminalMode = Config.Bind(S_SMART_TERMINAL, "BuildingMode", TerminalMode.PopUp,
                "Smart mode vs. buildings/structures.");
            SurfaceTerminalMode = Config.Bind(S_SMART_TERMINAL, "SurfaceMode", TerminalMode.Weave,
                "Smart mode vs. ground vehicles and other surface combatants not otherwise classified.");
            TerminalEmergencyFloor = Config.Bind(S_TERMINAL, "EmergencyFloorMeters", 3.0f, new ConfigDescription(
                "Absolute lowest floor (meters above radar terrain/water). If breached, positive climb is forced to prevent crashes.",
                new AcceptableValueRange<float>(1.0f, 15.0f)));
            TerminalFloorGuard = Config.Bind(S_TERMINAL, "FloorGuardMeters", 6.0f, new ConfigDescription(
                "Lookahead floor guard (meters). Clamps descent velocity when nearing the deck.",
                new AcceptableValueRange<float>(2.0f, 6.0f)));
            TerminalFloorRecoveryHeight = Config.Bind(S_TERMINAL, "FloorRecoveryHeightMeters", 12.0f, new ConfigDescription(
                "Target aim height (meters) during floor recovery.",
                new AcceptableValueRange<float>(5.0f, 50.0f)));
            TerminalHardCeiling = Config.Bind(S_TERMINAL, "HardCeilingMeters", 150.0f, new ConfigDescription(
                "Maximum altitude for low-altitude cruise missiles during terminal approach.",
                new AcceptableValueRange<float>(50.0f, 1000.0f)));
            FinalCommitDistance = Config.Bind(S_TERMINAL, "FinalCommitDistance", 200.0f, new ConfigDescription(
                "Range to target (meters) where terminal evasion fades out and waterline heading lock firmly commits into target hull.",
                new AcceptableValueRange<float>(50.0f, 500.0f)));
            TerminalImpactSpreadMeters = Config.Bind(S_TERMINAL, "TerminalImpactSpreadMeters", 20.0f, new ConfigDescription(
                "Lateral spread (meters) between salvo-mates' impact points during final commit, fading to 0 in the last ~20m/half the commit distance. Prevents missiles converging on the exact same point (and each other's blast radius) during the window they're most likely to be intercepted.",
                new AcceptableValueRange<float>(0.0f, 60.0f)));
            ConvergenceStartDistance = Config.Bind(S_TERMINAL, "ConvergenceStartDistance", 3500.0f, new ConfigDescription(
                "Distance (meters) where terminal evasion maneuvers begin fading down toward the commit point.",
                new AcceptableValueRange<float>(500.0f, 8000.0f)));
            AltitudeLookAheadSeconds = Config.Bind(S_TERMINAL, "AltitudeLookAheadSeconds", 0.8f, new ConfigDescription(
                "Lookahead time (seconds) for projecting vertical velocity into terrain clearance.",
                new AcceptableValueRange<float>(0.2f, 2.0f)));
            EmergencyRecoveryClimbSpeed = Config.Bind(S_TERMINAL, "EmergencyRecoveryClimbSpeed", 3.5f, new ConfigDescription(
                "Upward climb velocity (m/s) forced if emergency floor is penetrated.",
                new AcceptableValueRange<float>(1.0f, 10.0f)));
            WeavePeriodSeconds = Config.Bind(S_TERMINAL, "WeavePeriodSeconds", 2.2f, new ConfigDescription(
                "Period (seconds) for lateral sinusoidal weaving.",
                new AcceptableValueRange<float>(0.5f, 6.0f)));
            WeaveAmplitudeMeters = Config.Bind(S_TERMINAL, "WeaveAmplitudeMeters", 45.0f, new ConfigDescription(
                "Maximum lateral displacement (meters) during weave.",
                new AcceptableValueRange<float>(5.0f, 150.0f)));
            CorkscrewPeriodSeconds = Config.Bind(S_TERMINAL, "CorkscrewPeriodSeconds", 2.5f, new ConfigDescription(
                "Period (seconds) for 3D corkscrew rotation.",
                new AcceptableValueRange<float>(0.5f, 6.0f)));
            CorkscrewLateralAmplitude = Config.Bind(S_TERMINAL, "CorkscrewLateralAmplitude", 50.0f, new ConfigDescription(
                "Lateral radius (meters) of corkscrew.",
                new AcceptableValueRange<float>(5.0f, 100.0f)));
            CorkscrewVerticalAmplitude = Config.Bind(S_TERMINAL, "CorkscrewVerticalAmplitude", 18.0f, new ConfigDescription(
                "Vertical radius (meters) of corkscrew.",
                new AcceptableValueRange<float>(2.0f, 50.0f)));
            PopUpStartDistance = Config.Bind(S_TERMINAL, "PopUpStartDistance", 3000.0f, new ConfigDescription(
                "Distance (meters) from target to initiate pop-up climb.",
                new AcceptableValueRange<float>(800.0f, 6000.0f)));
            PopUpApexAltitude = Config.Bind(S_TERMINAL, "PopUpApexAltitude", 350.0f, new ConfigDescription(
                "Apex height (meters) above target for pop-up dive.",
                new AcceptableValueRange<float>(50.0f, 1000.0f)));
            TotSpeedSyncEnabled = Config.Bind(S_TOT_SPEED, "Enabled", true,
                "Forward cruise missiles automatically modulate throttle down during cruise so trailing missiles catch up, striking simultaneously.");
            MinimumSyncThrottle = Config.Bind(S_TOT_SPEED, "MinimumSyncThrottle", 0.5f, new ConfigDescription(
                "Lowest throttle setting forward missiles will reduce to while waiting for trailing missiles.",
                new AcceptableValueRange<float>(0.2f, 0.9f)));
            SyncReleaseDistance = Config.Bind(S_TOT_SPEED, "SyncReleaseDistance", 5000.0f, new ConfigDescription(
                "Distance (meters) from target where all missiles release throttle sync and accelerate to 100% full sprint.",
                new AcceptableValueRange<float>(300.0f, 5000.0f)));
        }
        private static void UpdateAspectCos()
        {
            SwarmMinAspectCos = Mathf.Cos(SwarmMaxAspectAngle.Value * Mathf.Deg2Rad);
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
                try { FlareToggleRegistry.Scan(); }
                catch (Exception e) { Logger.LogError("Flare toggle scan failed: " + e); }
                try { JamToggleRegistry.Scan(); }
                catch (Exception e) { Logger.LogError("Jam toggle scan failed: " + e); }
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