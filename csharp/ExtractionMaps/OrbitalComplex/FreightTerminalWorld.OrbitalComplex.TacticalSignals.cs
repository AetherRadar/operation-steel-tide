using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Read-only route telemetry for the Falltide facility.  The active beacons turn the
/// deterministic QRF response hint into a physical cue at the corridor where a squad will
/// actually meet pressure.  They never spawn or move actors, so clients can display the same
/// state without gaining authority over mission or AI decisions.
/// </summary>
public partial class FreightTerminalWorld
{
    private sealed class OrbitalComplexTacticalSignalRuntime
    {
        public OrbitalComplexTacticalSignalRuntime(
            string id,
            string localizationKey,
            string englishLabel,
            Vector3 position,
            Color baseColor,
            Node3D root,
            Label3D label,
            OmniLight3D light)
        {
            Id = id;
            LocalizationKey = localizationKey;
            EnglishLabel = englishLabel;
            Position = position;
            BaseColor = baseColor;
            Root = root;
            Label = label;
            Light = light;
        }

        public string Id { get; }
        public string LocalizationKey { get; }
        public string EnglishLabel { get; }
        public Vector3 Position { get; }
        public Color BaseColor { get; }
        public Node3D Root { get; }
        public Label3D Label { get; }
        public OmniLight3D Light { get; }
    }

    private static readonly (string Id, string LocalizationKey, string EnglishLabel,
        Vector3 Position, Color Color)[] OrbitalComplexTacticalSignalSpecs =
    {
        (
            "west_service_vector",
            "falltide_signal_west",
            "WEST SERVICE VECTOR",
            new Vector3(-148.0f, -13.85f, -92.0f),
            new Color(0.28f, 0.72f, 1.0f)),
        (
            "east_service_vector",
            "falltide_signal_east",
            "EAST SERVICE VECTOR",
            new Vector3(148.0f, -13.85f, -92.0f),
            new Color(0.42f, 0.86f, 0.78f)),
        (
            "north_tide_vector",
            "falltide_signal_north",
            "NORTH TIDE GATE VECTOR",
            new Vector3(0.0f, -13.85f, -181.0f),
            new Color(0.26f, 0.98f, 0.72f)),
        (
            "calibration_catwalk_vector",
            "falltide_signal_catwalk",
            "CALIBRATION CATWALK",
            new Vector3(0.0f, -0.95f, -88.0f),
            new Color(0.44f, 0.72f, 1.0f)),
        (
            "stormglass_core_vector",
            "falltide_signal_core",
            "STORMGLASS CORE",
            new Vector3(0.0f, -13.75f, -34.0f),
            new Color(1.0f, 0.34f, 0.14f))
    };

    private readonly List<OrbitalComplexTacticalSignalRuntime>
        _orbitalComplexTacticalSignals = new();
    private Node3D? _orbitalComplexTacticalSignalsRoot;
    private float _orbitalComplexTacticalSignalMessageCooldown;
    private bool _orbitalComplexTacticalSignalsBuilt;

