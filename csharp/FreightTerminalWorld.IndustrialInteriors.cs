using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private FreightIndustrialInteriorBuildResult? _industrialInteriors;
    private IReadOnlyList<FreightIndustrialRoomContentPlan> _industrialRoomContentPlans =
        Array.Empty<FreightIndustrialRoomContentPlan>();
    private readonly List<ResidentialSupplyCache> _industrialInteriorCaches = new();
    private readonly List<EnemyOperator> _industrialInteriorGuards = new();
    private int _industrialInteriorSeed;

    private void SpawnIndustrialInteriorContent()
    {
        _industrialInteriorCaches.Clear();
        _industrialInteriorGuards.Clear();
        if (IsBlackwaterRefineryMap || _industrialInteriors is null)
        {
            return;
        }

        foreach (var plan in _industrialRoomContentPlans)
        {
            switch (plan.Kind)
            {
                case FreightIndustrialRoomContentKind.SupplyCache:
                    SpawnIndustrialSupplyCache(plan);
                    break;
                case FreightIndustrialRoomContentKind.RestingSoldiers:
                    SpawnIndustrialRestingGuards(plan);
                    break;
            }
        }
    }

    private void SpawnIndustrialSupplyCache(FreightIndustrialRoomContentPlan plan)
    {
        var kinds = new[]
        {
            ResidentialCacheKind.SecurityArmory,
            ResidentialCacheKind.WorkshopLocker,
            ResidentialCacheKind.EvacuationLocker,
            ResidentialCacheKind.SmugglerCache
        };
        var kind = kinds[(int)(plan.Roll % (uint)kinds.Length)];
        var cache = new ResidentialSupplyCache
        {
            Name = $"IndustrialSupplyCache_{plan.Room.Index + 1:00}",
            Position = plan.Room.ContentLocalPoint,
            Rotation = new Vector3(0, (plan.Roll % 8u) * Mathf.Pi * 0.25f, 0)
        };
        cache.Configure(
            kind,
            towerIndex: 1000 + plan.Room.Index,
            floorIndex: 0,
            CreateResidentialCacheLoot(kind));
        cache.AddToGroup("industrial_interior_cache");
        plan.Room.Root.AddChild(cache);
        _industrialInteriorCaches.Add(cache);
        _lootSources.Add(cache);
        _lootWorldPoints.Add(cache.GlobalPosition);
    }

    private void SpawnIndustrialRestingGuards(FreightIndustrialRoomContentPlan plan)
    {
        for (var index = 0; index < plan.GuardCount; index++)
        {
            var spawnPoint = index == 0
                ? plan.Room.GuardWorldPointA
                : plan.Room.GuardWorldPointB;
            var guard = SpawnEnemy(
                spawnPoint,
                alerted: false,
                teamId: 0,
                initialWeapon: WeaponCatalog.Build(WeaponPlatform.MP5A5, 1),
                sentryMode: true,
                detectionRange: 32.0f);
            guard.Name = $"INDUSTRIAL_REST_GUARD_{plan.Room.Index + 1:00}_{index + 1:00}";
            guard.AddToGroup("industrial_interior_guard");
            var facing = plan.Room.FacingWorldPoint;
            facing.Y = guard.GlobalPosition.Y;
            if (guard.GlobalPosition.DistanceSquaredTo(facing) > 0.1f)
            {
                guard.LookAt(facing, Vector3.Up);
            }
            _industrialInteriorGuards.Add(guard);
        }
    }
}
