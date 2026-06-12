using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;

namespace AlteredDestination
{
    // Custom class to hold either a static coordinate or a dynamically tracked unit
    public class OverrideData
    {
        public GlobalPosition staticPos;
        public Unit targetUnit;
    }

    [BepInPlugin("com.checkpointcharlie.cruisemissile", "Checkpoint Charlie's Cruise Missile (Alternate destination)", "1.0.0")]
    public class AlteredDestinationPlugin : BaseUnityPlugin
    {
        public static ConditionalWeakTable<Missile, OverrideData> MissileWaypoints = new ConditionalWeakTable<Missile, OverrideData>();
        public static AlteredDestinationPlugin Instance;
        public static ConfigEntry<float> CruiseAltitude;
        public static ConfigEntry<bool> DirectNaval;
        public static ConfigEntry<float> SpreadRadius;

        private void Awake()
        {
            Instance = this;

            CruiseAltitude = Config.Bind("General", "Cruise Altitude", 5f, new ConfigDescription("Target radar altitude for cruise missiles in meters. Lower altitude increases the risk of terrain collision.", new AcceptableValueRange<float>(1f, 10f)));
            DirectNaval = Config.Bind("General", "Final approach against naval target", false, "Off (set as default) = Pop-up attack, On = Direct attack");
            SpreadRadius = Config.Bind("General", "Spread Radius", 15f, "Radius in meters to spread out missiles targeting the same location to prevent stacking.");

            var harmony = new Harmony("com.checkpointcharlie.cruisemissile");
            harmony.PatchAll();
            Logger.LogInfo("Checkpoint Charlie's Cruise Missile Mod Loaded!");
        }

        public static void Log(string message)
        {
            Instance.Logger.LogInfo(message);
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    public static class DynamicMap_MapControls_Patch
    {
        public static void Postfix(DynamicMap __instance)
        {
            // Detect right-click on maximized map
            if (DynamicMap.mapMaximized && Input.GetMouseButtonDown(1))
            {
                GlobalPosition cursorCoords;
                if (__instance.TryGetCursorCoordinates(out cursorCoords))
                {
                    bool clearWaypoint = Input.GetKey(KeyCode.LeftShift);
                    bool setAny = false;

                    // Terrain-Aware Waypoint: 
                    Vector3 localClick = cursorCoords.ToLocalPosition();
                    float terrainHeight = (float)cursorCoords.y;
                    Vector3 rayOrigin = new Vector3(localClick.x, 20000f, localClick.z);
                    
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 30000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        terrainHeight = hit.point.ToGlobalPosition().y;
                    }
                    else if (Terrain.activeTerrain != null)
                    {
                        terrainHeight = Terrain.activeTerrain.SampleHeight(localClick);
                        terrainHeight = new Vector3(localClick.x, terrainHeight, localClick.z).ToGlobalPosition().y;
                    }
                    
                    cursorCoords.y = terrainHeight;

                    Unit[] allUnits = UnityEngine.Object.FindObjectsOfType<Unit>();

                    foreach (var baseIcon in __instance.selectedIcons)
                    {
                        if (baseIcon is UnitMapIcon unitIcon && unitIcon.unit is Missile missile)
                        {
                            AlteredDestinationPlugin.MissileWaypoints.Remove(missile);

                            if (!clearWaypoint)
                            {
                                Unit closestEnemy = null;
                                float closestDist = 100f; // 100m radius for the pillar scan

                                foreach (Unit u in allUnits)
                                {
                                    if (u == null || u == missile || u.gameObject == null || !u.gameObject.activeInHierarchy) continue;
                                    if (u.NetworkHQ == missile.NetworkHQ) continue;

                                    GlobalPosition uPos = u.GlobalPosition();
                                    
                                    float dx = (float)(uPos.x - cursorCoords.x);
                                    float dz = (float)(uPos.z - cursorCoords.z);
                                    float dist2D = Mathf.Sqrt(dx * dx + dz * dz);

                                    if (dist2D < closestDist)
                                    {
                                        closestDist = dist2D;
                                        closestEnemy = u;
                                    }
                                }

                                var data = new OverrideData()
                                {
                                    staticPos = cursorCoords,
                                    targetUnit = closestEnemy
                                };

                                AlteredDestinationPlugin.MissileWaypoints.Add(missile, data);
                                setAny = true;

                                try
                                {
                                    FieldInfo targetField = AccessTools.Field(typeof(Missile), "target") ?? 
                                                            AccessTools.Field(typeof(Missile), "lockedTarget");
                                    
                                    if (targetField != null)
                                    {
                                        targetField.SetValue(missile, closestEnemy); 
                                    }

                                    FieldInfo idField = AccessTools.Field(typeof(Missile), "_targetID");
                                    if (idField != null && closestEnemy != null)
                                    {
                                        idField.SetValue(missile, closestEnemy.persistentID);
                                    }

                                    // Also update the seeker's target to ensure universal retargeting
                                    FieldInfo seekerField = AccessTools.Field(typeof(Missile), "seeker");
                                    if (seekerField != null)
                                    {
                                        object seeker = seekerField.GetValue(missile);
                                        if (seeker != null)
                                        {
                                            FieldInfo seekerTargetField = AccessTools.Field(seeker.GetType(), "targetUnit");
                                            if (seekerTargetField != null)
                                            {
                                                seekerTargetField.SetValue(seeker, closestEnemy);
                                            }
                                        }
                                    }
                                }
                                catch { }

                                if (closestEnemy != null)
                                {
                                    AlteredDestinationPlugin.Log($"Missile retargeted dynamically to enemy unit: {closestEnemy.name}");
                                }
                            }
                            else
                            {
                                AlteredDestinationPlugin.Log("Waypoint cleared for missile.");
                            }
                        }
                    }

                    if (setAny)
                    {
                        AlteredDestinationPlugin.Log("Waypoint assigned to selected missile(s) at " + cursorCoords.ToString());
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Missile), "SetAimpoint")]
    public static class Missile_SetAimpoint_Patch
    {
        private static FieldInfo seekerField = AccessTools.Field(typeof(Missile), "seeker");
        private static FieldInfo terminalModeField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "terminalMode");
        private static FieldInfo altitudeTargetField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "altitudeTarget");
        private static FieldInfo missileTargetField = AccessTools.Field(typeof(Missile), "target");
        private static FieldInfo seekerTargetField = AccessTools.Field(typeof(MissileSeeker), "targetUnit");
        
        // Reflection targets for pop-up suppression
        private static FieldInfo topAttackField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "topAttack");
        private static FieldInfo topAttackAmountField;
        private static FieldInfo topAttackActiveField;

