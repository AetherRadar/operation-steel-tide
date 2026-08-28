using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string ReadmeGalleryDirectory = "res://docs/media";

    private async void CaptureReadmeChineseGallery()
    {
        var window = GetWindow();
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1600, 900);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        SetCaptureLanguage("zh");
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        ConfigureReadmeCaptureLighting();
        DisableReadmeCaptureActors();

        var mates = _squadMates
            .Where(mate => IsInstanceValid(mate))
            .OrderBy(mate => mate.SquadSlot)
            .Take(2)
            .ToArray();
        var combatEnemies = _enemies
            .Where(enemy => IsInstanceValid(enemy))
            .Take(2)
            .ToArray();
        var actorsReady = mates.Length == 2 && combatEnemies.Length == 2;
        if (!actorsReady)
        {
            PrintReadmeCaptureResult(false, false, false, false, false, false, mates.Length);
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        _missionDirector.ExitDeploymentZone();
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 2));
        _player.Visible = true;
        _hud.Visible = true;
        _hud.SetAmmoTier(LootGrade.Rare);
        _hud.SetEnemyCount(18);
        _hud.SetMissionPhase("INFILTRATION", 0.0f, false);
        RefreshLocalizedObjective();

        StageReadmeSquad(mates);
        StageReadmePlayer(
            new Vector3(-73.5f, 0.2f, -105.5f),
            new Vector3(-84.2f, 1.65f, -121.5f));
        _hud.SetMinimapPlayer(_player.GlobalPosition, 216.0f);
        _hud.SetSquadOrder(SquadOrder.Move);
        await WaitFrames(24);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.AimCameraAtWorldPointForDiagnostics(new Vector3(-84.2f, 1.65f, -121.5f));
        await WaitFrames(10);
        var squadSaved = SaveReadmeGalleryFrame("gameplay-squad-zh.webp");

        foreach (var mate in mates)
        {
            mate.Visible = false;
        }
        _player.GlobalPosition = new Vector3(18.0f, 3.85f, -113.0f);
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 4.8f, -126.0f));
        _player.AimCameraAtWorldPointForDiagnostics(new Vector3(0.0f, 4.8f, -126.0f));
        _hud.SetMinimapPlayer(_player.GlobalPosition, 258.0f);
        _hud.SetMissionPhase("INFILTRATION", 0.0f, false);
        await WaitFrames(12);
        var tacticalSaved = SaveReadmeGalleryFrame("gameplay-tactical-zh.webp");

        var playerCombatPosition = new Vector3(0.8f, 0.2f, 25.0f);
        StageReadmeCombatEnemies(combatEnemies, playerCombatPosition);
        var targetPoint = combatEnemies[0].GlobalPosition + Vector3.Up * 1.25f;
        _player.ProcessMode = ProcessModeEnum.Inherit;
        StageReadmePlayer(
            playerCombatPosition,
            targetPoint);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 182.0f);
        _hud.SetMissionPhase("COMBAT", 0.0f, false);
        _hud.SetEnemyCount(12);
        await WaitFrames(18);
        var fired = _player.FireForDiagnostics();
        await WaitFrames(1);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.AimCameraAtWorldPointForDiagnostics(targetPoint);
        var combatSaved = fired && SaveReadmeGalleryFrame("gameplay-combat-zh.webp");

        PrepareReadmeCaptureBackpack();
        OpenPersonalBackpack();
        await WaitFrames(30);
        var lootSaved = SaveReadmeGalleryFrame("gameplay-loot-zh.webp");

        CloseLoot();
        _hud.ShowDemolitionBuy(new DemolitionBuySnapshot(
            7,
            3,
            3,
            DemolitionTeam.Attackers,
            4100,
            11.8f,
            DemolitionBuyDuration,
            false));
        _hud.SelectDemolitionBuySidearmForDiagnostics(DemolitionBuyCatalog.Gsh18Id);
        _hud.SelectDemolitionBuyPrimaryForDiagnostics(DemolitionBuyCatalog.M4A1Id);
        _hud.SetDemolitionBuyGrenadesForDiagnostics(1);
        _hud.SetDemolitionBuySmokeGrenadesForDiagnostics(1);
        await WaitFrames(18);
        var demolitionSaved = SaveReadmeGalleryFrame("gameplay-demolition-zh.webp");

        var valid = _languageSetting == "zh"
            && squadSaved
            && tacticalSaved
            && combatSaved
            && lootSaved
            && demolitionSaved;
        PrintReadmeCaptureResult(
            valid,
            squadSaved,
            tacticalSaved,
            combatSaved,
            lootSaved,
            demolitionSaved,
            mates.Length);
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private void ConfigureReadmeCaptureLighting()
    {
        _environmentRef.TonemapExposure = 1.22f;
        _environmentRef.AmbientLightEnergy = 1.34f;
        _environmentRef.FogDensity = 0.0008f;
        SetIfSupported(_environmentRef, "adjustment_brightness", 1.08f);
        SetIfSupported(_environmentRef, "adjustment_contrast", 1.01f);
        SetIfSupported(_environmentRef, "adjustment_saturation", 1.04f);
        _sunLight.RotationDegrees = new Vector3(-34.0f, 112.0f, 0.0f);
        _sunLight.LightColor = new Color(1.0f, 0.86f, 0.72f);
        _sunLight.LightEnergy = 1.05f;
        _fillLight.RotationDegrees = new Vector3(-26.0f, -62.0f, 0.0f);
        _fillLight.LightColor = new Color(0.58f, 0.72f, 0.84f);
        _fillLight.LightEnergy = 0.68f;
    }

    private void DisableReadmeCaptureActors()
    {
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.Visible = false;
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.Visible = false;
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var vehicle in _vehicles)
        {
            if (IsInstanceValid(vehicle))
            {
                vehicle.Visible = false;
            }
        }
        if (IsInstanceValid(_worldBoss))
        {
            _worldBoss!.Visible = false;
        }
    }

    private static void StageReadmeSquad(SquadMate[] mates)
    {
        var positions = new[]
        {
            new Vector3(-77.2f, 0.12f, -109.2f),
            new Vector3(-80.1f, 0.12f, -111.4f)
        };
        var target = new Vector3(-86.0f, 0.12f, -128.0f);
        for (var i = 0; i < mates.Length && i < positions.Length; i++)
        {
            var mate = mates[i];
            mate.Visible = true;
            mate.GlobalPosition = positions[i];
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetOrder(SquadOrder.Hold, positions[i]);
            mate.LookAt(new Vector3(target.X, positions[i].Y, target.Z), Vector3.Up);
            mate.SetAuthoredMovementPoseForDiagnostics(i == 0 ? 2.6f : 4.2f);
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private static void StageReadmeCombatEnemies(
        EnemyOperator[] enemies,
        Vector3 playerPosition)
    {
        var positions = new[]
        {
            new Vector3(0.0f, 0.12f, 15.5f),
            new Vector3(3.0f, 0.12f, 13.5f)
        };
        for (var i = 0; i < enemies.Length && i < positions.Length; i++)
        {
            var enemy = enemies[i];
            enemy.Visible = true;
            enemy.GlobalPosition = positions[i];
            enemy.GrantFireablePrimaryForDiagnostics(
                WeaponCatalog.Build(i == 0 ? WeaponPlatform.AK74 : WeaponPlatform.M4A1, 1));
            enemy.LookAt(
                new Vector3(playerPosition.X, positions[i].Y, playerPosition.Z),
                Vector3.Up);
            enemy.SetAuthoredCombatPoseForDiagnostics();
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private void StageReadmePlayer(Vector3 position, Vector3 target)
    {
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.GlobalPosition = position;
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(target);
        _player.SetViewPitchForDiagnostics(0.0f);
        _player.AimCameraAtWorldPointForDiagnostics(target);
    }

    private void PrepareReadmeCaptureBackpack()
    {
        _player.ClearBackpackForDiagnostics();
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Bandage, Quantity = 2, Grade = LootGrade.Common });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.FieldMedkit, Quantity = 1, Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Adrenaline, Quantity = 1, Grade = LootGrade.Epic });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 2, Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Rifle, Quantity = 45, Grade = LootGrade.Epic });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Weapon, Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 2), Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("armor_heavy"), Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("pack_heavy"), Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Valuable, ValuableKind = ValuableItemKind.GraphicsCard, Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Valuable, ValuableKind = ValuableItemKind.AntiqueClock, Grade = LootGrade.Legendary });
    }

    private bool SaveReadmeGalleryFrame(string fileName)
    {
        var image = GetViewport().GetTexture().GetImage();
        if (image.IsEmpty() || image.GetWidth() != 1600 || image.GetHeight() != 900)
        {
            return false;
        }
        var path = ProjectSettings.GlobalizePath($"{ReadmeGalleryDirectory}/{fileName}");
        return image.SaveWebp(path, lossy: true, quality: 0.9f) == Error.Ok;
    }

    private void PrintReadmeCaptureResult(
        bool valid,
        bool squad,
        bool tactical,
        bool combat,
        bool loot,
        bool demolition,
        int mateCount)
    {
        GD.Print(
            $"README_GALLERY_CAPTURE_CHECK valid={valid} language={_languageSetting} "
            + $"resolution=1600x900 mates={mateCount} squad={squad} tactical={tactical} "
            + $"combat={combat} loot={loot} demolition={demolition}");
        GD.Print($"README_GALLERY_CAPTURE_PASS valid={valid}");
    }
}
