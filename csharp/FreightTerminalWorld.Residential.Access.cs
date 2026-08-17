using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int ResidentialSkybridgeAccessStepCount = 36;
    private const int ResidentialSkybridgeAccessTransitionStepCount = 4;
    private const float ResidentialSkybridgeAccessRun = 11.52f;
    private const float ResidentialSkybridgeAccessStairOffset = 1.9f;
    private const float ResidentialSkybridgeAccessTreadWidth = 2.1f;
    private const float ResidentialSkybridgeAccessTreadThickness = 0.14f;
    private const float ResidentialSkybridgeAccessPlatformDepth = 2.6f;
    private const float ResidentialSkybridgeAccessVisualSurfaceInset = 0.03f;
    private const float ResidentialSkybridgeAccessSurfaceY = 2.0f * ResidentialFloorHeight + 0.18f;
    private const float ResidentialSkybridgeAccessGroundY = 0.1f;

    private sealed record ResidentialSkybridgeAccessRoute(
        int TowerIndex,
        int Side,
        Node3D Root,
        StaticBody3D CollisionBody,
        Vector3 BottomFeet,
        Vector3 MidFeet,
        Vector3 StairTopFeet,
        Vector3 LandingFeet,
        Vector3 PlatformFeet,
        Vector3 BridgeFeet,
        Vector3 Outward,
        Vector3 ClimbDirection,
        int StepCount,
        int VisualInstanceCount);

    private readonly List<ResidentialSkybridgeAccessRoute> _residentialSkybridgeAccesses = new();
    private readonly Dictionary<int, int> _residentialSkybridgeAccessPartners = new();

    public int ResidentialSkybridgeAccessCount => _residentialSkybridgeAccesses.Count;

    private void PlanResidentialSkybridgeAccesses()
    {
        _residentialSkybridgeAccessPartners.Clear();
        foreach (var link in ResidentialSkyLinks)
        {
            if (!link.Floors.Contains(2))
            {
                continue;
            }
            PlanResidentialSkybridgeAccessEndpoint(link.From, link.To);
            PlanResidentialSkybridgeAccessEndpoint(link.To, link.From);
        }
    }

    private void PlanResidentialSkybridgeAccessEndpoint(int towerIndex, int otherTowerIndex)
    {
        if (_residentialSkybridgeAccessPartners.ContainsKey(towerIndex))
        {
            return;
        }
        var side = ResidentialLinkSide(
            ResidentialTowerSpecs[towerIndex],
            ResidentialTowerSpecs[otherTowerIndex]);
        if (side <= 1)
        {
            _residentialSkybridgeAccessPartners[towerIndex] = otherTowerIndex;
        }
    }

    private bool HasResidentialSkybridgeAccessEndpoint(int towerIndex, int otherTowerIndex)
    {
        return _residentialSkybridgeAccessPartners.TryGetValue(towerIndex, out var partner)
            && partner == otherTowerIndex;
    }

    private int ResidentialSkybridgeAccessSillSide(
        int towerIndex,
        int otherTowerIndex,
        Vector3 bridgeDirection,
        float doorZ)
    {
        var spec = ResidentialTowerSpecs[towerIndex];
        var side = ResidentialLinkSide(spec, ResidentialTowerSpecs[otherTowerIndex]);
        var sign = side == 0 ? 1.0f : -1.0f;
        var wallX = sign * spec.Footprint.X * 0.5f;
        var stairTop = new Vector3(
            wallX + sign * ResidentialSkybridgeAccessStairOffset,
            ResidentialSkybridgeAccessSurfaceY,
            doorZ - ResidentialSkybridgeAccessPlatformDepth * 0.5f - 0.22f);
        var anchor = ResidentialLinkAnchor(
            spec,
            side,
            2.0f * ResidentialFloorHeight,
            doorZ);
        var offset = _residentialTowers[towerIndex].GlobalBasis * (stairTop - anchor);
        var bridgeLateral = new Vector3(bridgeDirection.Z, 0.0f, -bridgeDirection.X).Normalized();
        return offset.Dot(bridgeLateral) < 0.0f ? -1 : 1;
    }

    private void BuildResidentialSkybridgeAccessStairs()
    {
        _residentialSkybridgeAccesses.Clear();
        var visual = Mat(
            "residential_skybridge_access_visual",
            Colors.White,
            0.62f,
            0.42f);
        visual.VertexColorUseAsAlbedo = true;

        foreach (var assignment in _residentialSkybridgeAccessPartners.OrderBy(pair => pair.Key))
        {
            AddResidentialSkybridgeAccessIfNeeded(
                assignment.Key,
                assignment.Value,
                visual);
        }
    }

    private void AddResidentialSkybridgeAccessIfNeeded(
        int towerIndex,
        int otherTowerIndex,
        Godot.Material visual)
    {
        var spec = ResidentialTowerSpecs[towerIndex];
        var other = ResidentialTowerSpecs[otherTowerIndex];
        var side = ResidentialLinkSide(spec, other);
        // Skyway doors are intentionally on the east/west facades. Keep this guard so a
        // future north/south link cannot silently create a stair with the wrong tangent.
        if (side > 1 || !_residentialLinkSlots.TryGetValue(towerIndex, out var slots)
            || !slots.TryGetValue(side, out var slot))
        {
            return;
        }

        var tower = _residentialTowers[towerIndex];
        var sign = side == 0 ? 1.0f : -1.0f;
        var wallX = sign * spec.Footprint.X * 0.5f;
        var stairCenterX = wallX + sign * ResidentialSkybridgeAccessStairOffset;
        var doorZ = slot.DoorZ;
        var stepRun = ResidentialSkybridgeAccessRun / ResidentialSkybridgeAccessStepCount;
        var risingStepCount = ResidentialSkybridgeAccessStepCount
            - ResidentialSkybridgeAccessTransitionStepCount;
        var stepRise = (ResidentialSkybridgeAccessSurfaceY - ResidentialSkybridgeAccessGroundY)
            / risingStepCount;
        var platformNorthZ = doorZ - ResidentialSkybridgeAccessPlatformDepth * 0.5f;
        var stairStartZ = platformNorthZ - ResidentialSkybridgeAccessRun;
        // Let the landing overlap the first bridge meters so the player capsule
        // does not catch an exposed deck edge at the facade transition.
        var platformCenterX = wallX + sign * 1.0f;
        var root = new StaticBody3D
        {
            Name = $"SkybridgeAccess_T{towerIndex + 1:00}",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddToGroup("residential_skybridge_access");
        root.AddToGroup("residential_skybridge_access_collision");
        tower.AddChild(root);
        var collision = root;
        var visualTransforms = new List<Transform3D>(112);
        var visualColors = new List<Color>(112);
        void AppendVisuals(IEnumerable<Transform3D> transforms, Color color)
        {
            foreach (var transform in transforms)
            {
                visualTransforms.Add(transform);
                visualColors.Add(color);
            }
        }
        var treadColor = new Color(0.2f, 0.25f, 0.26f);
        var nosingColor = new Color(0.92f, 0.57f, 0.12f);
        var landingColor = new Color(0.23f, 0.29f, 0.29f);
        var guardColor = new Color(0.08f, 0.12f, 0.13f);
        var supportColor = new Color(0.15f, 0.2f, 0.2f);

        var treadTransforms = new List<Transform3D>(ResidentialSkybridgeAccessStepCount);
        var nosingTransforms = new List<Transform3D>(ResidentialSkybridgeAccessStepCount);
        var treadSize = new Vector3(
            ResidentialSkybridgeAccessTreadWidth,
            ResidentialSkybridgeAccessTreadThickness,
            stepRun * 1.08f);
        for (var step = 0; step < ResidentialSkybridgeAccessStepCount; step++)
        {
            var topY = ResidentialSkybridgeAccessGroundY
                + stepRise * Mathf.Min(step + 1, risingStepCount);
            var z = stairStartZ + stepRun * (step + 0.5f);
            var position = new Vector3(
                stairCenterX,
                topY - ResidentialSkybridgeAccessTreadThickness * 0.5f,
                z);
            treadTransforms.Add(new Transform3D(Basis.Identity.Scaled(treadSize), position));
            nosingTransforms.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(ResidentialSkybridgeAccessTreadWidth, 0.035f, 0.055f)),
                new Vector3(stairCenterX, topY + 0.016f, z + stepRun * 0.47f)));
        }

        var rampEndZ = stairStartZ + stepRun * risingStepCount;
        var rampRun = rampEndZ - stairStartZ;
        var rampRise = ResidentialSkybridgeAccessSurfaceY - ResidentialSkybridgeAccessGroundY;
        var rampAngle = -Mathf.Atan2(rampRise, rampRun);
        const float rampThickness = 0.18f;
        const float rampOverlap = 0.12f;
        var rampBasis = new Basis(Vector3.Right, rampAngle);
        var rampTopCenter = new Vector3(
            stairCenterX,
            (ResidentialSkybridgeAccessGroundY + ResidentialSkybridgeAccessSurfaceY) * 0.5f,
            (stairStartZ + rampEndZ) * 0.5f);
        var rampCollision = new CollisionShape3D
        {
            Name = $"SkybridgeAccessRampCollision_T{towerIndex + 1:00}",
            Position = rampTopCenter - rampBasis.Y * (rampThickness * 0.5f),
            Rotation = new Vector3(rampAngle, 0.0f, 0.0f),
            Shape = new BoxShape3D
            {
                Size = new Vector3(
                    ResidentialSkybridgeAccessTreadWidth,
                    rampThickness,
                    Mathf.Sqrt(rampRun * rampRun + rampRise * rampRise) + rampOverlap)
            }
        };
        collision.AddChild(rampCollision);
        const float transitionOverlap = 0.06f;
        var transitionStartZ = rampEndZ - transitionOverlap;
        var transitionEndZ = platformNorthZ + transitionOverlap;
        AddSkybridgeAccessCollision(
            collision,
            $"SkybridgeAccessTransitionCollision_T{towerIndex + 1:00}",
            new Vector3(
                stairCenterX,
                ResidentialSkybridgeAccessSurfaceY - ResidentialSkybridgeAccessTreadThickness * 0.5f,
                (transitionStartZ + transitionEndZ) * 0.5f),
            new Vector3(
                ResidentialSkybridgeAccessTreadWidth,
                ResidentialSkybridgeAccessTreadThickness,
                transitionEndZ - transitionStartZ));
        AppendVisuals(treadTransforms, treadColor);
        AppendVisuals(nosingTransforms, nosingColor);

        var landingTransforms = new List<Transform3D>(2);
        var bottomPlatformSize = new Vector3(2.5f, 0.16f, 1.8f);
        var bottomPlatformPosition = new Vector3(
            stairCenterX,
            ResidentialSkybridgeAccessGroundY - bottomPlatformSize.Y * 0.5f,
            stairStartZ - bottomPlatformSize.Z * 0.5f);
        AddSkybridgeAccessCollision(
            collision,
            $"SkybridgeAccessBottomPlatform_T{towerIndex + 1:00}",
            bottomPlatformPosition,
            bottomPlatformSize);
        landingTransforms.Add(new Transform3D(
            Basis.Identity.Scaled(bottomPlatformSize),
            bottomPlatformPosition));

        var topPlatformSize = new Vector3(
            4.2f,
            0.16f,
            ResidentialSkybridgeAccessPlatformDepth + 0.5f);
        var topPlatformPosition = new Vector3(
            platformCenterX,
            ResidentialSkybridgeAccessSurfaceY - topPlatformSize.Y * 0.5f,
            doorZ);
        AddSkybridgeAccessCollision(
            collision,
            $"SkybridgeAccessTopPlatform_T{towerIndex + 1:00}",
            topPlatformPosition,
            topPlatformSize);
        landingTransforms.Add(new Transform3D(
            Basis.Identity.Scaled(topPlatformSize),
            topPlatformPosition + Vector3.Down * ResidentialSkybridgeAccessVisualSurfaceInset));
        AppendVisuals(landingTransforms, landingColor);

        var edgeOffset = ResidentialSkybridgeAccessTreadWidth * 0.5f + 0.04f;
        var railStartZ = stairStartZ + stepRun * 0.5f;
        var railEndZ = platformNorthZ - 0.35f;
        var guardTransforms = new List<Transform3D>(28);
        foreach (var (edge, suffix) in new[]
        {
            (-edgeOffset, "Left"),
            (edgeOffset, "Right")
        })
        {
            var start = new Vector3(
                stairCenterX + edge,
                ResidentialSkybridgeAccessGroundY + stepRise + 0.88f,
                railStartZ);
            var endSurface = SkybridgeAccessSurfaceAtZ(
                railEndZ,
                stairStartZ,
                stepRun,
                stepRise,
                risingStepCount);
            var end = new Vector3(stairCenterX + edge, endSurface + 0.88f, railEndZ);
            AddSkybridgeAccessSlopedRail(
                collision,
                $"SkybridgeAccessRail{suffix}",
                start,
                end,
                towerIndex,
                guardTransforms);
            var postCount = Mathf.Max(3, Mathf.CeilToInt(start.DistanceTo(end) / 1.2f) + 1);
            for (var post = 0; post < postCount; post++)
            {
                var t = postCount == 1 ? 0.0f : post / (float)(postCount - 1);
                var position = start.Lerp(end, t);
                var surface = SkybridgeAccessSurfaceAtZ(
                    position.Z,
                    stairStartZ,
                    stepRun,
                    stepRise,
                    risingStepCount);
                position.Y = surface + 0.44f;
                guardTransforms.Add(new Transform3D(
                    Basis.Identity.Scaled(new Vector3(0.065f, 0.88f, 0.065f)),
                    position));
            }
        }
        AppendVisuals(guardTransforms, guardColor);

        // Keep the landing open because several skyways leave the facade diagonally. The
        // stair rails protect the approach and the skyway sills protect the outer span.
        var outerX = wallX + sign * 2.27f;

        // Two visible stringers and short landing supports make the route read as a real
        // galvanized fire escape without adding a second static body per tower.
        var supportTransforms = new List<Transform3D>(4);
        foreach (var edge in new[] { -edgeOffset + 0.08f, edgeOffset - 0.08f })
        {
            var from = new Vector3(
                stairCenterX + edge,
                ResidentialSkybridgeAccessGroundY + 0.02f,
                stairStartZ + stepRun * 0.5f);
            var to = new Vector3(
                stairCenterX + edge,
                ResidentialSkybridgeAccessSurfaceY - 0.2f,
                platformNorthZ - stepRun * 0.5f);
            AddSkybridgeAccessSlopedTransform(
                supportTransforms,
                from,
                to,
                new Vector2(0.14f, 0.18f));
        }
        foreach (var z in new[] { doorZ - 0.92f, doorZ + 0.92f })
        {
            var postPosition = new Vector3(
                outerX,
                (ResidentialSkybridgeAccessSurfaceY - 0.16f) * 0.5f,
                z);
            supportTransforms.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(
                    0.16f,
                    ResidentialSkybridgeAccessSurfaceY - 0.16f,
                    0.16f)),
                postPosition));
        }
        AppendVisuals(supportTransforms, supportColor);
        AddResidentialSkybridgeAccessBatch(
            root,
            $"SkybridgeAccessVisuals_T{towerIndex + 1:00}",
            visual,
            visualTransforms,
            visualColors,
            112.0f);
        var visualInstanceCount = visualTransforms.Count;

        var bottomFeet = tower.ToGlobal(new Vector3(stairCenterX, ResidentialSkybridgeAccessGroundY + 0.22f, stairStartZ - 0.48f));
        var midFeet = AccessStepFeet(tower, stairCenterX, stairStartZ, stepRun, stepRise, ResidentialSkybridgeAccessStepCount / 2);
        var stairTopFeet = tower.ToGlobal(new Vector3(
            stairCenterX,
            ResidentialSkybridgeAccessSurfaceY + 0.22f,
            platformNorthZ - 0.22f));
        var landingFeet = tower.ToGlobal(new Vector3(
            stairCenterX,
            ResidentialSkybridgeAccessSurfaceY + 0.22f,
            platformNorthZ + 0.65f));
        var platformFeet = tower.ToGlobal(new Vector3(
            wallX - sign * 0.52f,
            ResidentialSkybridgeAccessSurfaceY + 0.22f,
            doorZ));
        var otherSide = ResidentialLinkSide(other, spec);
        var otherDoorZ = _residentialLinkSlots[otherTowerIndex][otherSide].DoorZ;
        var bridgeAnchor = tower.ToGlobal(ResidentialLinkAnchor(spec, side, 2.0f * ResidentialFloorHeight, doorZ));
        var otherAnchor = _residentialTowers[otherTowerIndex].ToGlobal(
            ResidentialLinkAnchor(other, otherSide, 2.0f * ResidentialFloorHeight, otherDoorZ));
        var bridgeDirection = bridgeAnchor.DirectionTo(otherAnchor);
        bridgeDirection.Y = 0.0f;
        bridgeDirection = bridgeDirection.Normalized();
        var bridgeFeet = bridgeAnchor + bridgeDirection * 3.2f;
        bridgeFeet.Y = tower.ToGlobal(new Vector3(0.0f, ResidentialSkybridgeAccessSurfaceY + 0.22f, 0.0f)).Y;
        var outward = (tower.GlobalBasis * new Vector3(sign, 0.0f, 0.0f)).Normalized();
        var climbDirection = (tower.GlobalBasis * new Vector3(0.0f, 0.0f, 1.0f)).Normalized();
        _residentialSkybridgeAccesses.Add(new ResidentialSkybridgeAccessRoute(
            towerIndex,
            side,
            root,
            collision,
            bottomFeet,
            midFeet,
            stairTopFeet,
            landingFeet,
            platformFeet,
            bridgeFeet,
            outward,
            climbDirection,
            ResidentialSkybridgeAccessStepCount,
            visualInstanceCount));
    }

    private static Vector3 AccessStepFeet(
        Node3D tower,
        float stairCenterX,
        float stairStartZ,
        float stepRun,
        float stepRise,
        int step)
    {
        var clampedStep = Mathf.Clamp(step, 1, ResidentialSkybridgeAccessStepCount);
        var risingStepCount = ResidentialSkybridgeAccessStepCount
            - ResidentialSkybridgeAccessTransitionStepCount;
        var topY = ResidentialSkybridgeAccessGroundY
            + stepRise * Mathf.Min(clampedStep, risingStepCount);
        var z = stairStartZ + stepRun * (clampedStep - 0.5f);
        return tower.ToGlobal(new Vector3(stairCenterX, topY + 0.22f, z));
    }

    private static float SkybridgeAccessSurfaceAtZ(
        float z,
        float stairStartZ,
        float stepRun,
        float stepRise,
        int risingStepCount)
    {
        var completedSteps = (z - stairStartZ) / stepRun + 0.5f;
        return ResidentialSkybridgeAccessGroundY
            + stepRise * Mathf.Clamp(completedSteps, 1.0f, risingStepCount);
    }

    private static void AddSkybridgeAccessCollision(
        StaticBody3D body,
        string name,
        Vector3 position,
        Vector3 size,
        Vector3 rotation = default)
    {
        body.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Shape = new BoxShape3D { Size = size }
        });
    }

    private void AddResidentialSkybridgeAccessBatch(
        Node3D root,
        string name,
        Godot.Material material,
        IReadOnlyList<Transform3D> transforms,
        IReadOnlyList<Color> colors,
        float visibilityRange)
    {
        if (transforms.Count == 0 || transforms.Count != colors.Count)
        {
            return;
        }
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = SharedBoxMesh(Vector3.One),
            InstanceCount = transforms.Count
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
            multiMesh.SetInstanceColor(index, colors[index]);
        }
        var visual = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            MaterialOverride = material,
            VisibilityRangeEnd = visibilityRange,
            VisibilityRangeEndMargin = 10.0f
        };
        root.AddChild(visual);
        visual.AddToGroup("residential_skybridge_access_visual");
        visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private void AddSkybridgeAccessSlopedRail(
        StaticBody3D collision,
        string name,
        Vector3 from,
        Vector3 to,
        int towerIndex,
        ICollection<Transform3D> visualTransforms)
    {
        var delta = to - from;
        var horizontal = new Vector2(delta.X, delta.Z).Length();
        var length = delta.Length();
        if (length < 0.1f || horizontal < 0.1f)
        {
            return;
        }
        var yaw = Mathf.Atan2(delta.X, delta.Z);
        var angle = Mathf.Atan2(delta.Y, horizontal);
        var center = (from + to) * 0.5f;
        var rotation = new Vector3(-angle, yaw, 0.0f);
        AddSkybridgeAccessCollision(
            collision,
            $"{name}Collision_T{towerIndex + 1:00}",
            center,
            new Vector3(0.1f, 0.1f, length),
            rotation);
        visualTransforms.Add(new Transform3D(
            Basis.FromEuler(rotation).ScaledLocal(new Vector3(0.1f, 0.1f, length)),
            center));
    }

    private static void AddSkybridgeAccessSlopedTransform(
        ICollection<Transform3D> transforms,
        Vector3 from,
        Vector3 to,
        Vector2 crossSection)
    {
        var delta = to - from;
        var horizontal = new Vector2(delta.X, delta.Z).Length();
        var length = delta.Length();
        if (length < 0.1f || horizontal < 0.1f)
        {
            return;
        }
        var yaw = Mathf.Atan2(delta.X, delta.Z);
        var angle = Mathf.Atan2(delta.Y, horizontal);
        var rotation = new Vector3(-angle, yaw, 0.0f);
        transforms.Add(new Transform3D(
            Basis.FromEuler(rotation).ScaledLocal(new Vector3(crossSection.X, crossSection.Y, length)),
            (from + to) * 0.5f));
    }

    private async System.Threading.Tasks.Task<(
        bool Reached,
        float VerticalDelta,
        float StartY,
        float MinimumY,
        int MaximumStallFrames)> WalkSkybridgeAccessLeg(
        Vector3 start,
        Vector3 target)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        _player.GlobalPosition = start;
        _player.Velocity = Vector3.Zero;
        _player.SetStaminaForDiagnostics(100.0f);
        _player.RestoreMovementInput();
        await WaitFrames(6);

        var settledStart = _player.GlobalPosition;
        var walkDirection = target - settledStart;
        walkDirection.Y = 0.0f;
        walkDirection = walkDirection.Normalized();
        var previousProjection = 0.0f;
        var minimumY = settledStart.Y;
        var consecutiveStallFrames = 0;
        var maximumStallFrames = 0;
        var reached = false;
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 300; frame++)
        {
            Input.ActionPress("move_forward");
            _player.FaceWorldPointForDiagnostics(target);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            minimumY = Mathf.Min(minimumY, _player.GlobalPosition.Y);
            var delta = target - _player.GlobalPosition;
            var horizontalDistance = new Vector2(delta.X, delta.Z).Length();
            if (horizontalDistance < 0.65f && Mathf.Abs(delta.Y) < 0.8f)
            {
                reached = true;
                break;
            }
            if (frame > 2 && !_player.HasMovementIntent)
            {
                _player.RestoreMovementInput();
            }

            var progress = _player.GlobalPosition - settledStart;
            progress.Y = 0.0f;
            var projection = progress.Dot(walkDirection);
            if (frame >= 8 && horizontalDistance > 0.8f)
            {
                consecutiveStallFrames = projection - previousProjection < 0.005f
                    ? consecutiveStallFrames + 1
                    : 0;
                maximumStallFrames = System.Math.Max(maximumStallFrames, consecutiveStallFrames);
            }
            previousProjection = projection;
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");

        if (!reached)
        {
            var blockers = new List<string>();
            for (var collisionIndex = 0; collisionIndex < _player.GetSlideCollisionCount(); collisionIndex++)
            {
                var collision = _player.GetSlideCollision(collisionIndex);
                if (collision.GetCollider() is Node colliderNode)
                {
                    var normal = collision.GetNormal();
                    blockers.Add($"{colliderNode.Name}@n({normal.X:0.00},{normal.Y:0.00},{normal.Z:0.00})");
                }
            }
            GD.Print($"SKYBRIDGE_ACCESS_LEG_STALL start=({settledStart.X:0.00},{settledStart.Y:0.00},{settledStart.Z:0.00}) end=({_player.GlobalPosition.X:0.00},{_player.GlobalPosition.Y:0.00},{_player.GlobalPosition.Z:0.00}) target=({target.X:0.00},{target.Y:0.00},{target.Z:0.00}) ui_locked={_player.UiLocked} intent={_player.HasMovementIntent} floor={_player.IsOnFloor()} blockers={string.Join(',', blockers)}");
        }

        return (
            reached,
            _player.GlobalPosition.Y - settledStart.Y,
            settledStart.Y,
            minimumY,
            maximumStallFrames);
    }

    private async void ValidateSkybridgeAccess()
    {
        Engine.TimeScale = 2.0;
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(5);

        var expected = ResidentialTowerSpecs.Length;
        var distinctTowers = _residentialSkybridgeAccesses
            .Select(access => access.TowerIndex)
            .Distinct()
            .Count();
        var stepShapes = 0;
        var platformShapes = 0;
        var rampShapes = 0;
        var transitionShapes = 0;
        var concaveShapes = 0;
        var structureReady = _residentialSkybridgeAccesses.Count == expected;
        var visualsReady = true;
        var floorsReady = true;
        var clearanceReady = true;
        var bridgeEntriesReady = true;
        var platformVisualsSeparated = true;
        foreach (var access in _residentialSkybridgeAccesses)
        {
            var collisionChildren = access.CollisionBody.GetChildren();
            using var collisionChildrenBacking = collisionChildren.AsDisposable();
            var shapes = collisionChildren
                .OfType<CollisionShape3D>()
                .ToArray();
            var rampName = $"SkybridgeAccessRampCollision_T{access.TowerIndex + 1:00}";
            var transitionName = $"SkybridgeAccessTransitionCollision_T{access.TowerIndex + 1:00}";
            var accessRamps = shapes.Where(shape =>
                    shape.Name.ToString() == rampName
                    && !shape.Disabled
                    && shape.Shape is BoxShape3D)
                .ToArray();
            var accessTransitions = shapes.Where(shape =>
                    shape.Name.ToString() == transitionName
                    && !shape.Disabled
                    && shape.Shape is BoxShape3D)
                .ToArray();
            var accessConcaveShapes = shapes.Count(shape => shape.Shape is ConcavePolygonShape3D);
            var expectedRampAngle = -Mathf.Atan2(
                ResidentialSkybridgeAccessSurfaceY - ResidentialSkybridgeAccessGroundY,
                ResidentialSkybridgeAccessRun
                    * (ResidentialSkybridgeAccessStepCount - ResidentialSkybridgeAccessTransitionStepCount)
                    / ResidentialSkybridgeAccessStepCount);
            var solidRampReady = accessRamps.Length == 1
                && accessRamps[0].Shape is BoxShape3D ramp
                && ramp.Size.X >= ResidentialSkybridgeAccessTreadWidth - 0.01f
                && ramp.Size.Y >= 0.17f
                && Mathf.Abs(accessRamps[0].Rotation.X - expectedRampAngle) < 0.01f
                && accessTransitions.Length == 1
                && accessConcaveShapes == 0;
            var accessSteps = access.StepCount;
            var accessPlatforms = shapes.Count(shape => shape.Name.ToString().Contains("Platform", System.StringComparison.Ordinal));
            stepShapes += accessSteps;
            platformShapes += accessPlatforms;
            rampShapes += accessRamps.Length;
            transitionShapes += accessTransitions.Length;
            concaveShapes += accessConcaveShapes;
            structureReady &= access.StepCount == ResidentialSkybridgeAccessStepCount
                && accessSteps == ResidentialSkybridgeAccessStepCount
                && solidRampReady
                && accessPlatforms == 2;

            var visualBatch = access.Root.GetNodeOrNull<MultiMeshInstance3D>(
                $"SkybridgeAccessVisuals_T{access.TowerIndex + 1:00}");
            visualsReady &= visualBatch?.Multimesh?.InstanceCount == access.VisualInstanceCount;
            var topPlatformVisual = visualBatch?.Multimesh?.GetInstanceTransform(
                ResidentialSkybridgeAccessStepCount * 2 + 1);
            platformVisualsSeparated &= topPlatformVisual is Transform3D platformTransform
                && platformTransform.Origin.Y + platformTransform.Basis.Y.Length() * 0.5f
                    <= ResidentialSkybridgeAccessSurfaceY - 0.02f;
            floorsReady &= HasSkybridgeAccessFloor(access.BottomFeet)
                && HasSkybridgeAccessFloor(access.BridgeFeet);
            clearanceReady &= HasSkybridgeAccessClearance(access.BridgeFeet)
                && access.BridgeFeet.DistanceTo(access.PlatformFeet) > 0.8f;
            bridgeEntriesReady &= HasSkybridgeAccessEntryClearance(
                access.PlatformFeet,
                access.BridgeFeet);
        }

        var laneTraversalsReady = true;
        var laneTraversals = 0;
        var laneMaximumStallFrames = 0;
        var laneMinimumAscent = float.PositiveInfinity;
        var laneMinimumDescent = float.PositiveInfinity;
        var laneMinimumFloorMargin = float.PositiveInfinity;
        var requiredVerticalTravel = ResidentialSkybridgeAccessSurfaceY
            - ResidentialSkybridgeAccessGroundY
            - 0.7f;
        foreach (var access in _residentialSkybridgeAccesses.OrderByDescending(access => access.TowerIndex))
        {
            foreach (var lateralOffset in new[] { -0.52f, 0.0f, 0.52f })
            {
                var laneOffset = access.Outward * lateralOffset;
                var bottom = access.BottomFeet + laneOffset;
                var middle = access.MidFeet + laneOffset;
                var top = access.StairTopFeet + laneOffset;
                var laneFloorsReady = HasSkybridgeAccessFloor(middle)
                    && HasSkybridgeAccessFloor(top);

                var ascent = await WalkSkybridgeAccessLeg(bottom, top);
                var ascentDropMargin = ascent.MinimumY - Mathf.Min(ascent.StartY, top.Y);
                var ascentReady = ascent.Reached
                    && ascent.VerticalDelta > requiredVerticalTravel
                    && ascentDropMargin > -0.35f
                    && ascent.MaximumStallFrames <= 18;

                var descent = await WalkSkybridgeAccessLeg(top, bottom);
                var descentDropMargin = descent.MinimumY - Mathf.Min(descent.StartY, bottom.Y);
                var descentReady = descent.Reached
                    && -descent.VerticalDelta > requiredVerticalTravel
                    && descentDropMargin > -0.35f
                    && descent.MaximumStallFrames <= 18;

                laneTraversals++;
                laneMaximumStallFrames = System.Math.Max(
                    laneMaximumStallFrames,
                    System.Math.Max(ascent.MaximumStallFrames, descent.MaximumStallFrames));
                laneMinimumAscent = Mathf.Min(laneMinimumAscent, ascent.VerticalDelta);
                laneMinimumDescent = Mathf.Min(laneMinimumDescent, -descent.VerticalDelta);
                laneMinimumFloorMargin = Mathf.Min(
                    laneMinimumFloorMargin,
                    Mathf.Min(ascentDropMargin, descentDropMargin));
                laneTraversalsReady &= laneFloorsReady && ascentReady && descentReady;
                GD.Print($"SKYBRIDGE_ACCESS_LANE tower={access.TowerIndex + 1:00} offset={lateralOffset:0.00} valid={laneFloorsReady && ascentReady && descentReady} floors={laneFloorsReady} ascent={ascent.Reached}/{ascent.VerticalDelta:0.00} descent={descent.Reached}/{-descent.VerticalDelta:0.00} stall={ascent.MaximumStallFrames}/{descent.MaximumStallFrames} floor_margin={Mathf.Min(ascentDropMargin, descentDropMargin):0.00}");
            }
        }
        if (_residentialSkybridgeAccesses.Count == 0)
        {
            laneMinimumAscent = 0.0f;
            laneMinimumDescent = 0.0f;
            laneMinimumFloorMargin = 0.0f;
        }
        Engine.TimeScale = 1.0;

        var walkedRoutes = 0;
        var reached = 0;
        var minimumWalkGain = float.PositiveInfinity;
        const int waypointsPerRoute = 6;
        foreach (var access in _residentialSkybridgeAccesses)
        {
            _player.GlobalPosition = access.BottomFeet;
            _player.Velocity = Vector3.Zero;
            _player.SetStaminaForDiagnostics(100.0f);
            _player.RestoreMovementInput();
            await WaitFrames(6);
            var startY = _player.GlobalPosition.Y;
            var routeReached = 0;
            var waypoints = new[]
            {
                access.MidFeet,
                access.StairTopFeet,
                access.LandingFeet,
                access.PlatformFeet + access.Outward * 1.05f,
                access.PlatformFeet,
                access.BridgeFeet
            };
            foreach (var waypoint in waypoints)
            {
                Input.ActionRelease("move_forward");
                Input.ActionRelease("sprint");
                _player.Velocity = new Vector3(0.0f, _player.Velocity.Y, 0.0f);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                _player.RestoreMovementInput();
                Input.ActionPress("move_forward");
                var reachedWaypoint = false;
                for (var frame = 0; frame < 240; frame++)
                {
                    Input.ActionPress("move_forward");
                    _player.FaceWorldPointForDiagnostics(waypoint);
                    var delta = waypoint - _player.GlobalPosition;
                    var horizontalDistance = new Vector2(delta.X, delta.Z).Length();
                    if (horizontalDistance < 0.58f && Mathf.Abs(delta.Y) < 0.82f)
                    {
                        reachedWaypoint = true;
                        break;
                    }
                    if (frame > 2 && !_player.HasMovementIntent)
                    {
                        _player.RestoreMovementInput();
                    }
                    if (horizontalDistance > 1.8f)
                    {
                        Input.ActionPress("sprint");
                    }
                    else
                    {
                        Input.ActionRelease("sprint");
                    }
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
                Input.ActionRelease("sprint");
                Input.ActionRelease("move_forward");
                if (!reachedWaypoint)
                {
                    var tower = _residentialTowers[access.TowerIndex];
                    var local = tower.ToLocal(_player.GlobalPosition);
                    var targetLocal = tower.ToLocal(waypoint);
                    var blockers = new List<string>();
                    for (var collisionIndex = 0; collisionIndex < _player.GetSlideCollisionCount(); collisionIndex++)
                    {
                        var collision = _player.GetSlideCollision(collisionIndex);
                        if (collision.GetCollider() is Node colliderNode)
                        {
                            var normal = collision.GetNormal();
                            var position = collision.GetPosition();
                            blockers.Add($"{colliderNode.Name}@n({normal.X:0.0},{normal.Y:0.0},{normal.Z:0.0})p({position.X:0.0},{position.Y:0.0},{position.Z:0.0})");
                        }
                    }
                    GD.Print($"SKYBRIDGE_ACCESS_STALL tower={access.TowerIndex + 1:00} waypoint={routeReached} pos=({local.X:0.00},{local.Y:0.00},{local.Z:0.00}) target=({targetLocal.X:0.00},{targetLocal.Y:0.00},{targetLocal.Z:0.00}) blockers={string.Join(',', blockers)}");
                    break;
                }
                routeReached++;
                reached++;
            }
            var walkGain = _player.GlobalPosition.Y - startY;
            minimumWalkGain = Mathf.Min(minimumWalkGain, walkGain);
            var routeWalked = routeReached == waypoints.Length
                && _player.GlobalPosition.DistanceTo(access.BridgeFeet) < 1.45f
                && Mathf.Abs(_player.GlobalPosition.Y - access.BridgeFeet.Y) < 0.8f
                && walkGain > ResidentialFloorHeight + 2.2f;
            GD.Print($"SKYBRIDGE_ACCESS_ROUTE tower={access.TowerIndex + 1:00} valid={routeWalked} reached={routeReached}/{waypointsPerRoute} gain={walkGain:0.00}");
            if (routeWalked)
            {
                walkedRoutes++;
            }
            else
            {
                break;
            }
        }
        if (_residentialSkybridgeAccesses.Count == 0)
        {
            Input.ActionRelease("sprint");
            Input.ActionRelease("move_forward");
            minimumWalkGain = 0.0f;
        }
        var walked = walkedRoutes == expected;

        var vaultFloor = new StaticBody3D
        {
            Name = "LowFurnitureVaultDiagnosticFloor",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        AddChild(vaultFloor);
        var vaultOrigin = new Vector3(0.0f, 80.0f, 0.0f);
        vaultFloor.GlobalPosition = vaultOrigin;
        vaultFloor.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, -0.1f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(5.0f, 0.2f, 5.0f) }
        });
        var yellowDrawer = new ResidentialSearchableFurniture
        {
            Name = "LowFurnitureVaultDiagnostic"
        };
        yellowDrawer.Configure(
            ResidentialFurnitureKind.DeskDrawers,
            ResidentialRoomEventKind.Alarm,
            0,
            0,
            1,
            new[]
            {
                new LootItem
                {
                    Kind = LootItemKind.Medical,
                    MedicalKind = MedicalItemKind.Bandage,
                    Quantity = 1,
                    Grade = LootGrade.Common
                }
            });
        AddChild(yellowDrawer);
        yellowDrawer.GlobalPosition = vaultOrigin + new Vector3(0.0f, 0.03f, -0.78f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _player.GlobalPosition = vaultOrigin + Vector3.Up * 0.03f;
        _player.Velocity = Vector3.Zero;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.FaceWorldPointForDiagnostics(yellowDrawer.GlobalPosition);
        Input.ActionRelease("jump");
        Input.ActionRelease("move_forward");
        for (var frame = 0; frame < 3; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var vaultsBefore = _player.SuccessfulVaultsForDiagnostics;
        var vaultStartPosition = _player.GlobalPosition;
        Input.ActionPress("move_forward");
        Input.ActionPress("jump");
        for (var frame = 0; frame < 2; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var lowFurnitureVaultStarted = _player.IsVaulting;
        var sawVaultRise = _player.VaultPhaseForDiagnostics == "rise";
        var sawVaultCross = _player.VaultPhaseForDiagnostics == "cross";
        var sawVaultSettle = _player.VaultPhaseForDiagnostics == "settle";
        Input.ActionRelease("jump");
        Input.ActionRelease("move_forward");
        var vaultWaitFrames = 0;
        while (_player.IsVaulting && vaultWaitFrames < 90)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            sawVaultRise |= _player.VaultPhaseForDiagnostics == "rise";
            sawVaultCross |= _player.VaultPhaseForDiagnostics == "cross";
            sawVaultSettle |= _player.VaultPhaseForDiagnostics == "settle";
            vaultWaitFrames++;
        }
        var lowFurnitureVaultTimedOut = _player.IsVaulting;
        if (lowFurnitureVaultTimedOut)
        {
            _player.CancelLowObstacleVaultForDiagnostics();
        }
        var lowFurnitureVaulted = lowFurnitureVaultStarted
            && !lowFurnitureVaultTimedOut
            && _player.SuccessfulVaultsForDiagnostics == vaultsBefore + 1
            && _player.GlobalPosition.Y >= vaultOrigin.Y + 0.58f
            && new Vector2(
                _player.GlobalPosition.X - vaultStartPosition.X,
                _player.GlobalPosition.Z - vaultStartPosition.Z).Length() >= 0.34f;
        yellowDrawer.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        StaticBody3D AddVaultDiagnosticBox(string name, Vector3 center, Vector3 size)
        {
            var body = new StaticBody3D
            {
                Name = name,
                CollisionLayer = 1,
                CollisionMask = 0
            };
            AddChild(body);
            body.GlobalPosition = center;
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = size }
            });
            return body;
        }

        var blockedOrigin = vaultOrigin + new Vector3(1.35f, 0.0f, 0.0f);
        var blockedObstacle = AddVaultDiagnosticBox(
            "VaultBlockedObstacleDiagnostic",
            blockedOrigin + new Vector3(0.0f, 0.3f, -0.72f),
            new Vector3(0.8f, 0.6f, 0.5f));
        var blockedBeam = AddVaultDiagnosticBox(
            "VaultOverheadBeamDiagnostic",
            blockedOrigin + new Vector3(0.0f, 1.95f, 0.0f),
            new Vector3(1.0f, 0.2f, 0.22f));
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _player.GlobalPosition = blockedOrigin + Vector3.Up * 0.03f;
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(blockedObstacle.GlobalPosition);
        var blockedVaultCount = _player.SuccessfulVaultsForDiagnostics;
        Input.ActionPress("move_forward");
        Input.ActionPress("jump");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease("jump");
        Input.ActionRelease("move_forward");
        var blockedVaultResult = _player.LastVaultResultForDiagnostics;
        var blockedVaultRejected = !_player.IsVaulting
            && _player.SuccessfulVaultsForDiagnostics == blockedVaultCount
            && blockedVaultResult.StartsWith("rejected:path_blocked", System.StringComparison.Ordinal);
        blockedObstacle.QueueFree();
        blockedBeam.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var highOrigin = vaultOrigin + new Vector3(-1.35f, 0.0f, 0.0f);
        var highObstacle = AddVaultDiagnosticBox(
            "VaultHighObstacleDiagnostic",
            highOrigin + new Vector3(0.0f, 0.65f, -0.72f),
            new Vector3(0.8f, 1.3f, 0.5f));
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _player.GlobalPosition = highOrigin + Vector3.Up * 0.03f;
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(highObstacle.GlobalPosition);
        var highVaultCount = _player.SuccessfulVaultsForDiagnostics;
        Input.ActionPress("move_forward");
        Input.ActionPress("jump");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease("jump");
        Input.ActionRelease("move_forward");
        var highVaultRejected = !_player.IsVaulting
            && _player.SuccessfulVaultsForDiagnostics == highVaultCount
            && _player.LastVaultResultForDiagnostics.StartsWith("rejected:height", System.StringComparison.Ordinal);
        highObstacle.QueueFree();
        vaultFloor.QueueFree();

        var valid = structureReady
            && distinctTowers == expected
            && stepShapes == expected * ResidentialSkybridgeAccessStepCount
            && platformShapes == expected * 2
            && rampShapes == expected
            && transitionShapes == expected
            && concaveShapes == 0
            && visualsReady
            && floorsReady
            && clearanceReady
            && bridgeEntriesReady
            && platformVisualsSeparated
            && laneTraversals == expected * 3
            && laneTraversalsReady
            && walked
            && lowFurnitureVaulted
            && sawVaultRise
            && sawVaultCross
            && sawVaultSettle
            && blockedVaultRejected
            && highVaultRejected;
        GD.Print($"SKYBRIDGE_ACCESS_CHECK valid={valid} accesses={_residentialSkybridgeAccesses.Count}/{expected} towers={distinctTowers}/{expected} steps={stepShapes}/{expected * ResidentialSkybridgeAccessStepCount} ramps={rampShapes}/{expected} transitions={transitionShapes}/{expected} concave={concaveShapes} platforms={platformShapes}/{expected * 2} structure={structureReady} visuals={visualsReady} platform_layers={platformVisualsSeparated} floors={floorsReady} clearance={clearanceReady} bridge_entries={bridgeEntriesReady} lanes={laneTraversals}/{expected * 3} lanes_ready={laneTraversalsReady} lane_stall={laneMaximumStallFrames}/18 lane_ascent={laneMinimumAscent:0.00} lane_descent={laneMinimumDescent:0.00} lane_floor_margin={laneMinimumFloorMargin:0.00} walk={walked} walked_routes={walkedRoutes}/{expected} reached={reached}/{expected * waypointsPerRoute} min_walk_h={minimumWalkGain:0.00} low_furniture_vault={lowFurnitureVaulted} vault_started={lowFurnitureVaultStarted} vault_phases={sawVaultRise}/{sawVaultCross}/{sawVaultSettle} vault_timeout={lowFurnitureVaultTimedOut} vault_wait_frames={vaultWaitFrames} blocked_vault_rejected={blockedVaultRejected} blocked_vault_result={blockedVaultResult} high_vault_rejected={highVaultRejected}");
        GD.Print($"SKYBRIDGE_ACCESS_PASS valid={valid}");
        Engine.TimeScale = 1.0;
        GetTree().Quit(valid ? 0 : 2);
    }

    private bool HasSkybridgeAccessFloor(Vector3 feet)
    {
        return PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            feet + Vector3.Up * 0.55f,
            feet + Vector3.Down * 1.2f,
            _player.GetRid(),
            1);
    }

    private bool HasSkybridgeAccessClearance(Vector3 feet)
    {
        return !PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            feet + Vector3.Up * 0.12f,
            feet + Vector3.Up * 1.7f,
            _player.GetRid(),
            1);
    }

    private bool HasSkybridgeAccessEntryClearance(Vector3 platformFeet, Vector3 bridgeFeet)
    {
        var chestOffset = Vector3.Up * 0.68f;
        return !PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            platformFeet + chestOffset,
            bridgeFeet + chestOffset,
            _player.GetRid(),
            1);
    }

    private async void CaptureSkybridgeAccess()
    {
        DisableActorsForSurvivalDiagnostics();
        _hud.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Visible = false;
        if (_residentialSkybridgeAccesses.Count == 0)
        {
            GD.Print("SKYBRIDGE_ACCESS_CAPTURE accesses=0 path=skybridge_access_validation.png");
            GetTree().Quit(2);
            return;
        }

        var access = _residentialSkybridgeAccesses[0];
        var midpoint = (access.BottomFeet + access.StairTopFeet) * 0.5f;
        var camera = new Camera3D
        {
            Name = "SkybridgeAccessValidationCamera",
            Fov = 61.0f,
            Far = 420.0f
        };
        AddChild(camera);
        camera.GlobalPosition = midpoint
            + access.Outward * 14.0f
            - access.ClimbDirection * 5.0f
            + Vector3.Up * 3.4f;
        camera.LookAt(midpoint + access.ClimbDirection * 1.6f + Vector3.Up * 0.3f, Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(26);
        foreach (var sample in new[]
        {
            new Vector2(330.0f, 483.0f),
            new Vector2(430.0f, 450.0f),
            new Vector2(478.0f, 425.0f)
        })
        {
            var rayFrom = camera.ProjectRayOrigin(sample);
            var rayTo = rayFrom + camera.ProjectRayNormal(sample) * 80.0f;
            var hasHit = PhysicsRaycast.TryHit(
                GetWorld3D().DirectSpaceState,
                rayFrom,
                rayTo,
                1,
                out var hit);
            var collider = hasHit ? hit.Collider as Node : null;
            var hitPosition = hasHit ? hit.Position : Vector3.Zero;
            var hitLocal = _residentialTowers[access.TowerIndex].ToLocal(hitPosition);
            GD.Print($"SKYBRIDGE_ACCESS_SCREEN_RAY screen=({sample.X:0},{sample.Y:0}) collider={collider?.Name ?? "none"} parent={collider?.GetParent()?.Name ?? "none"} access={ReferenceEquals(collider, access.Root)} hit=({hitPosition.X:0.00},{hitPosition.Y:0.00},{hitPosition.Z:0.00}) local=({hitLocal.X:0.00},{hitLocal.Y:0.00},{hitLocal.Z:0.00})");
        }
        SaveViewportImage("res://skybridge_access_validation.png");

        camera.GlobalPosition = access.PlatformFeet + access.Outward * 1.25f + Vector3.Up * 1.42f;
        camera.LookAt(access.BridgeFeet + Vector3.Up * 1.18f, Vector3.Up);
        camera.Fov = 72.0f;
        await WaitFrames(12);
        SaveViewportImage("res://skybridge_entry_validation.png");
        GD.Print($"SKYBRIDGE_ACCESS_CAPTURE accesses={_residentialSkybridgeAccesses.Count} steps={_residentialSkybridgeAccesses.Sum(access => access.StepCount)} paths=skybridge_access_validation.png,skybridge_entry_validation.png");
        GetTree().Quit();
    }
}
