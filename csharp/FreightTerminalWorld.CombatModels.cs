using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateOperatorAnimations()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        AuthoredOperatorVisual? visual = null;
        var transitions = new System.Collections.Generic.List<string>();
        var sockets = false;
        var weaponSocketPosition = Vector3.Zero;
        var backWeaponSocketPosition = Vector3.Zero;
        var rifleFit = default(OperatorRifleFitInspection);
        var readyIdleFit = default(OperatorRifleFitInspection);
        var movementRifleFits = new System.Collections.Generic.List<string>();
        var movementRifleFitValid = true;
        var count = 0;
        try
        {
            visual = CombatModelLibrary.InstantiateOperator();
            AddChild(visual.Root);
            var animator = new AuthoredOperatorAnimator(visual);
            count = animator.AnimationCount;
            void Sample(
                float speed,
                bool weaponReadied,
                bool prone,
                bool crouched,
                bool aiming,
                bool downed,
                bool reviving,
                bool dead)
            {
                animator.Update(0.25f, speed, weaponReadied, prone, crouched, aiming, downed, reviving, dead);
                transitions.Add(animator.CurrentAnimation);
            }
            Sample(0.0f, false, false, false, false, false, false, false);
            Sample(0.0f, true, false, false, false, false, false, false);
            Sample(0.0f, true, false, false, true, false, false, false);
            Sample(1.8f, false, false, false, false, false, false, false);
            Sample(3.4f, false, false, false, false, false, false, false);
            Sample(5.2f, false, false, false, false, false, false, false);
            Sample(1.8f, true, false, false, false, false, false, false);
            Sample(3.4f, true, false, false, false, false, false, false);
            Sample(5.2f, true, false, false, false, false, false, false);
            Sample(1.8f, true, false, false, true, false, false, false);
            Sample(3.4f, true, false, false, true, false, false, false);
            Sample(5.2f, true, false, false, true, false, false, false);
            Sample(0.0f, false, false, true, false, false, false, false);
            Sample(1.5f, false, false, true, false, false, false, false);
            Sample(0.0f, true, false, true, false, false, false, false);
            Sample(1.5f, true, false, true, false, false, false, false);
            Sample(0.0f, true, false, true, true, false, false, false);
            Sample(1.5f, true, false, true, true, false, false, false);
            Sample(0.0f, true, true, false, true, false, false, false);
            Sample(1.1f, true, true, false, true, false, false, false);
            Sample(0.0f, false, false, false, false, false, true, false);
            Sample(0.0f, false, false, false, false, true, false, false);
            animator.PlayHit();
            transitions.Add(animator.CurrentAnimation);
            animator.PlayRevived();
            transitions.Add(animator.CurrentAnimation);
            animator.Update(0.7f, 0.0f, false, false, false, false, false, false, false);
            Sample(0.0f, false, false, false, false, false, false, true);
            sockets = IsInstanceValid(visual.WeaponSocket)
                && IsInstanceValid(visual.BackWeaponSocket)
                && IsInstanceValid(visual.HeadSocket)
                && IsInstanceValid(visual.VestSocket)
                && IsInstanceValid(visual.BackpackSocket)
                && IsInstanceValid(visual.TeamPatchSocket);
            weaponSocketPosition = visual.WeaponSocket.GlobalPosition;
            backWeaponSocketPosition = visual.BackWeaponSocket.GlobalPosition;
            visual.SetWeaponReadied(true);
            visual.AnimationPlayer.Play("ready_idle", 0.0);
            visual.AnimationPlayer.Seek(0.0, update: true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            readyIdleFit = visual.InspectRifleFit();
            visual.AnimationPlayer.Play("aim_idle", 0.0);
            visual.AnimationPlayer.Seek(0.0, update: true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            rifleFit = visual.InspectRifleFit();
            foreach (var (animation, time) in new[]
            {
                ("ready_idle", 0.0),
                ("ready_walk", 0.33),
                ("ready_run", 0.2),
                ("ready_sprint", 0.17),
                ("ready_crouch_idle", 0.0),
                ("ready_crouch_walk", 0.5),
                ("aim_walk", 0.33),
                ("aim_run", 0.2),
                ("aim_sprint", 0.17),
                ("aim_crouch_idle", 0.0),
                ("aim_crouch_walk", 0.5)
            })
            {
                visual.AnimationPlayer.Play(animation, 0.0);
                visual.AnimationPlayer.Seek(time, update: true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var movementFit = visual.InspectRifleFit();
                var handsFit = movementFit.PrimaryHandDistance <= 0.025f
                    && movementFit.SupportHandDistance <= 0.16f;
                movementRifleFitValid &= handsFit;
                movementRifleFits.Add(
                    $"{animation}:{handsFit}:primary={movementFit.PrimaryHandDistance:F3}:"
                    + $"support={movementFit.SupportHandDistance:F3}:"
                    + $"support_offset={movementFit.SupportHandOffset}:muzzle={movementFit.MuzzleOffset}");
            }
        }
        catch (System.Exception exception)
        {
            GD.PushError($"Operator animation validation failed to instantiate: {exception}");
        }

        var expected = new[]
        {
            "idle", "ready_idle", "aim_idle", "walk", "run", "sprint",
            "ready_walk", "ready_run", "ready_sprint", "aim_walk", "aim_run", "aim_sprint",
            "crouch_idle", "crouch_walk", "ready_crouch_idle", "ready_crouch_walk",
            "aim_crouch_idle", "aim_crouch_walk",
            "prone_idle", "prone_crawl", "revive_kneel", "downed", "hit",
            "revived", "death"
        };
        var transitionsValid = transitions.SequenceEqual(expected);
        var readyDistinct = readyIdleFit.WeaponOrigin.Y <= rifleFit.WeaponOrigin.Y - 0.18f;
        var readyCrossBody = Mathf.Abs(readyIdleFit.MuzzleOffset.X) >= 0.16f
            && readyIdleFit.MuzzleOffset.Z <= -0.38f;
        var valid = count == 25
            && sockets
            && transitionsValid
            && rifleFit.Valid
            && movementRifleFitValid
            && readyDistinct
            && readyCrossBody;
        GD.Print(
            $"OPERATOR_ANIMATIONS_CHECK count={count} sockets={sockets} "
            + $"weapon_socket={weaponSocketPosition} back_socket={backWeaponSocketPosition} "
            + $"rifle_fit={rifleFit.Valid} primary_hand={rifleFit.PrimaryHandDistance:F3} "
            + $"support_hand={rifleFit.SupportHandDistance:F3} "
            + $"support_offset={rifleFit.SupportHandOffset} "
            + $"ready_distinct={readyDistinct} ready_weapon={readyIdleFit.WeaponOrigin} "
            + $"ready_cross_body={readyCrossBody} aim_weapon={rifleFit.WeaponOrigin} "
            + $"ready_muzzle={readyIdleFit.MuzzleOffset} "
            + $"movement_rifle_fit={string.Join(',', movementRifleFits)} "
            + $"muzzle_offset={rifleFit.MuzzleOffset} stock_offset={rifleFit.StockOffset} "
            + $"transitions={string.Join('>', transitions)} expected={string.Join('>', expected)}");
        GD.Print($"OPERATOR_ANIMATIONS_PASS valid={valid}");
        visual?.Root.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async void ValidateCombatModels()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var weapon = CombatModelLibrary.InspectWeapon();
        var platformInspections = Enum.GetValues<WeaponPlatform>()
            .ToDictionary(platform => platform, CombatModelLibrary.InspectWeapon);
        var platformGeometry = platformInspections.ToDictionary(
            pair => pair.Key,
            pair => IsValidPlatformWeapon(pair.Key, pair.Value));
        var lineupCaptured = await CaptureAuthoredWeaponLineup();
        var firstPersonCaptures = await CaptureFirstPersonWeaponViews();
        var operatorModel = CombatModelLibrary.InspectOperator();
        var previewOperator = CombatModelLibrary.InspectPreviewOperator();
        var gsh18 = CombatModelLibrary.InspectGsh18();
        var desertEagle = CombatModelLibrary.InspectDesertEagle();
        var firstPersonSmg = CombatModelLibrary.InspectFirstPersonSmg45();
        var weaponGeometry = weapon.Loaded
            && weapon.RequiredNodes
            && weapon.MeshCount >= 8
            && weapon.Size.X is >= 0.15f and <= 0.8f
            && weapon.Size.Y is >= 0.25f and <= 1.15f
            && weapon.Size.Z is >= 1.4f and <= 2.5f;
        var operatorGeometry = operatorModel.Loaded
            && operatorModel.RequiredNodes
            && operatorModel.MeshCount >= 1
            && operatorModel.MaterialCount >= 8
            && operatorModel.Size.X is >= 0.55f and <= 1.25f
            && operatorModel.Size.Y is >= 1.75f and <= 2.3f
            && operatorModel.Size.Z is >= 0.3f and <= 0.8f;
        var previewOperatorGeometry = previewOperator.Loaded
            && previewOperator.RequiredNodes
            && previewOperator.MeshCount >= 1
            && previewOperator.MaterialCount >= 8
            && previewOperator.Size.X is >= 1.3f and <= 1.9f
            && previewOperator.Size.Y is >= 2.45f and <= 2.65f
            && previewOperator.Size.Z is >= 0.35f and <= 0.8f;
        var gsh18Geometry = gsh18.Loaded
            && gsh18.RequiredNodes
            && gsh18.MeshCount >= 10
            && gsh18.MaterialCount >= 1
            && gsh18.Size.X > 0.001f
            && gsh18.Size.Y > 0.001f
            && gsh18.Size.Z is >= 0.65f and <= 0.9f;
        var desertEagleGeometry = desertEagle.Loaded
            && desertEagle.RequiredNodes
            && desertEagle.MeshCount >= 20
            && desertEagle.MaterialCount >= 4
            && desertEagle.Size.X is >= 0.1f and <= 0.3f
            && desertEagle.Size.Y is >= 0.45f and <= 0.75f
            && desertEagle.Size.Z is >= 0.9f and <= 1.2f;
        var firstPersonSmgGeometry = firstPersonSmg.Loaded
            && firstPersonSmg.RequiredNodes
            && firstPersonSmg.MeshCount >= 7
            && firstPersonSmg.MaterialCount >= 2
            && firstPersonSmg.Size.X is >= 0.45f and <= 1.1f
            && firstPersonSmg.Size.Y is >= 0.3f and <= 0.9f
            && firstPersonSmg.Size.Z is >= 1.2f and <= 1.8f;
        var playerAuthored = _player.UsesAuthoredPrimaryWeaponForDiagnostics;
        var squadAuthored = _squadMates.Count > 0
            && _squadMates.Where(IsInstanceValid).All(mate => mate.UsesAuthoredOperatorForDiagnostics);
        var livingEnemies = _enemies.Where(IsInstanceValid).ToArray();
        var humanEnemies = livingEnemies.Where(enemy => !enemy.IsWorldBoss).ToArray();
        var enemiesAuthored = humanEnemies.Length > 0
            && humanEnemies.All(enemy => enemy.UsesAuthoredOperatorForDiagnostics);
        var garrison = livingEnemies.FirstOrDefault(enemy => !enemy.IsRivalSquad && !enemy.IsWorldBoss);
        var rivals = livingEnemies.Where(enemy => enemy.IsRivalSquad).ToArray();
        var garrisonColor = garrison?.AuthoredTeamColorForDiagnostics ?? Colors.Transparent;
        var factionAppearance = garrison is not null
            && rivals.Length >= 2
            && garrison.AuthoredGearOverlayCountForDiagnostics >= 3
            && garrisonColor.G > garrisonColor.R + 0.3f
            && garrisonColor.B > garrisonColor.R + 0.2f
            && rivals.All(enemy => enemy.AuthoredGearOverlayCountForDiagnostics >= 3)
            && rivals.Any(enemy =>
                ColorDistance(enemy.AuthoredTeamColorForDiagnostics, garrisonColor) > 0.55f)
            && rivals.Select(enemy => enemy.AuthoredTeamColorForDiagnostics).Distinct().Count() >= 2;
        var valid = weaponGeometry
            && platformGeometry.Values.All(value => value)
            && lineupCaptured
            && firstPersonCaptures.Values.All(value => value)
            && operatorGeometry
            && previewOperatorGeometry
            && gsh18Geometry
            && desertEagleGeometry
            && firstPersonSmgGeometry
            && playerAuthored
            && squadAuthored
            && enemiesAuthored
            && factionAppearance;

        GD.Print(
            $"COMBAT_MODELS_CHECK weapon_loaded={weapon.Loaded} weapon_nodes={weapon.RequiredNodes} "
            + $"weapon_meshes={weapon.MeshCount} weapon_size={weapon.Size} "
            + $"operator_loaded={operatorModel.Loaded} operator_nodes={operatorModel.RequiredNodes} "
            + $"operator_meshes={operatorModel.MeshCount} operator_materials={operatorModel.MaterialCount} "
            + $"operator_size={operatorModel.Size} "
            + $"preview_operator_loaded={previewOperator.Loaded} preview_operator_nodes={previewOperator.RequiredNodes} "
            + $"preview_operator_meshes={previewOperator.MeshCount} preview_operator_materials={previewOperator.MaterialCount} "
            + $"preview_operator_size={previewOperator.Size} "
            + $"gsh18_loaded={gsh18.Loaded} gsh18_nodes={gsh18.RequiredNodes} "
            + $"gsh18_meshes={gsh18.MeshCount} gsh18_materials={gsh18.MaterialCount} gsh18_size={gsh18.Size} "
            + $"deagle_loaded={desertEagle.Loaded} deagle_nodes={desertEagle.RequiredNodes} "
            + $"deagle_meshes={desertEagle.MeshCount} deagle_materials={desertEagle.MaterialCount} "
            + $"deagle_size={desertEagle.Size} "
            + $"smg45_fp_loaded={firstPersonSmg.Loaded} smg45_fp_nodes={firstPersonSmg.RequiredNodes} "
            + $"smg45_fp_meshes={firstPersonSmg.MeshCount} smg45_fp_materials={firstPersonSmg.MaterialCount} "
            + $"smg45_fp_size={firstPersonSmg.Size} "
            + $"platforms={string.Join(',', platformInspections.Select(pair => $"{pair.Key}:{FormatWeaponInspection(pair.Value, platformGeometry[pair.Key])}"))} "
            + $"lineup={lineupCaptured} "
            + $"first_person={string.Join(',', firstPersonCaptures.Select(pair => $"{pair.Key}:{pair.Value}"))} "
            + $"player_authored={playerAuthored} squad_authored={squadAuthored} "
            + $"enemies_authored={enemiesAuthored} enemies={livingEnemies.Length} "
            + $"faction_appearance={factionAppearance} garrison_color={garrisonColor} "
            + $"rival_colors={rivals.Select(enemy => enemy.AuthoredTeamColorForDiagnostics).Distinct().Count()}");
        GD.Print($"COMBAT_MODELS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async System.Threading.Tasks.Task<bool> CaptureAuthoredWeaponLineup()
    {
        var viewport = new SubViewport
        {
            Name = "AuthoredWeaponLineupViewport",
            Size = new Vector2I(1600, 900),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(viewport);
        var stage = new Node3D { Name = "AuthoredWeaponLineupStage" };
        viewport.AddChild(stage);
        stage.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.035f, 0.045f, 0.052f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.76f, 0.84f, 0.82f),
                AmbientLightEnergy = 1.35f
            }
        });
        stage.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-28.0f, -32.0f, 0.0f),
            LightColor = new Color(0.9f, 0.96f, 1.0f),
            LightEnergy = 1.4f,
            ShadowEnabled = false
        });
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 7.0f,
            Position = new Vector3(0.0f, 0.0f, 8.0f),
            Current = true
        };
        stage.AddChild(camera);

        var platforms = Enum.GetValues<WeaponPlatform>();
        for (var index = 0; index < platforms.Length; index++)
        {
            var platform = platforms[index];
            var column = index % 4;
            var row = index / 4;
            var x = (column - 1.5f) * 2.75f;
            var y = (1.5f - row) * 1.55f;
            var weapon = platform switch
            {
                WeaponPlatform.GSh18 => CombatModelLibrary.InstantiateGsh18(firstPerson: false).Root,
                WeaponPlatform.DesertEagle => CombatModelLibrary.InstantiateDesertEagle(firstPerson: false).Root,
                _ => CombatModelLibrary.InstantiateWeapon(platform, firstPerson: false).Root
            };
            var mount = new Node3D
            {
                Name = $"{platform}LineupMount",
                Position = new Vector3(x, y, 0.0f),
                RotationDegrees = new Vector3(0.0f, -90.0f, 0.0f)
            };
            weapon.Scale *= platform is WeaponPlatform.GSh18 or WeaponPlatform.DesertEagle
                ? 0.78f
                : 0.92f;
            mount.AddChild(weapon);
            stage.AddChild(mount);
            stage.AddChild(new Label3D
            {
                Text = platform.ToString(),
                Position = new Vector3(x, y - 0.56f, 0.0f),
                FontSize = 28,
                OutlineSize = 6,
                Modulate = new Color(0.82f, 0.9f, 0.88f),
                NoDepthTest = true
            });
        }

        await WaitFrames(5);
        var image = viewport.GetTexture().GetImage();
        var path = ProjectSettings.GlobalizePath("res://authored_weapon_lineup_validation.png");
        var saved = !image.IsEmpty() && image.SavePng(path) == Error.Ok;
        viewport.QueueFree();
        return saved;
    }

    private async System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<WeaponPlatform, bool>>
        CaptureFirstPersonWeaponViews()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = new Vector3(240.0f + mate.SquadSlot * 3.0f, 80.0f, 240.0f);
        }
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(8.0f, 0.2f, -8.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(8.0f, 0.2f, -80.0f));

        var captures = new System.Collections.Generic.Dictionary<WeaponPlatform, bool>();
        var cachedInstances = new System.Collections.Generic.Dictionary<WeaponPlatform, ulong>();
        foreach (var platform in Enum.GetValues<WeaponPlatform>())
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            await WaitFrames(8);
            var path = $"res://first_person_{platform.ToString().ToLowerInvariant()}_validation.png";
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
            SaveViewportImage(path);
            captures[platform] = _player.UsesAuthoredWeaponPlatformForDiagnostics(platform)
                && System.IO.File.Exists(absolutePath)
                && new System.IO.FileInfo(absolutePath).Length > 0;
            var instanceId = _player.AuthoredWeaponInstanceIdForDiagnostics(platform);
            if (instanceId != 0)
            {
                cachedInstances[platform] = instanceId;
            }
        }

        foreach (var pair in cachedInstances)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(pair.Key, 0));
            await WaitFrames(2);
            captures[pair.Key] &= _player.AuthoredWeaponInstanceIdForDiagnostics(pair.Key) == pair.Value;
        }

        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
        await WaitFrames(3);
        return captures;
    }

    private static bool IsValidPlatformWeapon(WeaponPlatform platform, CombatModelInspection inspection)
    {
        var minimumMeshes = platform switch
        {
            WeaponPlatform.M4A1 => 8,
            WeaponPlatform.GSh18 => 10,
            WeaponPlatform.DesertEagle => 20,
            _ => 1
        };
        return inspection.Loaded
            && inspection.RequiredNodes
            && inspection.MeshCount >= minimumMeshes
            && inspection.MaterialCount >= 1
            && inspection.Size.X > 0.001f
            && inspection.Size.Y > 0.001f
            && inspection.Size.Z > 0.001f;
    }

    private static string FormatWeaponInspection(
        CombatModelInspection inspection,
        bool valid)
        => $"valid={valid};loaded={inspection.Loaded};nodes={inspection.RequiredNodes};meshes={inspection.MeshCount};bounds={inspection.Size}";

    private static float ColorDistance(Color left, Color right)
    {
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return Mathf.Sqrt(red * red + green * green + blue * blue);
    }
}
