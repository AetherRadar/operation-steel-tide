using Godot;

namespace OperationSteelTide;

internal enum FirstPersonFieldUsePresentationKind
{
    Bandage,
    FieldMedkit,
    Adrenaline,
    ArmorPlate
}

internal readonly record struct FieldUsePresentationInspection(
    bool Loaded,
    bool Visible,
    FirstPersonFieldUsePresentationKind Kind,
    float Progress,
    bool KitVisible,
    bool GauzeVisible,
    bool InjectorVisible,
    bool PlateVisible,
    bool CarrierVisible,
    bool ArmsVisible,
    float PrimaryGripResidual,
    float SupportGripResidual,
    float PrimaryGripAngleResidual,
    float SupportGripAngleResidual,
    float LidOpenAngle,
    float GauzeTravel,
    float PlateTravel,
    float FlapOpenAngle);

internal sealed class FirstPersonFieldUsePresentation
{
    private const float ArmPresentationScale = 0.72f;
    private const float PrimaryGripCorrectionLimit = 0.20944f;
    private const float SupportPivotSwingLimit = 0.73304f;
    private const float SupportGripCorrectionLimit = 0.20944f;
    private static readonly Basis ArmPresentationBasis = new Basis(Vector3.Up, Mathf.Pi);

    private readonly Node3D _root;
    private readonly AuthoredFieldUsePropsVisual _props;
    private readonly AuthoredFirstPersonArmsVisual _arms;
    private readonly Basis _primaryGripBasisOffset;
    private readonly Basis _supportGripBasisOffset;
    private FirstPersonFieldUsePresentationKind _kind;
    private float _progress;
    private Transform3D _primaryGripTarget;
    private Transform3D _supportGripTarget;

    public FirstPersonFieldUsePresentation(Node3D camera)
    {
        _root = new Node3D
        {
            Name = "FirstPersonFieldUsePresentation",
            Position = new Vector3(0.0f, -0.28f, -0.74f),
            Visible = false
        };
        camera.AddChild(_root);

        _props = CombatModelLibrary.InstantiateFieldUseProps();
        _root.AddChild(_props.Root);
        _arms = CombatModelLibrary.InstantiateFirstPersonRifleArms();
        _root.AddChild(_arms.Root);

        _props.ResetPose();
        _arms.RightArm.Transform = Transform3D.Identity;
        _arms.LeftArm.Transform = Transform3D.Identity;
        var referencePrimary = MarkerTransform(_props.TraumaPrimaryGrip);
        _primaryGripBasisOffset = referencePrimary.Basis.Orthonormalized().Inverse()
            * ArmPresentationBasis;
        var referencePrimaryTarget = new Transform3D(
            ArmPresentationBasis.Scaled(Vector3.One * ArmPresentationScale),
            referencePrimary.Origin);
        _arms.Root.Transform = referencePrimaryTarget
            * _arms.RightGripTransformInRoot.AffineInverse();
        var referenceSupport = MarkerTransform(_props.TraumaLidGrip);
        var authoredSupportBasis = (_arms.Root.Transform
            * _arms.MarkerTransformInRoot(_arms.LeftGripFrame))
            .Basis
            .Orthonormalized();
        _supportGripBasisOffset = referenceSupport.Basis.Orthonormalized().Inverse()
            * authoredSupportBasis;
        Hide();
    }

    public bool Visible => _root.Visible;

    public void Present(FirstPersonFieldUsePresentationKind kind, float progress)
    {
        _kind = kind;
        _progress = Mathf.Clamp(progress, 0.0f, 0.999f);
        _root.Visible = true;
        _props.Root.Visible = true;
        _arms.Root.Visible = true;
        _arms.RightArm.Visible = true;
        _arms.LeftArm.Visible = true;
        _props.ResetPose();
        _arms.RightArm.Transform = Transform3D.Identity;
        _arms.LeftArm.Transform = Transform3D.Identity;

        switch (kind)
        {
            case FirstPersonFieldUsePresentationKind.FieldMedkit:
                PresentMedkit(_progress);
                break;
            case FirstPersonFieldUsePresentationKind.Adrenaline:
                PresentAdrenaline(_progress);
                break;
            case FirstPersonFieldUsePresentationKind.ArmorPlate:
                PresentArmorPlate(_progress);
                break;
            default:
                PresentBandage(_progress);
                break;
        }
    }

    public void Hide()
    {
        _root.Visible = false;
        _props.Root.Visible = false;
        _arms.Root.Visible = false;
        _props.ResetPose();
        _progress = 0.0f;
    }