        // Reflection targets for Jink (Zig-Zag) suppression
        private static FieldInfo jinkField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "jinkEvasion");
        private static FieldInfo jinkAmountField;
        private static FieldInfo jinkActiveField;

        private static Type shipType = AccessTools.TypeByName("Ship");
        private static ConditionalWeakTable<Unit, StrongBox<bool>> isShipCache = new ConditionalWeakTable<Unit, StrongBox<bool>>();

        // CRITICAL FIX: Performance Caches. Eliminates massive lag spikes during swarm terminal phases
        private static ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>> neuteredSeekersCache = new ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>>();
        private static ConditionalWeakTable<Missile, StrongBox<Vector2>> spreadCache = new ConditionalWeakTable<Missile, StrongBox<Vector2>>();
        private static ConditionalWeakTable<Missile, StrongBox<float>> failsafeTimers = new ConditionalWeakTable<Missile, StrongBox<float>>();

        private static bool IsShip(Unit targetUnit)
        {
            if (targetUnit == null) return false;
            if (isShipCache.TryGetValue(targetUnit, out var cachedResult)) return cachedResult.Value;
            string nameLower = targetUnit.name.ToLower();
            bool isShipFallback = nameLower.Contains("ship") || nameLower.Contains("corvette") || nameLower.Contains("carrier") || nameLower.Contains("cruiser") || nameLower.Contains("destroyer");
            bool isShip = (shipType != null && (targetUnit.GetComponentInParent(shipType) != null || targetUnit.GetComponentInChildren(shipType) != null)) || isShipFallback;
            isShipCache.Add(targetUnit, new StrongBox<bool>(isShip));
            return isShip;
        }

