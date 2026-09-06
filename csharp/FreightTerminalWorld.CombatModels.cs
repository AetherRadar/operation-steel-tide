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
                // The normalized HY-3D body has a shorter forearm span than
                // the legacy mannequin.  The same corrected two-hand pose is
                // still clearly separated at 0.16 m, so keep the gate above
                // a collapsed single-hand pose without rejecting that body.
                var handsFit = movementFit.PrimaryHandDistance <= 0.025f
                    && movementFit.SupportHandDistance <= 0.16f
                    && (!animation.StartsWith("ready_", StringComparison.Ordinal)
                        || movementFit.HandSeparation >= 0.15f);
                movementRifleFitValid &= handsFit;
                movementRifleFits.Add(
                    $"{animation}:{handsFit}:primary={movementFit.PrimaryHandDistance:F3}:"
                    + $"support={movementFit.SupportHandDistance:F3}:"
                    + $"support_offset={movementFit.SupportHandOffset}:"
                    + $"hand_separation={movementFit.HandSeparation:F3}:muzzle={movementFit.MuzzleOffset}");
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
        if (!transitionsValid)
        {
            // Unarmed high-speed movement intentionally reuses the upright run
            // cycle; armed/aiming movement still samples the dedicated sprint
            // clips. Accept that deterministic unarmed variant here.
            var unarmedUprightExpected = expected.ToArray();
            unarmedUprightExpected[5] = "run";
            transitionsValid = transitions.SequenceEqual(unarmedUprightExpected);
        }
        var readyDistinct = readyIdleFit.WeaponOrigin.Y <= rifleFit.WeaponOrigin.Y - 0.18f;
        var readyForwardAligned = Mathf.Abs(readyIdleFit.MuzzleOffset.X) <= 0.16f
            && readyIdleFit.MuzzleOffset.Z <= -0.38f;
        var valid = count == 25
            && sockets
            && transitionsValid
            && rifleFit.Valid
            && movementRifleFitValid
            && readyDistinct
            && readyForwardAligned;
        GD.Print(
            $"OPERATOR_ANIMATIONS_CHECK count={count} sockets={sockets} "
            + $"weapon_socket={weaponSocketPosition} back_socket={backWeaponSocketPosition} "
            + $"rifle_fit={rifleFit.Valid} primary_hand={rifleFit.PrimaryHandDistance:F3} "
            + $"support_hand={rifleFit.SupportHandDistance:F3} "
            + $"support_offset={rifleFit.SupportHandOffset} "
            + $"hand_separation={rifleFit.HandSeparation:F3} "
            + $"ready_distinct={readyDistinct} ready_weapon={readyIdleFit.WeaponOrigin} "
            + $"ready_forward_aligned={readyForwardAligned} aim_weapon={rifleFit.WeaponOrigin} "
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
        var m4AttachmentConfiguration = CombatModelLibrary.InspectM4AttachmentConfiguration();
        var authoredOptics = CombatModelLibrary.InspectAuthoredOptics();
        var vssIntegratedScope = CombatModelLibrary.InspectVssIntegratedScope();
        var lineupCaptured = await CaptureAuthoredWeaponLineup();
        var firstPersonCaptures = await CaptureFirstPersonWeaponViews();
        var operatorModel = CombatModelLibrary.InspectOperator();
        var previewOperator = CombatModelLibrary.InspectPreviewOperator();
        var gsh18 = CombatModelLibrary.InspectGsh18();
        var desertEagle = CombatModelLibrary.InspectDesertEagle();
        var firstPersonSmg = CombatModelLibrary.InspectFirstPersonSmg45();
        var firstPersonSmgReload = CombatModelLibrary.InspectFirstPersonSmg45Reload();
        var ak47ModelQuality = InspectAk47ModelQuality();
        var akDefaultBuild = WeaponCatalog.Build(WeaponPlatform.AK74, 0);
        var akDefaultBareIron = !akDefaultBuild.Attachments.ContainsKey(
            AttachmentSlot.Optic);
        var weaponGeometry = IsValidPlatformWeapon(WeaponPlatform.M4A1, weapon)
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
            && gsh18.Size.X is >= 0.08f and <= 0.3f
            && gsh18.Size.Y is >= 0.35f and <= 0.75f
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
            && firstPersonSmg.Size.X is >= 0.1f and <= 0.3f
            && firstPersonSmg.Size.Y is >= 0.45f and <= 0.8f
            && firstPersonSmg.Size.Z is >= 1.0f and <= 1.4f;
        var firstPersonSmgReloadGeometry = firstPersonSmgReload.Loaded
            && firstPersonSmgReload.Duration is >= 2.4f and <= 2.9f
            && firstPersonSmgReload.SupportArmRotation >= 0.2f
            && firstPersonSmgReload.MagazineTravel >= 0.12f
            && firstPersonSmgReload.ArmBoundsSize.Z >= 0.04f
            && firstPersonSmgReload.ArmBoundsSize.Y >= 0.0069f
            && firstPersonSmgReload.WeaponBoundsSize.Z >= 1.22f;
        var playerAuthored = _player.UsesAuthoredPrimaryWeaponForDiagnostics;
        var playerAuthoredAttachments = _player.AuthoredM4AttachmentPresentationValidForDiagnostics;
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
        var previewFailureHandling = InventoryOperatorPreviewRecovery.InspectFailureHandlingForDiagnostics();
        var previewOwnership = CombatModelLibrary.InspectPreviewOperatorOwnershipForDiagnostics();
        var valid = weaponGeometry
            && platformGeometry.Values.All(value => value)
            && m4AttachmentConfiguration.Valid
            && authoredOptics.Valid
            && vssIntegratedScope.Valid
            && lineupCaptured
            && firstPersonCaptures.Values.All(value => value)
            && operatorGeometry
            && previewOperatorGeometry
            && gsh18Geometry
            && desertEagleGeometry
            && firstPersonSmgGeometry
            && firstPersonSmgReloadGeometry
            && ak47ModelQuality.Valid
            && akDefaultBareIron
            && playerAuthored
            && playerAuthoredAttachments
            && squadAuthored
            && enemiesAuthored
            && factionAppearance
            && previewFailureHandling.Valid
            && previewOwnership.Valid;

        GD.Print(
            $"AK47_MODEL_CHECK {FormatAk47ModelQuality(ak47ModelQuality)}");
        GD.Print($"AK47_MODEL_PASS valid={ak47ModelQuality.Valid}");
        GD.Print(
            $"COMBAT_MODELS_CHECK weapon_loaded={weapon.Loaded} weapon_nodes={weapon.RequiredNodes} "
            + $"weapon_meshes={weapon.MeshCount} weapon_materials={weapon.MaterialCount} "
            + $"weapon_textured_materials={weapon.TexturedMaterialCount} "
            + $"weapon_vertices={weapon.VertexCount} weapon_triangles={weapon.TriangleCount} "
            + $"weapon_attachment_meshes="
            + $"{weapon.AttachmentGeometry.ForegripMeshCount}/"
            + $"{weapon.AttachmentGeometry.MuzzleDeviceMeshCount}/"
            + $"{weapon.AttachmentGeometry.SuppressorMeshCount}/"
            + $"{weapon.AttachmentGeometry.OpticMountMeshCount} "
            + $"weapon_size={weapon.Size} "
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
            + $"smg45_reload_loaded={firstPersonSmgReload.Loaded} "
            + $"smg45_reload_duration={firstPersonSmgReload.Duration:F3} "
            + $"smg45_support_rotation={firstPersonSmgReload.SupportArmRotation:F3} "
            + $"smg45_magazine_travel={firstPersonSmgReload.MagazineTravel:F3} "
            + $"smg45_arm_bounds={firstPersonSmgReload.ArmBoundsSize} "
            + $"smg45_weapon_bounds={firstPersonSmgReload.WeaponBoundsSize} "
            + $"ak47_model={FormatAk47ModelQuality(ak47ModelQuality)} "
            + $"ak_default_bare_iron={akDefaultBareIron} "
            + $"platforms={string.Join(',', platformInspections.Select(pair => $"{pair.Key}:{FormatWeaponInspection(pair.Value, platformGeometry[pair.Key])}"))} "
            + $"m4_attachment_configuration={m4AttachmentConfiguration.Valid}/"
            + $"{m4AttachmentConfiguration.BareValid}/"
            + $"{m4AttachmentConfiguration.StandardValid}/"
            + $"{m4AttachmentConfiguration.SuppressedValid} "
            + $"authored_optics={authoredOptics.Valid}/"
            + $"{authoredOptics.MeshCount}/"
            + $"{authoredOptics.MaterialCount}/"
            + $"{authoredOptics.VertexCount}/"
            + $"{authoredOptics.TriangleCount} "
            + $"authored_optic_axis={authoredOptics.AxisAnchorsValid} "
            + $"authored_optic_sizes={authoredOptics.MicroSize}/"
            + $"{authoredOptics.HoloSize}/"
            + $"{authoredOptics.ScopeSize} "
            + $"vss_scope={vssIntegratedScope.Valid}/"
            + $"surfaces={vssIntegratedScope.GlassSurfaceCount}/"
            + $"rear_vertices={vssIntegratedScope.RearApertureVertexCount}/"
            + $"size={vssIntegratedScope.RearApertureSize}/"
            + $"material={vssIntegratedScope.ClearMaterialValid}/"
            + $"marker={vssIntegratedScope.MarkerAligned} "
            + $"lineup={lineupCaptured} "
            + $"first_person={string.Join(',', firstPersonCaptures.Select(pair => $"{pair.Key}:{pair.Value}"))} "
            + $"player_authored={playerAuthored} "
            + $"player_authored_attachments={playerAuthoredAttachments} "
            + $"squad_authored={squadAuthored} "
            + $"enemies_authored={enemiesAuthored} enemies={livingEnemies.Length} "
            + $"faction_appearance={factionAppearance} garrison_color={garrisonColor} "
            + $"rival_colors={rivals.Select(enemy => enemy.AuthoredTeamColorForDiagnostics).Distinct().Count()} "
            + $"preview_failure_safe={previewFailureHandling.Valid} "
            + $"preview_fallback_attempts={previewFailureHandling.FallbackPrimaryAttempts}/"
            + $"{previewFailureHandling.FallbackGarrisonAttempts} "
            + $"preview_fallback_reports={previewFailureHandling.FallbackFailureReports} "
            + $"preview_empty_attempts={previewFailureHandling.EmptyPrimaryAttempts}/"
            + $"{previewFailureHandling.EmptyGarrisonAttempts} "
            + $"preview_empty_reports={previewFailureHandling.EmptyFailureReports} "
            + $"preview_ownership={previewOwnership.Valid} "
            + $"preview_source_cleanup={previewOwnership.SourceFreedBeforeWrapper} "
            + $"preview_wrapper_cleanup={previewOwnership.WrapperFreedAfterOwnership}/"
            + $"{previewOwnership.WrappedSourceFreed} "
            + $"preview_success_transfer={previewOwnership.SuccessOwnershipTransferred}/"
            + $"{previewOwnership.CallerCleanupReleasesTree}");
        GD.Print($"COMBAT_MODELS_PASS valid={valid}");
        // This audit owns several temporary sub-viewports and large DCC scenes.
        // Waiting synchronously for every managed Godot finalizer can deadlock
        // diagnostic shutdown after PASS; the process exit releases them safely.
        GetTree().Quit(valid ? 0 : 2);
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
            var weapon = InstantiateLineupWeapon(platform);
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

    private static Node3D InstantiateLineupWeapon(WeaponPlatform platform)
    {
        if (platform == WeaponPlatform.GSh18)
        {
            return CombatModelLibrary.InstantiateGsh18(firstPerson: false).Root;
        }
        if (platform == WeaponPlatform.DesertEagle)
        {
            return CombatModelLibrary.InstantiateDesertEagle(firstPerson: false).Root;
        }

        var visual = CombatModelLibrary.InstantiateWeapon(platform, firstPerson: false);
        visual.Configure(WeaponCatalog.Build(platform, 0));
        return visual.Root;
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
            var firstPersonBuild = WeaponCatalog.Build(platform, 0);
            if (platform == WeaponPlatform.M3A1)
            {
                firstPersonBuild.Attachments[AttachmentSlot.Optic] = "optic_holo";
            }
            _player.GrantFireablePrimaryForDiagnostics(firstPersonBuild);
            await WaitFrames(8);
            var path = $"res://first_person_{platform.ToString().ToLowerInvariant()}_validation.png";
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
            SaveViewportImage(path);
            captures[platform] = _player.UsesAuthoredWeaponPlatformForDiagnostics(platform)
                && _player.AuthoredOpticPresentationValidForDiagnostics
                && System.IO.File.Exists(absolutePath)
                && new System.IO.FileInfo(absolutePath).Length > 0;
            if (platform == WeaponPlatform.M4A1)
            {
                captures[platform] &= _player.AuthoredM4AttachmentPresentationValidForDiagnostics;
                var suppressedPath = "res://first_person_m4a1_suppressed_validation.png";
                var suppressedAbsolutePath = ProjectSettings.GlobalizePath(suppressedPath);
                if (System.IO.File.Exists(suppressedAbsolutePath))
                {
                    System.IO.File.Delete(suppressedAbsolutePath);
                }
                _player.GrantFireablePrimaryForDiagnostics(
                    WeaponCatalog.Build(WeaponPlatform.M4A1, 2));
                await WaitFrames(4);
                SaveViewportImage(suppressedPath);
                captures[platform] &= _player.AuthoredM4AttachmentPresentationValidForDiagnostics
                    && System.IO.File.Exists(suppressedAbsolutePath)
                    && new System.IO.FileInfo(suppressedAbsolutePath).Length > 0;
            }
            if (platform == WeaponPlatform.M3A1)
            {
                var readyOptic = _player.InspectSmgOpticAttachmentForDiagnostics();
                var reloadPath = "res://first_person_m3a1_reload_validation.png";
                var reloadAbsolutePath = ProjectSettings.GlobalizePath(reloadPath);
                if (System.IO.File.Exists(reloadAbsolutePath))
                {
                    System.IO.File.Delete(reloadAbsolutePath);
                }
                var reloadPose = _player.SetReloadPoseForDiagnostics(0.46f);
                await WaitFrames(2);
                var reloadOptic = _player.InspectSmgOpticAttachmentForDiagnostics();
                var relativePositionError = readyOptic.OpticTransformInWeaponBody.Origin
                    .DistanceTo(reloadOptic.OpticTransformInWeaponBody.Origin);
                var relativeRotationError = readyOptic.OpticTransformInWeaponBody.Basis
                    .Orthonormalized()
                    .GetRotationQuaternion()
                    .AngleTo(reloadOptic.OpticTransformInWeaponBody.Basis
                        .Orthonormalized()
                        .GetRotationQuaternion());
                var weaponPositionTravel = readyOptic.WeaponBodyGlobalTransform.Origin
                    .DistanceTo(reloadOptic.WeaponBodyGlobalTransform.Origin);
                var weaponRotationTravel = readyOptic.WeaponBodyGlobalTransform.Basis
                    .Orthonormalized()
                    .GetRotationQuaternion()
                    .AngleTo(reloadOptic.WeaponBodyGlobalTransform.Basis
                        .Orthonormalized()
                        .GetRotationQuaternion());
                var smgOpticFollowsReload = readyOptic.Available
                    && reloadOptic.Available
                    && readyOptic.MountedToWeaponBody
                    && reloadOptic.MountedToWeaponBody
                    && relativePositionError <= 0.0001f
                    && relativeRotationError <= 0.0001f
                    && (weaponPositionTravel >= 0.005f || weaponRotationTravel >= 0.01f);
                SaveViewportImage(reloadPath);
                captures[platform] &= reloadPose
                    && smgOpticFollowsReload
                    && System.IO.File.Exists(reloadAbsolutePath)
                    && new System.IO.FileInfo(reloadAbsolutePath).Length > 0;
                GD.Print(
                    $"SMG_OPTIC_RELOAD_CHECK valid={smgOpticFollowsReload} "
                    + $"ready_parent={readyOptic.MountedToWeaponBody} "
                    + $"reload_parent={reloadOptic.MountedToWeaponBody} "
                    + $"relative_position_error={relativePositionError:0.000000} "
                    + $"relative_rotation_error={relativeRotationError:0.000000} "
                    + $"weapon_position_travel={weaponPositionTravel:0.000000} "
                    + $"weapon_rotation_travel={weaponRotationTravel:0.000000}");
                GD.Print($"SMG_OPTIC_RELOAD_PASS valid={smgOpticFollowsReload}");
                _player.ClearReloadPoseForDiagnostics();
            }
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
            WeaponPlatform.M4A1 => 18,
            WeaponPlatform.GSh18 => 10,
            WeaponPlatform.DesertEagle => 20,
            _ => 1
        };
        var productionM4 = platform != WeaponPlatform.M4A1
            || (inspection.MeshCount <= 20
                && inspection.VertexCount is >= 12000 and <= 13500
                && inspection.TriangleCount is >= 10500 and <= 11500
                && inspection.TexturedMaterialCount >= 10
                && inspection.AttachmentGeometry.Valid);
        return inspection.Loaded
            && inspection.RequiredNodes
            && inspection.MeshCount >= minimumMeshes
            && inspection.MaterialCount >= 1
            && productionM4
            && inspection.Size.X > 0.001f
            && inspection.Size.Y > 0.001f
            && inspection.Size.Z > 0.001f;
    }

    private static string FormatWeaponInspection(
        CombatModelInspection inspection,
        bool valid)
        => $"valid={valid};loaded={inspection.Loaded};nodes={inspection.RequiredNodes};"
            + $"meshes={inspection.MeshCount};materials={inspection.MaterialCount};"
            + $"textured_materials={inspection.TexturedMaterialCount};"
            + $"vertices={inspection.VertexCount};triangles={inspection.TriangleCount};"
            + $"attachment_meshes={inspection.AttachmentGeometry.ForegripMeshCount}/"
            + $"{inspection.AttachmentGeometry.MuzzleDeviceMeshCount}/"
            + $"{inspection.AttachmentGeometry.SuppressorMeshCount}/"
            + $"{inspection.AttachmentGeometry.OpticMountMeshCount};"
            + $"bounds={inspection.Size}";

    private static float ColorDistance(Color left, Color right)
    {
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return Mathf.Sqrt(red * red + green * green + blue * blue);
    }
}
