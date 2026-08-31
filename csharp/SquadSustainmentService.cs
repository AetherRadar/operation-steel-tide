using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Explicit world integration required by the squad sustainment controller.
/// Loot ownership, network leases and presentation remain with their owners.
/// </summary>
internal interface ISquadSustainmentRuntime
{
    bool IsDemolitionMode { get; }
    bool MissionEnded { get; }
    bool ExtractionCountdownActive { get; }
    bool LocalPlayerDowned { get; }
    bool LocalPlayerEliminated { get; }
    bool ExtractionNetworkMatch { get; }
    bool PlayerCanBeRevived { get; }
    IReadOnlyList<SquadMate> SquadMates { get; }
    IReadOnlyList<ILootSource> LootSources { get; }
    ILootSource? OpenLootSource { get; }

    bool IsExtractionLootLeasedByOther(ILootSource source);
    void CommitLootMutation(ILootSource source);
}

/// <summary>
/// Low-frequency controller for corpse-package selection, reservation and
/// atomic AI equipment upgrades. It never searches the global scene tree.
/// </summary>
internal sealed class SquadSustainmentService
{
    private readonly ISquadSustainmentRuntime _runtime;
    private readonly Dictionary<ulong, LootReservation> _reservations = new();

    private readonly record struct LootReservation(ulong MateId, ulong ExpiresMilliseconds);

    internal SquadSustainmentService(ISquadSustainmentRuntime runtime)
    {
        _runtime = runtime;
    }

    internal bool Enabled
        => !_runtime.IsDemolitionMode
            && !_runtime.MissionEnded
            && !_runtime.ExtractionNetworkMatch;

    internal bool EvacuationInProgress => _runtime.ExtractionCountdownActive;