        // --- THE COUNTER-PITCH SYSTEM + HEIGHT FLOOR FAILSAFE ---
        // Violently suppresses any attempt by the physics engine or aerodynamics to pitch up or down, 
        // while engaging a 1-second emergency pull-up if altitude drops below 1 meter.
        private static void ApplyCounterPitch(Missile missile)
        {
            if (missile.rb != null)
            {
                float currentTime = Time.time;
                bool inEmergency = false;

                // Check Failsafe Timer Status
                if (failsafeTimers.TryGetValue(missile, out var timerBox))
                {
                    if (currentTime < timerBox.Value) 
                    {
                        inEmergency = true;
                    }
                    else if (missile.GlobalPosition().y < 1.0)
                    {
                        timerBox.Value = currentTime + 1.0f; // Trigger 1-second pull-up
                        inEmergency = true;
                    }
                }
                else
                {
                    if (missile.GlobalPosition().y < 1.0)
                    {
                        failsafeTimers.Add(missile, new StrongBox<float>(currentTime + 1.0f));
                        inEmergency = true;
                    }
                    else
                    {
                        failsafeTimers.Add(missile, new StrongBox<float>(0f));
                    }
                }

                // OPTIMIZATION: Avoid writing to Rigidbody if the values are already correct.
                // Writing to rb every frame forces PhysX to recalculate spatial trees, causing massive lag spikes.
                Vector3 vel = missile.rb.velocity;
                Vector3 euler = missile.rb.rotation.eulerAngles; // Use rb.rotation instead of transform.eulerAngles
                float currentPitch = euler.x > 180f ? euler.x - 360f : euler.x; // Normalize Unity Euler pitch
                Vector3 localAngVel = missile.transform.InverseTransformDirection(missile.rb.angularVelocity);

                bool needsVelUpdate = false;
                bool needsRotUpdate = false;
                bool needsAngVelUpdate = false;

                if (inEmergency)
                {
                    // EMERGENCY PULL-UP: Force upward velocity and positive pitch
                    if (vel.y < 0.5f) 
                    {
                        vel.y = 0.5f; // User's reduced climb rate
                        needsVelUpdate = true;
                    }

                    if (Mathf.Abs(currentPitch - (-2f)) > 0.1f) 
                    {
                        euler.x = -2f; // User's reduced pitch
                        needsRotUpdate = true;
                    }
                }
                else
                {
                    // NORMAL LOCK: Flat flight
                    if (Mathf.Abs(vel.y) > 0.1f)
                    {
                        vel.y = 0f;
                        needsVelUpdate = true;
                    }

                    if (Mathf.Abs(currentPitch) > 0.1f)
                    {
                        euler.x = 0f;
                        needsRotUpdate = true;
                    }
                }

                // Always kill pitch momentum if it's building up
                if (Mathf.Abs(localAngVel.x) > 0.05f)
                {
                    localAngVel.x = 0f;
                    needsAngVelUpdate = true;
                }

                // Apply only the dirty fields to keep the physics engine fast and happy
                if (needsVelUpdate) missile.rb.velocity = vel;
                if (needsRotUpdate) missile.rb.rotation = Quaternion.Euler(euler);
                if (needsAngVelUpdate) missile.rb.angularVelocity = missile.transform.TransformDirection(localAngVel);
            }
        }

