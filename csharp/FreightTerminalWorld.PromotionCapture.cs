using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private sealed record PromotionOperator(
        AuthoredOperatorVisual Visual,
        AuthoredOperatorAnimator Animator);

    private async void CapturePromotionMedia()
    {
        var window = GetWindow();
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1600, 900);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetCaptureLanguage("en");

        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.Visible = false;
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.Visible = false;
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        HidePromotionLabels(this);
        _hud.Visible = false;
        _player.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;

        ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
        _environmentRef.TonemapExposure = 1.12f;
        _environmentRef.AmbientLightEnergy = 0.46f;
        _environmentRef.FogDensity = 0.0011f;
        SetIfSupported(_environmentRef, "adjustment_brightness", 1.02f);
        SetIfSupported(_environmentRef, "adjustment_contrast", 1.12f);
        SetIfSupported(_environmentRef, "adjustment_saturation", 0.98f);

        var promotionRoot = new Node3D { Name = "PromotionCaptureStage" };
        AddChild(promotionRoot);
        promotionRoot.AddChild(new DirectionalLight3D
        {
            Name = "PromotionWarmKey",
            RotationDegrees = new Vector3(-34.0f, -128.0f, 0.0f),
            LightColor = new Color(1.0f, 0.72f, 0.48f),
            LightEnergy = 1.35f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90.0f
        });
        promotionRoot.AddChild(new DirectionalLight3D
        {
            Name = "PromotionCoolFill",
            RotationDegrees = new Vector3(-24.0f, 42.0f, 0.0f),
            LightColor = new Color(0.34f, 0.68f, 0.82f),
            LightEnergy = 0.54f,
            ShadowEnabled = false
        });

        var operators = new List<PromotionOperator>
        {
            AddPromotionOperator(
                promotionRoot,
                "Lead",
                OperatorVisualId.Garrison,
                new Vector3(0.2f, 0.12f, -98.4f),
                new Vector3(-6.4f, 0.12f, -99.3f),
                WeaponPlatform.M4A1,
                "aim_idle",
                0.26f),
            AddPromotionOperator(
                promotionRoot,
                "LeftFlank",
                OperatorVisualId.Garrison,
                new Vector3(1.9f, 0.12f, -96.6f),
                new Vector3(-6.4f, 0.12f, -99.3f),
                WeaponPlatform.AK74,
                "ready_walk",
                0.42f),
            AddPromotionOperator(
                promotionRoot,
                "RightFlank",
                OperatorVisualId.Garrison,
                new Vector3(2.6f, 0.12f, -100.3f),
                new Vector3(-6.4f, 0.12f, -99.3f),
                WeaponPlatform.M24,
                "aim_crouch_idle",
                0.18f)
        };

        var camera = new Camera3D
        {
            Name = "PromotionCaptureCamera",
            Fov = 46.0f,
            Near = 0.04f,
            Far = 520.0f
        };
        promotionRoot.AddChild(camera);
        camera.GlobalPosition = new Vector3(-4.35f, 0.98f, -101.15f);
        camera.LookAt(new Vector3(0.75f, 1.24f, -98.4f), Vector3.Up);
        camera.Fov = 38.0f;
        camera.MakeCurrent();
        await WaitFrames(28);

        camera.GlobalPosition = new Vector3(-3.45f, 1.02f, -100.55f);
        camera.LookAt(new Vector3(0.85f, 1.24f, -98.5f), Vector3.Up);
        camera.Fov = 44.0f;
        await WaitFrames(12);
        var squadSaved = SavePromotionWebp("squad.webp");

        SetPromotionPose(
            operators[0],
            new Vector3(6.5f, 0.12f, -98.8f),
            new Vector3(60.0f, 0.12f, -98.8f),
            "ready_walk",
            0.54f);
        SetPromotionPose(
            operators[1],
            new Vector3(3.8f, 0.12f, -96.7f),
            new Vector3(60.0f, 0.12f, -98.8f),
            "ready_walk",
            0.18f);
        SetPromotionPose(
            operators[2],
            new Vector3(8.4f, 0.12f, -100.5f),
            new Vector3(60.0f, 0.12f, -98.8f),
            "ready_walk",
            0.74f);
        camera.GlobalPosition = new Vector3(-13.5f, 2.1f, -103.5f);
        camera.LookAt(new Vector3(42.0f, 2.7f, -98.4f), Vector3.Up);
        camera.Fov = 48.0f;
        await WaitFrames(16);
        var citySaved = SavePromotionWebp("city.webp");

        SetPromotionPose(
            operators[0],
            new Vector3(7.5f, 0.12f, -97.4f),
            new Vector3(3.0f, 0.12f, -99.2f),
            "aim_idle",
            0.34f);
        SetPromotionPose(
            operators[1],
            new Vector3(10.2f, 0.12f, -100.4f),
            new Vector3(3.0f, 0.12f, -99.2f),
            "aim_crouch_idle",
            0.1f);
        operators[2].Visual.Root.Visible = false;
        camera.Current = false;
        _player.GlobalPosition = new Vector3(3.0f, 0.2f, -99.2f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(7.5f, 0.2f, -97.4f));
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 2));
        _player.Visible = true;
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.GetNode<Camera3D>("Head/CombatCamera").MakeCurrent();
        await WaitFrames(24);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        var heroSaved = SavePromotionWebp("hero.webp");
        var branding = BuildPromotionBranding();
        AddChild(branding);
        await WaitFrames(12);
        var socialSaved = SavePromotionSocialPreview();
        branding.QueueFree();

        var arsenalSaved = await CapturePromotionArsenal();
        var valid = heroSaved && squadSaved && citySaved && socialSaved && arsenalSaved;
        GD.Print(
            $"PROMOTION_CAPTURE valid={valid} hero={heroSaved} squad={squadSaved} "
            + $"city={citySaved} arsenal={arsenalSaved} social={socialSaved}");
        GD.Print($"PROMOTION_CAPTURE_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static PromotionOperator AddPromotionOperator(
        Node3D parent,
        string name,
        OperatorVisualId visualId,
        Vector3 position,
        Vector3 lookTarget,
        WeaponPlatform platform,
        string animation,
        float animationTime)
    {
        var visual = CombatModelLibrary.InstantiateOperator(
            visualId,
            WeaponCatalog.Build(platform, 2));
        visual.Root.Name = $"Promotion{name}";
        parent.AddChild(visual.Root);
        visual.SetWeaponReadied(true);
        var animator = new AuthoredOperatorAnimator(visual);
        var staged = new PromotionOperator(visual, animator);
        SetPromotionPose(staged, position, lookTarget, animation, animationTime);
        return staged;
    }

    private static void SetPromotionPose(
        PromotionOperator staged,
        Vector3 position,
        Vector3 lookTarget,
        string animation,
        float animationTime)
    {
        staged.Visual.Root.GlobalPosition = position;
        var flatTarget = new Vector3(lookTarget.X, position.Y, lookTarget.Z);
        staged.Visual.Root.LookAt(flatTarget, Vector3.Up);
        staged.Visual.AnimationPlayer.Play(animation, 0.0);
        staged.Visual.AnimationPlayer.Seek(animationTime, update: true);
        staged.Visual.AnimationPlayer.SpeedScale = 0.0f;
    }

    private static void HidePromotionLabels(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label3D label)
            {
                label.Visible = false;
            }
            HidePromotionLabels(child);
        }
    }

    private bool SavePromotionWebp(string fileName)
    {
        var image = GetViewport().GetTexture().GetImage();
        if (image.IsEmpty())
        {
            return false;
        }
        var path = ProjectSettings.GlobalizePath($"res://docs/media/{fileName}");
        return image.SaveWebp(path, lossy: true, quality: 0.9f) == Error.Ok;
    }

    private bool SavePromotionSocialPreview()
    {
        var image = GetViewport().GetTexture().GetImage();
        if (image.IsEmpty())
        {
            return false;
        }
        var cropHeight = Mathf.Min(image.GetHeight(), image.GetWidth() / 2);
        var y = Mathf.Max(0, (image.GetHeight() - cropHeight) / 2);
        var cropped = image.GetRegion(new Rect2I(0, y, image.GetWidth(), cropHeight));
        cropped.Resize(1280, 640, Image.Interpolation.Lanczos);
        var path = ProjectSettings.GlobalizePath("res://docs/media/social-preview.png");
        return cropped.SavePng(path) == Error.Ok;
    }

    private static CanvasLayer BuildPromotionBranding()
    {
        var layer = new CanvasLayer { Name = "PromotionBranding", Layer = 100 };
        layer.AddChild(new ColorRect
        {
            Position = new Vector2(0.0f, 680.0f),
            Size = new Vector2(1600.0f, 220.0f),
            Color = new Color(0.015f, 0.025f, 0.03f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        layer.AddChild(new ColorRect
        {
            Position = new Vector2(80.0f, 716.0f),
            Size = new Vector2(8.0f, 116.0f),
            Color = new Color(0.16f, 0.94f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var title = new Label
        {
            Position = new Vector2(116.0f, 704.0f),
            Size = new Vector2(1050.0f, 86.0f),
            Text = "OPERATION STEEL TIDE",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 46);
        title.AddThemeColorOverride("font_color", new Color(0.94f, 0.97f, 0.96f));
        layer.AddChild(title);
        var subtitle = new Label
        {
            Position = new Vector2(118.0f, 786.0f),
            Size = new Vector2(1050.0f, 42.0f),
            Text = "TACTICAL EXTRACTION  //  SQUAD COMMAND  //  OPEN SOURCE",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        subtitle.AddThemeColorOverride("font_color", new Color(0.45f, 0.78f, 0.72f));
        layer.AddChild(subtitle);
        return layer;
    }

    private async System.Threading.Tasks.Task<bool> CapturePromotionArsenal()
    {
        var viewport = new SubViewport
        {
            Name = "PromotionArsenalViewport",
            Size = new Vector2I(1600, 900),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(viewport);
        var stage = new Node3D { Name = "PromotionArsenalStage" };
        viewport.AddChild(stage);
        stage.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.018f, 0.042f, 0.039f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.58f, 0.7f, 0.66f),
                AmbientLightEnergy = 1.0f,
                TonemapMode = Godot.Environment.ToneMapper.Aces,
                TonemapExposure = 1.08f
            }
        });
        stage.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-36.0f, -42.0f, 0.0f),
            LightColor = new Color(0.78f, 0.96f, 0.92f),
            LightEnergy = 2.1f,
            ShadowEnabled = false
        });
        stage.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(18.0f, 132.0f, 0.0f),
            LightColor = new Color(1.0f, 0.54f, 0.28f),
            LightEnergy = 1.15f,
            ShadowEnabled = false
        });
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 5.4f,
            Position = new Vector3(0.0f, 0.0f, 8.0f),
            Current = true
        };
        stage.AddChild(camera);

        var entries = new[]
        {
            (WeaponPlatform.M4A1, new Vector3(-1.6f, 1.5f, 0.0f), 1.3f),
            (WeaponPlatform.AK74, new Vector3(-1.6f, 0.42f, 0.0f), 1.35f),
            (WeaponPlatform.AWM, new Vector3(-1.6f, -0.68f, 0.0f), 1.08f),
            (WeaponPlatform.DesertEagle, new Vector3(-1.6f, -1.75f, 0.0f), 1.14f)
        };
        foreach (var entry in entries)
        {
            var weapon = entry.Item1 == WeaponPlatform.DesertEagle
                ? CombatModelLibrary.InstantiateDesertEagle(firstPerson: false).Root
                : CombatModelLibrary.InstantiateWeapon(entry.Item1, firstPerson: false).Root;
            var mount = new Node3D
            {
                Name = $"Promotion{entry.Item1}Mount",
                Position = entry.Item2,
                RotationDegrees = new Vector3(0.0f, -90.0f, -3.0f)
            };
            weapon.Scale *= entry.Item3;
            mount.AddChild(weapon);
            stage.AddChild(mount);
            stage.AddChild(new Label3D
            {
                Text = entry.Item1 == WeaponPlatform.DesertEagle
                    ? "DESERT EAGLE"
                    : entry.Item1.ToString().ToUpperInvariant(),
                Position = new Vector3(2.15f, entry.Item2.Y, 0.0f),
                FontSize = 34,
                OutlineSize = 8,
                Modulate = new Color(0.78f, 0.9f, 0.86f),
                NoDepthTest = true,
                HorizontalAlignment = HorizontalAlignment.Left
            });
        }
        stage.AddChild(new Label3D
        {
            Text = "FIELD ARSENAL",
            Position = new Vector3(-4.55f, 2.4f, 0.0f),
            FontSize = 38,
            OutlineSize = 10,
            Modulate = new Color(0.16f, 0.94f, 0.72f),
            NoDepthTest = true,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        await WaitFrames(8);
        var image = viewport.GetTexture().GetImage();
        var path = ProjectSettings.GlobalizePath("res://docs/media/arsenal.webp");
        var saved = !image.IsEmpty()
            && image.SaveWebp(path, lossy: true, quality: 0.9f) == Error.Ok;
        viewport.QueueFree();
        return saved;
    }
}
