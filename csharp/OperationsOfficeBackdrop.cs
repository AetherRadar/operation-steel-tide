using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum OperationsOfficeFocus
{
    Neutral,
    QuickExtraction,
    Demolition
}

[GlobalClass]
public partial class OperationsOfficeBackdrop : Node3D
{
    public const string AuthoredSetScenePath =
        "res://assets/models/operations_office/operations_office_set.glb";

    private static readonly Vector3 QuickCameraOffset = new(-0.28f, -0.04f, -0.48f);
    private static readonly Vector3 DemolitionCameraOffset = new(0.24f, 0.06f, -0.2f);
    private static readonly Vector3 CameraFramingAdjustment = new(-0.48f, -0.5f, -1.35f);
    private static readonly Vector3 LookFramingAdjustment = new(0.0f, -0.38f, 0.0f);
    private static readonly Color NeutralAccent = new(0.32f, 0.72f, 0.62f);
    private static readonly Color QuickAccent = new(0.18f, 0.94f, 0.64f);
    private static readonly Color DemolitionAccent = new(1.0f, 0.46f, 0.16f);
    private const double DecorativeOperatorIdlePhase = 0.45;

    private Node3D _authoredSet = null!;
    private Camera3D _camera = null!;
    private OmniLight3D _ambientFill = null!;
    private OmniLight3D _neutralKeyLight = null!;
    private SpotLight3D _quickLight = null!;
    private SpotLight3D _demolitionLight = null!;
    private Node3D _cameraAnchor = null!;
    private Node3D _neutralLookAnchor = null!;
    private Node3D _quickLookAnchor = null!;
    private Node3D _demolitionLookAnchor = null!;
    private Node3D? _aircraftVisual;
    private Node3D? _leftRotor;
    private Node3D? _rightRotor;
    private OmniLight3D? _aircraftBeacon;
    private readonly List<Node3D> _operatorVisuals = new();
    private readonly List<(AnimationPlayer Player, double Phase)> _operatorAnimations = new();
    private Transform3D _leftRotorInitialTransform;
    private Transform3D _rightRotorInitialTransform;
    private Vector3 _neutralCameraPosition;
    private Vector3 _currentLookPosition;
    private Vector2 _pointerTarget;
    private Vector2 _pointerCurrent;
    private Vector2 _diagnosticPointer;
    private bool _diagnosticPointerEnabled;
    private bool _diagnosticPresentationFrozen;
    private bool _presentationActive;
    private double _presentationTime;
    private OperationsOfficeFocus _focus;

    public Camera3D Camera => _camera;
    public bool PresentationActive => _presentationActive;
    public OperationsOfficeFocus Focus => _focus;
    public int AuthoredMeshCount { get; private set; }
    public int AuthoredWindowCount { get; private set; }
    public int DecorativeOperatorCount => _operatorVisuals.Count;
    public bool UsesAuthoredAircraft => IsInstanceValid(_aircraftVisual);
    public bool PresentationResourcesActive
        => _ambientFill.Visible
        && _neutralKeyLight.Visible
        && _quickLight.Visible
        && _demolitionLight.Visible
        && _operatorVisuals.TrueForAll(
            visual => visual.ProcessMode == ProcessModeEnum.Inherit)
        && (!IsInstanceValid(_aircraftVisual)
            || _aircraftVisual!.ProcessMode == ProcessModeEnum.Inherit)
        && IsProcessing();
    public bool PresentationResourcesSuspended
        => !_ambientFill.Visible
        && !_neutralKeyLight.Visible
        && !_quickLight.Visible
        && !_demolitionLight.Visible
        && _operatorVisuals.TrueForAll(
            visual => visual.ProcessMode == ProcessModeEnum.Disabled)
        && (!IsInstanceValid(_aircraftVisual)
            || _aircraftVisual!.ProcessMode == ProcessModeEnum.Disabled)
        && !IsProcessing();
    public bool UsesAuthoredSet
        => IsInstanceValid(_authoredSet)
        && _authoredSet.SceneFilePath == AuthoredSetScenePath;
    public bool RequiredAnchorsReady
        => IsInstanceValid(_cameraAnchor)
        && IsInstanceValid(_neutralLookAnchor)
        && IsInstanceValid(_quickLookAnchor)
        && IsInstanceValid(_demolitionLookAnchor);
    public bool IsPresentationReady
        => UsesAuthoredSet
        && RequiredAnchorsReady
        && AuthoredMeshCount >= 24
        && AuthoredWindowCount >= 4
        && DecorativeOperatorCount == 2
        && UsesAuthoredAircraft
        && WorldEnvironmentSynchronized
        && PresentationCameraTuningReady
        && ProcessMode == ProcessModeEnum.Always;
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _authoredSet = GetNode<Node3D>("AuthoredSet");
        _camera = GetNode<Camera3D>("OperationsOfficeCamera");
        _camera.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _ambientFill = GetNode<OmniLight3D>("Lighting/AmbientFill");
        _neutralKeyLight = GetNode<OmniLight3D>("Lighting/NeutralKeyLight");
        _quickLight = GetNode<SpotLight3D>("Lighting/QuickFocusLight");
        _demolitionLight = GetNode<SpotLight3D>("Lighting/DemolitionFocusLight");

