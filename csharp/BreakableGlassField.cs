using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Batches many glass panes into a few draw calls and one query-only area. Each pane
/// keeps its own shape owner so a ray can remove exactly the pane that was hit.
/// </summary>
[GlobalClass]
public partial class BreakableGlassField : Area3D
{
    public const uint GlassCollisionLayer = 1u << 7;
    public const string GroupName = "breakable_glass_fields";
    public const string AudioGroupName = "glass_break_audio";

    private const float MinimumShatterDamage = 4.0f;
    private static BoxMesh? _unitBox;
    private static QuadMesh? _unitPane;
    private static AudioStreamWav? _glassBreakSound;

    private static BoxMesh UnitBox => _unitBox ??= new BoxMesh { Size = Vector3.One };
    private static QuadMesh UnitPane => _unitPane ??= new QuadMesh { Size = Vector2.One };

    internal static void ReleaseSharedResources()
    {
        _unitBox = null;
        _unitPane = null;
        _glassBreakSound = null;
    }

    private sealed class PaneState
    {
        public Vector3 Position;
        public Vector3 Size;
        public Vector3 Rotation;
        public Color Tint;
        public uint ShapeOwner;
        public uint MovementShapeOwner;
        public bool HasMovementShape;
        public bool Shattered;
    }

    private readonly List<PaneState> _panes = new();
    private readonly Dictionary<uint, int> _paneByShapeOwner = new();
    private Godot.Material? _glassMaterial;
    private Godot.Material? _frameMaterial;
    private Godot.Material? _backingMaterial;
    private MultiMesh? _glassMultiMesh;
    private bool _committed;
    private float _visibilityRange = 105.0f;

    public int PaneCount => _panes.Count;
    public int ShatteredCount { get; private set; }
    public int FrameInstanceCount { get; private set; }
    public Vector3 LastShatterPosition { get; private set; }
    public bool UsesSingleSurfaceVisual => _glassMultiMesh?.Mesh is QuadMesh;

    public void Configure(
        Godot.Material glassMaterial,
        Godot.Material frameMaterial,
        Godot.Material? backingMaterial = null,
        float visibilityRange = 105.0f,
        bool buildFrames = true,
        bool blocksMovementUntilShattered = false)
    {
        _glassMaterial = glassMaterial;
        _frameMaterial = frameMaterial;
        _backingMaterial = backingMaterial;
        _visibilityRange = visibilityRange;
        _buildFrames = buildFrames;
        _blocksMovementUntilShattered = blocksMovementUntilShattered;
    }

    public int AddPane(Vector3 position, Vector3 size, Color tint, Vector3 rotation = default)
    {
        if (_committed)
        {
            GD.PushError($"Cannot add a pane after {Name} has been committed.");
            return -1;
        }
        _panes.Add(new PaneState
        {
            Position = position,
            Size = size,
            Rotation = rotation,
            Tint = tint
        });
        return _panes.Count - 1;
    }

    public void Commit()
    {
        if (_committed || _panes.Count == 0)
        {
            return;
        }
        _committed = true;
        CollisionLayer = GlassCollisionLayer;
        CollisionMask = 0;
        Monitoring = false;
        Monitorable = true;
        InputRayPickable = false;

        BuildGlassVisuals();
        if (_buildFrames)
        {
            BuildFrameVisuals();
        }
        BuildBackingVisuals();
        BuildQueryShapes();
        BuildMovementShapes();
        ApplyFieldCollisionState();
        AddToGroup(GroupName);
    }

