using BepInEx.Configuration;
using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System;
using UnityEngine;

namespace AlteredDestination
{


    public class OverrideData
    {
        public GlobalPosition staticPos;
        public Unit targetUnit;
    }

    [BepInPlugin("com.checkpointcharlie.cruisemissile", "Checkpoint Charlie's Cruise Missile (Alternate destination)", "1.2.0")]
    public class AlteredDestinationPlugin : BaseUnityPlugin
    {
        public static ConditionalWeakTable<Missile, OverrideData> MissileWaypoints = new ConditionalWeakTable<Missile, OverrideData>();
        public static AlteredDestinationPlugin Instance;
        public static ConfigEntry<float> CruiseAltitude;
        public static ConfigEntry<bool> DirectNaval;

        public static ConfigEntry<bool> TorpedoMode;

        private void Awake()
        {
            Instance = this;

            CruiseAltitude = Config.Bind("General", "Cruise Altitude", 5f, new ConfigDescription("Target radar altitude for cruise missiles in meters. Lower altitude increases the risk of terrain collision.", new AcceptableValueRange<float>(1f, 10f)));
            DirectNaval = Config.Bind("General", "Final approach against naval target", false, "Off (set as default) = Pop-up attack, On = Direct attack");
            TorpedoMode = Config.Bind("General", "Torpedo Mode", false, "Cruise missiles will not collide with the sea surface (terrain2_tile).");

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
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef = AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<Missile, PersistentID> idRef = AccessTools.FieldRefAccess<Missile, PersistentID>("_targetID");
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef = AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef = AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");

        public static void Postfix(DynamicMap __instance)
        {

            if (DynamicMap.mapMaximized && Input.GetMouseButtonDown(1))
            {
                GlobalPosition cursorCoords;
                if (__instance.TryGetCursorCoordinates(out cursorCoords))
                {
                    bool clearWaypoint = Input.GetKey(KeyCode.LeftShift);
                    bool setAny = false;

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
                                float closestDist = 100f; 

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
                                    missileTargetRef(missile) = closestEnemy;

                                    if (closestEnemy != null)
                                    {
                                        idRef(missile) = closestEnemy.persistentID;
                                    }

                                    MissileSeeker seeker = seekerRef(missile);
                                    if (seeker != null)
                                    {
                                        seekerTargetRef(seeker) = closestEnemy;
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

    [HarmonyPatch(typeof(Missile), "DetectCollisions")]
    public static class Missile_DetectCollisions_Patch
    {
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef = AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef = AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef = AccessTools.FieldRefAccess<Missile, Unit>("target");

        public static bool Prefix(Missile __instance)
        {
            if (AlteredDestinationPlugin.TorpedoMode.Value && __instance.GlobalPosition().y < 50f && Torpedo.IsOverWater(__instance))
            {
                if (seekerRef(__instance) is OpticalSeekerCruiseMissile cSeeker)
                {
                    Unit targetUnit = seekerTargetRef(cSeeker) ?? missileTargetRef(__instance);
                    if (Missile_SetAimpoint_Patch.IsShip(targetUnit))
                    {

                    RaycastHit[] hits = __instance.rb.SweepTestAll(__instance.rb.velocity.normalized, __instance.rb.velocity.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Ignore);

                    foreach (var hit in hits)
                    {
                        var unit = hit.collider.GetComponentInParent<Unit>();
                        if (unit != null)
                        {

                            AccessTools.Method(typeof(Missile), "Detonate").Invoke(__instance, new object[] { hit.normal, true, false });
                            __instance.rb.velocity = Vector3.zero;
                            return false; 
                        }
                    }

                    return false;
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Missile), "SetAimpoint")]
    public static class Missile_SetAimpoint_Patch
    {
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef = AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, bool> terminalModeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, bool>("terminalMode");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, float> altitudeTargetRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, float>("altitudeTarget");
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef = AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef = AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");

        private static FieldInfo topAttackField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "topAttack");
        private static FieldInfo topAttackAmountField;
        private static FieldInfo topAttackActiveField;

        private static FieldInfo jinkField = AccessTools.Field(typeof(OpticalSeekerCruiseMissile), "jinkEvasion");
        private static FieldInfo jinkAmountField;
        private static FieldInfo jinkActiveField;

        private static Type shipType = AccessTools.TypeByName("Ship");
        private static ConditionalWeakTable<Unit, StrongBox<bool>> isShipCache = new ConditionalWeakTable<Unit, StrongBox<bool>>();

        private static ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>> neuteredSeekersCache = new ConditionalWeakTable<OpticalSeekerCruiseMissile, StrongBox<bool>>();

        private static ConditionalWeakTable<Missile, StrongBox<float>> failsafeTimers = new ConditionalWeakTable<Missile, StrongBox<float>>();

        public static bool IsShip(Unit targetUnit)
        {
            if (targetUnit == null) return false;
            if (isShipCache.TryGetValue(targetUnit, out var cachedResult)) return cachedResult.Value;
            string name = targetUnit.name;
            bool isShipFallback = name.IndexOf("ship", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("corvette", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("carrier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("cruiser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("destroyer", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isShip = (shipType != null && (targetUnit.GetComponentInParent(shipType) != null || targetUnit.GetComponentInChildren(shipType) != null)) || isShipFallback;
            isShipCache.Add(targetUnit, new StrongBox<bool>(isShip));
            return isShip;
        }

        private static void ApplyCounterPitch(Missile missile)
        {
            if (missile.rb != null)
            {
                float currentTime = Time.time;
                bool inEmergency = false;

                if (failsafeTimers.TryGetValue(missile, out var timerBox))
                {
                    if (currentTime < timerBox.Value) 
                    {
                        inEmergency = true;
                    }
                    else if (missile.GlobalPosition().y < 1.0 && !(AlteredDestinationPlugin.TorpedoMode.Value && Torpedo.IsOverWater(missile)))
                    {
                        timerBox.Value = currentTime + 1.0f; 
                        inEmergency = true;
                    }
                }
                else
                {
                    if (missile.GlobalPosition().y < 1.0 && !(AlteredDestinationPlugin.TorpedoMode.Value && Torpedo.IsOverWater(missile)))
                    {
                        failsafeTimers.Add(missile, new StrongBox<float>(currentTime + 1.0f));
                        inEmergency = true;
                    }
                    else
                    {
                        failsafeTimers.Add(missile, new StrongBox<float>(0f));
                    }
                }

                Vector3 vel = missile.rb.velocity;
                bool needsVelUpdate = false;

                if (inEmergency)
                {

                    if (vel.y < 0.5f) 
                    {
                        vel.y = 0.5f; 
                        needsVelUpdate = true;
                    }

                    Vector3 desiredUp = (Vector3.up + missile.transform.forward * 0.05f).normalized; 
                    Vector3 tiltAxis = Vector3.Cross(missile.transform.up, desiredUp);
                    missile.rb.AddTorque(tiltAxis * 50f, ForceMode.Acceleration);
                }
                else
                {

                    if (Mathf.Abs(vel.y) > 0.1f && !(AlteredDestinationPlugin.TorpedoMode.Value && Torpedo.IsOverWater(missile)))
                    {
                        vel.y = 0f;
                        needsVelUpdate = true;
                    }

                    Vector3 tiltAxis = Vector3.Cross(missile.transform.up, Vector3.up);
                    if (tiltAxis.sqrMagnitude > 0.0001f)
                    {
                        missile.rb.AddTorque(tiltAxis * 50f, ForceMode.Acceleration);
                    }
                }

                missile.rb.AddTorque(-missile.rb.angularVelocity * 1f, ForceMode.Acceleration);

                if (needsVelUpdate) missile.rb.velocity = vel;
            }
        }

        public static bool Prefix(Missile __instance, ref GlobalPosition aimPoint, ref Vector3 targetVel)
        {
            OpticalSeekerCruiseMissile cSeeker = seekerRef(__instance) as OpticalSeekerCruiseMissile;

            bool hasManualWaypoint = AlteredDestinationPlugin.MissileWaypoints.TryGetValue(__instance, out var data);

            float offsetX = 0f;
            float offsetZ = 0f;

            bool isTerminal = false;
            bool isShip = false;

            if (cSeeker != null)
            {
                isTerminal = terminalModeRef(cSeeker);

                if (isTerminal)
                {

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

                altitudeTargetRef(cSeeker) = AlteredDestinationPlugin.CruiseAltitude.Value;

                Unit targetUnit = seekerTargetRef(cSeeker) ?? missileTargetRef(__instance);
                isShip = IsShip(targetUnit);

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

                        ApplyCounterPitch(__instance);
                    }
                }
            }

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

                    aimPoint.x = dest.x + offsetX;
                    aimPoint.z = dest.z + offsetZ;
                    if (cSeeker != null)
                    {
                        aimPoint.y = __instance.GlobalPosition().y;
                    }
                    targetVel = newTargetVel;

                    if (cSeeker != null) ApplyCounterPitch(__instance);
                }
                else if (data.targetUnit == null)
                {

                    aimPoint.x = dest.x;
                    aimPoint.z = dest.z;

                    targetVel = Vector3.zero;
                }
            }

            if (cSeeker != null)
            {
                if (AlteredDestinationPlugin.TorpedoMode.Value && isShip && __instance.GlobalPosition().y < 2f && Torpedo.IsOverWater(__instance))
                {
                    altitudeTargetRef(cSeeker) = 0f;
                    Torpedo.ApplyTorpedoPhysics(__instance);
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

            if (altField != null) altField.SetValue(__instance, AlteredDestinationPlugin.CruiseAltitude.Value);
        }
    }

    public static class Torpedo
    {
        private const float SpringK = 2f;
        private const float DampK = 2f;

        public static bool IsOverWater(Missile missile)
        {
            if (Physics.Raycast(missile.transform.position, Vector3.down, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {

                if (Mathf.Abs((float)hit.point.ToGlobalPosition().y) < 1f)
                    return true;

                Transform t = hit.collider.transform;
                for (int i = 0; i < 3 && t != null; i++, t = t.parent)
                {
                    if (t.name.IndexOf("terrain2_tile", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false; 
            }
            return true; 
        }

        public static void ApplyTorpedoPhysics(Missile missile)
        {
            if (missile.rb == null) return;
            float liveGlobalY = (float)missile.GlobalPosition().y;
            float targetGlobalY = -1f;
            float yError = targetGlobalY - liveGlobalY;

            float SpringK = 50f;
            float DampK = 10f;
            float forceY = (yError * SpringK - missile.rb.velocity.y * DampK);

            missile.rb.AddForce(new Vector3(0f, forceY, 0f), ForceMode.Acceleration);

            if (liveGlobalY >= targetGlobalY && missile.rb.velocity.y > 0f)
            {
                missile.rb.velocity = new Vector3(missile.rb.velocity.x, 0f, missile.rb.velocity.z);
            }

            Vector3 tiltAxis = Vector3.Cross(missile.transform.up, Vector3.up);
            if (tiltAxis.sqrMagnitude > 0.0001f)
            {
                missile.rb.AddTorque(tiltAxis * 50f, ForceMode.Acceleration);
            }

            missile.rb.AddTorque(-missile.rb.angularVelocity * 1f, ForceMode.Acceleration);

    }
}

}