        _cameraAnchor = RequireAnchor("CameraAnchor");
        _neutralLookAnchor = RequireAnchor("NeutralLookAnchor");
        _quickLookAnchor = RequireAnchor("QuickLookAnchor");
        _demolitionLookAnchor = RequireAnchor("DemolitionLookAnchor");
        PositionLight(_neutralKeyLight, _cameraAnchor, _neutralLookAnchor);
        PositionLight(_quickLight, RequireAnchor("QuickLightAnchor"), _quickLookAnchor);
        PositionLight(_demolitionLight, RequireAnchor("DemolitionLightAnchor"), _demolitionLookAnchor);

        _neutralCameraPosition = ToLocal(_cameraAnchor.GlobalPosition) + CameraFramingAdjustment;
        _currentLookPosition = FramedLookPosition(_neutralLookAnchor);
        _camera.Position = _neutralCameraPosition;
        _camera.LookAt(ToGlobal(_currentLookPosition), Vector3.Up);
        ConfigureCameraEnvironment();

        AuthoredMeshCount = CountDescendants<MeshInstance3D>(_authoredSet);
        AuthoredWindowCount = CountNamedDescendants<MeshInstance3D>(_authoredSet, "Window");
        BuildDecorativeOperators();
        BuildDecorativeAircraft();
        SetPresentationActive(false);
    }

    public override void _Process(double delta)
    {
        if (_diagnosticPresentationFrozen)
        {
            return;
        }
        SynchronizeCameraEnvironment();
        AdvancePresentation(delta, readViewportPointer: true);
    }

    public void SetPresentationActive(bool active)
    {
        _presentationActive = active;
        SetProcess(active);
        _pointerTarget = Vector2.Zero;
        _pointerCurrent = Vector2.Zero;
        SetFocus(OperationsOfficeFocus.Neutral);
        // The authored shell remains at the extraction destination; only the
        // menu-specific lights and animated presentation actors suspend here.
        SetPresentationLightsVisible(active);
        foreach (var operatorVisual in _operatorVisuals)
        {
            operatorVisual.Visible = active;
            operatorVisual.ProcessMode = active && !_diagnosticPresentationFrozen
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }
        if (IsInstanceValid(_aircraftVisual))
        {
            _aircraftVisual!.Visible = active;
            _aircraftVisual.ProcessMode = active && !_diagnosticPresentationFrozen
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }
        if (IsInstanceValid(_aircraftBeacon))
        {
            _aircraftBeacon!.Visible = active;
        }
        if (active)
        {
            SynchronizeCameraEnvironment(force: true);
            ResetAccentLighting();
            ResetDecorativeMotion();
        }
        if (!active && IsInstanceValid(_camera))
        {
            _camera.Position = _neutralCameraPosition;
            _currentLookPosition = FramedLookPosition(_neutralLookAnchor);
            _camera.LookAt(ToGlobal(_currentLookPosition), Vector3.Up);
        }
    }

    public void SetFocus(OperationsOfficeFocus focus)
    {
        _focus = Enum.IsDefined(focus) ? focus : OperationsOfficeFocus.Neutral;
    }

    public void SetFocusFromUi(int focus)
    {
        SetFocus(Enum.IsDefined(typeof(OperationsOfficeFocus), focus)
            ? (OperationsOfficeFocus)focus
            : OperationsOfficeFocus.Neutral);
    }

    private void AdvancePresentation(double delta, bool readViewportPointer)
    {
        if (!_presentationActive || delta <= 0.0)
        {
            return;
        }

        var frameDelta = Mathf.Min((float)delta, 1.0f / 15.0f);
        _presentationTime += frameDelta;
        if (_diagnosticPointerEnabled)
        {
            _pointerTarget = _diagnosticPointer;
        }
        else if (readViewportPointer)
        {
            _pointerTarget = ReadViewportPointer();
        }

        var pointerBlend = 1.0f - Mathf.Exp(-frameDelta * 7.0f);
        _pointerCurrent = _pointerCurrent.Lerp(_pointerTarget, pointerBlend);
        var focusPosition = _focus switch
        {
            OperationsOfficeFocus.QuickExtraction => QuickCameraOffset,
            OperationsOfficeFocus.Demolition => DemolitionCameraOffset,
            _ => Vector3.Zero
        };
        var focusLook = _focus switch
        {
            OperationsOfficeFocus.QuickExtraction => FramedLookPosition(_quickLookAnchor),
            OperationsOfficeFocus.Demolition => FramedLookPosition(_demolitionLookAnchor),
            _ => FramedLookPosition(_neutralLookAnchor)
        };
        var idleDrift = new Vector2(
            Mathf.Sin((float)_presentationTime * 0.34f),
            Mathf.Cos((float)_presentationTime * 0.27f)) * 0.035f;
        var pointer = ClampPointer(_pointerCurrent + idleDrift);
        var targetCameraPosition = _neutralCameraPosition
            + focusPosition
            + new Vector3(pointer.X * 0.24f, pointer.Y * 0.1f, -Mathf.Abs(pointer.X) * 0.035f);
        var targetLookPosition = focusLook
            + new Vector3(pointer.X * 0.48f, pointer.Y * 0.22f, 0.0f);
        var cameraBlend = 1.0f - Mathf.Exp(-frameDelta * 4.8f);
        _camera.Position = _camera.Position.Lerp(targetCameraPosition, cameraBlend);
        _currentLookPosition = _currentLookPosition.Lerp(targetLookPosition, cameraBlend);
        _camera.LookAt(ToGlobal(_currentLookPosition), Vector3.Up);

        UpdateAccentLighting(frameDelta);
        UpdateDecorativeAircraft(frameDelta);
    }

    private void UpdateAccentLighting(float delta)
    {
        var pulse = 0.94f + Mathf.Sin((float)_presentationTime * 1.55f) * 0.06f;
        var quickTarget = _focus == OperationsOfficeFocus.QuickExtraction ? 4.2f : 0.9f;
        var demolitionTarget = _focus == OperationsOfficeFocus.Demolition ? 4.5f : 0.65f;
        var lightBlend = 1.0f - Mathf.Exp(-delta * 5.0f);
        _quickLight.LightEnergy = Mathf.Lerp(_quickLight.LightEnergy, quickTarget * pulse, lightBlend);
        _demolitionLight.LightEnergy = Mathf.Lerp(
            _demolitionLight.LightEnergy,
            demolitionTarget * (1.0f + (1.0f - pulse) * 0.7f),
            lightBlend);
        _quickLight.LightColor = NeutralAccent.Lerp(QuickAccent, _quickLight.LightEnergy / 4.2f);
        _demolitionLight.LightColor = NeutralAccent.Lerp(
            DemolitionAccent,
            _demolitionLight.LightEnergy / 4.5f);
        _ambientFill.LightEnergy = _ambientPresentationEnergy * (0.91f + pulse * 0.09f);
    }

    private void UpdateDecorativeAircraft(float delta)
    {
        if (!IsInstanceValid(_aircraftVisual) || !_aircraftVisual!.Visible)
        {
            return;
        }
        _leftRotor?.RotateY(delta * 4.2f);
        _rightRotor?.RotateY(-delta * 4.2f);
        if (IsInstanceValid(_aircraftBeacon))
        {
            _aircraftBeacon!.LightEnergy = 2.1f
                + (0.5f + 0.5f * Mathf.Sin((float)_presentationTime * 3.4f)) * 2.6f;
        }
    }

    private void BuildDecorativeOperators()
    {
        var specifications = new[]
        {
            (
                Visual: OperatorVisualId.Lynx,
                Anchor: "OperatorStandAnchor",
                Phase: DecorativeOperatorIdlePhase),
            (
                Visual: OperatorVisualId.Heron,
                Anchor: "OperatorDeskAnchor",
                Phase: DecorativeOperatorIdlePhase)
        };
        foreach (var specification in specifications)
        {
            try
            {
                var anchor = RequireAnchor(specification.Anchor);
                var visual = CombatModelLibrary.InstantiateOperator(
                    specification.Visual,
                    attachDefaultWeapon: false);
                anchor.AddChild(visual.Root);
                visual.Root.Position = Vector3.Zero;
                visual.Root.Rotation = Vector3.Zero;
                visual.AnimationPlayer.Play("idle");
                visual.AnimationPlayer.Seek(specification.Phase, update: true);
                visual.AnimationPlayer.Pause();
                _operatorVisuals.Add(visual.Root);
                _operatorAnimations.Add((visual.AnimationPlayer, specification.Phase));
            }
            catch (Exception error)
            {
                GD.PushWarning($"Operations office operator could not be presented: {error.Message}");
            }
        }
    }

    private void BuildDecorativeAircraft()
    {
        var rig = ExtractionAircraftVisualRig.TryInstantiate();
        if (rig is null)
        {
            return;
        }
        var anchor = RequireAnchor("AircraftAnchor");
        anchor.AddChild(rig.Root);
        rig.Root.Position = Vector3.Zero;
        rig.Root.Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f);
        _aircraftVisual = rig.Root;
        _leftRotor = rig.LeftRotor;
        _rightRotor = rig.RightRotor;
        _leftRotorInitialTransform = rig.LeftRotor.Transform;
        _rightRotorInitialTransform = rig.RightRotor.Transform;
        _aircraftBeacon = new OmniLight3D
        {
            Name = "AircraftNavigationBeacon",
            Position = new Vector3(0.0f, 1.4f, 0.2f),
            LightColor = new Color(1.0f, 0.24f, 0.12f),
            LightEnergy = 3.2f,
            OmniRange = 6.5f,
            ShadowEnabled = false
        };
        rig.Root.AddChild(_aircraftBeacon);
    }

    private void SetPresentationLightsVisible(bool visible)
    {
        _ambientFill.Visible = visible;
        _neutralKeyLight.Visible = visible;
        _quickLight.Visible = visible;
        _demolitionLight.Visible = visible;
    }

    private void ResetAccentLighting()
    {
        _ambientFill.LightEnergy = _ambientPresentationEnergy;
        _neutralKeyLight.LightEnergy = _neutralPresentationEnergy;
        _quickLight.LightEnergy = 0.9f;
        _demolitionLight.LightEnergy = 0.65f;
        _quickLight.LightColor = NeutralAccent.Lerp(QuickAccent, 0.9f / 4.2f);
        _demolitionLight.LightColor = NeutralAccent.Lerp(DemolitionAccent, 0.65f / 4.5f);
    }

    private void ResetDecorativeMotion()
    {
        foreach (var animation in _operatorAnimations)
        {
            animation.Player.Play("idle");
            animation.Player.Seek(animation.Phase, update: true);
            animation.Player.Pause();
        }
        if (IsInstanceValid(_leftRotor))
        {
            _leftRotor!.Transform = _leftRotorInitialTransform;
        }
        if (IsInstanceValid(_rightRotor))
        {
            _rightRotor!.Transform = _rightRotorInitialTransform;
        }
        if (IsInstanceValid(_aircraftBeacon))
        {
            _aircraftBeacon!.LightEnergy = 3.2f;
        }
    }

    private Vector2 ReadViewportPointer()
    {
        var viewport = GetViewport();
        var size = viewport.GetVisibleRect().Size;
        if (size.X <= 1.0f || size.Y <= 1.0f)
        {
            return Vector2.Zero;
        }
        var pointer = viewport.GetMousePosition() / size;
        return ClampPointer(new Vector2(pointer.X * 2.0f - 1.0f, 1.0f - pointer.Y * 2.0f));
    }

    private Node3D RequireAnchor(string name)
        => _authoredSet.FindChild(name, recursive: true, owned: false) as Node3D
            ?? throw new InvalidOperationException($"Operations office authored set is missing {name}.");

    private static void PositionLight(Node3D light, Node3D anchor, Node3D target)
    {
        light.GlobalPosition = anchor.GlobalPosition;
        light.LookAt(target.GlobalPosition, Vector3.Up);
    }

    private Vector3 FramedLookPosition(Node3D anchor)
        => ToLocal(anchor.GlobalPosition) + LookFramingAdjustment;

    private static Vector2 ClampPointer(Vector2 pointer)
        => new(
            Mathf.Clamp(pointer.X, -1.0f, 1.0f),
            Mathf.Clamp(pointer.Y, -1.0f, 1.0f));

    private static int CountDescendants<T>(Node node) where T : Node
    {
        var count = node is T ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            count += CountDescendants<T>(child);
        }
        return count;
    }

    private static int CountNamedDescendants<T>(Node node, string marker) where T : Node
    {
        var count = node is T && node.Name.ToString().Contains(marker, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            count += CountNamedDescendants<T>(child, marker);
        }
        return count;
    }
}
