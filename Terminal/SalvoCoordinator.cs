using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace AlteredDestination
{
    public class SalvoMemberState
    {
        public Missile missile;
        public OpticalSeekerCruiseMissile seeker;
        public Unit targetUnit;
        public int salvoIndex;
        public float phaseOffset;
        public SalvoGroup group;
        public bool totReleased;
        public bool isForwardFlareScreen;
        public int  flareScreenPairIndex;
        public int  flareScreenSide;
        public bool  flareSequenceStarted;
        public int   flaresBurst;         
        public float nextFlareBurstTime;
        public bool  jammingActive;
        public float jamPhaseEndTime;
        public float lastJamPulseTime;
    }
    public class SalvoGroup
    {
        public int id;
        public int key;
        public List<SalvoMemberState> members = new List<SalvoMemberState>();
        public float lastActivityTime;
        public void Clean()
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                var m = members[i].missile;
                if (m == null || m.disabled || !m.gameObject.activeInHierarchy)
                {
                    members.RemoveAt(i);
                }
            }
        }
        public float GetMaxDistanceTo(GlobalPosition fallbackTargetPos)
        {
            float maxDist = 0f;
            for (int i = 0; i < members.Count; i++)
            {
                var state = members[i];
                var m = state.missile;
                if (m == null || m.disabled) continue;
                GlobalPosition tPos = (state.targetUnit != null && !state.targetUnit.disabled)
                    ? state.targetUnit.GlobalPosition()
                    : fallbackTargetPos;
                float d = (tPos - m.GlobalPosition()).magnitude;
                if (d > maxDist) maxDist = d;
            }
            return maxDist;
        }
        public void RebalanceRoles()
        {
            Clean();
            int count = members.Count;
            if (count == 0) return;
            int screenerCount = Mathf.Clamp(count / 4, 0, 4);
            for (int i = 0; i < count; i++)
            {
                var state = members[i];
                state.salvoIndex  = i;
                state.phaseOffset = (float)i * 1.570796f;
                state.isForwardFlareScreen = (count >= 4) && (i % 4 == 3) && ((i / 4) < screenerCount);
                if (state.isForwardFlareScreen)
                {
                    int screenerNum          = i / 4;
                    state.flareScreenPairIndex = screenerNum / 2;
                    state.flareScreenSide      = screenerNum % 2;
                }
                else
                {
                    state.flareScreenPairIndex = 0;
                    state.flareScreenSide      = 0;
                }
            }
        }
    }
    public static class SalvoCoordinator
    {
        private static readonly ConditionalWeakTable<Missile, SalvoMemberState> missileStates =
            new ConditionalWeakTable<Missile, SalvoMemberState>();
        private static readonly Dictionary<int, SalvoGroup> activeSalvos =
            new Dictionary<int, SalvoGroup>();
        private static int nextSalvoId = 1;
        public static SalvoMemberState GetOrCreateState(Missile missile, OpticalSeekerCruiseMissile seeker, Unit target, GlobalPosition targetPos)
        {
            if (missile == null) return null;
            if (missileStates.TryGetValue(missile, out var state))
            {
                if (target != null)
                {
                    int correctKey = RedistributionSplitter.GetGroupKey(target);
                    if (state.group == null || state.group.key != correctKey)
                    {
                        MoveToGroup(state, correctKey);
                    }
                    state.targetUnit = target;
                }
                return state;
            }
            int groupKey = (target != null) ? RedistributionSplitter.GetGroupKey(target) : (int)(targetPos.x * 0.01f) ^ (int)(targetPos.z * 0.01f);
            SalvoGroup group = GetOrCreateGroup(groupKey);
            state = new SalvoMemberState
            {
                missile    = missile,
                seeker     = seeker,
                targetUnit = target,
                group      = group
            };
            group.members.Add(state);
            group.RebalanceRoles();
            missileStates.Add(missile, state);
            if (AlteredDestinationPlugin.Verbose)
            {
                AlteredDestinationPlugin.Log($"[SalvoCoordinator] Missile registered to Salvo #{group.id} (Slot {state.salvoIndex + 1}/{group.members.Count})");
            }
            return state;
        }
        private static SalvoGroup GetOrCreateGroup(int groupKey)
        {
            if (!activeSalvos.TryGetValue(groupKey, out var group) || Time.timeSinceLevelLoad - group.lastActivityTime > 45f)
            {
                group = new SalvoGroup
                {
                    id  = nextSalvoId++,
                    key = groupKey,
                    lastActivityTime = Time.timeSinceLevelLoad
                };
                activeSalvos[groupKey] = group;
            }
            else
            {
                group.lastActivityTime = Time.timeSinceLevelLoad;
            }
            return group;
        }
        private static void MoveToGroup(SalvoMemberState state, int correctKey)
        {
            SalvoGroup oldGroup = state.group;
            oldGroup?.members.Remove(state);
            SalvoGroup newGroup = GetOrCreateGroup(correctKey);
            state.group = newGroup;
            newGroup.members.Add(state);
            oldGroup?.RebalanceRoles();
            newGroup.RebalanceRoles();
        }
        public static bool TryGetState(Missile missile, out SalvoMemberState state)
        {
            return missileStates.TryGetValue(missile, out state);
        }
        public static void UpdateThrottle(Missile missile, SalvoMemberState state, GlobalPosition targetPos)
        {
            if (!AlteredDestinationPlugin.TotSpeedSyncEnabled.Value) return;
            if (missile == null || missile.rb == null || state == null) return;
            SalvoGroup group = state.group;
            if (group == null || group.members.Count < 2) return;
            float myDist     = (targetPos - missile.GlobalPosition()).magnitude;
            float releaseDist = Mathf.Max(300f, AlteredDestinationPlugin.SyncReleaseDistance.Value);
            if (myDist <= releaseDist)
            {
                if (AlteredDestinationPlugin.Verbose && !state.totReleased)
                {
                    state.totReleased = true;
                    AlteredDestinationPlugin.Log($"[TOT] {missile.unitName} released to full sprint at {Mathf.RoundToInt(myDist)}m.");
                }
                missile.SetThrottle(1.0f);
                return;
            }
            float maxDist  = group.GetMaxDistanceTo(targetPos);
            float deltaDist = Mathf.Max(0f, maxDist - myDist);
            float minThrottle = Mathf.Clamp(AlteredDestinationPlugin.MinimumSyncThrottle.Value, 0.2f, 1f);
            float throttle    = Mathf.Clamp(1.0f - deltaDist * 0.0008f, minThrottle, 1.0f);
            float blend        = Mathf.Clamp01((myDist - releaseDist) / 800f);
            float finalThrottle = Mathf.Lerp(1.0f, throttle, blend);
            missile.SetThrottle(finalThrottle);
        }
    }
}