using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Map-specific environmental dressing and the optional telemetry console.
/// The scene's hero meshes stay authored; these nodes are lightweight reactive
/// effects that make the power stages readable during a live round.
/// </summary>
public partial class FreightTerminalWorld
{
    private Node3D? _orbitalComplexRuntimePresentationFxRoot;
    private MeshInstance3D? _orbitalComplexRuntimeFloodFilm;
    private StandardMaterial3D? _orbitalComplexRuntimeFloodMaterial;
    private GpuParticles3D? _orbitalComplexRuntimeSteam;
    private OmniLight3D? _orbitalComplexRuntimePressureLight;
    private StandardMaterial3D? _orbitalComplexRuntimeTelemetryScreen;
    private Node3D? _orbitalComplexRuntimeTelemetryConsole;
    private float _orbitalComplexRuntimeTelemetryProgress;
    private bool _orbitalComplexRuntimeTelemetryUsed;

    private void RefreshOrbitalComplexLocalizedSignage()
    {
        if (_orbitalComplexRuntimePresentationFxRoot is null)
        {
            return;
        }

        foreach (var node in GetTree().GetNodesInGroup("orbital_complex_zone_sign"))
        {
            if (node is not Label3D label)
            {
                continue;
            }
            (string Key, string English) = label.Name.ToString() switch
            {
                "FALLTIDE_SIGN_INTAKE" => ("falltide_sign_intake", "INTAKE CAUSEWAY  //  SOUTH LOCK"),
                "FALLTIDE_SIGN_BREAKER" => ("falltide_sign_breaker", "BREAKER YARD  //  STORM GRID"),
                "FALLTIDE_SIGN_ARCHIVE" => ("falltide_sign_archive", "QUARANTINE ARCHIVE  //  CLEAN ROOM"),
                "FALLTIDE_SIGN_CORE" => ("falltide_sign_core", "STORMGLASS ARRAY  //  REACTOR RING"),
                "FALLTIDE_SIGN_GATE" => ("falltide_sign_gate", "NORTH TIDE GATE  //  RECOVERY RAIL"),
                _ => (string.Empty, label.Text)
            };
            if (!string.IsNullOrEmpty(Key))
            {
                label.Text = GameLocalization.Get(Key, _languageSetting, English);
            }
        }

        var layout = OrbitalComplexRuntimeLayout;
        foreach (var node in GetTree().GetNodesInGroup("orbital_complex_poi_label"))
        {
            if (node is not Label3D label
                || !int.TryParse(label.Name.ToString().Replace("FALLTIDE_SIGN_POI_", string.Empty), out var index)
                || index < 0
                || index >= layout.MinimapLandmarks.Count)
            {
                continue;
            }
            var landmark = layout.MinimapLandmarks[index];
            label.Text = GameLocalization.Get(
                landmark.LocalizationKey,
                _languageSetting,
                landmark.EnglishName);
        }
    }
    private float _orbitalComplexRuntimePressureTime;

