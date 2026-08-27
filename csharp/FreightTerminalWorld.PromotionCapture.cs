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
        foreach (var vehicle in _vehicles)
        {
            if (IsInstanceValid(vehicle))
            {
                vehicle.Visible = false;
            }
        }
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = false;
            HideGeometryRecursive(_extractionMarker);
        }
        if (_extractionAircraft is not null && IsInstanceValid(_extractionAircraft))
        {
            _extractionAircraft.Visible = false;
        }
        HidePromotionLabels(this);
        _hud.Visible = false;
        _player.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;

        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        _environmentRef.TonemapExposure = 1.24f;
        _environmentRef.AmbientLightEnergy = 1.38f;
        _environmentRef.FogDensity = 0.0009f;
        SetIfSupported(_environmentRef, "adjustment_brightness", 1.09f);
        SetIfSupported(_environmentRef, "adjustment_contrast", 1.02f);
        SetIfSupported(_environmentRef, "adjustment_saturation", 1.02f);
        _sunLight.RotationDegrees = new Vector3(-36.0f, 115.0f, 0.0f);
        _sunLight.LightColor = new Color(1.0f, 0.84f, 0.68f);
        _sunLight.LightEnergy = 1.08f;
        _fillLight.RotationDegrees = new Vector3(-28.0f, -65.0f, 0.0f);
        _fillLight.LightColor = new Color(0.55f, 0.68f, 0.80f);
        _fillLight.LightEnergy = 0.72f;
        if (_environmentRef.Sky?.SkyMaterial is ProceduralSkyMaterial skyMaterial)
        {
            skyMaterial.SkyTopColor = new Color(0.055f, 0.16f, 0.27f);
            skyMaterial.SkyHorizonColor = new Color(0.42f, 0.52f, 0.56f);
            skyMaterial.GroundBottomColor = new Color(0.075f, 0.095f, 0.10f);
            skyMaterial.GroundHorizonColor = new Color(0.25f, 0.29f, 0.29f);
            skyMaterial.SkyEnergyMultiplier = 1.2f;
            skyMaterial.GroundEnergyMultiplier = 0.74f;
        }

        var promotionRoot = new Node3D { Name = "PromotionCaptureStage" };
        AddChild(promotionRoot);

        var operators = new List<PromotionOperator>
        {
            AddPromotionOperator(
                promotionRoot,
                "Lead",
                OperatorVisualId.Garrison,
                new Vector3(0.0f, 0.12f, 18.0f),
                new Vector3(0.0f, 0.12f, -48.0f),
                WeaponPlatform.M4A1,
                "ready_walk",
                0.22f),
            AddPromotionOperator(
                promotionRoot,
                "LeftFlank",
                OperatorVisualId.Garrison,
                new Vector3(-2.4f, 0.12f, 20.6f),
                new Vector3(-0.6f, 0.12f, -48.0f),
                WeaponPlatform.AK74,
                "ready_walk",
                0.58f),
            AddPromotionOperator(
                promotionRoot,
                "RightFlank",
                OperatorVisualId.Garrison,
                new Vector3(2.3f, 0.12f, 21.4f),
                new Vector3(0.8f, 0.12f, -48.0f),
                WeaponPlatform.M24,
                "ready_walk",
                0.82f)
        };

        var camera = new Camera3D
        {
            Name = "PromotionCaptureCamera",
            Fov = 46.0f,
            Near = 0.04f,
            Far = 520.0f
        };
        promotionRoot.AddChild(camera);
        camera.GlobalPosition = new Vector3(0.8f, 1.58f, 25.0f);
        camera.LookAt(new Vector3(0.0f, 2.15f, 4.0f), Vector3.Up);
        camera.Fov = 44.0f;
        camera.MakeCurrent();
        await WaitFrames(28);
        var squadSaved = SavePromotionWebp("squad.webp");

        SetPromotionPose(
            operators[0],
            new Vector3(-81.0f, 0.12f, -112.0f),
            new Vector3(-86.0f, 0.12f, -128.0f),
            "ready_walk",
            0.28f);
        SetPromotionPose(
            operators[1],
            new Vector3(-77.2f, 0.12f, -109.2f),
            new Vector3(-86.0f, 0.12f, -128.0f),
            "ready_walk",
            0.64f);
        SetPromotionPose(
            operators[2],
            new Vector3(-84.0f, 0.12f, -109.0f),
            new Vector3(-86.0f, 0.12f, -128.0f),
            "ready_walk",
            0.86f);
        camera.GlobalPosition = new Vector3(-73.5f, 1.58f, -105.5f);
        camera.LookAt(new Vector3(-84.2f, 2.65f, -121.5f), Vector3.Up);
        camera.Fov = 38.0f;
        await WaitFrames(16);
        var heroSaved = SavePromotionWebp("hero.webp");
        var branding = BuildPromotionBranding();
        AddChild(branding);
        await WaitFrames(12);
        var socialSaved = SavePromotionSocialPreview();
        branding.QueueFree();

        foreach (var staged in operators)
        {
            staged.Visual.Root.Visible = false;
        }
        camera.GlobalPosition = new Vector3(18.0f, 5.4f, -113.0f);
        camera.LookAt(new Vector3(0.0f, 5.0f, -126.0f), Vector3.Up);
        camera.Fov = 47.0f;
        await WaitFrames(16);
        var citySaved = SavePromotionWebp("city.webp");

        var valid = heroSaved && squadSaved && citySaved && socialSaved;
        GD.Print(
            $"PROMOTION_CAPTURE valid={valid} hero={heroSaved} squad={squadSaved} "
            + $"city={citySaved} social={socialSaved}");
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

}
