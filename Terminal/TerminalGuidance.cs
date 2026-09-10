using System;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace AlteredDestination
{
    public static class TerminalGuidance
    {
        private static readonly ConditionalWeakTable<Missile, object> loggedWaterlineLock = new ConditionalWeakTable<Missile, object>();
        public static void ApplyFloorGuard(Missile missile, float distanceToTarget, ref GlobalPosition aimPoint)
        {
            if (missile == null || missile.rb == null || missile.rb.isKinematic) return;
            float radarAlt = Mathf.Max(0f, missile.radarAlt);
            Vector3 vel = missile.rb.velocity;
            float lookAhead = Mathf.Clamp(AlteredDestinationPlugin.AltitudeLookAheadSeconds.Value, 0.2f, 2f); 
            float projectedAlt = radarAlt + vel.y * lookAhead;
            float emergencyFloor = AlteredDestinationPlugin.TerminalEmergencyFloor.Value;       
            float floorGuard = AlteredDestinationPlugin.TerminalFloorGuard.Value;               
            float recoveryHeight = AlteredDestinationPlugin.TerminalFloorRecoveryHeight.Value;   
            float hardCeiling = AlteredDestinationPlugin.TerminalHardCeiling.Value;             
            bool modifiedVel = false;
            if (radarAlt > hardCeiling && vel.y > 0f)
            {
                vel.y = 0f;
                modifiedVel = true;
            }
            if (projectedAlt < floorGuard && vel.y < 0f)
            {
                float guardSpan = Mathf.Max(0.5f, floorGuard - emergencyFloor);
                float factor = Mathf.Clamp01((projectedAlt - emergencyFloor) / guardSpan);
                float maxDescent = Mathf.Lerp(0.5f, 5.0f, factor);
                if (vel.y < -maxDescent)
                {
                    vel.y = -maxDescent;
                    modifiedVel = true;
                }
                float aimFloor = (float)Datum.LocalSeaY + recoveryHeight;
                if (aimPoint.y < aimFloor)
                {
                    aimPoint.y = Mathf.Lerp(aimPoint.y, aimFloor, 1f - factor);
                }
            }
            if (radarAlt <= emergencyFloor)
            {
                float climbSpeed = Mathf.Max(2.5f, AlteredDestinationPlugin.EmergencyRecoveryClimbSpeed.Value); 
                if (vel.y < climbSpeed)
                {
                    vel.y = climbSpeed;
                    modifiedVel = true;
                }
                Vector3 tiltAxis = Vector3.Cross(missile.transform.up, Vector3.up);
                if (tiltAxis.sqrMagnitude > 0.0001f)
                {
                    missile.rb.AddTorque(tiltAxis * 40f, ForceMode.Acceleration);
                }
            }
            if (modifiedVel)
            {
                missile.rb.velocity = vel;
            }
        }
        public static void ApplyFinalHeadingAlignment(Missile missile, GlobalPosition targetWaterlinePos, float distance)
        {
            if (missile == null || missile.rb == null || missile.rb.isKinematic) return;
            float commitDistance = AlteredDestinationPlugin.FinalCommitDistance.Value; 
            if (distance > commitDistance) return;
            Vector3 vel = missile.rb.velocity;
            Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
            float speed = horizVel.magnitude;
            if (speed < 1f) return;
            Vector3 toTarget = targetWaterlinePos - missile.GlobalPosition();
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;
            float progress = 1f - Mathf.Clamp01(distance / commitDistance);
            float rateDeg = Mathf.Lerp(18f, 35f, progress);
            float maxRadDelta = rateDeg * Mathf.Deg2Rad * Time.fixedDeltaTime;
            Vector3 rotatedHoriz = Vector3.RotateTowards(horizVel.normalized, toTarget.normalized, maxRadDelta, 0f) * speed;
            vel.x = rotatedHoriz.x;
            vel.z = rotatedHoriz.z;
            missile.rb.velocity = vel;
        }
        public static void ApplyTerminalEvasion(Missile missile, SalvoMemberState state, GlobalPosition targetPos, float distance, ref GlobalPosition aimPoint, TerminalMode mode = TerminalMode.Vanilla, bool isShip = false)
        {
            if (missile == null) return;
            if (mode == TerminalMode.Vanilla) return;
            float commitDistance = AlteredDestinationPlugin.FinalCommitDistance.Value; 
            Vector3 toTarget = targetPos - missile.GlobalPosition();
            toTarget.y = 0f;
            Vector3 fwd = toTarget.sqrMagnitude > 1f ? toTarget.normalized : missile.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            if (distance <= commitDistance)
            {
                aimPoint.x = targetPos.x;
                aimPoint.z = targetPos.z;
                int salvoIndex = state != null ? state.salvoIndex : 0;
                float side = (salvoIndex % 2 == 0) ? -1f : 1f;
                float lane = (salvoIndex / 2) + 1;
                float spread = side * lane * Mathf.Max(0f, AlteredDestinationPlugin.TerminalImpactSpreadMeters.Value);
                float finalLockDistance = Mathf.Min(20f, commitDistance * 0.5f);
                float spreadFade = Mathf.Clamp01((distance - finalLockDistance) / Mathf.Max(1f, commitDistance - finalLockDistance));
                Vector3 offset = right * (spread * spreadFade);
                aimPoint.x += offset.x;
                aimPoint.z += offset.z;
                return;
            }
            float convStart = Mathf.Max(commitDistance + 500f, AlteredDestinationPlugin.ConvergenceStartDistance.Value); 
            float fadeFactor = Mathf.Clamp01((distance - commitDistance) / (convStart - commitDistance));
            fadeFactor = fadeFactor * fadeFactor * (3f - 2f * fadeFactor); 
            float t = Time.timeSinceLevelLoad;
            float phase = state != null ? state.phaseOffset : 0f;
            switch (mode)
            {
                case TerminalMode.Direct:
                    aimPoint.x = targetPos.x;
                    aimPoint.z = targetPos.z;
                    if (isShip)
                    {
                        aimPoint.y = missile.GlobalPosition().y;
                        ApplyCounterPitch(missile);
                        if (AlteredDestinationPlugin.Verbose && !loggedWaterlineLock.TryGetValue(missile, out _))
                        {
                            loggedWaterlineLock.Add(missile, null);
                            AlteredDestinationPlugin.Log($"[TerminalGuidance] {missile.unitName}: waterline lock engaged (Direct mode vs. ship) at {Mathf.RoundToInt(distance)}m.");
                        }
                    }
                    break;
                case TerminalMode.Weave:
                    float weaveFreq = 2f * Mathf.PI / Mathf.Max(0.5f, AlteredDestinationPlugin.WeavePeriodSeconds.Value); 
                    float weaveAmp = AlteredDestinationPlugin.WeaveAmplitudeMeters.Value * fadeFactor;                     
                    aimPoint += right * (Mathf.Sin(t * weaveFreq + phase) * weaveAmp);
                    break;
                case TerminalMode.Corkscrew:
                    float csFreq = 2f * Mathf.PI / Mathf.Max(0.5f, AlteredDestinationPlugin.CorkscrewPeriodSeconds.Value);
                    float csLatAmp = AlteredDestinationPlugin.CorkscrewLateralAmplitude.Value * fadeFactor;
                    float csVertAmp = AlteredDestinationPlugin.CorkscrewVerticalAmplitude.Value * fadeFactor;
                    aimPoint += right * (Mathf.Cos(t * csFreq + phase) * csLatAmp);
                    aimPoint.y += Mathf.Sin(t * csFreq + phase) * csVertAmp;
                    break;
                case TerminalMode.PopUp:
                    float popUpStart = Mathf.Max(commitDistance + 300f, AlteredDestinationPlugin.PopUpStartDistance.Value);
                    if (distance > commitDistance && distance <= popUpStart)
                    {
                        float popFraction = Mathf.InverseLerp(commitDistance, popUpStart, distance);
                        float apexHeight = AlteredDestinationPlugin.PopUpApexAltitude.Value;
                        aimPoint.y += Mathf.Sin(popFraction * Mathf.PI) * apexHeight;
                    }
                    break;
            }
        }
        private static void ApplyCounterPitch(Missile missile)
        {
            if (missile == null || missile.rb == null) return;
            Vector3 vel = missile.rb.velocity;
            if (Mathf.Abs(vel.y) > 0.1f)
            {
                vel.y = 0f;
                missile.rb.velocity = vel;
            }
            Vector3 tiltAxis = Vector3.Cross(missile.transform.up, Vector3.up);
            if (tiltAxis.sqrMagnitude > 0.0001f)
            {
                missile.rb.AddTorque(tiltAxis * 50f, ForceMode.Acceleration);
            }
        }
    }
}