using HarmonyLib;
using UnityEngine;
namespace AlteredDestination
{
    public static class MissileUtil
    {
        private static readonly AccessTools.FieldRef<Missile, Unit> missileTargetRef = AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<Missile, PersistentID> idRef = AccessTools.FieldRefAccess<Missile, PersistentID>("_targetID");
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> seekerRef = AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> seekerTargetRef = AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, Transform> targetPartRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, Transform>("targetPart");
        public static bool IsBoosting(Missile missile)
        {
            return missile.timeSinceSpawn < 10f || missile.boosterIsAttached;
        }
        public static MissileSeeker GetSeeker(Missile missile)
        {
            return missile == null ? null : seekerRef(missile);
        }
        public static Unit GetTarget(Missile missile)
        {
            if (missile == null) return null;
            MissileSeeker seeker = seekerRef(missile);
            return (seeker != null ? seekerTargetRef(seeker) : null) ?? missileTargetRef(missile);
        }
        public static void Retarget(Missile missile, Unit newTarget)
        {
            if (missile == null || newTarget == null) return;
            missileTargetRef(missile) = newTarget;
            idRef(missile) = newTarget.persistentID;
            MissileSeeker seeker = seekerRef(missile);
            if (seeker == null) return;
            seekerTargetRef(seeker) = newTarget;
            if (seeker is OpticalSeekerCruiseMissile cSeeker) targetPartRef(cSeeker) = null;
        }
    }
}