using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool ValidateHighValueLootAccess(
        out int accessibleLoot,
        out string firstBlocker)
    {
        accessibleLoot = 0;
        firstBlocker = "none";
        var exclusions = BuildRefineryLaneExclusions();
        foreach (var lootSource in _lootSources)
        {
            if (lootSource.LootNode is CollisionObject3D collisionObject
                && IsInstanceValid(collisionObject)
                && !exclusions.Contains(collisionObject.GetRid()))
            {
                exclusions.Add(collisionObject.GetRid());
            }
        }
        using var exclusionsBacking = exclusions.AsDisposable();
        using var capsule = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = capsule,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f,
            Exclude = exclusions
        };
        var space = GetWorld3D().DirectSpaceState;
        foreach (var zone in RefineryLayout.HighValueZones)
        {
            var origin = zone.Id == "grand_hotel"
                ? new Vector3(-86.0f, 0.2f, -118.1f)
                : new Vector3(86.0f, 0.2f, -2.0f);
            var positions = RefineryLayout.LootPlacements
                .Where(loot => loot.Grade >= LootGrade.Epic
                    && HorizontalDistance(loot.Position, zone.Center) <= zone.Radius)
                .Select(loot => loot.Position)
                .Concat(RefineryLayout.ValuablePlacements
                    .Where(loot => loot.Grade >= LootGrade.Epic
                        && HorizontalDistance(loot.Position, zone.Center) <= zone.Radius)
                    .Select(loot => loot.Position))
                .ToArray();
            if (positions.Length != 6)
            {
                firstBlocker = $"{zone.Id}:count_{positions.Length}";
                return false;
            }

            foreach (var target in positions)
            {
                var samples = Mathf.Max(1, Mathf.CeilToInt(origin.DistanceTo(target) / 0.45f));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var feet = origin.Lerp(target, sample / (float)samples);
                    query.Transform = new Transform3D(
                        Basis.Identity,
                        feet + Vector3.Up * 0.915f);
                    var hits = space.IntersectShape(query, 8);
                    using var hitsBacking = hits.AsDisposable();
                    if (hits.Count == 0)
                    {
                        continue;
                    }
                    using var hit = hits[0];
                    using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                    firstBlocker = colliderValue.AsGodotObject() is Node collider
                        ? $"{zone.Id}:{target.X:0.0},{target.Z:0.0}:{collider.Name}"
                        : $"{zone.Id}:{target.X:0.0},{target.Z:0.0}:unknown";
                    return false;
                }
                accessibleLoot++;
            }
        }
        return accessibleLoot == 12;
    }

    private const int JianghaiBuildingBlockingProbeCount = 14;

    private bool ValidateJianghaiBuildingCollision(
        out int blockingHits,
        out int clearRoutes,
        out string summary)
    {
        blockingHits = 0;
        clearRoutes = 0;
        summary = "ok";
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var blockingProbes = new[]
        {
            ("east_founders", new Vector3(25, 1.35f, -40), new Vector3(36, 1.35f, -40)),
            ("west_clock", new Vector3(-8, 1.35f, 26), new Vector3(-24, 1.35f, 26)),
            ("east_photo_row", new Vector3(7.8f, 1.2f, 12), new Vector3(29.8f, 1.2f, 12)),
            ("east_tea_row", new Vector3(7.4f, 1.2f, 24), new Vector3(29.4f, 1.2f, 24)),
            ("east_gate_row", new Vector3(8.7f, 1.2f, 48), new Vector3(28.7f, 1.2f, 48)),
            ("pawnshop_wall", new Vector3(-100, 1.0f, -124), new Vector3(-96, 1.0f, -124)),
            ("factory_gate", new Vector3(80.0f, 2.2f, -7.924f), new Vector3(83.0f, 2.2f, -7.924f)),
            ("market_rooftop_deck", new Vector3(0, 7.0f, -126), new Vector3(0, 2.0f, -126)),
            ("edge_west_04", new Vector3(-165, 1.35f, -32), new Vector3(-150, 1.35f, -32)),
            ("edge_east_04", new Vector3(165, 1.35f, -32), new Vector3(150, 1.35f, -32)),
            ("edge_west_05", new Vector3(-138, 1.35f, -60), new Vector3(-123, 1.35f, -60)),
            ("edge_east_05", new Vector3(138, 1.35f, -60), new Vector3(123, 1.35f, -60)),
            ("edge_west_06", new Vector3(-165, 1.35f, -88), new Vector3(-150, 1.35f, -88)),
            ("edge_east_06", new Vector3(165, 1.35f, -88), new Vector3(150, 1.35f, -88))
        };
        foreach (var probe in blockingProbes)
        {
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    probe.Item2,
                    probe.Item3,
                    exclusions,
                    1,
                    out var hit)
                || hit.Collider is not Node collider
                || !collider.IsInGroup(JianghaiGameplayCollisionBuilder.CollisionGroup))
            {
                summary = $"block:{probe.Item1}";
                return false;
            }
            blockingHits++;
        }

        var clearProbes = new[]
        {
            ("truck_low", new Vector3(-2.0f, 0.45f, 88), new Vector3(-2.0f, 0.45f, -212)),
            ("truck_mid", new Vector3(-0.5f, 1.4f, 88), new Vector3(-0.5f, 1.4f, -212)),
            ("truck_high", new Vector3(1.0f, 2.6f, 88), new Vector3(1.0f, 2.6f, -212))
        };
        foreach (var probe in clearProbes)
        {
            if (PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    probe.Item2,
                    probe.Item3,
                    exclusions,
                    1,
                    out var hit))
            {
                summary = hit.Collider is Node blocker
                    ? $"clear:{probe.Item1}:{blocker.Name}"
                    : $"clear:{probe.Item1}:unknown";
                return false;
            }
            clearRoutes++;
        }
        return blockingProbes.Length == JianghaiBuildingBlockingProbeCount
            && blockingHits == blockingProbes.Length
            && clearRoutes == clearProbes.Length;
    }

    private bool ValidateOldTownRouteProbes(out int checkedProbes, out string firstBlocker)
    {
        checkedProbes = 0;
        firstBlocker = "none";
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        foreach (var probe in RefineryLayout.RouteProbes)
        {
            checkedProbes++;
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    probe.From,
                    probe.To,
                    exclusions,
                    1,
                    out var hit))
            {
                continue;
            }
            var blocker = hit.Collider as Node3D;
            firstBlocker = blocker is null
                ? $"{probe.Name}:unknown@{hit.Position}"
                : $"{probe.Name}:{blocker.GetPath()}@{blocker.GlobalPosition}:hit={hit.Position}:shape={hit.Shape}";
            return false;
        }
        return checkedProbes == RefineryLayout.RouteProbes.Count;
    }

    private bool ValidateOldTownLandmarks()
    {
        if (_oldTownLandmarks is not { } landmarks)
        {
            return false;
        }
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var hotelEntryClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.HotelEntry, landmarks.HotelInterior, exclusions, 1);
        var treasuryEntryClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.TreasuryEntry, landmarks.TreasuryInterior, exclusions, 1);
        var hotelWallBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.HotelCenter + new Vector3(-14, 1.2f, 0), landmarks.HotelCenter, 1);
        var pawnshopAirProbes = new[]
        {
            (From: new Vector3(-86, 3.0f, -135.4f), To: new Vector3(-86, 3.0f, -136.6f)),
            (From: new Vector3(-98.0f, 3.0f, -122.1f), To: new Vector3(-98.0f, 3.0f, -122.9f)),
            (From: new Vector3(-94.5f, 3.5f, -111.4f), To: new Vector3(-94.5f, 3.5f, -112.3f))
        };
        var pawnshopAirClear = pawnshopAirProbes.All(probe =>
            !PhysicsRaycast.HasHit(GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var pawnshopVisibleBlocks = new[]
        {
            (From: new Vector3(-86, 1.0f, -135.4f), To: new Vector3(-86, 1.0f, -136.6f)),
            (From: new Vector3(-98.0f, 1.0f, -122.1f), To: new Vector3(-98.0f, 1.0f, -122.9f)),
            (From: new Vector3(-94.5f, 1.0f, -111.4f), To: new Vector3(-94.5f, 1.0f, -112.3f))
        }.Count(probe => PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var factoryAirWallProbes = new[]
        {
            (From: new Vector3(77, 1.2f, -10), To: new Vector3(77, 1.2f, -6)),
            (From: new Vector3(95, 1.2f, -10), To: new Vector3(95, 1.2f, -6)),
            (From: new Vector3(72, 1.2f, -1), To: new Vector3(76, 1.2f, -1)),
            (From: new Vector3(96, 1.2f, -1), To: new Vector3(100, 1.2f, -1)),
            (From: new Vector3(86, 1.2f, 13), To: new Vector3(86, 1.2f, 18))
        };
        var factoryAirWallsClear = factoryAirWallProbes.All(probe =>
            !PhysicsRaycast.HasHit(GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var factoryGateBlocks = new[]
        {
            (From: new Vector3(80.6f, 2.2f, -7.924f), To: new Vector3(83.0f, 2.2f, -7.924f)),
            (From: new Vector3(89.0f, 2.2f, -7.924f), To: new Vector3(91.4f, 2.2f, -7.924f)),
            (From: new Vector3(85.9f, 6.5f, -7.924f), To: new Vector3(85.9f, 4.0f, -7.924f))
        }.Count(probe => PhysicsRaycast.HasHit(GetWorld3D(), probe.From, probe.To, 1));
        var rooftopDeckBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(), new Vector3(0, 7.0f, -126), new Vector3(0, 2.0f, -126), 1);
        var marketRailBlocks = new[]
        {
            (From: new Vector3(1.5f, 4.845f, -123.5f), To: new Vector3(1.5f, 4.845f, -124.3f)),
            (From: new Vector3(1.5f, 5.445f, -123.5f), To: new Vector3(1.5f, 5.445f, -124.3f)),
            (From: new Vector3(1.5f, 4.845f, -127.7f), To: new Vector3(1.5f, 4.845f, -128.4f)),
            (From: new Vector3(1.5f, 5.445f, -127.7f), To: new Vector3(1.5f, 5.445f, -128.4f))
        }.Count(probe => PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var marketRailGapsClear = new[]
        {
            (From: new Vector3(1.5f, 5.15f, -123.5f), To: new Vector3(1.5f, 5.15f, -124.3f)),
            (From: new Vector3(1.5f, 5.15f, -127.7f), To: new Vector3(1.5f, 5.15f, -128.4f))
        }.All(probe => !PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var marketRailPostsBlock = new[]
        {
            (From: new Vector3(0, 5.15f, -123.5f), To: new Vector3(0, 5.15f, -124.3f)),
            (From: new Vector3(0, 5.15f, -127.7f), To: new Vector3(0, 5.15f, -128.4f))
        }.Count(probe => PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var rooftopWalkClear = ValidateRooftopCapsuleRoute(
            landmarks.RooftopRoute,
            out var rooftopBlocker);
        var traversalRegistered = _squadTraversalLinks.Any(link =>
            link.Source == "old_town_market_rooftop"
            && link.Bidirectional
            && link.ForwardPoints.Length >= 8);
        var clanHallReady = ValidateClanHallCollision(
            out var clanHallWallHits,
            out var clanHallRampHits,
            out var clanHallSummary);
        var countsReady = landmarks.LandmarkCount == 3
            && landmarks.HighValueZoneCount == 2
            && landmarks.CollisionShapeCount == 29
            && landmarks.EntryCount == 2
            && landmarks.RooftopRouteCount == 1
            && landmarks.GameplayCollisionContractError is null;
        GD.Print($"OLD_TOWN_LANDMARK_CHECK hotel_entry={hotelEntryClear} treasury_entry={treasuryEntryClear} hotel_wall={hotelWallBlocks} pawnshop_air_clear={pawnshopAirClear}:3 pawnshop_visible={pawnshopVisibleBlocks}/3 factory_air_clear={factoryAirWallsClear}:5 factory_gate={factoryGateBlocks}/3 clan_hall={clanHallReady}:walls={clanHallWallHits}/5:ramp={clanHallRampHits}/3:{clanHallSummary} rooftop_deck={rooftopDeckBlocks} rail_blocks={marketRailBlocks}/4 rail_gaps={marketRailGapsClear}:2 rail_posts={marketRailPostsBlock}/2 rooftop_clear={rooftopWalkClear}:{rooftopBlocker} traversal={traversalRegistered} counts={countsReady}:29 contract_error={landmarks.GameplayCollisionContractError ?? "none"}");
        return hotelEntryClear && treasuryEntryClear && hotelWallBlocks
            && pawnshopAirClear && pawnshopVisibleBlocks == 3
            && factoryAirWallsClear && factoryGateBlocks == 3
            && clanHallReady
            && rooftopDeckBlocks && marketRailBlocks == 4
            && marketRailGapsClear && marketRailPostsBlock == 2
            && rooftopWalkClear && traversalRegistered && countsReady;
    }

    private bool ValidateRooftopCapsuleRoute(
        IReadOnlyList<Vector3> route,
        out string blocker)
    {
        blocker = "none";
        if (route.Count < 8)
        {
            blocker = "route_missing";
            return false;
        }

        const float capsuleHeight = 1.75f;
        using var capsule = new CapsuleShape3D { Radius = 0.50f, Height = capsuleHeight };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = capsule,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f
        };
        var space = GetWorld3D().DirectSpaceState;
        const int firstElevatedDeckPoint = 3;
        var lastElevatedDeckPoint = route.Count - 4;
        for (var segment = firstElevatedDeckPoint; segment < lastElevatedDeckPoint; segment++)
        {
            var from = route[segment];
            var to = route[segment + 1];
            var samples = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 0.50f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var feet = from.Lerp(to, sample / (float)samples);
                query.Transform = new Transform3D(
                    Basis.Identity,
                    feet + Vector3.Up * (capsuleHeight * 0.5f + 0.04f));
                var hits = space.IntersectShape(query, 8);
                using var hitsBacking = hits.AsDisposable();
                if (hits.Count == 0)
                {
                    continue;
                }
                using var hit = hits[0];
                using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                blocker = colliderValue.AsGodotObject() is Node collider
                    ? $"segment_{segment}:{collider.Name}"
                    : $"segment_{segment}:unknown";
                return false;
            }
        }
        return true;
    }

    private Godot.Collections.Array<Rid> BuildRefineryLaneExclusions()
    {
        var exclusions = new Godot.Collections.Array<Rid> { _player.GetRid() };
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                exclusions.Add(enemy.GetRid());
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                exclusions.Add(mate.GetRid());
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                exclusions.Add(civilian.GetRid());
            }
        }
        foreach (var vehicle in _vehicles)
        {
            if (IsInstanceValid(vehicle))
            {
                exclusions.Add(vehicle.GetRid());
            }
        }
        foreach (var terminal in _objectiveTerminals)
        {
            if (!IsInstanceValid(terminal))
            {
                continue;
            }
            var terminalChildren = terminal.GetChildren();
            using var terminalChildrenBacking = terminalChildren.AsDisposable();
            foreach (var child in terminalChildren)
            {
                if (child is StaticBody3D body && IsInstanceValid(body))
                {
                    exclusions.Add(body.GetRid());
                }
            }
        }
        foreach (var door in _refineryDoors)
        {
            if (IsInstanceValid(door))
            {
                door.AddCollisionExclusions(exclusions);
            }
        }
        return exclusions;
    }
}
