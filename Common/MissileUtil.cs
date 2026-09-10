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
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, bool> terminalModeRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, bool>("terminalMode");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, GlobalPosition> knownPosRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<OpticalSeekerCruiseMissile, GlobalPosition> aimPosRef = AccessTools.FieldRefAccess<OpticalSeekerCruiseMissile, GlobalPosition>("aimPos");
        private static readonly AccessTools.FieldRef<OpticalSeeker, Transform> optTargetTransformRef = AccessTools.FieldRefAccess<OpticalSeeker, Transform>("targetTransform");
        private static readonly AccessTools.FieldRef<OpticalSeeker, GlobalPosition> optKnownPosRef = AccessTools.FieldRefAccess<OpticalSeeker, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<OpticalSeeker, Vector3> optKnownVelRef = AccessTools.FieldRefAccess<OpticalSeeker, Vector3>("knownVel");
        private static readonly AccessTools.FieldRef<OpticalSeeker, bool> optHasVisualRef = AccessTools.FieldRefAccess<OpticalSeeker, bool>("hasVisual");
        private static readonly AccessTools.FieldRef<OpticalSeekerBomb, Transform> bombTargetTransformRef = AccessTools.FieldRefAccess<OpticalSeekerBomb, Transform>("targetTransform");
        private static readonly AccessTools.FieldRef<OpticalSeekerBomb, GlobalPosition> bombKnownPosRef = AccessTools.FieldRefAccess<OpticalSeekerBomb, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<OpticalSeekerBomb, GlobalPosition> bombAimPosRef = AccessTools.FieldRefAccess<OpticalSeekerBomb, GlobalPosition>("aimPos");
        private static readonly AccessTools.FieldRef<OpticalSeekerBomb, bool> bombHasVisualRef = AccessTools.FieldRefAccess<OpticalSeekerBomb, bool>("hasVisual");
        private static readonly AccessTools.FieldRef<OpticalSeekerShell, Transform> shellTargetTransformRef = AccessTools.FieldRefAccess<OpticalSeekerShell, Transform>("targetTransform");
        private static readonly AccessTools.FieldRef<OpticalSeekerShell, GlobalPosition> shellKnownPosRef = AccessTools.FieldRefAccess<OpticalSeekerShell, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<OpticalSeekerShell, bool> shellHasVisualRef = AccessTools.FieldRefAccess<OpticalSeekerShell, bool>("hasVisual");
        private static readonly AccessTools.FieldRef<ARHSeeker, GlobalPosition> arhKnownPosRef = AccessTools.FieldRefAccess<ARHSeeker, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<ARHSeeker, Vector3> arhKnownVelRef = AccessTools.FieldRefAccess<ARHSeeker, Vector3>("knownVel");
        private static readonly AccessTools.FieldRef<ARHSeeker, bool> arhRadarLockEstablishedRef = AccessTools.FieldRefAccess<ARHSeeker, bool>("radarLockEstablished");
        private static readonly AccessTools.FieldRef<IRSeeker, GlobalPosition> irKnownPosRef = AccessTools.FieldRefAccess<IRSeeker, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<IRSeeker, Vector3> irKnownVelRef = AccessTools.FieldRefAccess<IRSeeker, Vector3>("knownVel");
        private static readonly AccessTools.FieldRef<IRSeeker, IRSource> irTargetRef = AccessTools.FieldRefAccess<IRSeeker, IRSource>("IRTarget");
        private static readonly AccessTools.FieldRef<IRSeeker, bool> irTargetOnLaunchRef = AccessTools.FieldRefAccess<IRSeeker, bool>("targetOnLaunch");
        private static readonly AccessTools.FieldRef<SARHSeeker, GlobalPosition> sarhKnownPosRef = AccessTools.FieldRefAccess<SARHSeeker, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<SARHSeeker, Vector3> sarhKnownVelRef = AccessTools.FieldRefAccess<SARHSeeker, Vector3>("knownVel");
        private static readonly AccessTools.FieldRef<SARHSeeker, Transform> sarhTargetTransformRef = AccessTools.FieldRefAccess<SARHSeeker, Transform>("targetTransform");
        private static readonly AccessTools.FieldRef<ARMSeeker, GlobalPosition> armKnownPosRef = AccessTools.FieldRefAccess<ARMSeeker, GlobalPosition>("knownPos");
        private static readonly AccessTools.FieldRef<ARMSeeker, Vector3> armKnownVelRef = AccessTools.FieldRefAccess<ARMSeeker, Vector3>("knownVel");
        private static readonly AccessTools.FieldRef<ARMSeeker, Radar> armTargetedRadarRef = AccessTools.FieldRefAccess<ARMSeeker, Radar>("targetedRadar");
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
            missile.SetTarget(newTarget);
            missileTargetRef(missile) = newTarget;
            idRef(missile) = newTarget.persistentID;
            MissileSeeker seeker = seekerRef(missile);
            if (seeker == null) return;
            seekerTargetRef(seeker) = newTarget;
            GlobalPosition tPos = newTarget.GlobalPosition();
            Vector3 tVel = newTarget.rb != null ? newTarget.rb.velocity : Vector3.zero;
            if (seeker.proximityFuse)
            {
                missile.SetProxyFuse(newTarget.transform, newTarget.rb);
            }
            if (seeker is OpticalSeekerCruiseMissile cSeeker)
            {
                targetPartRef(cSeeker) = null;
                terminalModeRef(cSeeker) = false;
                knownPosRef(cSeeker) = tPos;
                aimPosRef(cSeeker) = tPos;
                missile.SetAimpoint(tPos, Vector3.zero);
            }
            else if (seeker is OpticalSeeker optSeeker)
            {
                optTargetTransformRef(optSeeker) = (newTarget.maxRadius > 20f ? newTarget.GetRandomPart() : newTarget.transform);
                optKnownPosRef(optSeeker) = tPos;
                optKnownVelRef(optSeeker) = tVel;
                optHasVisualRef(optSeeker) = true;
                missile.SetAimpoint(tPos, tVel);
            }
            else if (seeker is OpticalSeekerBomb bombSeeker)
            {
                bombTargetTransformRef(bombSeeker) = (newTarget.maxRadius > 20f ? newTarget.GetRandomPart() : newTarget.transform);
                bombKnownPosRef(bombSeeker) = tPos;
                bombAimPosRef(bombSeeker) = tPos;
                bombHasVisualRef(bombSeeker) = true;
                missile.SetAimpoint(tPos, tVel);
            }
            else if (seeker is OpticalSeekerShell shellSeeker)
            {
                shellTargetTransformRef(shellSeeker) = (newTarget is Ship ? newTarget.GetRandomPart() : newTarget.transform);
                shellKnownPosRef(shellSeeker) = tPos;
                shellHasVisualRef(shellSeeker) = true;
                missile.SetAimpoint(tPos, tVel);
            }
            else if (seeker is ARHSeeker arhSeeker)
            {
                arhKnownPosRef(arhSeeker) = tPos;
                arhKnownVelRef(arhSeeker) = tVel;
                arhRadarLockEstablishedRef(arhSeeker) = false;
                missile.SetAimpoint(tPos, tVel);
            }
            else if (seeker is IRSeeker irSeeker)
            {
                irKnownPosRef(irSeeker) = tPos;
                irKnownVelRef(irSeeker) = tVel;
                irTargetRef(irSeeker) = newTarget.GetIRSource();
                irTargetOnLaunchRef(irSeeker) = true;
                missile.SetAimpoint(tPos, tVel);
            }
            else if (seeker is SARHSeeker sarhSeeker)
            {
                sarhTargetTransformRef(sarhSeeker) = newTarget.transform;
                sarhKnownPosRef(sarhSeeker) = tPos;
                sarhKnownVelRef(sarhSeeker) = tVel;
                missile.SetAimpoint(tPos, tVel);
            }
            else if (seeker is ARMSeeker armSeeker)
            {
                armKnownPosRef(armSeeker) = tPos;
                armKnownVelRef(armSeeker) = tVel;
                armTargetedRadarRef(armSeeker) = newTarget.radar as Radar;
                missile.SetAimpoint(tPos, tVel);
            }
            else
            {
                missile.SetAimpoint(tPos, tVel);
            }
        }
    }
}