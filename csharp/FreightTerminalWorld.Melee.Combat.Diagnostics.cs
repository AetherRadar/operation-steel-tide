using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct MeleeCombatDiagnostic(
        bool MultiTarget,
        bool TargetDeduplicated,
        bool PersistentRidSweep,
        bool WallBlocked,
        bool WallFeedback,
        bool ImpactDeduplicated,
        bool SurfaceProfilesDistinct,
        bool NearestHardTargetFeedback,
        bool ScratchContained,
        bool ScratchSurfaceHugging,
        bool ScratchFollowsCollider,
        bool ScratchProbeFailClosed,
        bool SuppressedWallFeedback,
        bool SuppressedWallPresentationOnly,
        bool SuppressedWallBlocked,
        int SuppressedImpactCount,
        int HitTargets)
    {
        public bool Valid => MultiTarget
            && TargetDeduplicated
            && PersistentRidSweep
            && WallBlocked
            && WallFeedback
            && ImpactDeduplicated
            && SurfaceProfilesDistinct
            && NearestHardTargetFeedback
            && ScratchContained
            && ScratchSurfaceHugging
            && ScratchFollowsCollider
            && ScratchProbeFailClosed
            && SuppressedWallFeedback
            && SuppressedWallPresentationOnly
            && SuppressedWallBlocked;
    }

    private async void ValidateMeleeImpact()
    {
        var combat = default(MeleeCombatDiagnostic);
        var failure = string.Empty;
        try
        {
            _player.UiLocked = false;
            _player.IsDead = false;
            EquipTianxuanForMeleeImpactDiagnostics();
            _player.SelectQuickSlot(PlayerQuickSlot.Melee, notify: false);
            await WaitFrames(8);
            combat = await ValidateMeleeCombatSemantics();
        }
        catch (System.Exception exception)
        {
            failure = $"{exception.GetType().Name}:{exception.Message}";
            GD.PushError($"Melee impact validation failed: {exception}");
        }
        var valid = string.IsNullOrEmpty(failure) && combat.Valid;
        GD.Print(
            $"MELEE_IMPACT_CHECK valid={valid} combat={FormatMeleeCombat(combat)} "
            + $"failure={(string.IsNullOrEmpty(failure) ? "none" : failure.Replace(' ', '_'))}");
        GD.Print($"MELEE_IMPACT_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async Task<MeleeCombatDiagnostic> ValidateMeleeCombatSemantics()
    {
        const float fixtureFloorY = 80.2f;
        const float bladeY = 81.08f;
        var fixtures = _enemies
            .Where(enemy => IsInstanceValid(enemy) && !enemy.IsWorldBoss)
            .Take(3)
            .ToArray();
        if (fixtures.Length < 3)
        {
            return default;
        }

        for (var index = 0; index < _enemies.Count; index++)
        {
            var enemy = _enemies[index];
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.SetPhysicsProcess(false);
            if (!fixtures.Contains(enemy))
            {
                enemy.GlobalPosition = new Vector3(220.0f + index, 70.0f, 220.0f);
            }
        }
        for (var index = 0; index < _squadMates.Count; index++)
        {
            var mate = _squadMates[index];
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.SetProcess(false);
            mate.SetPhysicsProcess(false);
            mate.GlobalPosition = new Vector3(240.0f + index, 70.0f, 240.0f);
        }

        _player.PrepareMeleeCombatFixtureForDiagnostics();
        _player.GlobalPosition = new Vector3(0.0f, fixtureFloorY, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, fixtureFloorY, 30.0f));
        PrepareMeleeFixture(fixtures[0], new Vector3(-0.42f, fixtureFloorY, 38.8f));
        PrepareMeleeFixture(fixtures[1], new Vector3(0.42f, fixtureFloorY, 38.8f));
        fixtures[2].GlobalPosition = new Vector3(220.0f, 70.0f, 220.0f);
        await WaitFrames(4);

        var firstHealth = fixtures[0].CurrentHealth;
        var secondHealth = fixtures[1].CurrentHealth;
        var hitTargets = _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-1.0f, bladeY, 39.3f),
            new Vector3(-1.0f, bladeY, 38.3f),
            new Vector3(1.0f, bladeY, 39.3f),
            new Vector3(1.0f, bladeY, 38.3f),
            beginSwing: true);
        var multiTarget = fixtures[0].CurrentHealth < firstHealth
            && fixtures[1].CurrentHealth < secondHealth
            && hitTargets == 2;
        var firstAfter = fixtures[0].CurrentHealth;
        var secondAfter = fixtures[1].CurrentHealth;
        var repeatedHitTargets = _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-1.0f, bladeY, 39.3f),
            new Vector3(-1.0f, bladeY, 38.3f),
            new Vector3(1.0f, bladeY, 39.3f),
            new Vector3(1.0f, bladeY, 38.3f),
            beginSwing: false);
        var targetDeduplicated = Mathf.IsEqualApprox(fixtures[0].CurrentHealth, firstAfter)
            && Mathf.IsEqualApprox(fixtures[1].CurrentHealth, secondAfter)
            && repeatedHitTargets == hitTargets;

        PrepareMeleeFixture(fixtures[0], new Vector3(-0.55f, fixtureFloorY, 38.8f));
        PrepareMeleeFixture(fixtures[1], new Vector3(0.95f, fixtureFloorY, 38.8f));
        fixtures[2].GlobalPosition = new Vector3(220.0f, 70.0f, 220.0f);
        await WaitFrames(4);
        var frontHealth = fixtures[0].CurrentHealth;
        var rearHealth = fixtures[1].CurrentHealth;
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_zhanma",
            2,
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(-0.12f, bladeY, 38.8f),
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(-0.12f, bladeY, 38.8f),
            beginSwing: true);
        var frontAfter = fixtures[0].CurrentHealth;
        var frontOnly = frontAfter < frontHealth
            && Mathf.IsEqualApprox(fixtures[1].CurrentHealth, rearHealth);
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_zhanma",
            2,
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(1.35f, bladeY, 38.8f),
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(1.35f, bladeY, 38.8f),
            beginSwing: false);
        var persistentRidSweep = frontOnly
            && Mathf.IsEqualApprox(fixtures[0].CurrentHealth, frontAfter)
            && fixtures[1].CurrentHealth < rearHealth;
        fixtures[0].GlobalPosition = new Vector3(221.0f, 70.0f, 220.0f);
        fixtures[1].GlobalPosition = new Vector3(222.0f, 70.0f, 220.0f);
        PrepareMeleeFixture(fixtures[2], new Vector3(0.0f, fixtureFloorY, 37.15f));
        var wall = CreateMeleeWallFixture(
            new Vector3(0.0f, fixtureFloorY + 0.95f, 38.0f),
            "masonry");
        AddChild(wall);
        await WaitFrames(4);

        var protectedHealth = fixtures[2].CurrentHealth;
        ResetMeleeSurfaceImpactDiagnostics();
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-0.9f, bladeY, 37.55f),
            new Vector3(-0.9f, bladeY, 36.75f),
            new Vector3(0.9f, bladeY, 37.55f),
            new Vector3(0.9f, bladeY, 36.75f),
            beginSwing: true);
        var wallBlocked = Mathf.IsEqualApprox(fixtures[2].CurrentHealth, protectedHealth);
        var masonrySignature = SoundLab.MeleeSurfaceImpactSignatureForDiagnostics(
            MeleeImpactSurface.Masonry,
            MeleeWeaponStyle.TianxuanDao);
        var wallFeedback = MeleeSurfaceImpactCountForDiagnostics == 1
            && MeleeSurfaceMarkCountForDiagnostics == 1
            && MeleeSurfaceAudioCountForDiagnostics == 1
            && LastMeleeImpactSurfaceForDiagnostics == MeleeImpactSurface.Masonry
            && LastMeleeScratchLengthForDiagnostics >= 0.18f
            && LastMeleeScratchLengthForDiagnostics
                / Mathf.Max(0.001f, LastMeleeScratchWidthForDiagnostics) >= 4.0f
            && LastMeleeImpactNormalForDiagnostics.Dot(Vector3.Back) >= 0.98f
            && Mathf.Abs(
                LastMeleeImpactPositionForDiagnostics.Z
                - (wall.GlobalPosition.Z + 0.11f)) <= 0.06f
            && LastMeleeImpactAudioLengthForDiagnostics is >= 0.2f and <= 0.8f
            && LastMeleeImpactAudioStartedForDiagnostics;
        var directImpactCount = MeleeSurfaceImpactCountForDiagnostics;
        var directMarkCount = MeleeSurfaceMarkCountForDiagnostics;
        var directAudioCount = MeleeSurfaceAudioCountForDiagnostics;
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-0.9f, bladeY, 37.55f),
            new Vector3(-0.9f, bladeY, 36.75f),
            new Vector3(0.9f, bladeY, 37.55f),
            new Vector3(0.9f, bladeY, 36.75f),
            beginSwing: false);
        var impactDeduplicated = MeleeSurfaceImpactCountForDiagnostics == directImpactCount
            && MeleeSurfaceMarkCountForDiagnostics == directMarkCount
            && MeleeSurfaceAudioCountForDiagnostics == directAudioCount;

        wall.SetMeta("melee_surface", "metal");
        ResetMeleeSurfaceImpactDiagnostics();
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-0.9f, bladeY, 37.55f),
            new Vector3(-0.9f, bladeY, 36.75f),
            new Vector3(0.9f, bladeY, 37.55f),
            new Vector3(0.9f, bladeY, 36.75f),
            beginSwing: true);
        var metalSignature = SoundLab.MeleeSurfaceImpactSignatureForDiagnostics(
            MeleeImpactSurface.Metal,
            MeleeWeaponStyle.TianxuanDao);
        var surfaceProfilesDistinct = MeleeSurfaceImpactCountForDiagnostics == 1
            && LastMeleeImpactSurfaceForDiagnostics == MeleeImpactSurface.Metal
            && masonrySignature != metalSignature;

        wall.SetMeta("melee_surface", "masonry");
        var barrel = new ExplosiveBarrel
        {
            Name = "MeleeNearestHardTargetDiagnostic",
            Main = this,
            Position = new Vector3(0.0f, fixtureFloorY + 0.3f, 38.75f)
        };
        AddChild(barrel);
        await WaitFrames(3);
        ResetMeleeSurfaceImpactDiagnostics();
        var hardTargetPathReached = _player.ResolveSuppressedMeleeWallContactForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-0.72f, bladeY, 37.58f),
            new Vector3(-0.72f, bladeY, 36.78f),
            new Vector3(0.72f, bladeY, 37.58f),
            new Vector3(0.72f, bladeY, 36.78f));
        var nearestHardTargetFeedback = hardTargetPathReached
            && MeleeSurfaceImpactCountForDiagnostics == 1
            && LastMeleeImpactSurfaceForDiagnostics == MeleeImpactSurface.Metal
            && LastMeleeImpactPositionForDiagnostics.Z >= 38.35f
            && LastMeleeImpactAttachedToColliderForDiagnostics
            && !barrel.Exploded;
        barrel.QueueFree();
        await WaitFrames(2);

        wall.SetMeta("melee_surface", "metal");
        PrepareMeleeFixture(fixtures[2], new Vector3(0.0f, fixtureFloorY, 37.15f));
        _player.PrepareMeleeCombatFixtureForDiagnostics();
        _player.GlobalPosition = new Vector3(0.0f, fixtureFloorY, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, fixtureFloorY, 30.0f));
        await WaitFrames(2);
        ResetMeleeSurfaceImpactDiagnostics();
        _player.ResolveSuppressedMeleeWallContactForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-0.72f, bladeY, 37.58f),
            new Vector3(-0.72f, bladeY, 36.78f),
            new Vector3(0.72f, bladeY, 37.58f),
            new Vector3(0.72f, bladeY, 36.78f));
        await WaitFrames(1);
        SaveViewportImage("res://melee_surface_impact_validation.png");
        _player.HideMeleePresentationForDiagnostics();
        await WaitFrames(40);
        SaveViewportImage("res://melee_surface_scratch_validation.png");
        wall.QueueFree();
        await WaitFrames(2);

        var narrowPost = CreateMeleeNarrowPostFixture(
            new Vector3(2.0f, fixtureFloorY + 0.95f, 38.0f),
            "metal");
        AddChild(narrowPost);
        await WaitFrames(2);
        ResetMeleeSurfaceImpactDiagnostics();
        var narrowImpactPosition = narrowPost.GlobalPosition
            + Vector3.Right * 0.055f
            + Vector3.Back * 0.11f;
        SpawnMeleeSurfaceImpact(
            narrowImpactPosition,
            Vector3.Back,
            Vector3.Right,
            narrowPost,
            0,
            MeleeWeaponStyle.ZhanmaDao);
        var scratchRoot = _meleeSurfaceMarks.LastOrDefault(mark => IsInstanceValid(mark));
        var scratchMesh = scratchRoot?.GetNodeOrNull<MeshInstance3D>("MultiStrokeScratch");
        var scratchContained = scratchRoot is not null
            && scratchMesh?.Mesh is not null
            && LastMeleeScratchEdgeClippedForDiagnostics
            && LastMeleeScratchSurfaceSupportedForDiagnostics
            && ScratchVerticesFitNarrowPost(scratchRoot, scratchMesh);
        var scratchSurfaceHugging = LastMeleeScratchSurfaceOffsetForDiagnostics <= 0.002f
            && ScratchGrooveMaterials.All(material =>
                material.ShadingMode == BaseMaterial3D.ShadingModeEnum.PerPixel
                && material.Transparency == BaseMaterial3D.TransparencyEnum.Alpha)
            && ScratchHighlightMaterials.All(material =>
                material.ShadingMode == BaseMaterial3D.ShadingModeEnum.PerPixel
                && material.Transparency == BaseMaterial3D.TransparencyEnum.Alpha
                && material.EmissionEnergyMultiplier <= 0.08f);
        var scratchFollowsCollider = false;
        if (scratchRoot is not null)
        {
            var markPositionBeforeMove = scratchRoot.GlobalPosition;
            var markLocalTransformBeforeMove = scratchRoot.Transform;
            var postMove = new Vector3(0.45f, 0.18f, -0.25f);
            narrowPost.GlobalPosition += postMove;
            await WaitFrames(2);
            scratchFollowsCollider = scratchRoot.GlobalPosition.DistanceTo(
                    markPositionBeforeMove + postMove) <= 0.002f
                && scratchRoot.Transform.IsEqualApprox(markLocalTransformBeforeMove);
        }

        ResetMeleeSurfaceImpactDiagnostics();
        await WaitFrames(1);
        narrowImpactPosition = narrowPost.GlobalPosition
            + Vector3.Right * 0.055f
            + Vector3.Back * 0.11f;
        SpawnMeleeSurfaceImpact(
            narrowImpactPosition,
            Vector3.Back,
            Vector3.Right,
            null,
            -1,
            MeleeWeaponStyle.ZhanmaDao);
        var unsupportedScratchRoot = _meleeSurfaceMarks.LastOrDefault(mark => IsInstanceValid(mark));
        var unsupportedScratchMesh = unsupportedScratchRoot?.GetNodeOrNull<MeshInstance3D>(
            "MultiStrokeScratch");
        var scratchProbeFailClosed = !LastMeleeScratchSurfaceSupportedForDiagnostics
            && Mathf.IsZeroApprox(LastMeleeScratchLengthForDiagnostics)
            && unsupportedScratchMesh?.Mesh is { } unsupportedMesh
            && unsupportedMesh.GetSurfaceCount() == 0;
        ResetMeleeSurfaceImpactDiagnostics();
        narrowPost.QueueFree();
        await WaitFrames(2);

        _player.PrepareMeleeCombatFixtureForDiagnostics();
        _player.GlobalPosition = new Vector3(0.0f, fixtureFloorY, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, fixtureFloorY, 30.0f));
        PrepareMeleeFixture(fixtures[2], new Vector3(0.0f, fixtureFloorY, 37.15f));
        var clearanceCage = CreateMeleeClearanceCage(
            _player.DiagnosticCameraPosition,
            "metal");
        AddChild(clearanceCage);
        await WaitFrames(3);
        ResetMeleeSurfaceImpactDiagnostics();
        var closeWallProtectedHealth = fixtures[2].CurrentHealth;
        var clearanceSuppressed = false;
        var attackStarted = false;
        var attackFinished = false;
        _player.SetPhysicsProcess(true);
        _player.StartMeleeAttackForDiagnostics();
        for (var frame = 0; frame < 90; frame++)
        {
            _player.GlobalPosition = new Vector3(0.0f, fixtureFloorY, 40.0f);
            _player.Velocity = Vector3.Zero;
            await WaitFrames(1);
            attackStarted |= _player.MeleeAttackActiveForDiagnostics;
            clearanceSuppressed |= _player.MeleeClearanceSuppressedForDiagnostics;
            if (attackStarted && !_player.MeleeAttackActiveForDiagnostics)
            {
                attackFinished = true;
                break;
            }
        }
        _player.SetPhysicsProcess(false);
        var closeWallHealthAfterContact = fixtures[2].CurrentHealth;
        var suppressedImpactCount = MeleeSurfaceImpactCountForDiagnostics;
        var suppressedWallFeedback = attackStarted
            && attackFinished
            && clearanceSuppressed
            && suppressedImpactCount == 1
            && MeleeSurfaceMarkCountForDiagnostics == 1
            && MeleeSurfaceAudioCountForDiagnostics == 1
            && LastMeleeImpactSurfaceForDiagnostics == MeleeImpactSurface.Metal;
        var suppressedWallPresentationOnly = !_player.MeleeBladeSweepResolvedForDiagnostics;
        var suppressedWallBlocked = Mathf.IsEqualApprox(
            closeWallHealthAfterContact,
            closeWallProtectedHealth);
        clearanceCage.QueueFree();
        return new MeleeCombatDiagnostic(
            multiTarget,
            targetDeduplicated,
            persistentRidSweep,
            wallBlocked,
            wallFeedback,
            impactDeduplicated,
            surfaceProfilesDistinct,
            nearestHardTargetFeedback,
            scratchContained,
            scratchSurfaceHugging,
            scratchFollowsCollider,
            scratchProbeFailClosed,
            suppressedWallFeedback,
            suppressedWallPresentationOnly,
            suppressedWallBlocked,
            suppressedImpactCount,
            hitTargets);
    }

    private static void PrepareMeleeFixture(EnemyOperator enemy, Vector3 position)
    {
        enemy.ResetTacticalStateForDiagnostics();
        enemy.GlobalPosition = position;
        enemy.Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f);
        enemy.Velocity = Vector3.Zero;
        enemy.ProcessMode = ProcessModeEnum.Inherit;
        enemy.SetProcess(false);
        enemy.SetPhysicsProcess(false);
    }

    private static StaticBody3D CreateMeleeWallFixture(
        Vector3 position,
        string surface)
    {
        var wall = new StaticBody3D
        {
            Name = $"Melee{surface}WallDiagnostic",
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        wall.SetMeta("melee_surface", surface);
        wall.AddChild(new CollisionShape3D
        {
            Name = "MeleeWallShape",
            Shape = new BoxShape3D { Size = new Vector3(3.0f, 2.3f, 0.22f) }
        });
        wall.AddChild(new MeshInstance3D
        {
            Name = "MeleeWallVisual",
            Mesh = new BoxMesh { Size = new Vector3(3.0f, 2.3f, 0.22f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("26353e"),
                Metallic = surface == "metal" ? 0.72f : 0.08f,
                Roughness = surface == "metal" ? 0.34f : 0.86f
            }
        });
        return wall;
    }

    private static StaticBody3D CreateMeleeClearanceCage(
        Vector3 position,
        string surface)
    {
        const float offset = 0.12f;
        const float thickness = 0.025f;
        const float span = 1.4f;
        var cage = new StaticBody3D
        {
            Name = "MeleeClearanceCageDiagnostic",
            Position = position,
            CollisionLayer = 1u << 29,
            CollisionMask = 0
        };
        cage.SetMeta("melee_surface", surface);
        AddCagePanel("Front", new Vector3(0.0f, 0.0f, -offset), new Vector3(span, span, thickness));
        AddCagePanel("Rear", new Vector3(0.0f, 0.0f, offset), new Vector3(span, span, thickness));
        AddCagePanel("Left", new Vector3(-offset, 0.0f, 0.0f), new Vector3(thickness, span, span));
        AddCagePanel("Right", new Vector3(offset, 0.0f, 0.0f), new Vector3(thickness, span, span));
        AddCagePanel("Top", new Vector3(0.0f, offset, 0.0f), new Vector3(span, thickness, span));
        AddCagePanel("Bottom", new Vector3(0.0f, -offset, 0.0f), new Vector3(span, thickness, span));
        return cage;

        void AddCagePanel(string suffix, Vector3 panelPosition, Vector3 size)
        {
            cage.AddChild(new CollisionShape3D
            {
                Name = $"MeleeClearanceCage{suffix}",
                Position = panelPosition,
                Shape = new BoxShape3D { Size = size }
            });
        }
    }

    private static StaticBody3D CreateMeleeNarrowPostFixture(
        Vector3 position,
        string surface)
    {
        var size = new Vector3(0.14f, 2.3f, 0.22f);
        var post = new StaticBody3D
        {
            Name = "MeleeNarrowPostDiagnostic",
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        post.SetMeta("melee_surface", surface);
        post.AddChild(new CollisionShape3D
        {
            Name = "MeleeNarrowPostShape",
            Shape = new BoxShape3D { Size = size }
        });
        post.AddChild(new MeshInstance3D
        {
            Name = "MeleeNarrowPostVisual",
            Mesh = new BoxMesh { Size = size }
        });
        return post;
    }

    private static bool ScratchVerticesFitNarrowPost(
        Node3D scratchRoot,
        MeshInstance3D scratchMesh)
    {
        const float halfWidth = 0.07f;
        const float halfHeight = 1.15f;
        const float frontSurface = 0.11f;
        var vertexCount = 0;
        for (var surface = 0; surface < scratchMesh.Mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = scratchMesh.Mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            foreach (var vertex in vertices)
            {
                var postLocalVertex = scratchRoot.Transform * (scratchMesh.Transform * vertex);
                if (Mathf.Abs(postLocalVertex.X) > halfWidth + 0.001f
                    || Mathf.Abs(postLocalVertex.Y) > halfHeight + 0.001f
                    || postLocalVertex.Z < frontSurface
                    || postLocalVertex.Z > frontSurface + 0.002f)
                {
                    return false;
                }
                vertexCount++;
            }
        }
        return vertexCount > 0;
    }

    private void EquipTianxuanForMeleeImpactDiagnostics()
    {
        if (string.Equals(
                _player.EquippedKnifeSkinId,
                "knife_tianxuan",
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.KnifeSkin,
            KnifeSkinId = "knife_tianxuan",
            Grade = LootGrade.Legendary
        });
    }

    private static string FormatMeleeCombat(MeleeCombatDiagnostic combat)
        => $"valid:{combat.Valid};multi:{combat.MultiTarget};"
            + $"dedupe:{combat.TargetDeduplicated};persistent:{combat.PersistentRidSweep};"
            + $"wall:{combat.WallBlocked};feedback:{combat.WallFeedback};"
            + $"impact_dedupe:{combat.ImpactDeduplicated};surfaces:{combat.SurfaceProfilesDistinct};"
            + $"nearest_hard_target:{combat.NearestHardTargetFeedback};"
            + $"scratch_contained:{combat.ScratchContained};"
            + $"scratch_surface_hugging:{combat.ScratchSurfaceHugging};"
            + $"scratch_follows:{combat.ScratchFollowsCollider};"
            + $"scratch_probe_fail_closed:{combat.ScratchProbeFailClosed};"
            + $"suppressed_feedback:{combat.SuppressedWallFeedback};"
            + $"suppressed_presentation_only:{combat.SuppressedWallPresentationOnly};"
            + $"suppressed_blocked:{combat.SuppressedWallBlocked};"
            + $"suppressed_impacts:{combat.SuppressedImpactCount};"
            + $"targets:{combat.HitTargets}";
}
