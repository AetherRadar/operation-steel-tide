using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private readonly record struct MeleeTrailSample(Vector3 BladeBase, Vector3 BladeTip);

    private bool _knifeEquipped;
    private bool _meleeAttackActive;
    private bool _meleeAttackQueued;
    private float _knifeTime;
    private float _meleeAttackDuration;
    private float _meleeComboWindow;
    private float _meleeDrawTime;
    private int _meleeAttackIndex = -1;
    private Node3D _knifeRoot = null!;
    private AuthoredMeleeVisual _authoredMelee = null!;
    private AuthoredFirstPersonArmsVisual _meleeArms = null!;
    private AudioStreamPlayer _meleeSwingAudio = null!;
    private MeshInstance3D _meleeTrail = null!;
    private ImmediateMesh _meleeTrailMesh = null!;
    private StandardMaterial3D _meleeTrailMaterial = null!;
    private readonly List<MeleeTrailSample> _meleeTrailSamples = new();
    private readonly HashSet<ulong> _meleeHitTargets = new();
    private readonly List<Rid> _meleeHitTargetRids = new();
    private Vector3 _previousMeleeBladeBase;
    private Vector3 _previousMeleeBladeTip;
    private float _previousMeleeAttackProgress;
    private float _meleeWallObstruction;
    private int _meleeSwingSequence;
    private long _meleeSwingStartedAtMsec;
    private long _meleeSweepSampleAtMsec;
    private bool _meleeSweepPrimed;
    private bool _meleeBladeSweepResolved;
    private bool _meleeWorldImpactSpawned;
    private bool _meleeClearanceSuppressed;
    private int _meleeClearanceClearFrames;
    private int _meleeSweepRayCount;

    private KnifeSkinDefinition CurrentMeleeDefinition
        => KnifeSkinCatalog.Definition(EquippedKnifeSkinId);

    internal bool UsesAuthoredMeleeForDiagnostics
        => IsInstanceValid(_authoredMelee?.Root)
        && _authoredMelee.Root.Visible;
    internal bool MeleeHandPoseMatchesDefinitionForDiagnostics
        => IsInstanceValid(_meleeArms?.RightArm)
        && _meleeArms.RightArm.Visible
        && IsInstanceValid(_meleeArms?.LeftArm)
        && _meleeArms.LeftArm.Visible == CurrentMeleeDefinition.TwoHanded;
    internal MeleeWeaponStyle MeleeStyleForDiagnostics => CurrentMeleeDefinition.Style;
    internal int MeleeAttackIndexForDiagnostics => _meleeAttackIndex;
    internal bool MeleeAttackActiveForDiagnostics => _meleeAttackActive;
    internal int MeleeTrailSampleCountForDiagnostics => _meleeTrailSamples.Count;
    internal bool MeleeBladeSweepResolvedForDiagnostics => _meleeBladeSweepResolved;
    internal int MeleeSweepRayCountForDiagnostics => _meleeSweepRayCount;

    private void BuildKnife()
    {
        var definition = CurrentMeleeDefinition;
        var presentation = MeleeAttackCatalog.PresentationFor(definition.Style);
        _knifeRoot = new Node3D
        {
            Name = "MeleePresentation",
            Position = presentation.Rest.Position,
            Rotation = presentation.Rest.Rotation,
            Scale = Vector3.One * presentation.PresentationScale,
            Visible = false
        };
        _camera.AddChild(_knifeRoot);

        try
        {
            _authoredMelee = CombatModelLibrary.InstantiateMelee(definition);
            _knifeRoot.AddChild(_authoredMelee.Root);
            AlignAuthoredMeleeArms(definition, presentation);
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored melee presentation unavailable: {exception.Message}");
        }

        _meleeSwingAudio = new AudioStreamPlayer
        {
            Name = "MeleeSwingAudio",
            VolumeDb = -4.5f
        };
        _knifeRoot.AddChild(_meleeSwingAudio);
        BuildMeleeTrail(definition.EdgeColor);
        CancelMeleeAction();
    }

    private void AlignAuthoredMeleeArms(
        KnifeSkinDefinition definition,
        MeleePresentationProfile presentation)
    {
        _meleeArms = CombatModelLibrary.InstantiateFirstPersonPistolServiceArms();
        _knifeRoot.AddChild(_meleeArms.Root);
        _meleeArms.RightArm.Transform = Transform3D.Identity;
        _meleeArms.LeftArm.Transform = Transform3D.Identity;
        _meleeArms.RightArm.Visible = true;
        _meleeArms.LeftArm.Visible = definition.TwoHanded;

        var primaryGrip = _knifeRoot.GlobalTransform.AffineInverse()
            * _authoredMelee.GripPrimary.GlobalTransform;
        var armScale = presentation.ArmPresentationScale
            / Mathf.Max(0.0001f, presentation.PresentationScale);
        var targetFrame = new Transform3D(
            new Basis(Vector3.Up, Mathf.Pi).Scaled(Vector3.One * armScale),
            primaryGrip.Origin);
        _meleeArms.Root.Transform = targetFrame
            * _meleeArms.RightGripTransformInRoot.AffineInverse();
        if (!definition.TwoHanded)
        {
            return;
        }

        var supportGrip = _knifeRoot.GlobalTransform.AffineInverse()
            * _authoredMelee.GripSupport.GlobalTransform;
        var supportTargetInArms = _meleeArms.Root.Transform.AffineInverse()
            * supportGrip.Origin;
        var supportGripInArms = _meleeArms.MarkerTransformInRoot(
            _meleeArms.LeftGripFrame).Origin;
        _meleeArms.LeftArm.Position += supportTargetInArms - supportGripInArms;
    }

    private void BuildMeleeTrail(Color accent)
    {
        _meleeTrailMesh = new ImmediateMesh();
        _meleeTrailMaterial = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            NoDepthTest = false
        };
        _meleeTrail = new MeshInstance3D
        {
            Name = "MeleeBladeTrail",
            Mesh = _meleeTrailMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = _meleeTrailMaterial
        };
        _meleeTrail.SetMeta("accent", accent);
        _camera.AddChild(_meleeTrail);
    }

    private void BeginMeleeDraw()
    {
        CancelMeleeAction();
        _meleeDrawTime = MeleeAttackCatalog.PresentationFor(
            CurrentMeleeDefinition.Style).DrawDuration;
    }

    private void CancelMeleeAction()
    {
        _knifeTime = 0.0f;
        _meleeAttackDuration = 0.0f;
        _meleeComboWindow = 0.0f;
        _meleeDrawTime = 0.0f;
        _meleeAttackIndex = -1;
        _meleeAttackActive = false;
        _meleeAttackQueued = false;
        _meleeSweepPrimed = false;
        _meleeBladeSweepResolved = false;
        _meleeWorldImpactSpawned = false;
        _meleeClearanceSuppressed = false;
        _meleeClearanceClearFrames = 0;
        _meleeSweepRayCount = 0;
        _meleeHitTargets.Clear();
        _meleeHitTargetRids.Clear();
        ClearMeleeTrail();
    }

    private void StartKnifeAttack()
    {
        if (_meleeAttackActive)
        {
            var progress = 1.0f - _knifeTime / Mathf.Max(0.001f, _meleeAttackDuration);
            if (progress >= 0.22f)
            {
                _meleeAttackQueued = true;
            }
            return;
        }
        if (_fireCooldown > 0.0f)
        {
            return;
        }

        var style = CurrentMeleeDefinition.Style;
        var nextAttack = _meleeComboWindow > 0.0f
            ? (_meleeAttackIndex + 1) % MeleeAttackCatalog.AttackCount(style)
            : 0;
        BeginMeleeAttack(nextAttack);
    }

    private void BeginMeleeAttack(int attackIndex)
    {
        var definition = CurrentMeleeDefinition;
        var attack = MeleeAttackCatalog.AttackFor(definition.Style, attackIndex);
        _meleeSwingSequence = unchecked(_meleeSwingSequence + 1);
        if (_meleeSwingSequence <= 0)
        {
            _meleeSwingSequence = 1;
        }
        _meleeAttackIndex = attackIndex;
        _meleeSwingStartedAtMsec = (long)Time.GetTicksMsec();
        _meleeSweepSampleAtMsec = _meleeSwingStartedAtMsec;
        _meleeAttackDuration = attack.Duration;
        _knifeTime = attack.Duration;
        _fireCooldown = attack.Duration * 0.88f;
        _meleeComboWindow = 0.0f;
        _meleeDrawTime = 0.0f;
        _meleeAttackActive = true;
        _meleeAttackQueued = false;
        _meleeSweepPrimed = false;
        _meleeBladeSweepResolved = false;
        _meleeWorldImpactSpawned = false;
        _meleeSweepRayCount = 0;
        _meleeHitTargets.Clear();
        _meleeHitTargetRids.Clear();
        ClearMeleeTrail();
        Main?.OnLocalMeleeSwingStarted(
            definition.Id,
            attackIndex,
            _meleeSwingSequence,
            _meleeSwingStartedAtMsec);
        if (IsInstanceValid(_meleeSwingAudio))
        {
            _meleeSwingAudio.Stop();
            _meleeSwingAudio.Stream = SoundLab.MeleeSwing(definition.Style, attackIndex);
            _meleeSwingAudio.PitchScale = _rng.RandfRange(0.96f, 1.04f);
            _meleeSwingAudio.Play();
        }
    }

    private void UpdateKnifeAnimation(float delta)
    {
        var definition = CurrentMeleeDefinition;
        var presentation = MeleeAttackCatalog.PresentationFor(definition.Style);

        if (!_knifeEquipped)
        {
            ClearMeleeTrail();
            return;
        }
        if (RoleActionBlocksWeapon || MedicalActionBlocksWeapon)
        {
            CancelMeleeAction();
            return;
        }
        if (!_meleeAttackActive)
        {
            _meleeComboWindow = Mathf.Max(0.0f, _meleeComboWindow - delta);
        }

        if (_meleeAttackActive)
        {
            var attack = MeleeAttackCatalog.AttackFor(definition.Style, _meleeAttackIndex);
            var progress = Mathf.Clamp(
                1.0f - _knifeTime / Mathf.Max(0.001f, attack.Duration),
                0.0f,
                1.0f);
            var rawTarget = AttackPose(presentation.Rest, attack, progress);
            SetMeleePose(rawTarget);
            var restingPose = ApplyMeleeWallClearance(
                presentation.Rest,
                definition,
                delta,
                out var wallObstruction);
            var target = ConstrainMeleePose(
                rawTarget,
                presentation.Rest,
                restingPose,
                wallObstruction,
                definition.TwoHanded);
            SetMeleePose(target);
            var clearanceSafe = FinalizeMeleePoseClearance(
                target,
                presentation.Rest,
                definition);
            UpdateMeleeTrail(clearanceSafe && progress is >= 0.18f and <= 0.76f);
            UpdateMeleeBladeSweep(
                definition,
                attack,
                progress,
                damageEnabled: clearanceSafe);
            if (_knifeTime <= 0.0f)
            {
                _meleeAttackActive = false;
                _meleeSweepPrimed = false;
                if (_meleeAttackQueued)
                {
                    BeginMeleeAttack(
                        (_meleeAttackIndex + 1) % MeleeAttackCatalog.AttackCount(definition.Style));
                }
                else
                {
                    _meleeComboWindow = MeleeAttackCatalog.ComboWindowDuration;
                }
            }
            return;
        }

        _meleeSweepPrimed = false;
        ClearMeleeTrail();
        if (_meleeDrawTime > 0.0f)
        {
            _meleeDrawTime = Mathf.Max(0.0f, _meleeDrawTime - delta);
            var progress = 1.0f - _meleeDrawTime / presentation.DrawDuration;
            MeleePose target;
            if (progress < 0.58f)
            {
                var phase = Mathf.SmoothStep(0.0f, 1.0f, progress / 0.58f);
                target = LerpPose(presentation.DrawStart, presentation.DrawFlourish, phase);
            }
            else
            {
                var phase = Mathf.SmoothStep(0.0f, 1.0f, (progress - 0.58f) / 0.42f);
                target = LerpPose(presentation.DrawFlourish, presentation.Rest, phase);
            }
            SetMeleePose(target);
            var restingPose = ApplyMeleeWallClearance(
                presentation.Rest,
                definition,
                delta,
                out var wallObstruction);
            target = ConstrainMeleePose(
                target,
                presentation.Rest,
                restingPose,
                wallObstruction,
                definition.TwoHanded);
            SetMeleePose(target);
            FinalizeMeleePoseClearance(target, presentation.Rest, definition);
            return;
        }

        var idlePose = ApplyMeleeWallClearance(
            presentation.Rest,
            definition,
            delta,
            out _);
        _knifeRoot.Position = _knifeRoot.Position.Lerp(
            idlePose.Position,
            SmoothFactor(12.0f, delta));
        _knifeRoot.Rotation = _knifeRoot.Rotation.Lerp(
            idlePose.Rotation,
            SmoothFactor(12.0f, delta));
        FinalizeMeleePoseClearance(
            new MeleePose(_knifeRoot.Position, _knifeRoot.Rotation),
            presentation.Rest,
            definition);
    }

    private static MeleePose AttackPose(
        MeleePose restingPose,
        MeleeAttackDefinition attack,
        float progress)
    {
        if (progress < 0.25f)
        {
            var phase = Mathf.SmoothStep(0.0f, 1.0f, progress / 0.25f);
            return LerpPose(restingPose, attack.Windup, phase);
        }
        if (progress < 0.68f)
        {
            var phase = Mathf.SmoothStep(0.0f, 1.0f, (progress - 0.25f) / 0.43f);
            return LerpPose(attack.Windup, attack.FollowThrough, phase);
        }
        var recovery = Mathf.SmoothStep(0.0f, 1.0f, (progress - 0.68f) / 0.32f);
        return LerpPose(attack.FollowThrough, restingPose, recovery);
    }

    private static MeleePose LerpPose(MeleePose from, MeleePose to, float weight)
        => new(
            from.Position.Lerp(to.Position, weight),
            from.Rotation.Lerp(to.Rotation, weight));

    private void UpdateMeleeTrail(bool active)
    {
        if (!active
            || !IsInstanceValid(_authoredMelee?.BladeBase)
            || !IsInstanceValid(_authoredMelee?.BladeTip))
        {
            ClearMeleeTrail();
            return;
        }

        _meleeTrailSamples.Add(new MeleeTrailSample(
            _camera.ToLocal(_authoredMelee.BladeBase.GlobalPosition),
            _camera.ToLocal(_authoredMelee.BladeTip.GlobalPosition)));
        while (_meleeTrailSamples.Count > 8)
        {
            _meleeTrailSamples.RemoveAt(0);
        }
        RebuildMeleeTrailMesh();
    }

    private void RebuildMeleeTrailMesh()
    {
        if (!IsInstanceValid(_meleeTrailMesh) || _meleeTrailSamples.Count < 2)
        {
            return;
        }
        var accent = _meleeTrail.GetMeta("accent").AsColor();
        _meleeTrailMesh.ClearSurfaces();
        _meleeTrailMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _meleeTrailMaterial);
        for (var index = 0; index < _meleeTrailSamples.Count; index++)
        {
            var alpha = Mathf.Lerp(0.02f, 0.52f, (index + 1.0f) / _meleeTrailSamples.Count);
            _meleeTrailMesh.SurfaceSetColor(new Color(accent.R, accent.G, accent.B, alpha * 0.38f));
            _meleeTrailMesh.SurfaceAddVertex(_meleeTrailSamples[index].BladeBase);
            _meleeTrailMesh.SurfaceSetColor(new Color(accent.R, accent.G, accent.B, alpha));
            _meleeTrailMesh.SurfaceAddVertex(_meleeTrailSamples[index].BladeTip);
        }
        _meleeTrailMesh.SurfaceEnd();
    }

    private void ClearMeleeTrail()
    {
        _meleeTrailSamples.Clear();
        if (IsInstanceValid(_meleeTrailMesh))
        {
            _meleeTrailMesh.ClearSurfaces();
        }
    }

    private void RebuildKnife()
    {
        var visible = IsInstanceValid(_knifeRoot) && _knifeRoot.Visible;
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.GetParent()?.RemoveChild(_knifeRoot);
            _knifeRoot.QueueFree();
        }
        if (IsInstanceValid(_meleeTrail))
        {
            _meleeTrail.GetParent()?.RemoveChild(_meleeTrail);
            _meleeTrail.QueueFree();
        }
        BuildKnife();
        _knifeRoot.Visible = visible;
        if (visible)
        {
            BeginMeleeDraw();
        }
    }

    internal void StartMeleeAttackForDiagnostics() => StartKnifeAttack();
}
