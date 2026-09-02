using Godot;

namespace OperationSteelTide;

/// <summary>
/// Optional high-risk interaction for the Falltide Recovery Array.  The bleed valve is
/// deliberately separate from the two mission terminals: a squad can choose to expose
/// itself for a short vault window, or ignore it and finish the clean objective route.
/// </summary>
public partial class FreightTerminalWorld
{
    private const string OrbitalComplexBleedValveGateId = "stormglass_vault";
    private const float OrbitalComplexBleedValveDurationSeconds = 30.0f;
    private const float OrbitalComplexBleedValveInteractionRadius = 3.0f;
    private const float OrbitalComplexBleedValveHoldSeconds = 2.6f;

    private Node3D? _orbitalComplexBleedValveAnchor;
    private Label3D? _orbitalComplexBleedValveLabel;
    private OmniLight3D? _orbitalComplexBleedValveLight;
    private float _orbitalComplexBleedValveInteractionProgress;
    private bool _orbitalComplexBleedValveUsed;
    private bool _orbitalComplexBleedValveExpiryWarningShown;
    private bool _orbitalComplexBleedValveExpiryMessageShown;

    /// <summary>
    /// Finds the authored control gallery and adds only gameplay-facing sign/light helpers.
    /// The room, console silhouette, and all major visible art remain owned by the GLB.
    /// </summary>
    private void BuildOrbitalComplexRuntimeBleedValve(OrbitalComplexMapLayout layout)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || !OrbitalComplexRuntimeSceneReady
            || _orbitalComplexBleedValveAnchor is not null)
        {
            return;
        }

        var anchor = FindOrbitalComplexAuthoredNode(
            _orbitalComplexRuntimeBuild?.AuthoredArtRoot,
            new[]
            {
                "ReactorControlGallery",
                "ArchiveTelemetryControl",
                "BreakerControlAnnex"
            });
        if (anchor is null)
        {
            // A gameplay-only fallback keeps the interaction contract usable if an authored
            // scene is replaced during iteration.  It has no visible geometry of its own.
            anchor = new Node3D
            {
                Name = "StormglassBleedValveFallback",
                Position = OrbitalComplexMapDefinition.StormglassArrayCenter
                    + new Vector3(0.0f, 0.0f, -50.0f)
            };
            _orbitalComplexRuntimeBuild?.GameplayRoot.AddChild(anchor);
        }

        anchor.AddToGroup("orbital_complex_interactions");
        anchor.SetMeta("falltide_interaction_id", "stormglass_bleed_valve");
        anchor.SetMeta("falltide_interaction_radius", OrbitalComplexBleedValveInteractionRadius);
        anchor.SetMeta("falltide_interaction_stage", 1);
        anchor.SetMeta("falltide_interaction_one_shot", true);
        _orbitalComplexBleedValveAnchor = anchor;

        _orbitalComplexBleedValveLabel = new Label3D
        {
            Name = "StormglassBleedValveLabel",
            Position = Vector3.Up * 3.15f,
            Text = GameLocalization.Get(
                "falltide_bleed_valve_label",
                _languageSetting,
                "STORMGLASS BLEED VALVE"),
            FontSize = 14,
            OutlineSize = 6,
            Modulate = new Color(1.0f, 0.68f, 0.24f, 0.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            VisibilityRangeEnd = 42.0f,
            Visible = false
        };
        anchor.AddChild(_orbitalComplexBleedValveLabel);

        _orbitalComplexBleedValveLight = new OmniLight3D
        {
            Name = "StormglassBleedValveLight",
            Position = Vector3.Up * 2.2f,
            LightColor = new Color(1.0f, 0.32f, 0.08f),
            LightEnergy = 0.0f,
            OmniRange = 6.5f,
            ShadowEnabled = false
        };
        _orbitalComplexBleedValveLight.SetMeta("falltide_fixture", "stormglass_bleed_valve");
        anchor.AddChild(_orbitalComplexBleedValveLight);

        // Keep the layout argument explicit at the call seam so future variants can place
        // their control panel from data without changing the interaction state machine.
        _ = layout;
    }

    /// <summary>
    /// Returns true while the optional panel owns the interaction slot.  The caller invokes
    /// this immediately before the normal objective interaction, so mission terminals keep
    /// their priority everywhere else in the facility.
    /// </summary>
    private bool UpdateOrbitalComplexBleedValveInteraction(float delta)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || IsExtractionNetworkMatch
            || _orbitalComplexBleedValveUsed
            || _objectiveStage != 1
            || _orbitalComplexBleedValveAnchor is null
            || !IsInstanceValid(_orbitalComplexBleedValveAnchor)
            || !IsInstanceValid(_player))
        {
            _orbitalComplexBleedValveInteractionProgress = 0.0f;
            return false;
        }

        if (_player.GlobalPosition.DistanceTo(_orbitalComplexBleedValveAnchor.GlobalPosition)
            > OrbitalComplexBleedValveInteractionRadius)
        {
            _orbitalComplexBleedValveInteractionProgress = 0.0f;
            return false;
        }

        _lootSearchTarget = null;
        _player.SetSearchPose(
            _orbitalComplexBleedValveInteractionProgress > 0.02f,
            _orbitalComplexBleedValveInteractionProgress);
        var action = GameLocalization.Get(
            "falltide_bleed_valve_prompt",
            _languageSetting,
            "HOLD F  //  OPEN STORMGLASS BLEED VALVE");
        var holding = Input.IsActionPressed(GameInputActions.Interact)
            && !_interactReleaseRequired;
        _orbitalComplexBleedValveInteractionProgress = holding
            ? Mathf.Min(
                1.0f,
                _orbitalComplexBleedValveInteractionProgress
                    + delta / OrbitalComplexBleedValveHoldSeconds)
            : Mathf.Max(
                0.0f,
                _orbitalComplexBleedValveInteractionProgress - delta * 2.5f);
        _hud.SetInteraction(
            action,
            _orbitalComplexBleedValveInteractionProgress,
            true);
        if (_orbitalComplexBleedValveInteractionProgress < 1.0f)
        {
            return true;
        }

        _interactReleaseRequired = true;
        _player.SetSearchPose(false);
        _orbitalComplexBleedValveInteractionProgress = 0.0f;
        ActivateOrbitalComplexBleedValve();
        return true;
    }

    private void ActivateOrbitalComplexBleedValve()
    {
        if (_orbitalComplexBleedValveUsed
            || IsExtractionNetworkMatch
            || _objectiveStage != 1
            || _orbitalComplexRuntimeBuild is not { } build
            || !build.ActivateGateOverride(
                OrbitalComplexBleedValveGateId,
                OrbitalComplexBleedValveDurationSeconds))
        {
            return;
        }

        _orbitalComplexBleedValveUsed = true;
        _orbitalComplexBleedValveExpiryWarningShown = false;
        _orbitalComplexBleedValveExpiryMessageShown = false;
        _levelRoot.SetMeta("falltide_bleed_valve_active", true);
        _levelRoot.SetMeta(
            "falltide_bleed_valve_duration",
            OrbitalComplexBleedValveDurationSeconds);

        // The shortcut is a loud decision: hostile AI enters combat immediately and the
        // threat meter is nudged toward a response wave, making the high-tier vault a real
        // risk/reward choice instead of a free objective skip.
        _missionDirector.RaiseConfirmedAlarm();
        _threatLevel = Mathf.Min((float)_reinforcementThreshold, _threatLevel + 24.0f);
        _hud.ShowLocalizedMessage(
            "falltide_bleed_valve_active",
            "STORMGLASS BLEED VALVE OPEN  //  VAULT WINDOW 30s  //  ALARM RAISED",
            new Color(1.0f, 0.46f, 0.16f));
    }

    /// <summary>Ticks the override and drives a visible warning pulse near the control room.</summary>
    private void UpdateOrbitalComplexBleedValvePresentation(float delta)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || _orbitalComplexRuntimeBuild is not { } build)
        {
            return;
        }

        // The pressure door has a real interlock: never slam it shut on an operator who
        // made the risky vault run.  The countdown pauses until the chamber is clear, then
        // resumes, preserving tension without producing an unwinnable soft-lock.
        var vaultOccupied = OrbitalComplexVaultOccupied();
        build.TickGateOverrides(vaultOccupied ? 0.0f : delta);
        var remaining = build.GateOverrideRemaining(OrbitalComplexBleedValveGateId);
        var active = remaining > 0.0f;
        _levelRoot?.SetMeta("falltide_bleed_valve_active", active);
        _levelRoot?.SetMeta("falltide_bleed_valve_remaining", remaining);

        if (IsInstanceValid(_orbitalComplexBleedValveLabel))
        {
            var stageVisible = _objectiveStage == 1 && !_orbitalComplexBleedValveUsed;
            _orbitalComplexBleedValveLabel.Visible = stageVisible || active;
            _orbitalComplexBleedValveLabel.Text = active
                ? GameLocalization.Format(
                    "falltide_bleed_valve_window",
                    _languageSetting,
                    "VAULT WINDOW  //  {0}s",
                    Mathf.CeilToInt(remaining).ToString("00"))
                : GameLocalization.Get(
                    "falltide_bleed_valve_label",
                    _languageSetting,
                    "STORMGLASS BLEED VALVE");
            _orbitalComplexBleedValveLabel.Modulate = active
                ? new Color(1.0f, 0.22f, 0.06f, 0.95f)
                : new Color(1.0f, 0.68f, 0.24f, stageVisible ? 0.92f : 0.0f);
        }

        if (IsInstanceValid(_orbitalComplexBleedValveLight))
        {
            var pulse = 0.82f + Mathf.Sin(Time.GetTicksMsec() * 0.012f) * 0.18f;
            _orbitalComplexBleedValveLight.LightEnergy = active
                ? 4.0f * pulse
                : _objectiveStage == 1 && !_orbitalComplexBleedValveUsed
                    ? 0.65f * pulse
                    : 0.0f;
            _orbitalComplexBleedValveLight.LightColor = active
                ? new Color(1.0f, 0.08f, 0.02f)
                : new Color(1.0f, 0.32f, 0.08f);
        }

        // The bleed valve briefly drives the telemetry dish into a visible emergency sweep;
        // this is additive to the normal stage-driven rotation and makes the choice readable
        // from the reactor hall before the player reaches the vault.
        if (active
            && build.PresentationNodes.TryGetValue("DishYaw", out var dishYaw)
            && IsInstanceValid(dishYaw))
        {
            dishYaw.RotateY(delta * 0.72f);
        }

        // Gate definitions carry one collision visual, while the authored vault has a
        // mirrored second leaf.  Animate that companion leaf here so the emergency opening
        // reads as a real two-panel pressure door instead of a one-sided slide.
        if (build.PresentationNodes.TryGetValue("VaultDoorRight", out var rightDoor)
            && IsInstanceValid(rightDoor))
        {
            AnimateOrbitalComplexOverrideDoor(rightDoor, delta, direction: 1.0f);
        }

        if (!active
            && _orbitalComplexBleedValveUsed
            && !_orbitalComplexBleedValveExpiryMessageShown)
        {
            _orbitalComplexBleedValveExpiryMessageShown = true;
            _levelRoot?.SetMeta("falltide_bleed_valve_expired", true);
            if (_objectiveStage < OrbitalComplexPowerRules.MaximumObjectiveStage)
            {
                _hud?.ShowLocalizedMessage(
                    "falltide_bleed_valve_expired",
                    "STORMGLASS VAULT WINDOW CLOSED  //  COMPLETE THE SECOND OBJECTIVE",
                    new Color(0.42f, 0.75f, 1.0f));
            }
        }
        else if (active && remaining <= 6.0f && !_orbitalComplexBleedValveExpiryWarningShown)
        {
            _orbitalComplexBleedValveExpiryWarningShown = true;
            _hud?.ShowLocalizedMessage(
                "falltide_bleed_valve_expiring",
                "VAULT WINDOW CLOSING  //  6 SECONDS",
                new Color(1.0f, 0.68f, 0.22f));
        }
    }

    private static void AnimateOrbitalComplexOverrideDoor(
        Node3D visual,
        float delta,
        float direction)
    {
        var fraction = visual.GetMeta("falltide_target_open_fraction", 0.0f).AsSingle();
        if (!visual.HasMeta("falltide_base_position"))
        {
            visual.SetMeta("falltide_base_position", visual.Position);
        }
        var basePosition = visual.GetMeta("falltide_base_position").AsVector3();
        var targetPosition = basePosition;
        targetPosition.X += direction * 5.5f * fraction;
        visual.Position = visual.Position.Lerp(
            targetPosition,
            Mathf.Clamp(delta * 5.0f, 0.0f, 1.0f));
    }

    private bool OrbitalComplexVaultOccupied()
    {
        var vaultCenter = OrbitalComplexMapDefinition.StormglassArrayCenter
            + new Vector3(0.0f, 0.0f, -18.0f);
        if (IsInstanceValid(_player)
            && _player.GlobalPosition.DistanceTo(vaultCenter) <= 9.0f)
        {
            return true;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate)
                && !mate.IsBodyBag
                && mate.GlobalPosition.DistanceTo(vaultCenter) <= 9.0f)
            {
                return true;
            }
        }
        return false;
    }
}