    private void BuildOrbitalComplexRuntimePresentationFX()
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || !IsInstanceValid(_levelRoot)
            || IsInstanceValid(_orbitalComplexRuntimePresentationFxRoot))
        {
            return;
        }

        _orbitalComplexRuntimePresentationFxRoot = new Node3D
        {
            Name = "FalltideReactivePresentation"
        };
        _orbitalComplexRuntimePresentationFxRoot.AddToGroup("orbital_complex_reactive_presentation");
        _levelRoot.AddChild(_orbitalComplexRuntimePresentationFxRoot);

        BuildOrbitalComplexZoneSignage();
        BuildOrbitalComplexTelemetryConsole();
        BuildOrbitalComplexDrydockAtmospherics();
        _orbitalComplexRuntimeTelemetryProgress = 0.0f;
        _orbitalComplexRuntimeTelemetryUsed = false;
    }

    private void BuildOrbitalComplexZoneSignage()
    {
        if (_orbitalComplexRuntimePresentationFxRoot is null)
        {
            return;
        }

        var signs = new[]
        {
            ("FALLTIDE_SIGN_INTAKE", new Vector3(0, -13.8f, 63), "falltide_sign_intake", "INTAKE CAUSEWAY  //  SOUTH LOCK", new Color(0.32f, 0.82f, 1.0f)),
            ("FALLTIDE_SIGN_BREAKER", new Vector3(-100, -13.6f, -30), "falltide_sign_breaker", "BREAKER YARD  //  STORM GRID", new Color(1.0f, 0.55f, 0.18f)),
            ("FALLTIDE_SIGN_ARCHIVE", new Vector3(100, -13.6f, -30), "falltide_sign_archive", "QUARANTINE ARCHIVE  //  CLEAN ROOM", new Color(0.68f, 0.56f, 1.0f)),
            ("FALLTIDE_SIGN_CORE", new Vector3(0, -11.9f, -6), "falltide_sign_core", "STORMGLASS ARRAY  //  REACTOR RING", new Color(1.0f, 0.32f, 0.16f)),
            ("FALLTIDE_SIGN_GATE", new Vector3(0, -13.8f, -181), "falltide_sign_gate", "NORTH TIDE GATE  //  RECOVERY RAIL", new Color(0.24f, 1.0f, 0.7f))
        };
        foreach (var (name, position, key, english, color) in signs)
        {
            var sign = new Label3D
            {
                Name = name,
                Position = position,
                Text = GameLocalization.Get(key, _languageSetting, english),
                FontSize = 22,
                PixelSize = 0.012f,
                OutlineSize = 7,
                Modulate = color,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = false,
                VisibilityRangeEnd = 78.0f,
                VisibilityRangeEndMargin = 12.0f
            };
            sign.AddToGroup("orbital_complex_zone_sign");
            _orbitalComplexRuntimePresentationFxRoot.AddChild(sign);
        }

        // The original pass only named the five headline rooms. Keep every
        // authored minimap point legible in the world too, especially the
        // lower dock and the two service loops that otherwise read as blank
        // circulation space.
        var layout = OrbitalComplexRuntimeLayout;
        for (var index = 0; index < layout.MinimapLandmarks.Count; index++)
        {
            var landmark = layout.MinimapLandmarks[index];
            var landmarkLabel = new Label3D
            {
                Name = $"FALLTIDE_SIGN_POI_{index:00}",
                Position = landmark.Position + Vector3.Up * 2.65f,
                Text = GameLocalization.Get(
                    landmark.LocalizationKey,
                    _languageSetting,
                    landmark.EnglishName),
                FontSize = 18,
                PixelSize = 0.009f,
                OutlineSize = 8,
                Modulate = new Color(
                    landmark.Color.R,
                    landmark.Color.G,
                    landmark.Color.B,
                    0.94f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 108.0f,
                VisibilityRangeEndMargin = 14.0f
            };
            landmarkLabel.AddToGroup("orbital_complex_poi_label");
            _orbitalComplexRuntimePresentationFxRoot.AddChild(landmarkLabel);
        }
    }

    private void BuildOrbitalComplexTelemetryConsole()
    {
        if (_orbitalComplexRuntimePresentationFxRoot is null)
        {
            return;
        }

        _orbitalComplexRuntimeTelemetryConsole = new Node3D
        {
            Name = "FalltideTelemetryConsole",
            Position = new Vector3(0.0f, -1.72f, -88.0f)
        };
        _orbitalComplexRuntimeTelemetryConsole.AddToGroup("orbital_complex_telemetry_console");
        _orbitalComplexRuntimeTelemetryConsole.SetMeta("falltide_interaction", "telemetry_sync");
        _orbitalComplexRuntimePresentationFxRoot.AddChild(_orbitalComplexRuntimeTelemetryConsole);

        var bodyMaterial = Mat(
            "falltide_console_body",
            new Color(0.035f, 0.07f, 0.08f),
            metallic: 0.74f,
            roughness: 0.28f);
        _orbitalComplexRuntimeTelemetryConsole.AddChild(new MeshInstance3D
        {
            Name = "TelemetryConsoleHousing",
            Mesh = new BoxMesh { Size = new Vector3(1.25f, 1.25f, 0.62f) },
            Position = new Vector3(0.0f, 0.62f, 0.0f),
            MaterialOverride = bodyMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        });

        _orbitalComplexRuntimeTelemetryScreen = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.02f, 0.52f, 0.64f),
            EmissionEnabled = true,
            Emission = new Color(0.02f, 0.72f, 1.0f),
            EmissionEnergyMultiplier = 3.8f,
            Metallic = 0.12f,
            Roughness = 0.22f
        };
        _orbitalComplexRuntimeTelemetryConsole.AddChild(new MeshInstance3D
        {
            Name = "TelemetryConsoleScreen",
            Mesh = new BoxMesh { Size = new Vector3(0.82f, 0.48f, 0.035f) },
            Position = new Vector3(0.0f, 0.86f, -0.325f),
            RotationDegrees = new Vector3(-8.0f, 0.0f, 0.0f),
            MaterialOverride = _orbitalComplexRuntimeTelemetryScreen,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        _orbitalComplexRuntimeTelemetryConsole.AddChild(new Label3D
        {
            Name = "TelemetryConsoleLabel",
            Position = new Vector3(0.0f, 1.62f, 0.0f),
            Text = GameLocalization.Get(
                "falltide_telemetry_console_label",
                _languageSetting,
                "TELEMETRY SYNC  //  HOLD F"),
            FontSize = 13,
            OutlineSize = 5,
            Modulate = new Color(0.28f, 0.9f, 1.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 22.0f
        });
    }

    private void BuildOrbitalComplexDrydockAtmospherics()
    {
        if (_orbitalComplexRuntimePresentationFxRoot is null)
        {
            return;
        }

        var filmMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.02f, 0.4f, 0.48f, 0.18f),
            EmissionEnabled = true,
            Emission = new Color(0.02f, 0.5f, 0.62f),
            EmissionEnergyMultiplier = 1.6f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _orbitalComplexRuntimeFloodMaterial = filmMaterial;
        _orbitalComplexRuntimeFloodFilm = new MeshInstance3D
        {
            Name = "DrydockPressureFilm",
            Position = new Vector3(0.0f, -32.05f, -34.0f),
            Mesh = new PlaneMesh { Size = new Vector2(47.0f, 31.0f) },
            MaterialOverride = filmMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        _orbitalComplexRuntimeFloodFilm.AddToGroup("orbital_complex_pressure_effect");
        _orbitalComplexRuntimePresentationFxRoot.AddChild(_orbitalComplexRuntimeFloodFilm);

        _orbitalComplexRuntimePressureLight = new OmniLight3D
        {
            Name = "DrydockPressurePulseLight",
            Position = new Vector3(0.0f, -29.6f, -34.0f),
            LightColor = new Color(0.06f, 0.58f, 0.78f),
            LightEnergy = 0.0f,
            OmniRange = 38.0f,
            ShadowEnabled = false
        };
        _orbitalComplexRuntimePresentationFxRoot.AddChild(_orbitalComplexRuntimePressureLight);

        var steamMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            AlbedoColor = new Color(0.5f, 0.84f, 0.9f, 0.16f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.7f, 0.86f),
            EmissionEnergyMultiplier = 1.2f
        };
        _orbitalComplexRuntimeSteam = new GpuParticles3D
        {
            Name = "DrydockCoolantSteam",
            Amount = 42,
            Lifetime = 4.8,
            VisibilityAabb = new Aabb(new Vector3(-26.0f, -1.0f, -18.0f), new Vector3(52.0f, 15.0f, 36.0f)),
            Position = new Vector3(0.0f, -31.6f, -34.0f),
            Emitting = false,
            DrawPass1 = new QuadMesh
            {
                Size = new Vector2(0.72f, 0.72f),
                Material = steamMaterial
            },
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(21.0f, 0.35f, 12.0f),
                Direction = Vector3.Up,
                Spread = 22.0f,
                Gravity = new Vector3(0.0f, 0.18f, 0.0f),
                InitialVelocityMin = 0.45f,
                InitialVelocityMax = 1.15f,
                ScaleMin = 0.25f,
                ScaleMax = 0.85f,
                Color = new Color(0.58f, 0.9f, 0.96f, 0.15f)
            }
        };
        _orbitalComplexRuntimeSteam.AddToGroup("orbital_complex_pressure_effect");
        _orbitalComplexRuntimePresentationFxRoot.AddChild(_orbitalComplexRuntimeSteam);

        _orbitalComplexRuntimePresentationFxRoot.AddChild(new Label3D
        {
            Name = "DrydockPressureLabel",
            Position = new Vector3(0.0f, -28.2f, -34.0f),
            Text = GameLocalization.Get(
                "falltide_pressure_zone_label",
                _languageSetting,
                "LOWER DOCK  //  PRESSURE ZONE"),
            FontSize = 12,
            OutlineSize = 5,
            Modulate = new Color(0.28f, 0.82f, 0.94f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 32.0f
        });
    }

    private bool UpdateOrbitalComplexRuntimePresentationFX(float delta)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || !OrbitalComplexRuntimeSceneReady
            || _orbitalComplexRuntimeBuild is null)
        {
            return false;
        }

        _orbitalComplexRuntimePressureTime += Mathf.Max(0.0f, delta);
        var stage = _orbitalComplexRuntimeBuild.PowerState.ObjectiveStage;
        var powered = stage >= 1;
        var full = stage >= OrbitalComplexPowerRules.MaximumObjectiveStage;
        if (_orbitalComplexRuntimeFloodFilm is not null && IsInstanceValid(_orbitalComplexRuntimeFloodFilm))
        {
            var pressureStrength = OrbitalComplexUndertowPressureStrength;
            _orbitalComplexRuntimeFloodFilm.Visible = powered && pressureStrength > 0.02f;
            var filmScale = 0.96f + Mathf.Sin(_orbitalComplexRuntimePressureTime * (full ? 1.8f : 1.15f)) * 0.035f;
            var filmFootprint = 0.72f + pressureStrength * 0.28f;
            _orbitalComplexRuntimeFloodFilm.Scale = new Vector3(
                filmScale * filmFootprint,
                1.0f,
                filmScale * filmFootprint);
            if (_orbitalComplexRuntimeFloodMaterial is not null)
            {
                var filmColor = _orbitalComplexRuntimeFloodMaterial.AlbedoColor;
                filmColor.A = 0.18f * pressureStrength;
                _orbitalComplexRuntimeFloodMaterial.AlbedoColor = filmColor;
                _orbitalComplexRuntimeFloodMaterial.EmissionEnergyMultiplier =
                    1.6f * pressureStrength;
            }
        }
        if (_orbitalComplexRuntimeSteam is not null && IsInstanceValid(_orbitalComplexRuntimeSteam))
        {
            var pressureStrength = OrbitalComplexUndertowPressureStrength;
            _orbitalComplexRuntimeSteam.Emitting = powered && pressureStrength > 0.04f;
            _orbitalComplexRuntimeSteam.Amount = Mathf.RoundToInt(
                (full ? 58.0f : 34.0f) * pressureStrength);
        }
        if (_orbitalComplexRuntimePressureLight is not null && IsInstanceValid(_orbitalComplexRuntimePressureLight))
        {
            var pressureStrength = OrbitalComplexUndertowPressureStrength;
            var pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_orbitalComplexRuntimePressureTime * (full ? 2.7f : 1.6f)));
            _orbitalComplexRuntimePressureLight.LightEnergy = powered
                ? (full ? 5.6f : 3.4f) * pulse * pressureStrength
                : 0.0f;
            _orbitalComplexRuntimePressureLight.LightColor = OrbitalComplexUndertowDrained
                ? new Color(0.08f, 0.78f, 0.62f)
                : new Color(0.06f, 0.58f, 0.78f);
        }
        return false;
    }

    private bool UpdateOrbitalComplexTelemetryInteraction(float delta)
    {
        // Telemetry changes enemy scan timers and the local threat meter.  Those
        // values are not part of the extraction snapshot, so keep this optional
        // side action single-player until it has an authoritative network RPC.
        if (IsExtractionNetworkMatch)
        {
            _orbitalComplexRuntimeTelemetryProgress = 0.0f;
            if (IsInstanceValid(_player))
            {
                _player.SetSearchPose(false);
            }
            return false;
        }
        if (_orbitalComplexRuntimeBuild is null
            || _orbitalComplexRuntimeTelemetryConsole is null
            || !IsInstanceValid(_orbitalComplexRuntimeTelemetryConsole)
            || LocalPlayerCannotInteract)
        {
            _orbitalComplexRuntimeTelemetryProgress = 0.0f;
            return false;
        }

        var distance = _player.GlobalPosition.DistanceTo(
            _orbitalComplexRuntimeTelemetryConsole.GlobalPosition + Vector3.Up * 0.65f);
        if (distance > 2.8f)
        {
            if (_orbitalComplexRuntimeTelemetryProgress > 0.0f)
            {
                _orbitalComplexRuntimeTelemetryProgress = 0.0f;
                _player.SetSearchPose(false);
            }
            return false;
        }

        _lootSearchTarget = null;
        _player.SetSearchPose(_orbitalComplexRuntimeTelemetryProgress > 0.02f, _orbitalComplexRuntimeTelemetryProgress);
        if (_orbitalComplexRuntimeBuild.PowerState.ObjectiveStage < 1)
        {
            _orbitalComplexRuntimeTelemetryProgress = 0.0f;
            _hud.SetInteraction(
                GameLocalization.Get("falltide_telemetry_blackout", _languageSetting, "TELEMETRY NODE OFFLINE  //  RESTORE POWER"),
                -1.0f,
                true);
            return true;
        }
        if (_orbitalComplexRuntimeTelemetryUsed)
        {
            _hud.SetInteraction(
                GameLocalization.Get("falltide_telemetry_synced", _languageSetting, "TELEMETRY SYNCED  //  THREATS MARKED"),
                -1.0f,
                true);
            return true;
        }

        var holding = Input.IsActionPressed(GameInputActions.Interact) && !_interactReleaseRequired;
        _orbitalComplexRuntimeTelemetryProgress = holding
            ? Mathf.Min(1.0f, _orbitalComplexRuntimeTelemetryProgress + delta / 2.6f)
            : Mathf.Max(0.0f, _orbitalComplexRuntimeTelemetryProgress - delta * 1.8f);
        _hud.SetInteraction(
            GameLocalization.Get("falltide_telemetry_sync", _languageSetting, "SYNC TELEMETRY  //  REVEAL THREATS"),
            _orbitalComplexRuntimeTelemetryProgress,
            true);
        if (_orbitalComplexRuntimeTelemetryProgress < 1.0f)
        {
            return true;
        }

        _interactReleaseRequired = true;
        _orbitalComplexRuntimeTelemetryUsed = true;
        _orbitalComplexRuntimeTelemetryProgress = 0.0f;
        _player.SetSearchPose(false);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy) && !enemy.IsDead)
            {
                enemy.SetScanned(18.0f);
            }
        }
        _threatLevel = Mathf.Max(0.0f, _threatLevel - 12.0f);
        if (_orbitalComplexRuntimeTelemetryScreen is not null)
        {
            _orbitalComplexRuntimeTelemetryScreen.AlbedoColor = new Color(0.18f, 0.86f, 0.5f);
            _orbitalComplexRuntimeTelemetryScreen.Emission = new Color(0.18f, 1.0f, 0.62f);
            _orbitalComplexRuntimeTelemetryScreen.EmissionEnergyMultiplier = 4.6f;
        }
        _hud.ShowLocalizedMessage(
            "falltide_telemetry_synced",
            "TELEMETRY SYNCED  //  THREATS MARKED  //  RESPONSE WINDOW EXTENDED",
            new Color(0.28f, 0.88f, 1.0f));
        return true;
    }
}
