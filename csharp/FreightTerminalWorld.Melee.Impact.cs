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
    private static readonly StandardMaterial3D[] ScratchGrooveMaterials = [
        CreateScratchMaterial(new Color(0.07f, 0.065f, 0.058f, 0.92f)),
        CreateScratchMaterial(new Color(0.12f, 0.15f, 0.17f, 0.96f)),
        CreateScratchMaterial(new Color(0.14f, 0.065f, 0.025f, 0.94f))
    ];
    private static readonly StandardMaterial3D[] ScratchHighlightMaterials = [
        CreateScratchMaterial(new Color(0.72f, 0.69f, 0.62f), 0.2f, transparent: false),
        CreateScratchMaterial(new Color(0.86f, 0.93f, 0.98f), 1.25f, transparent: false),
        CreateScratchMaterial(new Color(0.82f, 0.52f, 0.24f), 0.18f, transparent: false)
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
        var overallWidth = strokeWidth * 3.4f;
        var anchor = ResolveMeleeImpactAnchor(collider);

        var markRoot = new Node3D { Name = "MeleeBladeScratch" };
        markRoot.AddToGroup(MeleeSurfaceMarkGroup);
        markRoot.SetMeta(MeleeSurfaceMetadataKey, surface.ToString().ToLowerInvariant());
        markRoot.SetMeta("scratch_length", length);
        markRoot.SetMeta("scratch_width", overallWidth);
        markRoot.SetMeta("impact_shape", shape);
        anchor.AddChild(markRoot);
        markRoot.GlobalTransform = new Transform3D(
            new Basis(tangent, bitangent, impactNormal),
            position + impactNormal * 0.014f);

        var scratch = new MeshInstance3D
        {
            Name = "MultiStrokeScratch",
            Mesh = BuildScratchMesh(surface, length, strokeWidth),
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
        _lastMeleeScratchLength = length;
        _lastMeleeScratchWidth = overallWidth;
        _lastMeleeImpactNormal = impactNormal;
        _lastMeleeImpactPosition = position;
        _lastMeleeImpactAudioLength = impactStream.GetLength();
        _lastMeleeImpactAudioStarted = impactAudio.Playing;
        _lastMeleeImpactAttachedToCollider = anchor != this;
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
        MeleeImpactSurface surface, float length, float strokeWidth)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, ScratchGrooveMaterial(surface));
        AddScratchStroke(mesh, length, strokeWidth, 0.0f, 0.025f, 0.0f, 0.0f);
        AddScratchStroke(mesh, length * 0.79f, strokeWidth * 0.7f, 0.0f, -0.11f, strokeWidth * 1.35f, 0.0f);
        AddScratchStroke(mesh, length * 0.66f, strokeWidth * 0.62f, -length * 0.08f, 0.13f, -strokeWidth * 1.35f, 0.0f);
        mesh.SurfaceEnd();

        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, ScratchHighlightMaterial(surface));
        AddScratchStroke(mesh, length * 0.88f, strokeWidth * 0.58f, length * 0.012f, 0.025f, strokeWidth * 0.12f, 0.003f);
        AddScratchStroke(mesh, length * 0.68f, strokeWidth * 0.46f, length * 0.015f, -0.11f, strokeWidth * 1.45f, 0.003f);
        AddScratchStroke(mesh, length * 0.54f, strokeWidth * 0.42f, -length * 0.05f, 0.13f, -strokeWidth * 1.28f, 0.003f);
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
        float depth)
    {
        var start = new Vector2(-length * 0.5f + xOffset, yOffset - slope * length * 0.5f);
        var end = new Vector2(length * 0.5f + xOffset, yOffset + slope * length * 0.5f);
        var direction = (end - start).Normalized();
        var perpendicular = new Vector2(-direction.Y, direction.X) * (width * 0.5f);
        var a = start - perpendicular;
        var b = start + perpendicular;
        var c = end + perpendicular;
        var d = end - perpendicular;
        mesh.SurfaceAddVertex(new Vector3(a.X, a.Y, depth));
        mesh.SurfaceAddVertex(new Vector3(b.X, b.Y, depth));
        mesh.SurfaceAddVertex(new Vector3(c.X, c.Y, depth));
        mesh.SurfaceAddVertex(new Vector3(a.X, a.Y, depth));
        mesh.SurfaceAddVertex(new Vector3(c.X, c.Y, depth));
        mesh.SurfaceAddVertex(new Vector3(d.X, d.Y, depth));
    }

    private static StandardMaterial3D ScratchGrooveMaterial(MeleeImpactSurface surface)
        => ScratchGrooveMaterials[(int)surface];

    private static StandardMaterial3D ScratchHighlightMaterial(MeleeImpactSurface surface)
        => ScratchHighlightMaterials[(int)surface];

    private static StandardMaterial3D CreateScratchMaterial(
        Color color,
        float emission = 0.0f,
        bool transparent = true)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = transparent
                ? BaseMaterial3D.TransparencyEnum.Alpha
                : BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = color,
            EmissionEnabled = emission > 0.0f,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = emission
        };
    }

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
