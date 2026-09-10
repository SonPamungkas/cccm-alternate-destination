using UnityEngine;
namespace AlteredDestination
{
    public static class MissileJammer
    {
        public static void UpdateJamming(SalvoMemberState state, Missile missile, Unit target)
        {
            if (state == null || missile == null || target == null || target.disabled) return;
            if (!AlteredDestinationPlugin.EnableTerminalJamming.Value) return;
            if (!JamToggleRegistry.IsJamEnabled(missile)) return;
            float now = Time.timeSinceLevelLoad;
            if (state.jamPhaseEndTime <= 0f)
            {
                state.jammingActive = true;
                state.jamPhaseEndTime = now + Mathf.Max(0.1f, AlteredDestinationPlugin.JamActiveDuration.Value);
            }
            else if (now >= state.jamPhaseEndTime)
            {
                state.jammingActive = !state.jammingActive;
                float duration = state.jammingActive
                    ? AlteredDestinationPlugin.JamActiveDuration.Value
                    : AlteredDestinationPlugin.JamCooldownDuration.Value;
                state.jamPhaseEndTime = now + Mathf.Max(0.1f, duration);
                if (AlteredDestinationPlugin.Verbose)
                    AlteredDestinationPlugin.Log($"[Jam] {missile.unitName} vs {target.unitName}: {(state.jammingActive ? "jamming" : "cooldown")}.");
            }
            if (!state.jammingActive) return;
            float pulseInterval = Mathf.Max(0.05f, AlteredDestinationPlugin.JamPulseInterval.Value);
            if (now - state.lastJamPulseTime < pulseInterval) return;
            state.lastJamPulseTime = now;
            Unit jamTarget = FindIncomingArhThreat(missile) ?? target;
            jamTarget.Jam(new Unit.JamEventArgs
            {
                jamAmount = Mathf.Max(0f, AlteredDestinationPlugin.JammingIntensity.Value),
                jammingUnit = missile
            });
            if (AlteredDestinationPlugin.Verbose && jamTarget != target)
                AlteredDestinationPlugin.Log($"[Jam] {missile.unitName}: prioritizing incoming ARH threat {jamTarget.unitName} over ship target {target.unitName}.");
        }
        private static Unit FindIncomingArhThreat(Missile missile)
        {
            var allUnits = UnitRegistry.allUnits;
            for (int i = 0; i < allUnits.Count; i++)
            {
                if (!(allUnits[i] is Missile candidate) || candidate.disabled) continue;
                MissileSeeker seeker = MissileUtil.GetSeeker(candidate);
                if (!(seeker is ARHSeeker) && !(seeker is SARHSeeker)) continue;
                if (MissileUtil.GetTarget(candidate) == missile) return candidate;
            }
            return null;
        }
    }
}