using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public sealed class MissileFlareBinding : MonoBehaviour
    {
        private Unit sourceUnit;
        private IRSource source;
        internal void Bind(Unit unit, IRSource irSource)
        {
            sourceUnit = unit;
            source     = irSource;
        }
        private void OnDestroy()
        {
            if (sourceUnit != null && source != null)
                sourceUnit.RemoveIRSource(source);
            sourceUnit = null;
            source     = null;
        }
    }
    public static class FlareScreenDispenser
    {
        private static readonly FieldInfo irSourceField     = AccessTools.Field(typeof(IRFlare), "IR");
        private static readonly FieldInfo velocityField     = AccessTools.Field(typeof(IRFlare), "velocity");
        private static readonly FieldInfo aircraftField     = AccessTools.Field(typeof(IRFlare), "aircraft");
        private static readonly FieldInfo nearAircraftField = AccessTools.Field(typeof(IRFlare), "nearAircraft");
        private static readonly FieldInfo smokeField        = AccessTools.Field(typeof(IRFlare), "smokeParticles");
        private static readonly FieldInfo mainField         = AccessTools.Field(typeof(IRFlare), "main");
        private static readonly FieldInfo emitParamsField   = AccessTools.Field(typeof(IRFlare), "emitParams");
        private static readonly FieldInfo flareParticlesField = AccessTools.Field(typeof(IRFlare), "flareParticles");
        private static readonly FieldInfo flareLightField     = AccessTools.Field(typeof(IRFlare), "flareLight");
        private static readonly FieldInfo burnTimeField       = AccessTools.Field(typeof(IRFlare), "burnTime");
        private static bool loggedVisualFieldsMissing;
        private static bool loggedBurnTimeFieldMissing;
        private static bool loggedMissingFlareOwner;
        private static readonly FieldInfo flarePrefabField = AccessTools.Field(typeof(FlareEjector), "flarePrefab");
        private static GameObject cachedFlarePrefab;
        private static float      nextFlarePrefabSearchTime;
        private static bool       loggedMissingFlarePrefab;
        public static void UpdateTerminalFlares(SalvoMemberState state, Missile missile, float distance)
        {
            if (state == null || missile == null || !AlteredDestinationPlugin.EnableTerminalFlares.Value) return;
            if (!FlareToggleRegistry.IsFlareEnabled(missile)) return;
            bool isFlareScreen = state.isForwardFlareScreen;
            int budget = Mathf.Max(0, isFlareScreen
                ? AlteredDestinationPlugin.ForwardFlareScreenFlaresPerMissile.Value
                : AlteredDestinationPlugin.TerminalFlaresPerMissile.Value);
            if (budget <= 0 || state.flaresBurst >= budget) return;
            float startDistanceMeters = Mathf.Max(50f, isFlareScreen
                ? AlteredDestinationPlugin.ForwardFlareScreenStartDistance.Value
                : AlteredDestinationPlugin.TerminalFlareStartDistance.Value);
            float burstInterval = Mathf.Max(0.05f, isFlareScreen
                ? AlteredDestinationPlugin.ForwardFlareScreenBurstInterval.Value
                : AlteredDestinationPlugin.TerminalFlareBurstInterval.Value);
            if (distance > startDistanceMeters) return;
            float now = Time.timeSinceLevelLoad;
            if (!state.flareSequenceStarted)
            {
                state.flareSequenceStarted = true;
                state.nextFlareBurstTime = now;
                if (AlteredDestinationPlugin.Verbose)
                    AlteredDestinationPlugin.Log($"[FlareScreen] Missile terminal flare sequence started: {missile.unitName} at {Mathf.RoundToInt(distance)}m ({budget} flares, {Mathf.Max(1, AlteredDestinationPlugin.TerminalFlaresPerBurst.Value)} per burst every {burstInterval:0.##}s{(isFlareScreen ? ", FORWARD FLARE SCREEN)." : ").")}");
            }
            if (now + 0.0001f < state.nextFlareBurstTime) return;
            int burstSize = Mathf.Min(Mathf.Max(1, AlteredDestinationPlugin.TerminalFlaresPerBurst.Value), budget - state.flaresBurst);
            int dispensed = 0;
            for (int i = 0; i < burstSize; i++)
            {
                if (SpawnMissileFlare(state, missile, state.flaresBurst + i, burstSize)) dispensed++;
            }
            state.flaresBurst += dispensed;
            state.nextFlareBurstTime = now + burstInterval;
            if (dispensed == 0)
            {
                state.nextFlareBurstTime = now + 1f;
                AlteredDestinationPlugin.LogError($"[FlareScreen] Cruise-missile flare burst emitted 0/{burstSize}. The mod will retry in one second.");
            }
        }
        private static bool SpawnMissileFlare(SalvoMemberState state, Missile missile, int sequenceIndex, int burstCount)
        {
            if (missile == null || missile.rb == null) return false;
            float side = ((sequenceIndex & 1) == 0) ? -1f : 1f;
            if (burstCount == 1) side = ((sequenceIndex & 2) == 0) ? -1f : 1f;
            Vector3 forward = missile.transform.forward;
            Vector3 flat = forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat).normalized;
            float rearwardSpeed = Mathf.Max(0f, AlteredDestinationPlugin.TerminalFlareRearwardSpeed.Value);
            float sideSpeed     = Mathf.Max(0f, AlteredDestinationPlugin.TerminalFlareSideSpeed.Value);
            float upwardSpeed   = Mathf.Max(0f, AlteredDestinationPlugin.TerminalFlareUpwardSpeed.Value);
            Vector3 launchVelocity = missile.rb.velocity - forward * rearwardSpeed + right * (side * sideSpeed) + Vector3.up * upwardSpeed;
            float sideOffset = Mathf.Max(0f, AlteredDestinationPlugin.TerminalFlareSideOffset.Value);
            float upOffset   = Mathf.Max(0f, AlteredDestinationPlugin.TerminalFlareUpOffset.Value);
            float rearOffset = Mathf.Max(0f, AlteredDestinationPlugin.TerminalFlareRearOffset.Value);
            Vector3 spawnPosition = missile.transform.position + right * (side * sideOffset) + Vector3.up * upOffset - forward * rearOffset;
            Aircraft ownerAircraft = ResolveFlareOwner(missile);
            GameObject flarePrefab = GetFlarePrefab(ownerAircraft);
            if (flarePrefab == null) return false;
            return TrySpawnNativeMissileFlare(missile, ownerAircraft, flarePrefab, spawnPosition, launchVelocity);
        }
        private static Aircraft ResolveFlareOwner(Missile missile)
        {
            if (missile != null && missile.owner is Aircraft ownerAircraft) return ownerAircraft;
            Aircraft resolved = null;
            try
            {
                if (missile != null && missile.ownerID.TryGetUnit(out Unit unit))
                    resolved = unit as Aircraft;
            }
            catch
            {
                resolved = null;
            }
            if (resolved == null && !loggedMissingFlareOwner)
            {
                loggedMissingFlareOwner = true;
                if (AlteredDestinationPlugin.Verbose)
                    AlteredDestinationPlugin.Log("[FlareScreen] The launching aircraft is no longer available (or missile is ship-launched). Native flare initialization will be mirrored directly and the stock IR source will be registered on the cruise missile.");
            }
            return resolved;
        }
        private static bool TrySpawnNativeMissileFlare(Missile missile, Aircraft ownerAircraft, GameObject flarePrefab, Vector3 spawnPosition, Vector3 launchVelocity)
        {
            GameObject spawned = null;
            GameObject ejectionPoint = null;
            IRSource source = null;
            try
            {
                if (NetworkSceneSingleton<Spawner>.i == null)
                {
                    AlteredDestinationPlugin.LogError("[FlareScreen] The Nuclear Option local Spawner is unavailable; native flare launch skipped.");
                    return false;
                }
                spawned = NetworkSceneSingleton<Spawner>.i.SpawnLocal(flarePrefab, Datum.origin);
                if (spawned == null) return false;
                spawned.transform.position = spawnPosition;
                IRFlare flare = spawned.GetComponent<IRFlare>();
                if (flare == null) { UnityEngine.Object.Destroy(spawned); return false; }
                if (ownerAircraft != null)
                {
                    ejectionPoint = new GameObject("NO Native Flare Ejection Point");
                    ejectionPoint.transform.SetParent(Datum.origin, worldPositionStays: true);
                    ejectionPoint.transform.position = spawnPosition;
                    ejectionPoint.transform.rotation = missile.transform.rotation;
                    flare.LaunchFlare(ownerAircraft, ejectionPoint.transform, launchVelocity);
                    source = irSourceField != null ? irSourceField.GetValue(flare) as IRSource : null;
                    if (source == null) { UnityEngine.Object.Destroy(spawned); return false; }
                    ownerAircraft.RemoveIRSource(source);
                    missile.AddIRSource(source);
                    aircraftField?.SetValue(flare, null);
                    nearAircraftField?.SetValue(flare, false);
                }
                else if (!InitializeStockFlareForMissile(flare, missile, spawnPosition, launchVelocity, out source))
                {
                    UnityEngine.Object.Destroy(spawned);
                    return false;
                }
                var binding = spawned.GetComponent<MissileFlareBinding>() ?? spawned.AddComponent<MissileFlareBinding>();
                binding.Bind(missile, source);
                return true;
            }
            catch (Exception ex)
            {
                if (source != null && missile != null) missile.RemoveIRSource(source);
                if (spawned != null) UnityEngine.Object.Destroy(spawned);
                AlteredDestinationPlugin.LogError("[FlareScreen] Native aircraft flare launch failed: " + ex);
                return false;
            }
            finally
            {
                if (ejectionPoint != null) UnityEngine.Object.Destroy(ejectionPoint);
            }
        }
        private static bool InitializeStockFlareForMissile(IRFlare flare, Missile missile, Vector3 launchPos, Vector3 launchVel, out IRSource source)
        {
            source = null;
            bool fieldsOk = irSourceField != null && velocityField != null && aircraftField != null
                         && nearAircraftField != null && smokeField != null
                         && mainField != null && emitParamsField != null;
            if (!fieldsOk)
            {
                AlteredDestinationPlugin.LogError("[FlareScreen] The current Nuclear Option IRFlare fields do not match the expected stock implementation.");
                return false;
            }
            if (burnTimeField != null)
            {
                burnTimeField.SetValue(flare, Mathf.Max(0.1f, AlteredDestinationPlugin.TerminalFlareLifetime.Value));
            }
            else if (!loggedBurnTimeFieldMissing)
            {
                loggedBurnTimeFieldMissing = true;
                AlteredDestinationPlugin.LogError("[FlareScreen] IRFlare burnTime field not found — flare will use whatever lifetime the prefab happens to have serialized.");
            }
            source = new IRSource(flare.transform, 1f, flare: true);
            irSourceField.SetValue(flare, source);
            flare.transform.position = launchPos - launchVel * Time.deltaTime;
            velocityField.SetValue(flare, launchVel);
            aircraftField.SetValue(flare, null);
            nearAircraftField.SetValue(flare, false);
            missile.AddIRSource(source);
            var smoke = smokeField.GetValue(flare) as ParticleSystem;
            if (smoke == null) { missile.RemoveIRSource(source); source = null; return false; }
            var mainMod    = (ParticleSystem.MainModule)mainField.GetValue(flare);
            var emitParams = (ParticleSystem.EmitParams)emitParamsField.GetValue(flare);
            emitParams.position = flare.transform.GlobalPosition().AsVector3();
            emitParams.velocity = launchVel + mainMod.startSpeed.constant * launchVel.magnitude * 0.01f
                                * new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
            emitParamsField.SetValue(flare, emitParams);
            smoke.Emit(emitParams, 1);
            if (flareParticlesField != null && flareLightField != null)
            {
                if (flareParticlesField.GetValue(flare) is ParticleSystem flareFx && !flareFx.isPlaying)
                    flareFx.Play();
                if (flareLightField.GetValue(flare) is Light flareLight)
                    flareLight.enabled = true;
            }
            else if (!loggedVisualFieldsMissing)
            {
                loggedVisualFieldsMissing = true;
                AlteredDestinationPlugin.LogError("[FlareScreen] IRFlare flareParticles/flareLight fields not found — flare will spawn with no visible flame.");
            }
            return true;
        }
        private static GameObject GetFlarePrefab(Aircraft ownerAircraft)
        {
            if (cachedFlarePrefab != null) return cachedFlarePrefab;
            float now = Time.timeSinceLevelLoad;
            if (now < nextFlarePrefabSearchTime) return null;
            nextFlarePrefabSearchTime = now + 3f;
            if (flarePrefabField == null) return null;
            try
            {
                if (ownerAircraft != null)
                {
                    var ejectors = ownerAircraft.GetComponentsInChildren<FlareEjector>(includeInactive: true);
                    foreach (var ej in ejectors)
                    {
                        var go = flarePrefabField.GetValue(ej) as GameObject;
                        if (go != null && go.GetComponent<IRFlare>() != null)
                        {
                            cachedFlarePrefab = go;
                            loggedMissingFlarePrefab = false;
                            AlteredDestinationPlugin.Log($"[FlareScreen] Cached flare prefab '{go.name}' from owner aircraft's own FlareEjector.");
                            return cachedFlarePrefab;
                        }
                    }
                }
                var allEjectors = Resources.FindObjectsOfTypeAll<FlareEjector>();
                foreach (var ej in allEjectors)
                {
                    var go = flarePrefabField.GetValue(ej) as GameObject;
                    if (go != null && go.GetComponent<IRFlare>() != null)
                    {
                        cachedFlarePrefab = go;
                        loggedMissingFlarePrefab = false;
                        AlteredDestinationPlugin.Log($"[FlareScreen] Cached flare prefab '{go.name}' from a global FlareEjector search.");
                        return cachedFlarePrefab;
                    }
                }
                var flares = Resources.FindObjectsOfTypeAll<IRFlare>();
                foreach (var fl in flares)
                {
                    if (fl.gameObject != null)
                    {
                        cachedFlarePrefab = fl.gameObject;
                        loggedMissingFlarePrefab = false;
                        AlteredDestinationPlugin.Log($"[FlareScreen] Cached flare prefab '{fl.gameObject.name}' from a direct IRFlare search (no FlareEjector loaded).");
                        return cachedFlarePrefab;
                    }
                }
            }
            catch (Exception ex)
            {
                AlteredDestinationPlugin.LogError("[FlareScreen] Flare prefab search failed: " + ex.Message);
            }
            if (!loggedMissingFlarePrefab)
            {
                loggedMissingFlarePrefab = true;
                AlteredDestinationPlugin.LogError("[FlareScreen] No loaded FlareEjector or IRFlare prefab was found — will retry until one is available.");
            }
            return null;
        }
    }
}