    /// <summary>Builds small light/label cues; architecture remains authored by the GLB.</summary>
    private void BuildOrbitalComplexTacticalSignals()
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || !OrbitalComplexRuntimeSceneReady
            || _orbitalComplexTacticalSignalsBuilt
            || !IsInstanceValid(_levelRoot))
        {
            return;
        }

        _orbitalComplexTacticalSignalsRoot = new Node3D
        {
            Name = "FalltideTacticalRouteSignals"
        };
        _orbitalComplexTacticalSignalsRoot.AddToGroup(
            "orbital_complex_tactical_route_signals");
        _levelRoot.AddChild(_orbitalComplexTacticalSignalsRoot);

        foreach (var spec in OrbitalComplexTacticalSignalSpecs)
        {
            var root = new Node3D
            {
                Name = $"TacticalSignal_{spec.Id}",
                Position = spec.Position
            };
            root.AddToGroup("orbital_complex_tactical_route_signal");
            root.SetMeta("falltide_signal_id", spec.Id);
            root.SetMeta("falltide_signal_localization_key", spec.LocalizationKey);
            root.SetMeta("falltide_signal_active", false);
            _orbitalComplexTacticalSignalsRoot.AddChild(root);

            var label = new Label3D
            {
                Name = "RouteSignalLabel",
                Position = Vector3.Up * 1.45f,
                Text = GameLocalization.Get(
                    spec.LocalizationKey,
                    _languageSetting,
                    spec.EnglishLabel),
                FontSize = 12,
                OutlineSize = 5,
                Modulate = new Color(spec.Color.R, spec.Color.G, spec.Color.B, 0.0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                VisibilityRangeEnd = 54.0f,
                Visible = false
            };
            root.AddChild(label);

            var light = new OmniLight3D
            {
                Name = "RouteSignalLight",
                Position = Vector3.Up * 0.72f,
                LightColor = spec.Color,
                LightEnergy = 0.0f,
                OmniRange = 8.5f,
                ShadowEnabled = false
            };
            light.SetMeta("falltide_signal_id", spec.Id);
            root.AddChild(light);

            _orbitalComplexTacticalSignals.Add(
                new OrbitalComplexTacticalSignalRuntime(
                    spec.Id,
                    spec.LocalizationKey,
                    spec.EnglishLabel,
                    spec.Position,
                    spec.Color,
                    root,
                    label,
                    light));
        }

        _orbitalComplexTacticalSignalMessageCooldown = 0.0f;
        _orbitalComplexTacticalSignalsBuilt = true;
        _levelRoot.SetMeta(
            "falltide_tactical_route_signal_count",
            _orbitalComplexTacticalSignals.Count);
    }

    /// <summary>
    /// Updates active route cues from the authoritative stage projection.  Network clients
    /// may render the cues, but only the local authority emits the radio hint and mutates no
    /// mission/AI state here.
    /// </summary>
    private void UpdateOrbitalComplexTacticalSignals(float delta)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || !_orbitalComplexTacticalSignalsBuilt
            || _orbitalComplexRuntimeBuild is not { } build
            || !OrbitalComplexRuntimeSceneReady
            || !IsInstanceValid(_levelRoot))
        {
            return;
        }

        _orbitalComplexTacticalSignalMessageCooldown = Mathf.Max(
            0.0f,
            _orbitalComplexTacticalSignalMessageCooldown - Mathf.Max(0.0f, delta));
        var stage = build.PowerState.ObjectiveStage;
        var response = build.PowerState.Presentation.ResponseHint;
        var westActive = stage >= 1
            && (response is OrbitalComplexResponseActivationHint.QrfWestApproach
                or OrbitalComplexResponseActivationHint.QrfAndBossActive);
        var eastActive = stage >= 1
            && (response is OrbitalComplexResponseActivationHint.QrfEastApproach
                or OrbitalComplexResponseActivationHint.QrfAndBossActive);
        var tideActive = stage >= 1;
        var catwalkActive = stage >= 1;
        var coreActive = stage >= OrbitalComplexPowerRules.MaximumObjectiveStage;
        _levelRoot.SetMeta("falltide_active_qrf_west", westActive);
        _levelRoot.SetMeta("falltide_active_qrf_east", eastActive);
        _levelRoot.SetMeta("falltide_active_tide_vector", tideActive);
        _levelRoot.SetMeta("falltide_active_catwalk_vector", catwalkActive);
        _levelRoot.SetMeta("falltide_active_core_vector", coreActive);

        OrbitalComplexTacticalSignalRuntime? nearestActive = null;
        var nearestDistanceSquared = float.PositiveInfinity;
        foreach (var signal in _orbitalComplexTacticalSignals)
        {
            var active = signal.Id switch
            {
                "west_service_vector" => westActive,
                "east_service_vector" => eastActive,
                "north_tide_vector" => tideActive,
                "calibration_catwalk_vector" => catwalkActive,
                "stormglass_core_vector" => coreActive,
                _ => false
            };
            if (!IsInstanceValid(signal.Root)
                || !IsInstanceValid(signal.Label)
                || !IsInstanceValid(signal.Light))
            {
                continue;
            }

            var pulse = 0.58f
                + 0.42f * (0.5f + 0.5f * Mathf.Sin(
                    _orbitalComplexRuntimePresentationTime * (active ? 4.4f : 1.3f)
                    + signal.Position.X * 0.014f));
            var dangerColor = signal.Id is "north_tide_vector"
                or "stormglass_core_vector"
                ? new Color(1.0f, 0.26f, 0.08f)
                : new Color(1.0f, 0.48f, 0.12f);
            signal.Root.SetMeta("falltide_signal_active", active);
            signal.Root.SetMeta("falltide_signal_stage", stage);
            signal.Label.Visible = active;
            signal.Label.Text = GameLocalization.Get(
                signal.LocalizationKey,
                _languageSetting,
                signal.EnglishLabel);
            signal.Label.Modulate = active
                ? new Color(dangerColor.R, dangerColor.G, dangerColor.B, 0.95f)
                : new Color(signal.BaseColor.R, signal.BaseColor.G, signal.BaseColor.B, 0.0f);
            signal.Light.LightColor = active ? dangerColor : signal.BaseColor;
            signal.Light.LightEnergy = active ? 2.2f * pulse : 0.0f;

            if (!active || !IsInstanceValid(_player))
            {
                continue;
            }
            // Resolve the authored cue in world space.  The Falltide root is currently
            // identity-transformed, but using the live node keeps proximity radio correct
            // if the map is instanced under an offset/rotated streaming origin later.
            var distanceSquared = _player.GlobalPosition.DistanceSquaredTo(
                signal.Root.GlobalPosition);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestActive = signal;
            }
        }

        // Route beacons remain visible during a downed/extraction state so a late
        // spectator can still orient themselves, but do not emit stale mission
        // radio once this local player can no longer act (or before deployment).
        if (IsExtractionNetworkClient
            || LocalPlayerCannotInteract
            || _missionPhase == "DEPLOYMENT"
            || nearestActive is null
            || nearestDistanceSquared > 22.0f * 22.0f
            || _orbitalComplexTacticalSignalMessageCooldown > 0.0f
            || !IsInstanceValid(_hud))
        {
            return;
        }

        var (messageKey, englishMessage, color) = nearestActive.Id switch
        {
            "west_service_vector" => (
                "qrf_inbound",
                "QRF VECTOR ACTIVE  //  WEST SERVICE TUNNEL",
                new Color(1.0f, 0.46f, 0.18f)),
            "east_service_vector" => (
                "qrf_inbound",
                "QRF VECTOR ACTIVE  //  EAST SERVICE TUNNEL",
                new Color(1.0f, 0.46f, 0.18f)),
            "north_tide_vector" => (
                "falltide_emergency_power",
                "TIDE GATE RESPONSE  //  NORTH RECOVERY RAIL HOT",
                new Color(0.32f, 1.0f, 0.72f)),
            "calibration_catwalk_vector" => (
                "falltide_vertical_access",
                "UPPER RING EXPOSED  //  WATCH THE CALIBRATION CATWALK",
                new Color(0.42f, 0.78f, 1.0f)),
            _ => (
                "falltide_full_power",
                "STORMGLASS RESPONSE  //  BOSS VECTOR ACTIVE",
                new Color(1.0f, 0.3f, 0.12f))
        };
        _hud.ShowLocalizedMessage(messageKey, englishMessage, color);
        _orbitalComplexTacticalSignalMessageCooldown = 10.0f;
        _levelRoot.SetMeta(
            "falltide_last_route_signal",
            nearestActive.Id);
    }
}
