using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateInventoryEquipment()
    {
        var valid = false;
        try
        {
            ActivateBattlefieldFromOperationsOffice();
            foreach (var enemy in _enemies) enemy.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = new Vector3(0, .2f, 82);
            _player.Velocity = Vector3.Zero;
            LootItem Weapon(WeaponPlatform platform, LootGrade grade) => new()
            {
                Kind = LootItemKind.Weapon, Weapon = WeaponCatalog.Build(platform, 1), Grade = grade
            };
            _player.EquipFromLoot(Weapon(WeaponPlatform.M3A1, LootGrade.Uncommon));
            _player.EquipFromLoot(Weapon(WeaponPlatform.AK74, LootGrade.Uncommon));
            _player.EquipFromLoot(Weapon(WeaponPlatform.P226, LootGrade.Uncommon));
            foreach (var definition in new[] { "helmet_heavy", "armor_carrier", "pack_heavy" })
                _player.EquipFromLoot(new LootItem
                {
                    Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create(definition),
                    Grade = LootGrade.Rare
                });

            var source = _enemies.First();
            source.TakeDamage(9999, source.GlobalPosition + Vector3.Up, _player);
            source.Loot.Clear();
            var upgrade = Weapon(WeaponPlatform.M4A1, LootGrade.Legendary);
            source.Loot.Add(upgrade);
            OpenLootLocal(source);
            while (_player.Backpack.Count < _player.BackpackCapacity)
                _player.Backpack.Add(new LootItem { Kind = LootItemKind.Valuable });
            TakeLootItem(upgrade.Id);
            var upgraded = _player.PrimaryWeaponBuild?.Platform == WeaponPlatform.M4A1
                && _player.PrimaryWeaponGrade == LootGrade.Legendary
                && _player.SecondaryWeaponPlatform == WeaponPlatform.AK74
                && source.Loot.Any(item => item.Weapon?.Platform == WeaponPlatform.M3A1
                    && item.Grade == LootGrade.Uncommon)
                && _player.Backpack.Count == _player.BackpackCapacity;
            var sidearm = Weapon(WeaponPlatform.M1911, LootGrade.Epic);
            source.Loot.Add(sidearm);
            TakeLootItem(sidearm.Id);
            upgraded &= _player.SidearmWeaponPlatform == WeaponPlatform.M1911
                && source.Loot.Any(item => item.Weapon?.Platform == WeaponPlatform.P226);
            upgraded &= LootInteractionPolicy.SelectAutomaticWeaponSlot(false, LootGrade.Common,
                LootGrade.Legendary, LootGrade.Uncommon, null) is null;
            upgraded &= LootInteractionPolicy.SelectAutomaticWeaponSlot(false, LootGrade.Uncommon,
                LootGrade.Legendary, LootGrade.Uncommon, null) is null;
            upgraded &= LootInteractionPolicy.SelectAutomaticWeaponSlot(true, LootGrade.Common,
                null, null, LootGrade.Epic) is null;

            var fits = true;
            foreach (var language in new[] { "zh", "en" })
            {
                _hud.SetLanguage(language);
                _hud.ShowLoot("Equipment review", source.Loot, _player);
                await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
                fits &= _hud.EquipmentSlotsFitForDiagnostics;
                SaveViewportImage($"res://logs/equipment_{language}_validation.png");
                GD.Print($"INVENTORY_EQUIPMENT_LAYOUT language={language} fits={_hud.EquipmentSlotsFitForDiagnostics} detail={_hud.EquipmentLayoutForDiagnostics}");
            }
            var view = GD.Load<PackedScene>(LootEquipmentSlotView.ScenePath).Instantiate<LootEquipmentSlotView>();
            AddChild(view);
            view.Visible = false;
            var intents = 0;
            view.RemoveRequested += () => intents++;
            view.SetEquipment(EquipmentCatalog.Create("helmet_heavy"), "zh");
            view.SetLanguage("en");
            var silentBinding = intents == 0;
            view.GetNode<Button>("Margin/Content/Header/Remove").EmitSignal(Button.SignalName.Pressed);
            var intent = silentBinding && intents == 1;
            view.QueueFree();
            valid = upgraded && fits && intent;
            GD.Print($"INVENTORY_EQUIPMENT_CHECK upgrades={upgraded} layout={fits} intent={intent} valid={valid}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"INVENTORY_EQUIPMENT_CHECK error={exception}");
        }
        GD.Print($"INVENTORY_EQUIPMENT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
