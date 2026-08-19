using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OperationSteelTide;

public static class ExtractionLootNetworkCodec
{
    public static string SerializeItems(IReadOnlyList<LootItem> items)
    {
        var wire = new LootItemWire[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            wire[index] = LootItemWire.FromItem(items[index]);
        }
        return JsonSerializer.Serialize(wire);
    }

    public static List<LootItem> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<LootItem>();
        }
        try
        {
            var wire = JsonSerializer.Deserialize<LootItemWire[]>(json)
                ?? Array.Empty<LootItemWire>();
            var result = new List<LootItem>(wire.Length);
            foreach (var item in wire)
            {
                var converted = item.ToItem();
                if (converted is not null)
                {
                    result.Add(converted);
                }
            }
            return result;
        }
        catch (JsonException)
        {
            return new List<LootItem>();
        }
    }

    private sealed class LootItemWire
    {
        public string Id { get; set; } = string.Empty;
        public int Kind { get; set; }
        public int Grade { get; set; }
        public int Quantity { get; set; }
        public int WeaponPlatform { get; set; } = -1;
        public Dictionary<int, string> Attachments { get; set; } = new();
        public string AttachmentId { get; set; } = string.Empty;
        public string EquipmentId { get; set; } = string.Empty;
        public float EquipmentDurability { get; set; }
        public int AmmoCaliber { get; set; }
        public string KnifeSkinId { get; set; } = string.Empty;
        public int MedicalKind { get; set; }
        public int ValuableKind { get; set; }

        public static LootItemWire FromItem(LootItem item)
        {
            var wire = new LootItemWire
            {
                Id = item.Id,
                Kind = (int)item.Kind,
                Grade = (int)item.Grade,
                Quantity = item.Quantity,
                WeaponPlatform = item.Weapon is null ? -1 : (int)item.Weapon.Platform,
                AttachmentId = item.AttachmentId,
                EquipmentId = item.Equipment?.DefinitionId ?? string.Empty,
                EquipmentDurability = item.Equipment?.Durability ?? 0.0f,
                AmmoCaliber = (int)item.AmmoCaliber,
                KnifeSkinId = item.KnifeSkinId,
                MedicalKind = (int)item.MedicalKind,
                ValuableKind = (int)item.ValuableKind
            };
            if (item.Weapon is not null)
            {
                foreach (var pair in item.Weapon.Attachments)
                {
                    wire.Attachments[(int)pair.Key] = pair.Value;
                }
            }
            return wire;
        }

        public LootItem? ToItem()
        {
            if (!Enum.IsDefined(typeof(LootItemKind), Kind)
                || !Enum.IsDefined(typeof(LootGrade), Grade)
                || !Enum.IsDefined(typeof(AmmoCaliber), AmmoCaliber)
                || !Enum.IsDefined(typeof(MedicalItemKind), MedicalKind)
                || !Enum.IsDefined(typeof(ValuableItemKind), ValuableKind))
            {
                return null;
            }
            WeaponBuild? weapon = null;
            if (WeaponPlatform >= 0 && Enum.IsDefined(typeof(WeaponPlatform), WeaponPlatform))
            {
                weapon = new WeaponBuild { Platform = (WeaponPlatform)WeaponPlatform };
                foreach (var pair in Attachments)
                {
                    if (Enum.IsDefined(typeof(AttachmentSlot), pair.Key))
                    {
                        weapon.Attachments[(AttachmentSlot)pair.Key] = pair.Value;
                    }
                }
            }
            EquipmentItem? equipment = null;
            if (!string.IsNullOrEmpty(EquipmentId))
            {
                equipment = new EquipmentItem
                {
                    DefinitionId = EquipmentId,
                    Durability = EquipmentDurability
                };
            }
            return new LootItem
            {
                Id = string.IsNullOrEmpty(Id) ? Guid.NewGuid().ToString("N") : Id,
                Kind = (LootItemKind)Kind,
                Grade = (LootGrade)Grade,
                Quantity = Math.Max(0, Quantity),
                Weapon = weapon,
                AttachmentId = AttachmentId,
                Equipment = equipment,
                AmmoCaliber = (AmmoCaliber)AmmoCaliber,
                KnifeSkinId = KnifeSkinId,
                MedicalKind = (MedicalItemKind)MedicalKind,
                ValuableKind = (ValuableItemKind)ValuableKind
            };
        }
    }
}
