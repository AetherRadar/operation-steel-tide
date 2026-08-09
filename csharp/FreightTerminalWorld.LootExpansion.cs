using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void SpawnCivilianValuableLoot()
    {
        var placements = new (Vector3 Position, ValuableItemKind Kind, LootGrade Grade)[]
        {
            (new Vector3(-121, 0.2f, -34), ValuableItemKind.CannedCoffee, LootGrade.Common),
            (new Vector3(-111, 0.2f, 22), ValuableItemKind.CeramicTeaSet, LootGrade.Common),
            (new Vector3(-65, 0.2f, -119), ValuableItemKind.HandToolSet, LootGrade.Uncommon),
            (new Vector3(-34, 0.2f, 73), ValuableItemKind.SmartPhone, LootGrade.Uncommon),
            (new Vector3(84, 0.2f, 66), ValuableItemKind.Wristwatch, LootGrade.Uncommon),
            (new Vector3(43, 0.2f, -38), ValuableItemKind.VintageCamera, LootGrade.Rare),
            (new Vector3(21, 0.2f, -142), ValuableItemKind.GraphicsCard, LootGrade.Rare),
            (new Vector3(124, 0.2f, -66), ValuableItemKind.DesignerPerfume, LootGrade.Rare),
            (new Vector3(-49, 0.2f, -18), ValuableItemKind.CollectorCoin, LootGrade.Epic),
            (new Vector3(17, 0.2f, 74), ValuableItemKind.GoldJewelry, LootGrade.Epic),
            (new Vector3(55, 0.2f, -113), ValuableItemKind.EncryptedDrive, LootGrade.Epic),
            (new Vector3(31, 0.2f, 71), ValuableItemKind.AntiqueClock, LootGrade.Legendary)
        };

        for (var index = 0; index < placements.Length; index++)
        {
            var placement = placements[index];
            var item = new LootItem
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = placement.Kind,
                Grade = placement.Grade
            };
            var pickup = new GradedLootPickup
            {
                Name = $"CivilianValuable_{index + 1:00}_{placement.Kind}",
                Position = placement.Position
            };
            pickup.Configure(
                item,
                ValuableItems.DisplayName(placement.Kind, "en"),
                ValuableItems.DisplayName(placement.Kind, "zh"));
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
        }
    }
}