    public FieldUsePresentationInspection Inspect()
    {
        var rightGrip = _arms.Root.Transform
            * _arms.MarkerTransformInRoot(_arms.RightGripFrame);
        var leftGrip = _arms.Root.Transform
            * _arms.MarkerTransformInRoot(_arms.LeftGripFrame);
        return new FieldUsePresentationInspection(
            true,
            _root.Visible,
            _kind,
            _progress,
            _props.TraumaKit.Visible,
            _props.TraumaGauzePack.Visible,
            _props.TraumaInjector.Visible,
            _props.ArmorPlate.Visible,
            _props.ArmorCarrier.Visible,
            _arms.Root.Visible && _arms.RightArm.Visible && _arms.LeftArm.Visible,
            rightGrip.Origin.DistanceTo(_primaryGripTarget.Origin),
            leftGrip.Origin.DistanceTo(_supportGripTarget.Origin),
            BasisAngle(rightGrip.Basis, _primaryGripTarget.Basis),
            BasisAngle(leftGrip.Basis, _supportGripTarget.Basis),
            BasisAngle(_props.TraumaKitLid.Transform.Basis, _props.LidRest.Basis),
            _props.TraumaGauzePack.Position.DistanceTo(_props.GauzeRest.Origin),
            _props.ArmorPlate.Position.DistanceTo(_props.PlateRest.Origin),
            BasisAngle(_props.ArmorCarrierFlap.Transform.Basis, _props.FlapRest.Basis));
    }

    private void PresentMedkit(float progress)
    {
        _props.TraumaKit.Visible = true;
        var draw = Ease(Range(progress, 0.0f, 0.18f));
        var stow = Ease(Range(progress, 0.84f, 0.99f));
        var carry = new Vector3(0.12f, -0.38f, 0.10f).Lerp(Vector3.Zero, draw);
        carry = carry.Lerp(new Vector3(0.08f, -0.31f, 0.10f), stow);
        _props.TraumaKit.Transform = OffsetPose(
            _props.KitRest,
            carry,
            new Vector3(
                Mathf.Lerp(0.28f, -0.08f, draw),
                Mathf.Lerp(-0.30f, -0.10f, draw),
                Mathf.Lerp(0.24f, 0.08f, draw)));

        var lidOpen = Ease(Range(progress, 0.16f, 0.38f))
            * (1.0f - Ease(Range(progress, 0.78f, 0.92f)));
        _props.TraumaKitLid.Transform = RotatePose(
            _props.LidRest,
            Vector3.Right,
            -1.22f * lidOpen);

        var gauzeDraw = Ease(Range(progress, 0.34f, 0.58f));
        var gauzeApply = Ease(Range(progress, 0.58f, 0.78f));
        var gauzeStow = Ease(Range(progress, 0.84f, 0.96f));
        _props.TraumaGauzePack.Visible = progress >= 0.28f && progress < 0.97f;
        var gauzeOffset = new Vector3(0.0f, -0.02f, 0.0f).Lerp(
            new Vector3(-0.18f, 0.16f, -0.06f),
            gauzeDraw);
        gauzeOffset = gauzeOffset.Lerp(
            new Vector3(-0.23f, 0.04f, 0.04f),
            gauzeApply);
        gauzeOffset = gauzeOffset.Lerp(new Vector3(-0.05f, -0.24f, 0.10f), gauzeStow);
        _props.TraumaGauzePack.Transform = OffsetPose(
            _props.GauzeRest,
            gauzeOffset,
            new Vector3(0.10f + gauzeApply * 0.42f, -0.18f, -0.12f));

        var leftMarker = progress < 0.46f
            ? _props.TraumaLidGrip
            : _props.TraumaGauzeGrip;
        // Keep the dominant hand planted on the kit while the support hand
        // opens the lid and extracts the dressing. Sending both hands to the
        // gauze made the forearms cross and read as one disconnected limb.
        _primaryGripTarget = GripTarget(_props.TraumaPrimaryGrip, Vector3.Zero, primary: true);
        _supportGripTarget = GripTarget(
            leftMarker,
            progress >= 0.46f ? new Vector3(-0.08f, 0.01f, 0.01f) : Vector3.Zero,
            primary: false);
        AlignArmsToTargets();
    }