    private void BuildGlassVisuals()
    {
        _glassMultiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            InstanceCount = _panes.Count,
            Mesh = UnitPane
        };
        for (var index = 0; index < _panes.Count; index++)
        {
            var pane = _panes[index];
            _glassMultiMesh.SetInstanceTransform(index, PaneSurfaceTransform(pane));
            _glassMultiMesh.SetInstanceColor(index, pane.Tint);
        }
        AddChild(new MultiMeshInstance3D
        {
            Name = "IntactGlass",
            Multimesh = _glassMultiMesh,
            MaterialOverride = _glassMaterial,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = _visibilityRange,
            VisibilityRangeEndMargin = 12.0f
        });
    }

    private void BuildFrameVisuals()
    {
        var frameTransforms = new List<Transform3D>(_panes.Count * 5);
        foreach (var pane in _panes)
        {
            AppendFrameTransforms(pane, frameTransforms);
        }
        FrameInstanceCount = frameTransforms.Count;
        var frames = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = frameTransforms.Count,
            Mesh = UnitBox
        };
        for (var index = 0; index < frameTransforms.Count; index++)
        {
            frames.SetInstanceTransform(index, frameTransforms[index]);
        }
        AddChild(new MultiMeshInstance3D
        {
            Name = "WindowFrames",
            Multimesh = frames,
            MaterialOverride = _frameMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = _visibilityRange,
            VisibilityRangeEndMargin = 12.0f
        });
    }

    private void BuildBackingVisuals()
    {
        if (_backingMaterial is null)
        {
            return;
        }
        var backings = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = _panes.Count,
            Mesh = UnitBox
        };
        for (var index = 0; index < _panes.Count; index++)
        {
            var pane = _panes[index];
            var size = pane.Size;
            var thinAxis = ThinAxis(size);
            size[thinAxis] = Mathf.Max(0.012f, size[thinAxis] * 0.42f);
            size[(thinAxis + 1) % 3] *= 0.91f;
            size[(thinAxis + 2) % 3] *= 0.91f;
            backings.SetInstanceTransform(index, BoxTransform(pane, Vector3.Zero, size));
        }
        AddChild(new MultiMeshInstance3D
        {
            Name = "WindowRecesses",
            Multimesh = backings,
            MaterialOverride = _backingMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = _visibilityRange,
            VisibilityRangeEndMargin = 12.0f
        });
    }

    private void BuildQueryShapes()
    {
        for (var index = 0; index < _panes.Count; index++)
        {
            var pane = _panes[index];
            pane.ShapeOwner = CreateShapeOwner(this);
            ShapeOwnerSetTransform(
                pane.ShapeOwner,
                new Transform3D(Basis.FromEuler(pane.Rotation), pane.Position));
            ShapeOwnerAddShape(pane.ShapeOwner, new BoxShape3D { Size = pane.Size });
            _paneByShapeOwner[pane.ShapeOwner] = index;
        }
    }

    private static void AppendFrameTransforms(PaneState pane, List<Transform3D> transforms)
    {
        var size = pane.Size;
        var thinAxis = ThinAxis(size);
        var axisA = (thinAxis + 1) % 3;
        var axisB = (thinAxis + 2) % 3;
        var border = 0.075f;
        var depth = Mathf.Max(0.065f, size[thinAxis] * 1.8f);

        var longA = Vector3.Zero;
        longA[thinAxis] = depth;
        longA[axisA] = size[axisA] + border * 1.8f;
        longA[axisB] = border;
        var offsetB = Vector3.Zero;
        offsetB[axisB] = size[axisB] * 0.5f;
        transforms.Add(BoxTransform(pane, offsetB, longA));
        transforms.Add(BoxTransform(pane, -offsetB, longA));

        var longB = Vector3.Zero;
        longB[thinAxis] = depth;
        longB[axisA] = border;
        longB[axisB] = size[axisB];
        var offsetA = Vector3.Zero;
        offsetA[axisA] = size[axisA] * 0.5f;
        transforms.Add(BoxTransform(pane, offsetA, longB));
        transforms.Add(BoxTransform(pane, -offsetA, longB));

        var mullion = longB;
        mullion[axisA] = 0.045f;
        transforms.Add(BoxTransform(pane, Vector3.Zero, mullion));
    }

    private static Transform3D BoxTransform(PaneState pane, Vector3 localOffset, Vector3 size)
    {
        var rotation = Basis.FromEuler(pane.Rotation);
        return new Transform3D(rotation.Scaled(size), pane.Position + rotation * localOffset);
    }

    private static Transform3D PaneSurfaceTransform(PaneState pane)
    {
        var thinAxis = ThinAxis(pane.Size);
        var surfaceBasis = thinAxis switch
        {
            0 => new Basis(
                Vector3.Back * pane.Size.Z,
                Vector3.Up * pane.Size.Y,
                Vector3.Right),
            1 => new Basis(
                Vector3.Right * pane.Size.X,
                Vector3.Back * pane.Size.Z,
                Vector3.Up),
            _ => new Basis(
                Vector3.Right * pane.Size.X,
                Vector3.Up * pane.Size.Y,
                Vector3.Back)
        };
        return new Transform3D(Basis.FromEuler(pane.Rotation) * surfaceBasis, pane.Position);
    }

    private static int ThinAxis(Vector3 size)
    {
        if (size.X <= size.Y && size.X <= size.Z)
        {
            return 0;
        }
        return size.Y <= size.Z ? 1 : 2;
    }

    public bool TryShatterShape(
        int shapeIndex,
        Vector3 hitPosition,
        Vector3 hitNormal,
        Vector3 shotDirection,
        float damage,
        bool spawnEffects = true)
    {
        if (!_committed || damage < MinimumShatterDamage)
        {
            return false;
        }
        var owner = ShapeFindOwner(shapeIndex);
        if (!_paneByShapeOwner.TryGetValue(owner, out var paneIndex)
            || IsPaneShattered(paneIndex))
        {
            return false;
        }
        return !_hasLocalShatterAuthority
            || ShatterPane(paneIndex, hitPosition, hitNormal, shotDirection, spawnEffects);
    }

    private bool ShatterPane(
        int paneIndex,
        Vector3 hitPosition,
        Vector3 hitNormal,
        Vector3 shotDirection,
        bool spawnEffects)
    {
        if (!_committed
            || !_fieldActive
            || paneIndex < 0
            || paneIndex >= _panes.Count
            || _panes[paneIndex].Shattered)
        {
            return false;
        }
        var shatterMask = ShatterMaskForPane(paneIndex);
        var changed = false;
        _suppressPaneShatteredEvent = true;
        try
        {
            for (var linkedIndex = 0; linkedIndex < _panes.Count; linkedIndex++)
            {
                if (linkedIndex >= sizeof(uint) * 8
                    || (shatterMask & (1u << linkedIndex)) == 0u
                    || _panes[linkedIndex].Shattered)
                {
                    continue;
                }
                var linkedPane = _panes[linkedIndex];
                var linkedPosition = linkedIndex == paneIndex
                    ? hitPosition
                    : ToGlobal(linkedPane.Position);
                changed |= ShatterSinglePane(
                    linkedIndex,
                    linkedPosition,
                    hitNormal,
                    shotDirection,
                    spawnEffects && linkedIndex == paneIndex);
            }
        }
        finally
        {
            _suppressPaneShatteredEvent = false;
        }
        if (changed)
        {
            // Linked partners are processed in index order, but the replicated impact
            // belongs to the pane the attack actually touched.
            LastShatterPosition = hitPosition;
            NotifyPaneShattered(paneIndex);
        }
        return changed;
    }

    private bool ShatterSinglePane(
        int paneIndex,
        Vector3 hitPosition,
        Vector3 hitNormal,
        Vector3 shotDirection,
        bool spawnEffects,
        bool requireFieldActive = true)
    {
        if (!_committed
            || requireFieldActive && !_fieldActive
            || paneIndex < 0
            || paneIndex >= _panes.Count
            || _panes[paneIndex].Shattered)
        {
            return false;
        }
        var pane = _panes[paneIndex];
        pane.Shattered = true;
        ShapeOwnerSetDisabled(pane.ShapeOwner, true);
        SetPaneMovementCollisionDisabled(pane, true);
        _glassMultiMesh?.SetInstanceTransform(
            paneIndex,
            new Transform3D(Basis.Identity.Scaled(Vector3.Zero), pane.Position));
        ShatteredCount++;
        LastShatterPosition = hitPosition;
        if (spawnEffects && IsInsideTree())
        {
            SpawnShatterEffects(hitPosition, hitNormal, shotDirection);
        }
        return true;
    }

    public bool TryGetIntactPaneRay(out Vector3 from, out Vector3 to, out int paneIndex)
    {
        for (var index = 0; index < _panes.Count; index++)
        {
            var pane = _panes[index];
            if (pane.Shattered)
            {
                continue;
            }
            var localNormal = Vector3.Zero;
            localNormal[ThinAxis(pane.Size)] = 1.0f;
            var rotation = Basis.FromEuler(pane.Rotation);
            var worldCenter = ToGlobal(pane.Position);
            var worldNormal = GlobalBasis * (rotation * localNormal);
            worldNormal = worldNormal.Normalized();
            from = worldCenter + worldNormal * 1.2f;
            to = worldCenter - worldNormal * 1.2f;
            paneIndex = index;
            return true;
        }
        from = Vector3.Zero;
        to = Vector3.Zero;
        paneIndex = -1;
        return false;
    }

    public bool IsPaneCollisionDisabled(int paneIndex)
    {
        return paneIndex >= 0
            && paneIndex < _panes.Count
            && IsShapeOwnerDisabled(_panes[paneIndex].ShapeOwner);
    }

    public bool IsPaneShattered(int paneIndex)
    {
        return paneIndex >= 0 && paneIndex < _panes.Count && _panes[paneIndex].Shattered;
    }

    internal bool IsShapeShattered(int shapeIndex)
    {
        if (!_committed || shapeIndex < 0)
        {
            return false;
        }
        var owner = ShapeFindOwner(shapeIndex);
        return _paneByShapeOwner.TryGetValue(owner, out var paneIndex)
            && IsPaneShattered(paneIndex);
    }

    public int ShatterWithinRadius(Vector3 worldPosition, float radius, int effectBudget, out int effectsUsed)
    {
        var shattered = 0;
        effectsUsed = 0;
        if (!_hasLocalShatterAuthority)
        {
            return 0;
        }
        for (var index = 0; index < _panes.Count; index++)
        {
            var pane = _panes[index];
            if (pane.Shattered)
            {
                continue;
            }
            var panePosition = ToGlobal(pane.Position);
            if (panePosition.DistanceSquaredTo(worldPosition) > radius * radius)
            {
                continue;
            }
            var direction = worldPosition.DirectionTo(panePosition);
            var useEffects = effectsUsed < effectBudget;
            if (ShatterPane(index, panePosition, direction, direction, useEffects))
            {
                shattered++;
                effectsUsed += useEffects ? 1 : 0;
            }
        }
        return shattered;
    }

    public static bool TryShatterAlongRay(
        World3D world,
        Vector3 from,
        Vector3 to,
        float damage,
        Vector3 shotDirection,
        out Vector3 hitPosition,
        bool spawnEffects = true)
    {
        hitPosition = to;
        if (!TryFindIntactPaneAlongRay(
                world,
                from,
                to,
                out var glass,
                out var paneIndex,
                out hitPosition,
                out var hitNormal)
            || glass is null)
        {
            return false;
        }
        if (damage < MinimumShatterDamage)
        {
            return false;
        }
        if (glass._worldOcclusionRequired
            && PhysicsRaycast.TryHit(
                world,
                from,
                to,
                SightCollisionMask,
                out var nearerBody,
                collideWithAreas: false,
                collideWithBodies: true)
            && from.DistanceTo(nearerBody.Position) + 0.003f
                < from.DistanceTo(hitPosition))
        {
            return false;
        }
        if (!glass._hasLocalShatterAuthority)
        {
            return true;
        }
        return glass.ShatterPane(
            paneIndex,
            hitPosition,
            hitNormal,
            shotDirection,
            spawnEffects);
    }

    private void SpawnShatterEffects(Vector3 position, Vector3 hitNormal, Vector3 shotDirection)
    {
        var root = GetTree().CurrentScene ?? GetTree().Root;
        var direction = shotDirection.LengthSquared() > 0.001f
            ? shotDirection.Normalized()
            : -hitNormal.Normalized();
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.16f,
            Direction = direction,
            Spread = 52.0f,
            Gravity = new Vector3(0, -8.2f, 0),
            InitialVelocityMin = 2.8f,
            InitialVelocityMax = 7.8f,
            AngularVelocityMin = -720.0f,
            AngularVelocityMax = 720.0f,
            DampingMin = 0.2f,
            DampingMax = 0.65f,
            ScaleMin = 0.55f,
            ScaleMax = 1.35f,
            Color = new Color(0.64f, 0.9f, 0.96f, 0.82f)
        };
        var shardMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.58f, 0.88f, 0.94f, 0.72f),
            Metallic = 0.34f,
            Roughness = 0.08f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var particles = new GpuParticles3D
        {
            Name = "GlassShardBurst",
            Amount = 22,
            Lifetime = 1.35,
            OneShot = true,
            Explosiveness = 1.0f,
            Randomness = 0.42f,
            VisibilityAabb = new Aabb(new Vector3(-8, -8, -8), new Vector3(16, 16, 16)),
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.13f, 0.065f), Material = shardMaterial }
        };
        root.AddChild(particles);
        particles.GlobalPosition = position;
        particles.Emitting = true;

        var flash = new OmniLight3D
        {
            Name = "GlassImpactFlash",
            LightColor = new Color(0.58f, 0.88f, 1.0f),
            LightEnergy = 2.8f,
            OmniRange = 3.2f,
            ShadowEnabled = false
        };
        root.AddChild(flash);
        flash.GlobalPosition = position + hitNormal * 0.04f;

        var stream = _glassBreakSound ??= SoundLab.GlassBreak();
        var pitch = 0.94f + (ShatteredCount % 5) * 0.025f;
        var spatialAudio = new AudioStreamPlayer3D
        {
            Name = "GlassBreakAudio",
            Stream = stream,
            VolumeDb = 3.0f,
            UnitSize = 2.4f,
            MaxDistance = 72.0f,
            MaxDb = 5.0f,
            PitchScale = pitch
        };
        spatialAudio.AddToGroup(AudioGroupName);
        root.AddChild(spatialAudio);
        spatialAudio.GlobalPosition = position;
        spatialAudio.Play();

        AudioStreamPlayer? closeAudio = null;
        var camera = GetViewport().GetCamera3D();
        if (camera is not null && camera.GlobalPosition.DistanceTo(position) <= 30.0f)
        {
            closeAudio = new AudioStreamPlayer
            {
                Name = "GlassBreakCloseAudio",
                Stream = stream,
                VolumeDb = -0.5f,
                PitchScale = pitch * 1.015f
            };
            closeAudio.AddToGroup(AudioGroupName);
            root.AddChild(closeAudio);
            closeAudio.Play();
        }

        var tween = root.CreateTween().SetParallel(true);
        tween.TweenProperty(flash, "light_energy", 0.0f, 0.11f);
        tween.TweenProperty(particles, "transparency", 1.0f, 0.55f).SetDelay(0.78f);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            particles.QueueFree();
            flash.QueueFree();
            spatialAudio.QueueFree();
            closeAudio?.QueueFree();
        }));
    }
}
