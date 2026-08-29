using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    internal const string MeleeSurfaceMarkGroup = "melee_surface_marks";
    internal const string MeleeSurfaceAudioGroup = "melee_surface_audio";
    private const string MeleeSurfaceMetadataKey = "melee_surface";
    private const int MeleeSurfaceMarkLimit = 64;
    private const int MeleeSurfaceAudioLimit = 12;
    private const float ScratchSurfaceOffset = 0.0015f;
    private const float ScratchHighlightDepth = 0.00035f;
    private const float ScratchProbeDepth = 0.035f;
    private const float ScratchProbeStep = 0.04f;
    private const float ScratchEdgeInset = 0.003f;
    private const int ScratchProbeRefinementSteps = 4;
    private static readonly StandardMaterial3D[] ScratchGrooveMaterials = [
        CreateScratchMaterial(new Color(0.07f, 0.065f, 0.058f, 0.68f)),
        CreateScratchMaterial(new Color(0.12f, 0.15f, 0.17f, 0.72f)),
        CreateScratchMaterial(new Color(0.14f, 0.065f, 0.025f, 0.7f))
    ];
    private static readonly StandardMaterial3D[] ScratchHighlightMaterials = [
        CreateScratchMaterial(new Color(0.58f, 0.55f, 0.49f, 0.38f)),
        CreateScratchMaterial(new Color(0.65f, 0.7f, 0.72f, 0.44f), 0.08f),
        CreateScratchMaterial(new Color(0.64f, 0.34f, 0.16f, 0.4f))
    ];
    private static readonly StandardMaterial3D MasonryDebrisMaterial = CreateTransientSurfaceMaterial(
        new Color(0.46f, 0.43f, 0.37f, 0.64f));
    private static readonly StandardMaterial3D WoodDebrisMaterial = CreateTransientSurfaceMaterial(
        new Color(0.64f, 0.35f, 0.12f, 0.96f));
    private readonly Queue<Node3D> _meleeSurfaceMarks = new();
    private readonly Queue<AudioStreamPlayer3D> _meleeSurfaceAudioPlayers = new();
    private int _meleeSurfaceImpactCount;
    private int _meleeSurfaceMarkCount;
    private int _meleeSurfaceAudioCount;
    private MeleeImpactSurface _lastMeleeImpactSurface = MeleeImpactSurface.Masonry;
    private float _lastMeleeScratchLength;
    private float _lastMeleeScratchWidth;
    private Vector3 _lastMeleeImpactNormal;
    private Vector3 _lastMeleeImpactPosition;
    private double _lastMeleeImpactAudioLength;
    private bool _lastMeleeImpactAudioStarted;
    private bool _lastMeleeImpactAttachedToCollider;
    private bool _lastMeleeScratchEdgeClipped;
    private bool _lastMeleeScratchSurfaceSupported;
    private float _lastMeleeScratchSurfaceOffset;

    internal int MeleeSurfaceImpactCountForDiagnostics => _meleeSurfaceImpactCount;
    internal int MeleeSurfaceMarkCountForDiagnostics => _meleeSurfaceMarkCount;
    internal int MeleeSurfaceAudioCountForDiagnostics => _meleeSurfaceAudioCount;
    internal MeleeImpactSurface LastMeleeImpactSurfaceForDiagnostics => _lastMeleeImpactSurface;
    internal float LastMeleeScratchLengthForDiagnostics => _lastMeleeScratchLength;
    internal float LastMeleeScratchWidthForDiagnostics => _lastMeleeScratchWidth;
    internal Vector3 LastMeleeImpactNormalForDiagnostics => _lastMeleeImpactNormal;
    internal Vector3 LastMeleeImpactPositionForDiagnostics => _lastMeleeImpactPosition;
    internal double LastMeleeImpactAudioLengthForDiagnostics => _lastMeleeImpactAudioLength;
    internal bool LastMeleeImpactAudioStartedForDiagnostics => _lastMeleeImpactAudioStarted;
    internal bool LastMeleeImpactAttachedToColliderForDiagnostics => _lastMeleeImpactAttachedToCollider;
    internal bool LastMeleeScratchEdgeClippedForDiagnostics => _lastMeleeScratchEdgeClipped;
    internal bool LastMeleeScratchSurfaceSupportedForDiagnostics => _lastMeleeScratchSurfaceSupported;
    internal float LastMeleeScratchSurfaceOffsetForDiagnostics => _lastMeleeScratchSurfaceOffset;

    private readonly record struct ScratchPlacement(
        Vector3 Position,
        float Length,
        float WidthScale,
        bool EdgeClipped,
        bool SurfaceSupported);

    /// <summary>Creates presentation for one blade contact; callers own damage and deduplication.</summary>
    internal void SpawnMeleeSurfaceImpact(
        Vector3 position,
        Vector3 normal,
        Vector3 bladeTravel,
        GodotObject? collider,
        int shape,
        MeleeWeaponStyle style)
    {
        var surface = ResolveMeleeImpactSurface(collider, shape);
        var impactNormal = ResolveImpactNormal(normal, bladeTravel);
        var tangent = ResolveScratchTangent(impactNormal, bladeTravel);
        var bitangent = impactNormal.Cross(tangent).Normalized();
        var (length, strokeWidth) = ScratchDimensions(style);
        var placement = ResolveScratchPlacement(
            position,
            impactNormal,
            tangent,
            bitangent,
            collider,
            shape,
            length,
            strokeWidth);
        var overallWidth = ScratchFootprintHalfWidth(placement.Length, strokeWidth)
            * placement.WidthScale
            * 2.0f;
        var anchor = ResolveMeleeImpactAnchor(collider);

        var markRoot = new Node3D { Name = "MeleeBladeScratch" };
        markRoot.AddToGroup(MeleeSurfaceMarkGroup);
        markRoot.SetMeta(MeleeSurfaceMetadataKey, surface.ToString().ToLowerInvariant());
        markRoot.SetMeta("scratch_length", placement.Length);
        markRoot.SetMeta("scratch_width", overallWidth);
        markRoot.SetMeta("edge_clipped", placement.EdgeClipped);
        markRoot.SetMeta("impact_shape", shape);
        anchor.AddChild(markRoot);
        markRoot.GlobalTransform = new Transform3D(
            new Basis(tangent, bitangent, impactNormal),
            placement.Position + impactNormal * ScratchSurfaceOffset);

        var scratch = new MeshInstance3D
        {
            Name = "MultiStrokeScratch",
            Mesh = BuildScratchMesh(surface, placement.Length, strokeWidth, placement.WidthScale),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        markRoot.AddChild(scratch);
        TrackLimitedEffect(_meleeSurfaceMarks, markRoot, MeleeSurfaceMarkLimit, MeleeSurfaceMarkGroup);
        ScheduleMeleeSurfaceMarkFade(markRoot, scratch, surface);

        var impactStream = SoundLab.MeleeSurfaceImpact(surface, style);
        var impactAudio = new AudioStreamPlayer3D
        {
            Name = "MeleeSurfaceImpactAudio",
            Stream = impactStream,
            VolumeDb = SurfaceImpactVolume(surface),
            UnitSize = 2.0f,
            MaxDistance = 42.0f,
            MaxDb = 4.0f,
            PitchScale = _rng.RandfRange(0.96f, 1.04f)
        };
        impactAudio.AddToGroup(MeleeSurfaceAudioGroup);
        anchor.AddChild(impactAudio);
        impactAudio.GlobalPosition = position + impactNormal * 0.025f;
        impactAudio.Finished += impactAudio.QueueFree;
        impactAudio.Play();
        TrackLimitedEffect(
            _meleeSurfaceAudioPlayers, impactAudio, MeleeSurfaceAudioLimit, MeleeSurfaceAudioGroup);

        SpawnMeleeSurfaceDebris(position, impactNormal, tangent, surface);

        _meleeSurfaceImpactCount++;
        _meleeSurfaceMarkCount++;
        _meleeSurfaceAudioCount++;
        _lastMeleeImpactSurface = surface;
        _lastMeleeScratchLength = placement.Length;
        _lastMeleeScratchWidth = overallWidth;
        _lastMeleeImpactNormal = impactNormal;
        _lastMeleeImpactPosition = position;
        _lastMeleeImpactAudioLength = impactStream.GetLength();
        _lastMeleeImpactAudioStarted = impactAudio.Playing;
        _lastMeleeImpactAttachedToCollider = anchor != this;
        _lastMeleeScratchEdgeClipped = placement.EdgeClipped;
        _lastMeleeScratchSurfaceSupported = placement.SurfaceSupported;
        _lastMeleeScratchSurfaceOffset = ScratchSurfaceOffset + ScratchHighlightDepth;
    }

    internal void ResetMeleeSurfaceImpactDiagnostics()
    {
        ReleaseTrackedEffects(_meleeSurfaceMarks, MeleeSurfaceMarkGroup);
        ReleaseTrackedEffects(_meleeSurfaceAudioPlayers, MeleeSurfaceAudioGroup);
        (_meleeSurfaceImpactCount, _meleeSurfaceMarkCount, _meleeSurfaceAudioCount) = (0, 0, 0);
        _lastMeleeImpactSurface = MeleeImpactSurface.Masonry;
        (_lastMeleeScratchLength, _lastMeleeScratchWidth) = (0.0f, 0.0f);
        (_lastMeleeImpactNormal, _lastMeleeImpactPosition) = (Vector3.Zero, Vector3.Zero);
        _lastMeleeImpactAudioLength = 0.0;
        (_lastMeleeImpactAudioStarted, _lastMeleeImpactAttachedToCollider) = (false, false);
        (_lastMeleeScratchEdgeClipped, _lastMeleeScratchSurfaceSupported) = (false, false);
        _lastMeleeScratchSurfaceOffset = 0.0f;
    }

    private static void ReleaseTrackedEffects<T>(Queue<T> effects, string group)
        where T : Node
    {
        while (effects.Count > 0)
        {
            var effect = effects.Dequeue();
            if (!IsInstanceValid(effect))
            {
                continue;
            }
            effect.RemoveFromGroup(group);
            effect.QueueFree();
        }
    }

    private static void TrackLimitedEffect<T>(Queue<T> effects, T effect, int limit, string group)
        where T : Node
    {
        while (effects.Count > 0 && !IsInstanceValid(effects.Peek()))
        {
            effects.Dequeue();
        }
        effects.Enqueue(effect);
        while (effects.Count > limit)
        {
            var oldest = effects.Dequeue();
            if (!IsInstanceValid(oldest))
            {
                continue;
            }
            oldest.RemoveFromGroup(group);
            oldest.QueueFree();
        }
    }

    private void ScheduleMeleeSurfaceMarkFade(
        Node3D markRoot,
        GeometryInstance3D scratch,
        MeleeImpactSurface surface)
    {
        var holdDuration = surface switch
        {
            MeleeImpactSurface.Metal => 22.0f,
            MeleeImpactSurface.Wood => 16.0f,
            _ => 19.0f
        };
        var tween = markRoot.CreateTween();
        tween.TweenInterval(holdDuration);
        tween.TweenProperty(scratch, "transparency", 1.0f, 2.4f);
        tween.TweenCallback(Callable.From(markRoot.QueueFree));
    }

    private static ImmediateMesh BuildScratchMesh(
        MeleeImpactSurface surface, float length, float strokeWidth, float widthScale)
    {
        var mesh = new ImmediateMesh();
        if (length <= 0.001f || widthScale <= 0.001f)
        {
            return mesh;
        }
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, ScratchGrooveMaterial(surface));
        AddScratchStroke(mesh, length, strokeWidth, 0.0f, 0.025f, 0.0f, 0.0f, widthScale);
        AddScratchStroke(mesh, length * 0.79f, strokeWidth * 0.7f, 0.0f, -0.11f, strokeWidth * 1.35f, 0.0f, widthScale);
        AddScratchStroke(mesh, length * 0.66f, strokeWidth * 0.62f, -length * 0.08f, 0.13f, -strokeWidth * 1.35f, 0.0f, widthScale);
        mesh.SurfaceEnd();

        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, ScratchHighlightMaterial(surface));
        AddScratchStroke(mesh, length * 0.88f, strokeWidth * 0.58f, length * 0.012f, 0.025f, strokeWidth * 0.12f, ScratchHighlightDepth, widthScale);
        AddScratchStroke(mesh, length * 0.68f, strokeWidth * 0.46f, length * 0.015f, -0.11f, strokeWidth * 1.45f, ScratchHighlightDepth, widthScale);
        AddScratchStroke(mesh, length * 0.54f, strokeWidth * 0.42f, -length * 0.05f, 0.13f, -strokeWidth * 1.28f, ScratchHighlightDepth, widthScale);
        mesh.SurfaceEnd();
        return mesh;
    }

    private static void AddScratchStroke(
        ImmediateMesh mesh,
        float length,
        float width,
        float xOffset,
        float slope,
        float yOffset,
        float depth,
        float widthScale)
    {
        var start = new Vector2(
            -length * 0.5f + xOffset,
            (yOffset - slope * length * 0.5f) * widthScale);
        var end = new Vector2(
            length * 0.5f + xOffset,
            (yOffset + slope * length * 0.5f) * widthScale);
        var direction = (end - start).Normalized();
        var perpendicular = new Vector2(-direction.Y, direction.X)
            * (width * widthScale * 0.5f);
        var a = start - perpendicular;
        var b = start + perpendicular;
        var c = end + perpendicular;
        var d = end - perpendicular;
        AddScratchVertex(mesh, a, depth);
        AddScratchVertex(mesh, c, depth);
        AddScratchVertex(mesh, b, depth);
        AddScratchVertex(mesh, a, depth);
        AddScratchVertex(mesh, d, depth);
        AddScratchVertex(mesh, c, depth);
    }

    private static void AddScratchVertex(ImmediateMesh mesh, Vector2 point, float depth)
    {
        mesh.SurfaceSetNormal(Vector3.Back);
        mesh.SurfaceAddVertex(new Vector3(point.X, point.Y, depth));
    }

    private static StandardMaterial3D ScratchGrooveMaterial(MeleeImpactSurface surface)
        => ScratchGrooveMaterials[(int)surface];

    private static StandardMaterial3D ScratchHighlightMaterial(MeleeImpactSurface surface)
        => ScratchHighlightMaterials[(int)surface];

    private static StandardMaterial3D CreateScratchMaterial(
        Color color,
        float emission = 0.0f)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = color,
            Roughness = 0.9f,
            EmissionEnabled = emission > 0.0f,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = emission
        };
    }

    private ScratchPlacement ResolveScratchPlacement(
        Vector3 position,
        Vector3 normal,
        Vector3 tangent,
        Vector3 bitangent,
        GodotObject? collider,
        int shape,
        float length,
        float strokeWidth)
    {
        if (collider is null
            || !GodotObject.IsInstanceValid(collider)
            || !ScratchPointMatchesSurface(position, normal, collider, shape))
        {
            return new ScratchPlacement(position, 0.0f, 0.0f, true, false);
        }

        var halfLength = length * 0.5f;
        var halfWidth = ScratchFootprintHalfWidth(length, strokeWidth);
        var negativeLength = InsetScratchExtent(
            FindScratchSurfaceExtent(position, normal, -tangent, collider, shape, halfLength),
            halfLength);
        var positiveLength = InsetScratchExtent(
            FindScratchSurfaceExtent(position, normal, tangent, collider, shape, halfLength),
            halfLength);
        var negativeWidth = InsetScratchExtent(
            FindScratchSurfaceExtent(position, normal, -bitangent, collider, shape, halfWidth),
            halfWidth);
        var positiveWidth = InsetScratchExtent(
            FindScratchSurfaceExtent(position, normal, bitangent, collider, shape, halfWidth),
            halfWidth);

        var clippedLength = Mathf.Max(0.004f, negativeLength + positiveLength);
        var availableWidth = Mathf.Max(0.004f, negativeWidth + positiveWidth);
        var center = position
            + tangent * ((positiveLength - negativeLength) * 0.5f)
            + bitangent * ((positiveWidth - negativeWidth) * 0.5f);
        var widthScale = Mathf.Min(
            1.0f,
            availableWidth
                / Mathf.Max(0.004f, ScratchFootprintHalfWidth(clippedLength, strokeWidth) * 2.0f));

        if (!ScratchFootprintMatchesSurface(
                center,
                normal,
                tangent,
                bitangent,
                collider,
                shape,
                clippedLength,
                strokeWidth,
                widthScale))
        {
            center = position;
            clippedLength = Mathf.Max(0.004f, Mathf.Min(negativeLength, positiveLength) * 2.0f);
            availableWidth = Mathf.Max(0.004f, Mathf.Min(negativeWidth, positiveWidth) * 2.0f);
            widthScale = Mathf.Min(
                1.0f,
                availableWidth
                    / Mathf.Max(
                        0.004f,
                        ScratchFootprintHalfWidth(clippedLength, strokeWidth) * 2.0f));
        }

        var supported = false;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            supported = ScratchFootprintMatchesSurface(
                center,
                normal,
                tangent,
                bitangent,
                collider,
                shape,
                clippedLength,
                strokeWidth,
                widthScale);
            if (supported)
            {
                break;
            }
            clippedLength *= 0.72f;
            widthScale *= 0.72f;
        }

        var edgeClipped = clippedLength < length - 0.002f || widthScale < 0.98f;
        return new ScratchPlacement(
            center,
            supported ? clippedLength : 0.0f,
            supported ? widthScale : 0.0f,
            edgeClipped,
            supported);
    }

    private float FindScratchSurfaceExtent(
        Vector3 position,
        Vector3 normal,
        Vector3 direction,
        GodotObject collider,
        int shape,
        float maximumExtent)
    {
        var supportedExtent = 0.0f;
        var candidate = Mathf.Min(ScratchProbeStep, maximumExtent);
        while (candidate <= maximumExtent + 0.0001f)
        {
            if (!ScratchPointMatchesSurface(
                    position + direction * candidate,
                    normal,
                    collider,
                    shape))
            {
                var unsupportedExtent = candidate;
                for (var refinement = 0; refinement < ScratchProbeRefinementSteps; refinement++)
                {
                    var midpoint = (supportedExtent + unsupportedExtent) * 0.5f;
                    if (ScratchPointMatchesSurface(
                            position + direction * midpoint,
                            normal,
                            collider,
                            shape))
                    {
                        supportedExtent = midpoint;
                    }
                    else
                    {
                        unsupportedExtent = midpoint;
                    }
                }
                return supportedExtent;
            }

            supportedExtent = candidate;
            if (Mathf.IsEqualApprox(candidate, maximumExtent))
            {
                break;
            }
            candidate = Mathf.Min(candidate + ScratchProbeStep, maximumExtent);
        }
        return supportedExtent;
    }

    private static float InsetScratchExtent(float supportedExtent, float requestedExtent)
        => supportedExtent < requestedExtent - 0.0005f
            ? Mathf.Max(0.0f, supportedExtent - ScratchEdgeInset)
            : requestedExtent;

    private bool ScratchFootprintMatchesSurface(
        Vector3 center,
        Vector3 normal,
        Vector3 tangent,
        Vector3 bitangent,
        GodotObject collider,
        int shape,
        float length,
        float strokeWidth,
        float widthScale)
    {
        var halfLength = length * 0.5f;
        var halfWidth = ScratchFootprintHalfWidth(length, strokeWidth) * widthScale;
        return ScratchPointMatchesSurface(center, normal, collider, shape)
            && ScratchPointMatchesSurface(
                center - tangent * halfLength - bitangent * halfWidth,
                normal,
                collider,
                shape)
            && ScratchPointMatchesSurface(
                center - tangent * halfLength + bitangent * halfWidth,
                normal,
                collider,
                shape)
            && ScratchPointMatchesSurface(
                center + tangent * halfLength - bitangent * halfWidth,
                normal,
                collider,
                shape)
            && ScratchPointMatchesSurface(
                center + tangent * halfLength + bitangent * halfWidth,
                normal,
                collider,
                shape);
    }

    private bool ScratchPointMatchesSurface(
        Vector3 point,
        Vector3 normal,
        GodotObject collider,
        int shape)
    {
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                point + normal * ScratchProbeDepth,
                point - normal * ScratchProbeDepth,
                uint.MaxValue,
                out var hit,
                collideWithAreas: true,
                collideWithBodies: true)
            || hit.Collider is null
            || hit.Collider.GetInstanceId() != collider.GetInstanceId()
            || shape >= 0 && hit.Shape >= 0 && hit.Shape != shape)
        {
            return false;
        }
        return hit.Normal.Dot(normal) >= 0.94f
            && Mathf.Abs((hit.Position - point).Dot(normal)) <= 0.012f;
    }

    private static float ScratchFootprintHalfWidth(float length, float strokeWidth)
        => Mathf.Max(
            0.0125f * length + strokeWidth * 0.5f,
            Mathf.Max(
                0.04345f * length + strokeWidth * 1.7f,
                0.0429f * length + strokeWidth * 1.66f));

    private static (float Length, float Width) ScratchDimensions(MeleeWeaponStyle style)
        => style switch
        {
            MeleeWeaponStyle.ZhanmaDao => (0.62f, 0.016f),
            MeleeWeaponStyle.TianxuanDao => (0.52f, 0.014f),
            _ => (0.28f, 0.011f)
        };

    private static float SurfaceImpactVolume(MeleeImpactSurface surface)
        => surface switch
        {
            MeleeImpactSurface.Metal => 2.0f,
            MeleeImpactSurface.Wood => -1.0f,
            _ => 0.0f
        };

    private void SpawnMeleeSurfaceDebris(
        Vector3 position, Vector3 normal, Vector3 tangent, MeleeImpactSurface surface)
    {
        switch (surface)
        {
            case MeleeImpactSurface.Metal:
                SpawnMetalSlashSparks(position, normal, tangent);
                break;
            case MeleeImpactSurface.Wood:
                SpawnWoodSlashChips(position, normal, tangent);
                break;
            default:
                SpawnMasonrySlashDust(position, normal, tangent);
                break;
        }
    }

    private void SpawnMetalSlashSparks(Vector3 position, Vector3 normal, Vector3 tangent)
    {
        for (var index = 0; index < 7; index++)
        {
            var distance = _rng.RandfRange(0.18f, 0.56f);
            var sparkDirection = (normal * _rng.RandfRange(0.45f, 1.0f)
                + tangent * _rng.RandfRange(-0.72f, 0.72f)
                + Vector3.Up * _rng.RandfRange(-0.12f, 0.48f)).Normalized();
            var color = index % 3 == 0
                ? new Color(1.0f, 0.84f, 0.34f)
                : new Color(1.0f, 0.39f, 0.08f);
            SpawnTracer(position + normal * 0.024f, position + sparkDirection * distance, color);
        }
    }

    private void SpawnMasonrySlashDust(Vector3 position, Vector3 normal, Vector3 tangent)
    {
        for (var index = 0; index < 5; index++)
        {
            var dust = new MeshInstance3D
            {
                Name = "MeleeMasonryDust",
                Mesh = new SphereMesh
                {
                    Radius = _rng.RandfRange(0.024f, 0.05f),
                    Height = 0.085f,
                    RadialSegments = 6,
                    Rings = 3
                },
                MaterialOverride = MasonryDebrisMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(dust);
            dust.GlobalPosition = position + normal * 0.025f;
            var target = dust.Position
                + normal * _rng.RandfRange(0.16f, 0.34f)
                + tangent * _rng.RandfRange(-0.16f, 0.16f)
                + Vector3.Up * _rng.RandfRange(0.02f, 0.2f);
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(dust, "position", target, 0.52f);
            tween.TweenProperty(dust, "scale", Vector3.One * _rng.RandfRange(2.0f, 3.0f), 0.52f);
            tween.TweenProperty(dust, "transparency", 1.0f, 0.56f);
            tween.Chain().TweenCallback(Callable.From(dust.QueueFree));
        }
    }

    private void SpawnWoodSlashChips(Vector3 position, Vector3 normal, Vector3 tangent)
    {
        for (var index = 0; index < 6; index++)
        {
            var chip = new MeshInstance3D
            {
                Name = "MeleeWoodChip",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(
                        _rng.RandfRange(0.025f, 0.07f),
                        _rng.RandfRange(0.005f, 0.012f),
                        _rng.RandfRange(0.006f, 0.014f))
                },
                MaterialOverride = WoodDebrisMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(chip);
            chip.GlobalPosition = position + normal * 0.026f;
            var target = chip.Position
                + normal * _rng.RandfRange(0.14f, 0.36f)
                + tangent * _rng.RandfRange(-0.25f, 0.25f)
                + Vector3.Up * _rng.RandfRange(-0.04f, 0.24f);
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(chip, "position", target, 0.48f);
            var rotation = new Vector3(
                _rng.RandfRange(-2.4f, 2.4f), _rng.RandfRange(-2.4f, 2.4f),
                _rng.RandfRange(-2.4f, 2.4f));
            tween.TweenProperty(chip, "rotation", rotation, 0.48f);
            tween.TweenProperty(chip, "transparency", 1.0f, 0.52f);
            tween.Chain().TweenCallback(Callable.From(chip.QueueFree));
        }
    }

    private static StandardMaterial3D CreateTransientSurfaceMaterial(Color color)
        => new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = color
        };

    private static Vector3 ResolveImpactNormal(Vector3 normal, Vector3 bladeTravel)
    {
        if (normal.LengthSquared() > 0.000001f)
        {
            return normal.Normalized();
        }
        if (bladeTravel.LengthSquared() > 0.000001f)
        {
            return -bladeTravel.Normalized();
        }
        return Vector3.Back;
    }

    private static Vector3 ResolveScratchTangent(Vector3 normal, Vector3 bladeTravel)
    {
        var tangent = bladeTravel.Slide(normal);
        if (tangent.LengthSquared() > 0.000001f)
        {
            return tangent.Normalized();
        }
        var reference = Mathf.Abs(normal.Dot(Vector3.Up)) < 0.92f ? Vector3.Up : Vector3.Right;
        return reference.Cross(normal).Normalized();
    }

    private Node3D ResolveMeleeImpactAnchor(GodotObject? collider)
    {
        if (collider is Node node)
        {
            for (Node? current = node; current is not null; current = current.GetParent())
            {
                if (current is Node3D node3D && node3D.IsInsideTree())
                {
                    return node3D;
                }
            }
        }
        return this;
    }
}
