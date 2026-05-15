using BepInEx;
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

        private void Awake()
        {
            Instance = this;
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

                    // 4. Terrain-Aware Waypoint: 
                    // Convert the global coordinate to a local Vector3 so we can perform physics operations
                    Vector3 localClick = cursorCoords.ToLocalPosition();
                    float terrainHeight = (float)cursorCoords.y;
                    Vector3 rayOrigin = new Vector3(localClick.x, 20000f, localClick.z);
                    
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 30000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        // Convert the hit point back to a GlobalPosition to get the absolute terrain height
                        terrainHeight = hit.point.ToGlobalPosition().y;
                    }
                    else if (Terrain.activeTerrain != null)
                    {
                        // Fallback to Unity terrain sampling using local coordinates
                        terrainHeight = Terrain.activeTerrain.SampleHeight(localClick);
                        // SampleHeight returns the height relative to terrain object, so we convert back
                        terrainHeight = new Vector3(localClick.x, terrainHeight, localClick.z).ToGlobalPosition().y;
                    }
                    
                    // Assign the terrain-aware height back into the static coordinate
                    cursorCoords.y = terrainHeight;

                    // Grab all units in the scene once per click to check if the user clicked near one
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
                                    // Skip invalid units, self, or destroyed units
                                    if (u == null || u == missile || u.gameObject == null || !u.gameObject.activeInHierarchy) continue;

                                    // Check for opposite faction
                                    if (u.NetworkHQ == missile.NetworkHQ) continue;

                                    GlobalPosition uPos = u.GlobalPosition();
                                    
                                    // 1. Pillar Scan: Calculate 2D distance by strictly ignoring the Y (Height) axis.
                                    float dx = (float)(uPos.x - cursorCoords.x);
                                    float dz = (float)(uPos.z - cursorCoords.z);
                                    float dist2D = Mathf.Sqrt(dx * dx + dz * dz);

                                    // 2. Closest to Center: Constantly update if we find a unit closer to the exact click center
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

                                // Aggressively overwrite the missile's internal target variable using Reflection.
                                // This ensures the UI, Seeker, and Proximity Fuse all update to the new target.
                                try
                                {
                                    FieldInfo targetField = AccessTools.Field(typeof(Missile), "target") ?? 
                                                            AccessTools.Field(typeof(Missile), "lockedTarget");
                                    
                                    if (targetField != null)
                                    {
                                        targetField.SetValue(missile, closestEnemy); 
                                    }

                                    // Also sync the _targetID to prevent the game from reverting our 'target' field
                                    FieldInfo idField = AccessTools.Field(typeof(Missile), "_targetID");
                                    if (idField != null && closestEnemy != null)
                                    {
                                        idField.SetValue(missile, closestEnemy.persistentID);
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
        public static bool Prefix(Missile __instance, ref GlobalPosition aimPoint, ref Vector3 targetVel)
        {
            if (AlteredDestinationPlugin.MissileWaypoints.TryGetValue(__instance, out var data))
            {
                GlobalPosition dest;
                Vector3 newTargetVel = Vector3.zero;
                
                // If we snapped to a specific unit, and it's still alive/active, continuously track its position
                if (data.targetUnit != null && !data.targetUnit.disabled && data.targetUnit.gameObject.activeInHierarchy)
                {
                    dest = data.targetUnit.GlobalPosition();
                    
                    // Use the built-in rb property for direct velocity access (faster than GetComponent)
                    if (data.targetUnit.rb != null)
                    {
                        newTargetVel = data.targetUnit.rb.velocity;
                    }
                }
                else
                {
                    // 3. Failsafe: If no unit was found (or if it was destroyed), fallback to our static, terrain-aware map coordinate
                    dest = data.staticPos;
                }

                // Calculate 2D distance between the missile and the target
                GlobalPosition currentPos = __instance.GlobalPosition();
                float dx = currentPos.x - dest.x;
                float dz = currentPos.z - dest.z;
                float dist2D = Mathf.Sqrt(dx * dx + dz * dz);

                // Hijack X and Z for horizontal guidance
                aimPoint.x = dest.x;
                aimPoint.z = dest.z;

                // Cruise Phase vs Terminal Phase
                if (dist2D < 2000f) 
                {
                    // Terminal dive to override height mapping (dives to terrain height or unit height)
                    aimPoint.y = dest.y;
                }

                // Feed the new target's velocity into the aimpoint logic so Proportional Navigation still works
                targetVel = newTargetVel;
            }
            
            return true;
        }
    }
}