        public static bool Prefix(Missile __instance, ref GlobalPosition aimPoint, ref Vector3 targetVel)
        {
            var seekerObj = seekerField.GetValue(__instance);
            OpticalSeekerCruiseMissile cSeeker = seekerObj as OpticalSeekerCruiseMissile;
            
            bool hasManualWaypoint = AlteredDestinationPlugin.MissileWaypoints.TryGetValue(__instance, out var data);

            // Calculate deterministic spread offset using cache to eliminate per-frame System.Random lag spikes
            float offsetX = 0f;
            float offsetZ = 0f;
            if (AlteredDestinationPlugin.SpreadRadius.Value > 0f)
            {
                if (spreadCache.TryGetValue(__instance, out var spreadBox))
                {
                    offsetX = spreadBox.Value.x;
                    offsetZ = spreadBox.Value.y;
                }
                else
                {
                    System.Random rand = new System.Random(__instance.GetInstanceID());
                    float angle = (float)rand.NextDouble() * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt((float)rand.NextDouble()) * AlteredDestinationPlugin.SpreadRadius.Value;
                    
                    offsetX = Mathf.Cos(angle) * radius;
                    offsetZ = Mathf.Sin(angle) * radius;
                    
                    spreadCache.Add(__instance, new StrongBox<Vector2>(new Vector2(offsetX, offsetZ)));
                }
            }

            bool isTerminal = false;

            if (cSeeker != null)
            {
                var termObj = terminalModeField?.GetValue(cSeeker);
                if (termObj != null) isTerminal = (bool)termObj;

                if (isTerminal)
                {
                    // IN TERMINAL MODE: Completely suppress Top Attack & Jink Evasion
                    // Using neuteredSeekersCache guarantees this heavy reflection only runs ONCE per missile!
                    if (!neuteredSeekersCache.TryGetValue(cSeeker, out _))
                    {
                        if (topAttackField != null)
                        {
                            var top = topAttackField.GetValue(cSeeker);
                            if (top != null)
                            {
                                if (topAttackAmountField == null) topAttackAmountField = AccessTools.Field(top.GetType(), "amount") ?? AccessTools.Field(top.GetType(), "Amount");
                                if (topAttackActiveField == null) topAttackActiveField = AccessTools.Field(top.GetType(), "active") ?? AccessTools.Field(top.GetType(), "Active");
                                
                                if (topAttackAmountField != null) topAttackAmountField.SetValue(top, 0f);
                                if (topAttackActiveField != null) topAttackActiveField.SetValue(top, false);
                                
                                topAttackField.SetValue(cSeeker, top); 
                            }
                        }

                        if (jinkField != null)
                        {
                            var jink = jinkField.GetValue(cSeeker);
                            if (jink != null)
                            {
                                if (jinkAmountField == null) jinkAmountField = AccessTools.Field(jink.GetType(), "amount") ?? AccessTools.Field(jink.GetType(), "Amount");
                                if (jinkActiveField == null) jinkActiveField = AccessTools.Field(jink.GetType(), "active") ?? AccessTools.Field(jink.GetType(), "Active");
                                
                                if (jinkAmountField != null) jinkAmountField.SetValue(jink, 0f);
                                if (jinkActiveField != null) jinkActiveField.SetValue(jink, false);
                                
                                jinkField.SetValue(cSeeker, jink); 
                            }
                        }
                        
                        neuteredSeekersCache.Add(cSeeker, new StrongBox<bool>(true));
                    }
                }

                // ALWAYS ENFORCE CRUISE RADAR (Both phases)
                altitudeTargetField.SetValue(cSeeker, AlteredDestinationPlugin.CruiseAltitude.Value);

                Unit targetUnit = (Unit)seekerTargetField.GetValue(cSeeker) ?? (Unit)missileTargetField.GetValue(__instance);
                bool isShip = IsShip(targetUnit);

                // 1. GLOBAL LOGIC: Direct Naval Override
                if (AlteredDestinationPlugin.DirectNaval.Value && isShip && !hasManualWaypoint)
                {
                    if (isTerminal) 
                    {
                        GlobalPosition tPos = targetUnit.GlobalPosition();

                        aimPoint.x = tPos.x + offsetX;
                        aimPoint.z = tPos.z + offsetZ;
                        aimPoint.y = __instance.GlobalPosition().y;

                        if (targetUnit.rb != null) 
                        {
                            targetVel = targetUnit.rb.velocity;
                            targetVel.y = 0f; 
                        }

                        // Engage absolute physics lock against pop-up behavior + height failsafe
                        ApplyCounterPitch(__instance);
                    }
                }
            }

            // 2. MOD LOGIC: Manual Waypoint Override
            if (hasManualWaypoint)
            {
                GlobalPosition dest;
                Vector3 newTargetVel = Vector3.zero;

                if (data.targetUnit != null && !data.targetUnit.disabled && data.targetUnit.gameObject.activeInHierarchy)
                {
                    dest = data.targetUnit.GlobalPosition();
                    if (data.targetUnit.rb != null) 
                    {
                        newTargetVel = data.targetUnit.rb.velocity;
                        newTargetVel.y = 0f; 
                    }
                }
                else
                {
                    dest = data.staticPos;
                }

                // Determine terminal status for non-cruise missiles (which lack cSeeker)
                bool terminalOverride = isTerminal;
                if (cSeeker == null)
                {
                    GlobalPosition currentPos = __instance.GlobalPosition();
                    float dx = (float)(currentPos.x - dest.x);
                    float dz = (float)(currentPos.z - dest.z);
                    if (Mathf.Sqrt(dx * dx + dz * dz) < 3000f) terminalOverride = true;
                }

                if (terminalOverride)
                {
                    // TERMINAL PHASE: Apply the full custom flight behavior (spread + direct strike + speed inheritance)
                    aimPoint.x = dest.x + offsetX;
                    aimPoint.z = dest.z + offsetZ;
                    if (cSeeker != null)
                    {
                        aimPoint.y = __instance.GlobalPosition().y;
                    }
                    targetVel = newTargetVel;

                    // Engage absolute physics lock against pop-up behavior + height failsafe
                    if (cSeeker != null) ApplyCounterPitch(__instance);
                }
                else if (data.targetUnit == null)
                {
                    // CRUISE PHASE (Static Map Coordinate):
                    aimPoint.x = dest.x;
                    aimPoint.z = dest.z;
                    // Leave Y completely vanilla so the cruise radar can keep it safely above the water.
                    targetVel = Vector3.zero;
                }
            }
            
            return true;
        }
    }

    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "Initialize")]
    public static class OpticalSeekerCruiseMissile_Initialize_Patch
    {
        private static FieldInfo altField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "altitudeTarget");

        public static void Postfix(OpticalSeekerCruiseMissile __instance)
        {
            // Enforce default Cruise parameters at spawn.
            if (altField != null) altField.SetValue(__instance, AlteredDestinationPlugin.CruiseAltitude.Value);
        }
    }
}