using Godot;

namespace OperationSteelTide;

/// <summary>
/// Signature side-system for Falltide's west maintenance ring.  Restoring the
/// Undertow pumps is optional: it depressurizes the lower dry dock, clears its
/// obscuring coolant layer, and turns a hazardous-looking detour into a safer
/// high-value rotation.  The objective chain never depends on it.
/// </summary>
public partial class FreightTerminalWorld
{
    private const float OrbitalComplexUndertowInteractionRadius = 3.2f;
    private const float OrbitalComplexUndertowHoldSeconds = 3.4f;

    private Node3D? _orbitalComplexUndertowConsole;
    private Node3D? _orbitalComplexUndertowWheel;
    private Label3D? _orbitalComplexUndertowLabel;
    private OmniLight3D? _orbitalComplexUndertowLight;
    private StandardMaterial3D? _orbitalComplexUndertowScreen;
    private float _orbitalComplexUndertowProgress;
    private float _orbitalComplexUndertowDrainBlend;
    private float _orbitalComplexUndertowPressureWarningCooldown;
    private bool _orbitalComplexUndertowDrained;

    private bool OrbitalComplexUndertowDrained
        => _orbitalComplexUndertowDrained;

    private float OrbitalComplexUndertowPressureStrength
        => 1.0f - Mathf.Clamp(_orbitalComplexUndertowDrainBlend, 0.0f, 1.0f);

