using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 PlatformMagazineHome = new(0.0f, -0.20f, -0.31f);
    private static readonly Vector3 PlatformSpareMagazineHome = new(-0.30f, -0.62f, -0.18f);
    private static readonly Vector3 PlatformChargingHandleHome = new(0.075f, 0.085f, -0.05f);
    private bool _reloadStartedEmpty;

    internal bool ReloadStartedEmptyForDiagnostics => _reloadStartedEmpty;

    private bool UsesPlatformReloadPresentation()
        => EquippedWeapon.Platform != WeaponPlatform.M3A1
            && !WeaponCatalog.IsSidearm(EquippedWeapon.Platform);

    private bool UsesSidearmReloadPresentation()
        => WeaponCatalog.IsSidearm(EquippedWeapon.Platform);

    private void UpdateSidearmReloadAnimation()
        => UpdateProfiledReloadAnimation();

    private void UpdatePlatformReloadAnimation()
        => UpdateProfiledReloadAnimation();

    private void UpdateProfiledReloadAnimation()
    {
        if (!_isReloading || EquippedWeapon.Platform == WeaponPlatform.M3A1)
        {
            return;
        }

        var progress = Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
        var profile = FirstPersonReloadProfileCatalog.For(EquippedWeapon.Platform);
        var sidearm = WeaponCatalog.IsSidearm(profile.Platform);
        UpdateProfiledReloadSounds(profile, progress);
        var wellGrip = profile.MagazineHome + profile.MagazineGripOffset;
        var extractedGrip = profile.ExtractedMagazine + profile.MagazineGripOffset;
        var stowedGrip = profile.StowedMagazine + profile.MagazineGripOffset;
        var spareGrip = profile.SpareMagazineHome + profile.MagazineGripOffset;
        var readyGrip = profile.ReadyMagazine + profile.MagazineGripOffset;

        if (profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine)
        {
            UpdateInternalMagazineReloadAnimation(profile, progress);
            UpdateProfiledFallbackForearm();
            return;
        }

        _magazine.Visible = progress < profile.StowEnd;
        _magazine.Position = profile.MagazineHome;
        _magazine.Rotation = profile.MagazineRotation;
        // Hide both magazines during the pouch hand-off.  The replacement
        // becomes visible only once the support hand has actually acquired it.
        _spareMagazine.Visible = progress >= profile.AcquireEnd;
        _spareMagazine.Position = profile.SpareMagazineHome;
        _spareMagazine.Rotation = profile.StowedRotation;
        var sidearmSlideLockedOpen = sidearm
            && _reloadStartedEmpty
            && progress < profile.ActionEnd;
        var hkEmptyReload = profile.Mechanism
                == FirstPersonReloadMechanism.HkSlapMagazine
            && _reloadStartedEmpty;
        var hkLock = hkEmptyReload && progress < profile.ActionEnd
            ? SmoothSegment(
                progress,
                0.0f,
                Mathf.Max(0.001f, profile.ReachEnd * 0.78f))
            : 0.0f;
        _chargingHandle.Position = profile.ActionHome
            + (sidearmSlideLockedOpen
                ? profile.ActionTravel
                : profile.ActionTravel * hkLock);
        _supportHand.Position = profile.SupportHome;
        _supportHand.Rotation = profile.SupportRotation;

        if (progress < profile.ReachEnd)
        {
            var t = SmoothSegment(progress, 0.0f, profile.ReachEnd);
            _supportHand.Position = profile.SupportHome.Lerp(wellGrip, t);
            _supportHand.Rotation = profile.SupportRotation.Lerp(
                profile.MagazineHandRotation,
                t);
        }
        else if (progress < profile.ExtractEnd)
        {
            var t = SmoothSegment(progress, profile.ReachEnd, profile.ExtractEnd);
            ApplyMagazineExtraction(profile, t);
            _supportHand.Position = _magazine.Position + profile.MagazineGripOffset;
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.StowEnd)
        {
            var t = SmoothSegment(progress, profile.ExtractEnd, profile.StowEnd);
            _magazine.Position = profile.ExtractedMagazine.Lerp(
                profile.StowedMagazine,
                t);
            _magazine.Rotation = profile.ExtractedRotation.Lerp(
                profile.StowedRotation,
                t);
            _supportHand.Position = extractedGrip.Lerp(stowedGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.AcquireEnd)
        {
            // The removed magazine remains at its pouch endpoint while the
            // hand changes over to the spare. Moving this hidden node back to
            // the magwell creates a visible one-frame target jump.
            _magazine.Position = profile.StowedMagazine;
            _magazine.Rotation = profile.StowedRotation;
            var t = SmoothSegment(progress, profile.StowEnd, profile.AcquireEnd);
            _supportHand.Position = stowedGrip.Lerp(spareGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.InsertEnd)
        {
            var t = SmoothSegment(progress, profile.AcquireEnd, profile.InsertEnd);
            _spareMagazine.Position = profile.SpareMagazineHome.Lerp(
                profile.ReadyMagazine,
                t);
            _spareMagazine.Rotation = profile.StowedRotation.Lerp(
                profile.ExtractedRotation,
                t);
            _supportHand.Position = spareGrip.Lerp(readyGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.SeatEnd)
        {
            var t = SmoothSegment(progress, profile.InsertEnd, profile.SeatEnd);
            ApplyMagazineInsertion(profile, t);
            _supportHand.Position = _spareMagazine.Position
                + profile.MagazineGripOffset;
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.ActionEnd || sidearm)
        {
            UpdateReloadAction(profile, progress, wellGrip);
        }
        else
        {
            var usesAction = !sidearm
                && profile.UsesAction(_reloadStartedEmpty);
            var returnedDuringAction = sidearm
                || !usesAction
                || profile.Mechanism
                    == FirstPersonReloadMechanism.HkSlapMagazine;
            var fromAction = returnedDuringAction
                ? profile.SupportHome
                : profile.ActionGrip;
            var fromRotation = returnedDuringAction
                ? profile.SupportRotation
                : profile.ActionHandRotation;
            var t = SmoothSegment(progress, profile.ActionEnd, 1.0f);
            _spareMagazine.Position = profile.MagazineHome;
            _spareMagazine.Rotation = profile.MagazineRotation;
            _supportHand.Position = fromAction.Lerp(profile.SupportHome, t);
            _supportHand.Rotation = fromRotation.Lerp(profile.SupportRotation, t);
        }

        // These nodes remain invisible compatibility targets when the authored
        // skinned reload rig is active. The fallback pose is deterministic, but
        // production presentation never moves a static whole-arm mesh here.
        UpdateProfiledFallbackForearm();
    }

    private void UpdateInternalMagazineReloadAnimation(
        FirstPersonReloadProfile profile,
        float progress)
    {
        var portGrip = profile.MagazineHome + profile.MagazineGripOffset;
        var pouchGrip = profile.SpareMagazineHome + profile.MagazineGripOffset;
        var stagedGrip = profile.StowedMagazine + profile.MagazineGripOffset;
        var readyGrip = profile.ReadyMagazine + profile.MagazineGripOffset;

        // The M24 keeps its hinged floorplate/internal box in the receiver.
        // Five individually authored cartridges are the replacement loading
        // component and travel from the pouch into the open loading port.
        _magazine.Visible = true;
        _magazine.Position = profile.MagazineHome;
        _magazine.Rotation = profile.MagazineRotation;
        _spareMagazine.Visible = progress >= profile.ReachEnd
            && progress < profile.SeatEnd;
        _spareMagazine.Position = profile.SpareMagazineHome;
        _spareMagazine.Rotation = profile.StowedRotation;
        _chargingHandle.Position = profile.ActionHome;
        _supportHand.Position = profile.SupportHome;
        _supportHand.Rotation = profile.SupportRotation;

        if (progress < profile.ReachEnd)
        {
            var t = SmoothSegment(progress, 0.0f, profile.ReachEnd);
            _supportHand.Position = profile.SupportHome.Lerp(pouchGrip, t);
            _supportHand.Rotation = profile.SupportRotation.Lerp(
                profile.MagazineHandRotation,
                t);
        }
        else if (progress < profile.ExtractEnd)
        {
            var t = SmoothSegment(progress, profile.ReachEnd, profile.ExtractEnd);
            _spareMagazine.Position = profile.SpareMagazineHome.Lerp(
                profile.StowedMagazine,
                t);
            _supportHand.Position = pouchGrip.Lerp(stagedGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.StowEnd)
        {
            var t = SmoothSegment(progress, profile.ExtractEnd, profile.StowEnd);
            _spareMagazine.Position = profile.StowedMagazine.Lerp(
                profile.ReadyMagazine,
                t);
            _spareMagazine.Rotation = profile.StowedRotation.Lerp(
                profile.ExtractedRotation,
                t);
            _supportHand.Position = stagedGrip.Lerp(readyGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.AcquireEnd)
        {
            _spareMagazine.Position = profile.ReadyMagazine;
            _spareMagazine.Rotation = profile.ExtractedRotation;
            _supportHand.Position = readyGrip;
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.InsertEnd)
        {
            var t = SmoothSegment(progress, profile.AcquireEnd, profile.InsertEnd);
            _spareMagazine.Position = profile.ReadyMagazine.Lerp(
                profile.MagazineHome,
                t);
            _spareMagazine.Rotation = profile.ExtractedRotation.Lerp(
                profile.MagazineRotation,
                t);
            _supportHand.Position = readyGrip.Lerp(portGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.SeatEnd)
        {
            var t = SmoothSegment(progress, profile.InsertEnd, profile.SeatEnd);
            var feedPulse = Mathf.Sin(t * Mathf.Pi * 5.0f) * (1.0f - t) * 0.012f;
            _spareMagazine.Position = profile.MagazineHome
                + Vector3.Forward * feedPulse;
            _spareMagazine.Rotation = profile.MagazineRotation;
            _supportHand.Position = _spareMagazine.Position
                + profile.MagazineGripOffset;
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.ActionEnd)
        {
            UpdateReloadAction(profile, progress, portGrip);
        }
        else
        {
            var t = SmoothSegment(progress, profile.ActionEnd, 1.0f);
            _supportHand.Position = profile.ActionGrip.Lerp(profile.SupportHome, t);
            _supportHand.Rotation = profile.ActionHandRotation.Lerp(
                profile.SupportRotation,
                t);
        }
    }

    private void UpdateProfiledFallbackForearm()
    {
        var sidearm = WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        _supportForearm.Position = _supportHand.Position
            + (sidearm
                ? new Vector3(-0.08f, -0.23f, 0.10f)
                : new Vector3(-0.09f, -0.24f, 0.10f));
        _supportForearm.Rotation = sidearm
            ? new Vector3(0.18f, 0.06f, -0.24f)
            : new Vector3(0.22f, 0.05f, -0.28f);
    }

    private void ApplyMagazineExtraction(FirstPersonReloadProfile profile, float t)
    {
        if (profile.Mechanism == FirstPersonReloadMechanism.RockAndLockMagazine)
        {
            // AK/VSS magazines must first rock their rear lug clear, then pull
            // away. A straight vertical lerp reads like an AR-style magwell.
            var rock = SmoothStep(Mathf.Clamp(t * 1.8f, 0.0f, 1.0f));
            var pull = SmoothStep(Mathf.Clamp((t - 0.42f) / 0.58f, 0.0f, 1.0f));
            _magazine.Position = profile.MagazineHome.Lerp(
                profile.ExtractedMagazine,
                pull);
            _magazine.Rotation = profile.MagazineRotation.Lerp(
                profile.ExtractedRotation,
                rock);
            return;
        }

        _magazine.Position = profile.MagazineHome.Lerp(
            profile.ExtractedMagazine,
            t);
        _magazine.Rotation = profile.MagazineRotation.Lerp(
            profile.ExtractedRotation,
            t);
    }

    private void ApplyMagazineInsertion(FirstPersonReloadProfile profile, float t)
    {
        if (profile.Mechanism == FirstPersonReloadMechanism.RockAndLockMagazine)
        {
            // Hook the front lug before rotating the magazine home. Separating
            // those two beats restores the recognisable AK/VSS lock-in motion.
            var hook = SmoothStep(Mathf.Clamp(t / 0.48f, 0.0f, 1.0f));
            var lockRotation = SmoothStep(Mathf.Clamp(
                (t - 0.35f) / 0.65f,
                0.0f,
                1.0f));
            _spareMagazine.Position = profile.ReadyMagazine.Lerp(
                profile.MagazineHome,
                hook);
            _spareMagazine.Rotation = profile.ExtractedRotation.Lerp(
                profile.MagazineRotation,
                lockRotation);
            return;
        }

        // A short final over-travel gives straight magazines a distinct seat
        // impact without borrowing the rock-and-lock rotation.
        var seat = SmoothStep(Mathf.Clamp(t / 0.82f, 0.0f, 1.0f));
        var impact = Mathf.Sin(
                Mathf.Clamp((t - 0.80f) / 0.20f, 0.0f, 1.0f) * Mathf.Pi)
            * 0.008f;
        _spareMagazine.Position = profile.ReadyMagazine.Lerp(
                profile.MagazineHome,
                seat)
            + Vector3.Down * impact;
        _spareMagazine.Rotation = profile.ExtractedRotation.Lerp(
            profile.MagazineRotation,
            seat);
    }

    private void UpdateReloadAction(
        FirstPersonReloadProfile profile,
        float progress,
        Vector3 wellGrip)
    {
        _spareMagazine.Position = profile.MagazineHome;
        _spareMagazine.Rotation = profile.MagazineRotation;
        if (WeaponCatalog.IsSidearm(profile.Platform))
        {
            var sidearmActionProgress = Mathf.InverseLerp(
                profile.SeatEnd,
                profile.ActionEnd,
                progress);
            if (!profile.UsesAction(_reloadStartedEmpty))
            {
                var retreat = SmoothSegment(
                    progress,
                    profile.SeatEnd,
                    1.0f);
                _supportHand.Position = wellGrip.Lerp(
                    profile.SupportHome,
                    retreat);
                _supportHand.Rotation = profile.MagazineHandRotation.Lerp(
                    profile.SupportRotation,
                    retreat);
                return;
            }

            // Empty pistol reloads now show the support hand making a short,
            // readable slide contact instead of cycling the mechanism under a
            // floating fist. The compact crop permits this CS-style beat
            // without exposing an upper sleeve at the camera near plane.
            var reachSlide = SidearmReloadActionReachBlend(
                profile,
                progress);
            var retreatSlide = SidearmReloadReturnBlend(
                profile,
                progress);
            var atSlide = wellGrip.Lerp(profile.ActionGrip, reachSlide);
            var atSlideRotation = profile.MagazineHandRotation.Lerp(
                profile.ActionHandRotation,
                reachSlide);
            _supportHand.Position = atSlide.Lerp(
                profile.SupportHome,
                retreatSlide);
            _supportHand.Rotation = atSlideRotation.Lerp(
                profile.SupportRotation,
                retreatSlide);
            var release = SmoothStep(Mathf.Clamp(
                (sidearmActionProgress - 0.46f) / 0.20f,
                0.0f,
                1.0f));
            _chargingHandle.Position = profile.ActionHome
                + profile.ActionTravel * (1.0f - release);
            return;
        }

        if (!profile.UsesAction(_reloadStartedEmpty))
        {
            var t = SmoothSegment(progress, profile.SeatEnd, profile.ActionEnd);
            _supportHand.Position = wellGrip.Lerp(profile.SupportHome, t);
            _supportHand.Rotation = profile.MagazineHandRotation.Lerp(
                profile.SupportRotation,
                t);
            return;
        }

        var actionProgress = Mathf.InverseLerp(profile.SeatEnd, profile.ActionEnd, progress);
        if (profile.Mechanism == FirstPersonReloadMechanism.HkSlapMagazine)
        {
            // The MP5 action is not an AR-style pull-and-return. Its handle is
            // already locked back during the exchange; the hand reaches the
            // cocking support, holds for a readable beat, then slaps it down
            // and retreats as the bolt runs home.
            var reachHandle = SmoothStep(Mathf.Clamp(
                actionProgress / 0.30f,
                0.0f,
                1.0f));
            var slapRelease = SmoothStep(Mathf.Clamp(
                (actionProgress - 0.46f) / 0.18f,
                0.0f,
                1.0f));
            var retreat = SmoothStep(Mathf.Clamp(
                (actionProgress - 0.62f) / 0.38f,
                0.0f,
                1.0f));
            var atHandle = wellGrip.Lerp(profile.ActionGrip, reachHandle);
            var atHandleRotation = profile.MagazineHandRotation.Lerp(
                profile.ActionHandRotation,
                reachHandle);
            _supportHand.Position = atHandle.Lerp(profile.SupportHome, retreat);
            _supportHand.Rotation = atHandleRotation.Lerp(
                profile.SupportRotation,
                retreat);
            _chargingHandle.Position = profile.ActionHome
                + profile.ActionTravel * (1.0f - slapRelease);
            return;
        }

        var reach = SmoothStep(Mathf.Clamp(actionProgress / 0.36f, 0.0f, 1.0f));
        var cycle = Mathf.Clamp((actionProgress - 0.34f) / 0.66f, 0.0f, 1.0f);
        var travel = Mathf.Sin(cycle * Mathf.Pi);
        _supportHand.Position = wellGrip.Lerp(profile.ActionGrip, reach);
        _supportHand.Rotation = profile.MagazineHandRotation.Lerp(
            profile.ActionHandRotation,
            reach);
        _chargingHandle.Position = profile.ActionHome + profile.ActionTravel * travel;
    }

    private void ResetProfiledReloadRig()
    {
        var profile = FirstPersonReloadProfileCatalog.For(EquippedWeapon.Platform);
        _magazine.Visible = true;
        _magazine.Position = profile.MagazineHome;
        _magazine.Rotation = profile.MagazineRotation;
        _spareMagazine.Visible = false;
        _spareMagazine.Position = profile.SpareMagazineHome;
        _spareMagazine.Rotation = profile.StowedRotation;
        _chargingHandle.Position = profile.ActionHome;
        _supportHand.Position = profile.SupportHome;
        _supportHand.Rotation = profile.SupportRotation;
        _reloadStartedEmpty = false;
    }

    private void UpdateProfiledReloadSounds(
        FirstPersonReloadProfile profile,
        float progress)
    {
        var internalMagazine = profile.Mechanism
            == FirstPersonReloadMechanism.InternalMagazine;
        PlayPlatformReloadSound(
            0,
            progress,
            profile.ReachEnd + (internalMagazine ? 0.060f : 0.080f),
            internalMagazine ? 0.96f : 0.91f);
        PlayPlatformReloadSound(
            1,
            progress,
            profile.InsertEnd + (internalMagazine ? 0.025f : 0.035f),
            internalMagazine ? 1.08f : 1.04f);

        if (!profile.UsesAction(_reloadStartedEmpty))
        {
            return;
        }

        var actionThreshold = profile.Mechanism switch
        {
            FirstPersonReloadMechanism.PistolMagazine
                => profile.SeatEnd + 0.040f,
            FirstPersonReloadMechanism.HkSlapMagazine
                => Mathf.Lerp(profile.SeatEnd, profile.ActionEnd, 0.50f),
            _ => profile.SeatEnd + 0.065f
        };
        var actionPitch = profile.Mechanism switch
        {
            FirstPersonReloadMechanism.PistolMagazine => 1.12f,
            FirstPersonReloadMechanism.HkSlapMagazine => 1.16f,
            _ => 1.10f
        };
        PlayPlatformReloadSound(2, progress, actionThreshold, actionPitch);
    }

    private void PlayPlatformReloadSound(
        int stage,
        float progress,
        float threshold,
        float pitch)
    {
        // A long render frame can cross an entire pose phase. The ordered
        // scheduler above normally advances every due milestone, while this
        // comparison also lets the newest due cue recover if an earlier stage
        // was absent from restored state instead of permanently muting reloads.
        if (_reloadSoundStage > stage || progress <= threshold)
        {
            return;
        }

        _reloadSoundStage = stage + 1;
        if (IsInstanceValid(_reloadAudio))
        {
            _reloadAudio.PitchScale = pitch;
            _reloadAudio.Play();
        }
    }

    private static float SmoothSegment(float value, float start, float end)
        => SmoothStep(Mathf.InverseLerp(start, end, value));

    private static float SidearmReloadActionPeak(
        FirstPersonReloadProfile profile)
        => Mathf.Lerp(profile.SeatEnd, profile.ActionEnd, 0.50f);

    private static float SidearmReloadActionReachBlend(
        FirstPersonReloadProfile profile,
        float progress)
        => SmoothSegment(
            progress,
            profile.InsertEnd,
            SidearmReloadActionPeak(profile));

    private static float SidearmReloadReturnBlend(
        FirstPersonReloadProfile profile,
        float progress)
        => SmoothSegment(
            progress,
            SidearmReloadActionPeak(profile),
            1.0f);
}