    internal bool ShouldSuppressLooting(SquadMate mate)
    {
        if (_runtime.IsDemolitionMode
            || _runtime.MissionEnded
            || _runtime.ExtractionCountdownActive
            || _runtime.LocalPlayerDowned
            || _runtime.LocalPlayerEliminated
            || _runtime.ExtractionNetworkMatch
            || _runtime.PlayerCanBeRevived)
        {
            return true;
        }
        var squadMates = _runtime.SquadMates;
        for (var index = 0; index < squadMates.Count; index++)
        {
            var candidate = squadMates[index];
            if (GodotObject.IsInstanceValid(candidate)
                && !ReferenceEquals(candidate, mate)
                && candidate.CanBeRevived)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Chooses one nearby corpse package from the maintained source list. Calls
    /// are staggered by each mate and avoid sorting or temporary collections.
    /// </summary>
    internal bool TryReserveBestSource(
        SquadMate mate,
        float range,
        out ILootSource? source)
    {
        source = null;
        if (!GodotObject.IsInstanceValid(mate)
            || mate.IsDowned
            || mate.IsBodyBag
            || mate.IsHumanProxy
            || ShouldSuppressLooting(mate))
        {
            return false;
        }

        var now = Time.GetTicksMsec();
        var rangeSquared = range * range;
        var bestScore = 0.0f;
        var lootSources = _runtime.LootSources;
        for (var sourceIndex = 0; sourceIndex < lootSources.Count; sourceIndex++)
        {
            var candidate = lootSources[sourceIndex];
            if ((candidate is not EnemyOperator && candidate is not SquadBodyBag)
                || !candidate.IsSearchable
                || !GodotObject.IsInstanceValid(candidate.LootNode)
                || ReferenceEquals(candidate, _runtime.OpenLootSource)
                || Mathf.Abs(candidate.LootNode.GlobalPosition.Y - mate.GlobalPosition.Y) > 3.2f)
            {
                continue;
            }
            var distanceSquared = mate.GlobalPosition.DistanceSquaredTo(
                candidate.LootNode.GlobalPosition);
            if (distanceSquared > rangeSquared
                || mate.IsSustainmentSourceCoolingDown(candidate)
                || IsReservedByOther(candidate, mate, now)
                || _runtime.IsExtractionLootLeasedByOther(candidate))
            {
                continue;
            }

            var maximumUtility = 0.0f;
            for (var itemIndex = 0; itemIndex < candidate.Loot.Count; itemIndex++)
            {
                var item = candidate.Loot[itemIndex];
                var utility = item.Kind == LootItemKind.Weapon && item.Weapon is not null
                    ? mate.EvaluateSustainmentWeaponUtility(
                        item,
                        MatchingAmmoGrade(candidate, item.Weapon))
                    : mate.EvaluateSustainmentLootUtility(item);
                maximumUtility = Mathf.Max(
                    maximumUtility,
                    utility);
            }
            if (maximumUtility <= 0.0f)
            {
                continue;
            }
            var score = maximumUtility - Mathf.Sqrt(distanceSquared) * 1.8f;
            if (score <= bestScore)
            {
                continue;
            }
            bestScore = score;
            source = candidate;
        }

        if (source is null)
        {
            return false;
        }
        _reservations[source.LootNode.GetInstanceId()] = new LootReservation(
            mate.GetInstanceId(),
            now + 30_000);
        return true;
    }

    internal void Release(SquadMate mate, ILootSource source)
    {
        if (GodotObject.IsInstanceValid(source.LootNode))
        {
            Release(mate, source.LootNode.GetInstanceId());
        }
    }

    internal void Release(SquadMate mate, ulong sourceId)
    {
        if (sourceId == 0)
        {
            return;
        }
        if (_reservations.TryGetValue(sourceId, out var reservation)
            && (!GodotObject.IsInstanceValid(mate)
                || reservation.MateId == mate.GetInstanceId()))
        {
            _reservations.Remove(sourceId);
        }
    }

    internal bool IsReservationOwner(SquadMate mate, ulong sourceId)
    {
        if (!GodotObject.IsInstanceValid(mate)
            || sourceId == 0
            || !_reservations.TryGetValue(sourceId, out var reservation))
        {
            return false;
        }
        if (reservation.ExpiresMilliseconds <= Time.GetTicksMsec())
        {
            _reservations.Remove(sourceId);
            return false;
        }
        return reservation.MateId == mate.GetInstanceId();
    }

    /// <summary>
    /// Atomically takes every useful upgrade at an already reached package.
    /// Previously recovered gear is returned; free baseline kit is never minted.
    /// </summary>
    internal bool TryTakeLoot(SquadMate mate, ILootSource source)
    {
        if (!GodotObject.IsInstanceValid(mate)
            || mate.IsDowned
            || mate.IsBodyBag
            || mate.Order != SquadOrder.Follow
            || ShouldSuppressLooting(mate)
            || !source.IsSearchable
            || !GodotObject.IsInstanceValid(source.LootNode)
            || mate.GlobalPosition.DistanceTo(source.LootNode.GlobalPosition) > 2.5f
            || ReferenceEquals(source, _runtime.OpenLootSource)
            || _runtime.IsExtractionLootLeasedByOther(source))
        {
            return false;
        }
        var sourceId = source.LootNode.GetInstanceId();
        if (!IsReservationOwner(mate, sourceId))
        {
            return false;
        }

        source.OnSearched();
        var changed = false;
        var carriedWeaponRemoved = false;
        var bestWeaponIndex = FindBestWeaponIndex(
            mate,
            source,
            out var weaponAmmoIndex,
            out var weaponAmmoGrade);
        if (bestWeaponIndex >= 0)
        {
            var incoming = source.Loot[bestWeaponIndex];
            var incomingAmmoQuantity = weaponAmmoIndex >= 0
                ? source.Loot[weaponAmmoIndex].Quantity
                : 0;
            if (mate.TryEquipSustainmentWeapon(
                    incoming,
                    weaponAmmoGrade,
                    incomingAmmoQuantity,
                    out var replacement,
                    out var replacementAmmo))
            {
                if (replacement is null)
                {
                    source.Loot.RemoveAt(bestWeaponIndex);
                    if (weaponAmmoIndex > bestWeaponIndex)
                    {
                        weaponAmmoIndex--;
                    }
                }
                else
                {
                    source.Loot[bestWeaponIndex] = replacement;
                }
                if (weaponAmmoIndex >= 0)
                {
                    if (replacementAmmo is null)
                    {
                        source.Loot.RemoveAt(weaponAmmoIndex);
                    }
                    else
                    {
                        source.Loot[weaponAmmoIndex] = replacementAmmo;
                    }
                }
                else if (replacementAmmo is not null)
                {
                    source.Loot.Add(replacementAmmo);
                }
                carriedWeaponRemoved = true;
                changed = true;
            }
        }

        // One item per equipment slot can improve the paper doll. Three bounded
        // passes avoid both sorting and temporary collections.
        for (var pass = 0; pass < 3; pass++)
        {
            var equipmentIndex = FindBestItemIndex(mate, source, LootItemKind.Equipment);
            if (equipmentIndex < 0)
            {
                break;
            }
            var incoming = source.Loot[equipmentIndex];
            if (!mate.TryEquipSustainmentEquipment(incoming, out var replacement))
            {
                break;
            }
            if (replacement is null)
            {
                source.Loot.RemoveAt(equipmentIndex);
            }
            else
            {
                source.Loot[equipmentIndex] = replacement;
            }
            changed = true;
        }

        while (mate.SustainmentSupplyCount < mate.SustainmentSupplyCapacityForWorld)
        {
            var supplyIndex = FindBestSupplyIndex(mate, source);
            if (supplyIndex < 0)
            {
                break;
            }
            var item = source.Loot[supplyIndex];
            if (mate.TryStoreSustainmentSupply(item, 1) != 1)
            {
                break;
            }
            item.Quantity--;
            if (item.Quantity <= 0)
            {
                source.Loot.RemoveAt(supplyIndex);
            }
            changed = true;
        }

        if (!changed)
        {
            return false;
        }
        if (carriedWeaponRemoved && source is EnemyOperator corpse)
        {
            corpse.MarkCarriedWeaponRemoved();
        }
        _runtime.CommitLootMutation(source);
        return true;
    }

    internal int RecoveredValue(IReadOnlyList<SquadMate> mates)
    {
        var total = 0;
        for (var index = 0; index < mates.Count; index++)
        {
            var mate = mates[index];
            if (GodotObject.IsInstanceValid(mate)
                && mate.IsExtractionPassenger
                && !mate.IsDowned
                && !mate.IsBodyBag)
            {
                total += mate.RecoveredSustainmentValue;
            }
        }
        return total;
    }

    private static int FindBestItemIndex(
        SquadMate mate,
        ILootSource source,
        LootItemKind kind)
    {
        var bestIndex = -1;
        var bestUtility = 0.0f;
        for (var index = 0; index < source.Loot.Count; index++)
        {
            var item = source.Loot[index];
            if (item.Kind != kind)
            {
                continue;
            }
            var utility = mate.EvaluateSustainmentLootUtility(item);
            if (utility > bestUtility)
            {
                bestUtility = utility;
                bestIndex = index;
            }
        }
        return bestIndex;
    }

    private static int FindBestWeaponIndex(
        SquadMate mate,
        ILootSource source,
        out int bestAmmoIndex,
        out LootGrade bestAmmoGrade)
    {
        var bestWeaponIndex = -1;
        bestAmmoIndex = -1;
        bestAmmoGrade = LootGrade.Common;
        var bestUtility = 0.0f;
        for (var index = 0; index < source.Loot.Count; index++)
        {
            var item = source.Loot[index];
            if (item.Kind != LootItemKind.Weapon
                || item.Weapon is null
                || item.Quantity != 1)
            {
                continue;
            }
            var ammoIndex = FindBestMatchingAmmoIndex(
                source,
                item.Weapon,
                out var ammoGrade);
            var utility = mate.EvaluateSustainmentWeaponUtility(item, ammoGrade);
            if (utility <= bestUtility)
            {
                continue;
            }
            bestUtility = utility;
            bestWeaponIndex = index;
            bestAmmoIndex = ammoIndex;
            bestAmmoGrade = ammoGrade;
        }
        return bestWeaponIndex;
    }

    private static LootGrade MatchingAmmoGrade(ILootSource source, WeaponBuild weapon)
    {
        FindBestMatchingAmmoIndex(source, weapon, out var grade);
        return grade;
    }

    private static int FindBestMatchingAmmoIndex(
        ILootSource source,
        WeaponBuild weapon,
        out LootGrade grade)
    {
        var caliber = WeaponCatalog.Weapon(weapon.Platform).Caliber;
        var bestIndex = -1;
        grade = LootGrade.Common;
        for (var index = 0; index < source.Loot.Count; index++)
        {
            var item = source.Loot[index];
            if (item.Kind != LootItemKind.Ammunition
                || item.Quantity <= 0
                || item.AmmoCaliber != caliber
                || (bestIndex >= 0 && item.Grade <= grade))
            {
                continue;
            }
            bestIndex = index;
            grade = item.Grade;
        }
        return bestIndex;
    }

    private static int FindBestSupplyIndex(SquadMate mate, ILootSource source)
    {
        var bestIndex = -1;
        var bestUtility = 0.0f;
        for (var index = 0; index < source.Loot.Count; index++)
        {
            var item = source.Loot[index];
            if (item.Quantity <= 0
                || item.Kind is not (LootItemKind.Medical or LootItemKind.ArmorPlate))
            {
                continue;
            }
            var utility = mate.EvaluateSustainmentLootUtility(item);
            if (utility > bestUtility)
            {
                bestUtility = utility;
                bestIndex = index;
            }
        }
        return bestIndex;
    }

    private bool IsReservedByOther(ILootSource source, SquadMate mate, ulong now)
    {
        var sourceId = source.LootNode.GetInstanceId();
        if (!_reservations.TryGetValue(sourceId, out var reservation))
        {
            return false;
        }
        if (reservation.ExpiresMilliseconds <= now)
        {
            _reservations.Remove(sourceId);
            return false;
        }
        return reservation.MateId != mate.GetInstanceId();
    }
}