    private void PresentBandage(float progress)
    {
        _props.TraumaGauzePack.Visible = true;
        var draw = Ease(Range(progress, 0.0f, 0.20f));
        var wrap = Ease(Range(progress, 0.24f, 0.78f));
        var stow = Ease(Range(progress, 0.82f, 0.99f));
        var offset = new Vector3(0.10f, -0.34f, 0.12f).Lerp(
            new Vector3(0.02f, 0.05f, 0.0f),
            draw);
        offset += new Vector3(
            -0.15f * wrap,
            Mathf.Sin(wrap * Mathf.Pi * 2.0f) * 0.035f,
            0.04f * wrap);
        offset = offset.Lerp(new Vector3(-0.04f, -0.30f, 0.10f), stow);
        _props.TraumaGauzePack.Transform = OffsetPose(
            _props.GauzeRest,
            offset,
            new Vector3(0.08f + wrap * 0.34f, -0.22f, -0.16f + wrap * 0.22f));

        _primaryGripTarget = GripTarget(
            _props.TraumaGauzeGrip,
            new Vector3(0.075f, -0.01f, 0.01f),
            primary: true);
        _supportGripTarget = GripTarget(
            _props.TraumaGauzeGrip,
            new Vector3(-0.11f, 0.015f, 0.015f),
            primary: false);
        AlignArmsToTargets();
    }

    private void PresentAdrenaline(float progress)
    {
        _props.TraumaInjector.Visible = true;
        var draw = Ease(Range(progress, 0.0f, 0.24f));
        var aim = Ease(Range(progress, 0.24f, 0.50f));
        var inject = Ease(Range(progress, 0.50f, 0.72f));
        var stow = Ease(Range(progress, 0.78f, 0.99f));
        var offset = new Vector3(0.16f, -0.36f, 0.14f).Lerp(
            new Vector3(0.12f, 0.08f, 0.0f),
            draw);
        offset = offset.Lerp(new Vector3(-0.10f, 0.02f, 0.03f), aim);
        offset += new Vector3(-0.045f * inject, -0.018f * inject, 0.02f * inject);
        offset = offset.Lerp(new Vector3(0.12f, -0.34f, 0.12f), stow);
        _props.TraumaInjector.Transform = OffsetPose(
            _props.InjectorRest,
            offset,
            new Vector3(-0.12f, -0.24f + aim * 0.42f, 0.18f));

        _primaryGripTarget = GripTarget(
            _props.InjectorPrimaryGrip,
            Vector3.Zero,
            primary: true);
        var supportFrame = GripTarget(
            _props.InjectorPrimaryGrip,
            Vector3.Zero,
            primary: false);
        _supportGripTarget = new Transform3D(
            supportFrame.Basis,
            new Vector3(-0.21f, 0.01f - 0.035f * inject, 0.02f));
        AlignArmsToTargets();
    }

    private void PresentArmorPlate(float progress)
    {
        _props.ArmorPlate.Visible = true;
        _props.ArmorCarrier.Visible = true;
        var draw = Ease(Range(progress, 0.0f, 0.22f));
        var align = Ease(Range(progress, 0.22f, 0.52f));
        var insert = Ease(Range(progress, 0.52f, 0.80f));
        var lockFlap = Ease(Range(progress, 0.78f, 0.94f));
        var stow = Ease(Range(progress, 0.92f, 0.999f));

        _props.ArmorCarrier.Transform = OffsetPose(
            _props.CarrierRest,
            new Vector3(-0.02f, -0.34f, 0.08f).Lerp(Vector3.Zero, draw),
            new Vector3(-0.10f, 0.0f, 0.0f));
        _props.ArmorCarrierFlap.Transform = RotatePose(
            _props.FlapRest,
            Vector3.Right,
            -1.08f * (1.0f - lockFlap));

        var plateOffset = new Vector3(0.18f, -0.40f, 0.12f).Lerp(
            new Vector3(0.14f, 0.11f, -0.02f),
            draw);
        plateOffset = plateOffset.Lerp(new Vector3(0.0f, 0.13f, -0.02f), align);
        plateOffset = plateOffset.Lerp(new Vector3(0.0f, -0.04f, 0.03f), insert);
        plateOffset = plateOffset.Lerp(new Vector3(0.0f, -0.25f, 0.10f), stow);
        _props.ArmorPlate.Transform = OffsetPose(
            _props.PlateRest,
            plateOffset,
            new Vector3(
                Mathf.Lerp(0.24f, -0.08f, align),
                Mathf.Lerp(-0.25f, 0.0f, align),
                Mathf.Lerp(0.18f, 0.0f, align)));

        _primaryGripTarget = GripTarget(_props.ArmorPrimaryGrip, Vector3.Zero, primary: true);
        _supportGripTarget = GripTarget(_props.ArmorSupportGrip, Vector3.Zero, primary: false);
        AlignArmsToTargets();
    }