    private void BuildOrbitalComplexRuntimeUndertowSump(
        OrbitalComplexMapLayout layout)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || !OrbitalComplexRuntimeSceneReady
            || _orbitalComplexRuntimePresentationFxRoot is null
            || IsInstanceValid(_orbitalComplexUndertowConsole))
        {
            return;
        }

        _orbitalComplexUndertowDrained = false;
        _orbitalComplexUndertowProgress = 0.0f;
        _orbitalComplexUndertowDrainBlend = 0.0f;
        _orbitalComplexUndertowPressureWarningCooldown = 0.0f;

        // The DCC scene owns the sump, pump loops, and surrounding architecture.
        // This small control head only communicates the gameplay state and gives
        // the player a precise interaction target at the authored service bay.
        _orbitalComplexUndertowConsole = new Node3D
        {
            Name = "UndertowSumpControl",
            Position = OrbitalComplexMapDefinition.UndertowSumpCenter
                + new Vector3(0.0f, 0.18f, 0.0f),
            RotationDegrees = new Vector3(0.0f, 90.0f, 0.0f)
        };
        _orbitalComplexUndertowConsole.AddToGroup("orbital_complex_interactions");
        _orbitalComplexUndertowConsole.SetMeta(
            "falltide_interaction_id",
            "undertow_sump_purge");
        _orbitalComplexUndertowConsole.SetMeta(
            "falltide_interaction_radius",
            OrbitalComplexUndertowInteractionRadius);
        _orbitalComplexUndertowConsole.SetMeta("falltide_interaction_stage", 1);
        _orbitalComplexUndertowConsole.SetMeta("falltide_interaction_one_shot", true);
        _orbitalComplexRuntimePresentationFxRoot.AddChild(
            _orbitalComplexUndertowConsole);

        var housingMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.035f, 0.065f, 0.07f),
            Metallic = 0.82f,
            Roughness = 0.32f
        };
        var safetyMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.28f, 0.05f),
            Metallic = 0.45f,
            Roughness = 0.36f,
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.035f, 0.006f),
            EmissionEnergyMultiplier = 1.1f
        };
        _orbitalComplexUndertowConsole.AddChild(new MeshInstance3D
        {
            Name = "UndertowControlHousing",
            Position = new Vector3(0.0f, 0.78f, 0.0f),
            Mesh = new BoxMesh { Size = new Vector3(1.42f, 1.56f, 0.72f) },
            MaterialOverride = housingMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        });

        _orbitalComplexUndertowScreen = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.72f, 0.14f, 0.035f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.12f, 0.015f),
            EmissionEnergyMultiplier = 3.0f,
            Roughness = 0.18f
        };
        _orbitalComplexUndertowConsole.AddChild(new MeshInstance3D
        {
            Name = "UndertowControlScreen",
            Position = new Vector3(0.0f, 1.03f, -0.375f),
            Mesh = new BoxMesh { Size = new Vector3(0.84f, 0.48f, 0.035f) },
            MaterialOverride = _orbitalComplexUndertowScreen,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        _orbitalComplexUndertowWheel = new Node3D
        {
            Name = "UndertowManualWheel",
            Position = new Vector3(0.0f, 0.38f, -0.48f)
        };
        _orbitalComplexUndertowConsole.AddChild(_orbitalComplexUndertowWheel);
        _orbitalComplexUndertowWheel.AddChild(new MeshInstance3D
        {
            Name = "UndertowWheelHub",
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.32f,
                BottomRadius = 0.32f,
                Height = 0.10f,
                RadialSegments = 18
            },
            MaterialOverride = safetyMaterial
        });
        _orbitalComplexUndertowWheel.AddChild(new MeshInstance3D
        {
            Name = "UndertowWheelCrossbarA",
            Mesh = new BoxMesh { Size = new Vector3(0.92f, 0.10f, 0.10f) },
            MaterialOverride = safetyMaterial
        });
        _orbitalComplexUndertowWheel.AddChild(new MeshInstance3D
        {
            Name = "UndertowWheelCrossbarB",
            RotationDegrees = new Vector3(0.0f, 0.0f, 90.0f),
            Mesh = new BoxMesh { Size = new Vector3(0.92f, 0.10f, 0.10f) },
            MaterialOverride = safetyMaterial
        });

        _orbitalComplexUndertowLabel = new Label3D
        {
            Name = "UndertowSumpControlLabel",
            Position = new Vector3(0.0f, 2.15f, 0.0f),
            Text = GameLocalization.Get(
                "falltide_undertow_label",
                _languageSetting,
                "UNDERTOW SUMP  //  MANUAL PURGE"),
            FontSize = 14,
            OutlineSize = 6,
            Modulate = new Color(0.22f, 0.82f, 0.94f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 42.0f
        };
        _orbitalComplexUndertowConsole.AddChild(_orbitalComplexUndertowLabel);

        _orbitalComplexUndertowLight = new OmniLight3D
        {
            Name = "UndertowSumpStatusLight",
            Position = new Vector3(0.0f, 1.45f, 0.0f),
            LightColor = new Color(1.0f, 0.18f, 0.04f),
            LightEnergy = 0.7f,
            OmniRange = 8.0f,
            ShadowEnabled = false
        };
        _orbitalComplexUndertowConsole.AddChild(_orbitalComplexUndertowLight);

        _levelRoot.SetMeta("falltide_undertow_sump_built", true);
        _levelRoot.SetMeta("falltide_undertow_drained", false);
        _ = layout;
    }

    private bool UpdateOrbitalComplexUndertowInteraction(float delta)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || IsExtractionNetworkMatch
            || LocalPlayerCannotInteract
            || _orbitalComplexUndertowConsole is null
            || !IsInstanceValid(_orbitalComplexUndertowConsole))
        {
            _orbitalComplexUndertowProgress = 0.0f;
            return false;
        }

        var target = _orbitalComplexUndertowConsole.GlobalPosition
            + Vector3.Up * 0.78f;
        if (_player.GlobalPosition.DistanceTo(target)
            > OrbitalComplexUndertowInteractionRadius)
        {
            if (_orbitalComplexUndertowProgress > 0.0f)
            {
                _player.SetSearchPose(false);
            }
            _orbitalComplexUndertowProgress = 0.0f;
            return false;
        }

        _lootSearchTarget = null;
        if (_orbitalComplexRuntimeBuild is null
            || _orbitalComplexRuntimeBuild.PowerState.ObjectiveStage < 1)
        {
            _orbitalComplexUndertowProgress = 0.0f;
            _player.SetSearchPose(false);
            _hud.SetInteraction(
                GameLocalization.Get(
                    "falltide_undertow_offline",
                    _languageSetting,
                    "UNDERTOW PUMPS OFFLINE  //  RESTORE EMERGENCY POWER"),
                -1.0f,
                true);
            return true;
        }
        if (_orbitalComplexUndertowDrained)
        {
            _orbitalComplexUndertowProgress = 0.0f;
            _player.SetSearchPose(false);
            _hud.SetInteraction(
                GameLocalization.Get(
                    "falltide_undertow_drained",
                    _languageSetting,
                    "UNDERTOW PURGED  //  LOWER DOCK PRESSURE STABLE"),
                -1.0f,
                true);
            return true;
        }

        var holding = Input.IsActionPressed(GameInputActions.Interact)
            && !_interactReleaseRequired;
        _orbitalComplexUndertowProgress = holding
            ? Mathf.Min(
                1.0f,
                _orbitalComplexUndertowProgress
                    + delta / OrbitalComplexUndertowHoldSeconds)
            : Mathf.Max(
                0.0f,
                _orbitalComplexUndertowProgress - delta * 2.0f);
        _player.SetSearchPose(
            _orbitalComplexUndertowProgress > 0.02f,
            _orbitalComplexUndertowProgress);
        _hud.SetInteraction(
            GameLocalization.Get(
                "falltide_undertow_prompt",
                _languageSetting,
                "HOLD F  //  PURGE UNDERTOW SUMP"),
            _orbitalComplexUndertowProgress,
            true);
        if (_orbitalComplexUndertowProgress < 1.0f)
        {
            return true;
        }

        _interactReleaseRequired = true;
        _orbitalComplexUndertowProgress = 0.0f;
        _orbitalComplexUndertowDrained = true;
        _player.SetSearchPose(false);
        _levelRoot.SetMeta("falltide_undertow_drained", true);
        _levelRoot.SetMeta("falltide_drydock_pressure_stable", true);
        _threatLevel = Mathf.Max(0.0f, _threatLevel - 10.0f);
        _hud.ShowLocalizedMessage(
            "falltide_undertow_active",
            "UNDERTOW PUMPS ENGAGED  //  LOWER DOCK PURGED  //  COOLANT COVER CLEARED",
            new Color(0.18f, 0.9f, 0.84f));
        return true;
    }

    private void UpdateOrbitalComplexUndertowPresentation(float delta)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || _orbitalComplexRuntimeBuild is null)
        {
            return;
        }

        var stagePowered = _orbitalComplexRuntimeBuild.PowerState.ObjectiveStage >= 1;
        var targetBlend = _orbitalComplexUndertowDrained ? 1.0f : 0.0f;
        _orbitalComplexUndertowDrainBlend = Mathf.MoveToward(
            _orbitalComplexUndertowDrainBlend,
            targetBlend,
            Mathf.Max(0.0f, delta) * 0.42f);

        if (IsInstanceValid(_orbitalComplexUndertowWheel))
        {
            var targetRotation = _orbitalComplexUndertowDrained
                ? Mathf.Pi * 1.75f
                : 0.0f;
            var rotation = _orbitalComplexUndertowWheel.Rotation;
            rotation.Z = Mathf.Lerp(
                rotation.Z,
                targetRotation,
                Mathf.Clamp(delta * 2.6f, 0.0f, 1.0f));
            _orbitalComplexUndertowWheel.Rotation = rotation;
        }

        var pulse = 0.72f
            + 0.28f * (0.5f + 0.5f * Mathf.Sin(
                _orbitalComplexRuntimePresentationTime
                    * (_orbitalComplexUndertowDrained ? 1.4f : 4.2f)));
        if (IsInstanceValid(_orbitalComplexUndertowLight))
        {
            _orbitalComplexUndertowLight.LightColor = _orbitalComplexUndertowDrained
                ? new Color(0.08f, 1.0f, 0.64f)
                : stagePowered
                    ? new Color(1.0f, 0.18f, 0.04f)
                    : new Color(0.12f, 0.3f, 0.36f);
            _orbitalComplexUndertowLight.LightEnergy = stagePowered
                ? (_orbitalComplexUndertowDrained ? 2.8f : 1.5f) * pulse
                : 0.25f;
        }
        if (_orbitalComplexUndertowScreen is not null)
        {
            var color = _orbitalComplexUndertowDrained
                ? new Color(0.04f, 0.84f, 0.48f)
                : stagePowered
                    ? new Color(0.92f, 0.18f, 0.035f)
                    : new Color(0.05f, 0.22f, 0.26f);
            _orbitalComplexUndertowScreen.AlbedoColor = color;
            _orbitalComplexUndertowScreen.Emission = color;
            _orbitalComplexUndertowScreen.EmissionEnergyMultiplier = stagePowered
                ? 3.2f * pulse
                : 0.6f;
        }
        if (IsInstanceValid(_orbitalComplexUndertowLabel))
        {
            _orbitalComplexUndertowLabel.Text = _orbitalComplexUndertowDrained
                ? GameLocalization.Get(
                    "falltide_undertow_drained",
                    _languageSetting,
                    "UNDERTOW PURGED  //  PRESSURE STABLE")
                : stagePowered
                    ? GameLocalization.Get(
                        "falltide_undertow_label",
                        _languageSetting,
                        "UNDERTOW SUMP  //  MANUAL PURGE")
                    : GameLocalization.Get(
                        "falltide_undertow_offline",
                        _languageSetting,
                        "UNDERTOW PUMPS OFFLINE");
            _orbitalComplexUndertowLabel.Modulate = _orbitalComplexUndertowDrained
                ? new Color(0.16f, 1.0f, 0.68f)
                : stagePowered
                    ? new Color(1.0f, 0.42f, 0.12f)
                    : new Color(0.24f, 0.58f, 0.65f);
        }

        if (!stagePowered || _orbitalComplexUndertowDrained)
        {
            _orbitalComplexUndertowPressureWarningCooldown = 0.0f;
            return;
        }

        // The optional purge remains a single-player side action until it has an
        // authoritative snapshot/RPC.  Keep the pressure film and status light
        // readable in a match, but do not ask a network player to perform an
        // interaction that is intentionally locked for this session.
        if (IsExtractionNetworkMatch)
        {
            _orbitalComplexUndertowPressureWarningCooldown = 0.0f;
            return;
        }

        _orbitalComplexUndertowPressureWarningCooldown = Mathf.Max(
            0.0f,
            _orbitalComplexUndertowPressureWarningCooldown - delta);
        if (_player.GlobalPosition.Y > -24.0f)
        {
            return;
        }
        var offset = _player.GlobalPosition
            - OrbitalComplexMapDefinition.DryDockCenter;
        var horizontalDistance = new Vector2(offset.X, offset.Z).Length();
        if (horizontalDistance > 36.0f
            || _orbitalComplexUndertowPressureWarningCooldown > 0.0f)
        {
            return;
        }

        _orbitalComplexUndertowPressureWarningCooldown = 11.0f;
        _hud.ShowLocalizedMessage(
            "falltide_pressure_warning",
            "LOWER DOCK PRESSURE UNSTABLE  //  PURGE UNDERTOW SUMP TO CLEAR COOLANT COVER",
            new Color(0.16f, 0.76f, 0.92f));
    }
}