    private void AlignArmsToTargets()
    {
        // Treat the DCC grip frames as desired hand contact, not as permission
        // to rotate an entire rigid first-person sleeve through any angle. The
        // authored arms do not have a wrist skeleton, so unrestricted marker
        // alignment can point a sleeve opening at the top or side of the frame.
        // Keep the dominant arm near its bottom-fed presentation basis while
        // still accepting a small amount of the prop-authored grip direction.
        _primaryGripTarget = new Transform3D(
            ConstrainBasis(
                ArmPresentationBasis,
                _primaryGripTarget.Basis,
                PrimaryGripCorrectionLimit),
            _primaryGripTarget.Origin);
        var targetFrame = ScaledGripFrame(_primaryGripTarget);
        _arms.Root.Transform = targetFrame * _arms.RightGripTransformInRoot.AffineInverse();

        // The support arm is solved anatomically in two bounded steps. First,
        // swing the rigid forearm around its authored sleeve pivot toward the
        // contact point. Then accept only a small part of the remaining DCC
        // grip-frame twist. A final reach correction places the glove exactly
        // on the prop without allowing the sleeve basis to roll sideways.
        var rawSupportTarget = _supportGripTarget;
        var supportTargetInArms = _arms.Root.Transform.AffineInverse()
            * ScaledGripFrame(rawSupportTarget);
        var restTransform = _arms.LeftArm.Transform;
        var restGripInArms = _arms.MarkerTransformInRoot(_arms.LeftGripFrame);
        var restReach = restGripInArms.Origin - restTransform.Origin;
        var targetReach = supportTargetInArms.Origin - restTransform.Origin;
        var pivotBasis = restTransform.Basis;
        if (restReach.LengthSquared() > 0.000001f
            && targetReach.LengthSquared() > 0.000001f)
        {
            var desiredSwing = new Basis(new Quaternion(
                restReach.Normalized(),
                targetReach.Normalized())) * restTransform.Basis;
            pivotBasis = ConstrainBasis(
                restTransform.Basis,
                desiredSwing,
                SupportPivotSwingLimit);
        }

        var fullyAligned = supportTargetInArms * restGripInArms.AffineInverse();
        var constrainedSupportBasis = ConstrainBasis(
            pivotBasis,
            fullyAligned.Basis,
            SupportGripCorrectionLimit);
        _arms.LeftArm.Transform = new Transform3D(
            constrainedSupportBasis,
            restTransform.Origin);
        var constrainedGripInArms = _arms.MarkerTransformInRoot(_arms.LeftGripFrame);
        _arms.LeftArm.Position += supportTargetInArms.Origin - constrainedGripInArms.Origin;

        var finalSupportGrip = _arms.Root.Transform
            * _arms.MarkerTransformInRoot(_arms.LeftGripFrame);
        _supportGripTarget = new Transform3D(
            finalSupportGrip.Basis.Orthonormalized(),
            rawSupportTarget.Origin);
    }

    private Transform3D GripTarget(Node3D marker, Vector3 offset, bool primary)
    {
        var markerFrame = MarkerTransform(marker);
        var basisOffset = primary ? _primaryGripBasisOffset : _supportGripBasisOffset;
        return new Transform3D(
            markerFrame.Basis.Orthonormalized() * basisOffset,
            markerFrame.Origin + offset);
    }

    private Transform3D MarkerTransform(Node3D marker)
        => _props.Root.Transform * _props.MarkerTransformInRoot(marker);

    private static Transform3D ScaledGripFrame(Transform3D frame)
        => new(
            frame.Basis.Orthonormalized().Scaled(Vector3.One * ArmPresentationScale),
            frame.Origin);

    private static float BasisAngle(Basis left, Basis right)
        => left.Orthonormalized()
            .GetRotationQuaternion()
            .AngleTo(right.Orthonormalized().GetRotationQuaternion());

    private static Basis ConstrainBasis(Basis stable, Basis desired, float maximumAngle)
    {
        var stableFrame = stable.Orthonormalized();
        var desiredFrame = desired.Orthonormalized();
        var angle = BasisAngle(stableFrame, desiredFrame);
        if (angle <= maximumAngle || angle <= 0.00001f)
        {
            return desiredFrame;
        }
        return stableFrame
            .Slerp(desiredFrame, maximumAngle / angle)
            .Orthonormalized();
    }

    private static Transform3D OffsetPose(
        Transform3D rest,
        Vector3 offset,
        Vector3 rotation)
    {
        var rotationBasis = Basis.FromEuler(rotation);
        return new Transform3D(rotationBasis * rest.Basis, rest.Origin + offset);
    }

    private static Transform3D RotatePose(
        Transform3D rest,
        Vector3 axis,
        float radians)
        => new Transform3D(rest.Basis.Rotated(axis, radians), rest.Origin);

    private static float Range(float value, float start, float end)
        => Mathf.Clamp((value - start) / Mathf.Max(0.001f, end - start), 0.0f, 1.0f);

    private static float Ease(float value)
    {
        var t = Mathf.Clamp(value, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }
}
