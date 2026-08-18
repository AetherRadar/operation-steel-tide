using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly Vector3 OperationsOfficeOrigin = new(520.0f, 42.0f, 380.0f);
    public static readonly Vector3 OperationsOfficeHelipad = OperationsOfficeOrigin + new Vector3(4.0f, 1.55f, -21.0f);

    private Node3D _operationsOfficeScene = null!;
    private Camera3D _operationsOfficeCamera = null!;
    private readonly List<MeshInstance3D> _operationsOfficeGlassPanes = new();
    private bool _operationsOfficeActive;

    public bool IsOperationsOfficeActive => _operationsOfficeActive;
    public bool IsOperationsOfficeCameraCurrent
        => IsInstanceValid(_operationsOfficeCamera)
        && GetViewport().GetCamera3D() == _operationsOfficeCamera;
    public int OperationsOfficeScenePartCount
        => IsInstanceValid(_operationsOfficeScene) ? _operationsOfficeScene.GetChildCount() : 0;
    public int OperationsOfficeGlassPaneCount => _operationsOfficeGlassPanes.Count;
    public bool OperationsOfficeUsesSingleSurfaceGlass
        => _operationsOfficeGlassPanes.Count >= 9
        && _operationsOfficeGlassPanes.TrueForAll(pane => IsInstanceValid(pane) && pane.Mesh is QuadMesh);

    private void BuildOperationsOffice()
    {
        _operationsOfficeScene = new Node3D
        {
            Name = "RemoteOperationsOffice",
            Position = OperationsOfficeOrigin
        };
        AddChild(_operationsOfficeScene);

        var concrete = Mat("ops_office_concrete", new Color(0.12f, 0.145f, 0.14f), 0.18f, 0.8f);
        var floor = Mat("ops_office_floor", new Color(0.055f, 0.075f, 0.068f), 0.42f, 0.44f);
        var trim = Mat("ops_office_trim", new Color(0.025f, 0.04f, 0.038f), 0.78f, 0.28f);
        var desk = Mat("ops_office_desk", new Color(0.16f, 0.19f, 0.18f), 0.52f, 0.38f);
        var green = Mat(
            "ops_office_green",
            new Color(0.07f, 0.62f, 0.39f),
            0.22f,
            0.28f,
            new Color(0.02f, 0.42f, 0.2f));
        var amber = Mat(
            "ops_office_amber",
            new Color(0.92f, 0.48f, 0.12f),
            0.15f,
            0.3f,
            new Color(0.72f, 0.18f, 0.025f));
        var screen = Mat(
            "ops_office_screen",
            new Color(0.025f, 0.16f, 0.14f),
            0.18f,
            0.2f,
            new Color(0.01f, 0.3f, 0.22f));
        var glass = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.035f, 0.13f, 0.15f, 0.36f),
            Metallic = 0.28f,
            Roughness = 0.12f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // The office occupies the top floor of a remote tower. Its roof helipad is visible
        // through the command-room windows and doubles as the extraction destination.
        OfficeBox("OperationsTower", new Vector3(0, -21.0f, -4.0f), new Vector3(34.0f, 42.0f, 28.0f), concrete);
        OfficeBox("OperationsFloor", new Vector3(0, -0.24f, 0), new Vector3(30.0f, 0.48f, 18.0f), floor);
        OfficeBox("OperationsCeiling", new Vector3(0, 4.65f, 0), new Vector3(30.0f, 0.28f, 18.0f), trim);
        OfficeBox("OperationsLeftWall", new Vector3(-14.85f, 2.2f, 0), new Vector3(0.3f, 4.5f, 18.0f), concrete);
        OfficeBox("OperationsRightWall", new Vector3(14.85f, 2.2f, 2.5f), new Vector3(0.3f, 4.5f, 13.0f), concrete);
        OfficeBox("OperationsRearLintel", new Vector3(0, 4.15f, -8.85f), new Vector3(30.0f, 0.9f, 0.3f), trim);
        OfficeBox("OperationsRearSill", new Vector3(0, 0.38f, -8.85f), new Vector3(30.0f, 0.76f, 0.3f), trim);
        OfficeGlass("OperationsWindow", new Vector3(0, 2.28f, -8.78f), new Vector2(29.4f, 3.0f), glass);
        for (var x = -12.0f; x <= 12.0f; x += 4.0f)
        {
            OfficeBox($"WindowMullion_{x:0}", new Vector3(x, 2.25f, -8.7f), new Vector3(0.09f, 3.7f, 0.12f), trim);
        }

        BuildOperationsCommandWall(trim, screen, green, amber);
        BuildOperationsWorkstations(desk, trim, screen, green);
        BuildOperationsMapTable(desk, screen, green, amber);
        BuildOperationsLighting(trim);
        BuildOperationsHelipad(concrete, trim, green, amber, glass);

        var title = new Label3D
        {
            Name = "OperationsOfficeSign",
            Text = "SPECIAL OPERATIONS  //  STEEL TIDE",
            Position = new Vector3(3.8f, 3.52f, -8.42f),
            FontSize = 38,
            Modulate = new Color(0.45f, 1.0f, 0.75f),
            OutlineSize = 6,
            NoDepthTest = false
        };
        _operationsOfficeScene.AddChild(title);

        _operationsOfficeCamera = new Camera3D
        {
            Name = "OperationsOfficeCamera",
            Position = new Vector3(7.6f, 2.65f, 7.4f),
            Fov = 67.0f,
            Far = 460.0f,
            Current = false
        };
        _operationsOfficeScene.AddChild(_operationsOfficeCamera);
        _operationsOfficeCamera.LookAt(OperationsOfficeOrigin + new Vector3(3.2f, 1.72f, -4.2f), Vector3.Up);
    }

    private void BuildOperationsCommandWall(
        Godot.Material trim,
        Godot.Material screen,
        Godot.Material green,
        Godot.Material amber)
    {
        var root = new Node3D { Name = "CommandWall", Position = new Vector3(7.2f, 0.0f, -8.28f) };
        _operationsOfficeScene.AddChild(root);
        OfficeBox(root, "CommandWallBacking", new Vector3(0, 2.15f, 0), new Vector3(11.6f, 3.8f, 0.22f), trim);
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var x = -3.75f + column * 3.75f;
                var y = 1.32f + row * 1.68f;
                OfficeBox(root, $"CommandScreen_{row}_{column}", new Vector3(x, y, 0.16f), new Vector3(3.25f, 1.32f, 0.08f), screen);
                var indicator = (row + column) % 3 == 0 ? amber : green;
                OfficeBox(root, $"CommandTrace_{row}_{column}", new Vector3(x, y - 0.28f, 0.22f), new Vector3(2.55f, 0.055f, 0.03f), indicator);
                OfficeBox(root, $"CommandPulse_{row}_{column}", new Vector3(x - 0.78f + column * 0.05f, y + 0.23f, 0.22f), new Vector3(0.06f, 0.46f, 0.03f), indicator);
            }
        }
    }

    private void BuildOperationsWorkstations(
        Godot.Material desk,
        Godot.Material trim,
        Godot.Material screen,
        Godot.Material green)
    {
        for (var station = 0; station < 3; station++)
        {
            var root = new Node3D
            {
                Name = $"OperationsWorkstation_{station + 1}",
                Position = new Vector3(-8.3f + station * 4.4f, 0, -1.65f + Mathf.Abs(station - 1) * 0.35f),
                Rotation = new Vector3(0, -0.08f + station * 0.08f, 0)
            };
            _operationsOfficeScene.AddChild(root);
            OfficeBox(root, "DeskTop", new Vector3(0, 0.92f, 0), new Vector3(3.6f, 0.18f, 1.45f), desk);
            OfficeBox(root, "DeskLegLeft", new Vector3(-1.48f, 0.45f, 0), new Vector3(0.16f, 0.9f, 1.1f), trim);
            OfficeBox(root, "DeskLegRight", new Vector3(1.48f, 0.45f, 0), new Vector3(0.16f, 0.9f, 1.1f), trim);
            for (var monitor = 0; monitor < 2; monitor++)
            {
                var x = -0.85f + monitor * 1.7f;
                OfficeBox(root, $"MonitorStand_{monitor}", new Vector3(x, 1.24f, -0.18f), new Vector3(0.08f, 0.55f, 0.08f), trim);
                OfficeBox(root, $"Monitor_{monitor}", new Vector3(x, 1.62f, -0.25f), new Vector3(1.42f, 0.78f, 0.08f), screen, new Vector3(-0.08f, 0, 0));
                OfficeBox(root, $"MonitorLine_{monitor}", new Vector3(x, 1.62f, -0.195f), new Vector3(0.9f, 0.04f, 0.025f), green, new Vector3(-0.08f, 0, 0));
            }
            OfficeBox(root, "ChairSeat", new Vector3(0, 0.58f, 1.02f), new Vector3(1.1f, 0.15f, 1.0f), trim);
            OfficeBox(root, "ChairBack", new Vector3(0, 1.15f, 1.46f), new Vector3(1.1f, 1.05f, 0.14f), trim, new Vector3(-0.13f, 0, 0));
        }
    }

    private void BuildOperationsMapTable(
        Godot.Material desk,
        Godot.Material screen,
        Godot.Material green,
        Godot.Material amber)
    {
        var root = new Node3D { Name = "TacticalMapTable", Position = new Vector3(7.2f, 0, -2.3f) };
        _operationsOfficeScene.AddChild(root);
        OfficeBox(root, "MapTableBase", new Vector3(0, 0.58f, 0), new Vector3(5.2f, 1.15f, 3.25f), desk);
        OfficeBox(root, "MapTableDisplay", new Vector3(0, 1.2f, 0), new Vector3(4.75f, 0.08f, 2.8f), screen);
        for (var x = -1.8f; x <= 1.8f; x += 0.9f)
        {
            OfficeBox(root, $"MapGridX_{x:0.0}", new Vector3(x, 1.25f, 0), new Vector3(0.025f, 0.015f, 2.45f), green);
        }
        for (var z = -1.0f; z <= 1.0f; z += 0.5f)
        {
            OfficeBox(root, $"MapGridZ_{z:0.0}", new Vector3(0, 1.25f, z), new Vector3(4.2f, 0.015f, 0.025f), green);
        }
        foreach (var marker in new[]
        {
            new Vector3(-1.2f, 1.38f, -0.6f),
            new Vector3(0.35f, 1.38f, 0.7f),
            new Vector3(1.45f, 1.38f, -0.2f)
        })
        {
            var visual = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.15f, Height = 0.28f, RadialSegments = 12 },
                Position = marker,
                MaterialOverride = marker.X > 1.0f ? amber : green
            };
            root.AddChild(visual);
        }
    }

    private void BuildOperationsLighting(Godot.Material trim)
    {
        foreach (var x in new[] { -9.5f, -3.2f, 3.2f, 9.5f })
        {
            OfficeBox($"CeilingLightHousing_{x:0}", new Vector3(x, 4.42f, -0.5f), new Vector3(3.4f, 0.1f, 0.55f), trim);
            _operationsOfficeScene.AddChild(new OmniLight3D
            {
                Name = $"CeilingLight_{x:0}",
                Position = new Vector3(x, 4.18f, -0.5f),
                LightColor = new Color(0.72f, 0.9f, 0.84f),
                LightEnergy = 2.1f,
                OmniRange = 8.5f,
                ShadowEnabled = x is -3.2f or 3.2f
            });
        }
    }

    private void BuildOperationsHelipad(
        Godot.Material concrete,
        Godot.Material trim,
        Godot.Material green,
        Godot.Material amber,
        Godot.Material glass)
    {
        OfficeBox("HelipadBridge", new Vector3(4.0f, 0.22f, -12.4f), new Vector3(8.0f, 0.4f, 7.0f), concrete);
        OfficeBox("HelipadDeck", new Vector3(4.0f, 0.1f, -21.0f), new Vector3(21.0f, 0.5f, 18.0f), concrete);
        var ring = new MeshInstance3D
        {
            Name = "HelipadRing",
            Position = new Vector3(4.0f, 0.39f, -21.0f),
            Mesh = new TorusMesh { InnerRadius = 5.9f, OuterRadius = 6.15f, Rings = 48, RingSegments = 8 },
            MaterialOverride = green
        };
        _operationsOfficeScene.AddChild(ring);
        OfficeBox("HelipadHLeft", new Vector3(2.6f, 0.42f, -21.0f), new Vector3(0.48f, 0.08f, 4.0f), green);
        OfficeBox("HelipadHRight", new Vector3(5.4f, 0.42f, -21.0f), new Vector3(0.48f, 0.08f, 4.0f), green);
        OfficeBox("HelipadHBar", new Vector3(4.0f, 0.42f, -21.0f), new Vector3(3.25f, 0.08f, 0.48f), green);
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Mathf.Tau / 8.0f;
            var position = new Vector3(4.0f + Mathf.Cos(angle) * 8.2f, 0.65f, -21.0f + Mathf.Sin(angle) * 6.8f);
            var marker = new MeshInstance3D
            {
                Name = $"HelipadMarker_{i}",
                Position = position,
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.36f, RadialSegments = 12 },
                MaterialOverride = i % 2 == 0 ? amber : green
            };
            _operationsOfficeScene.AddChild(marker);
        }
        OfficeBox("RoofSafetyRailLeft", new Vector3(-6.4f, 0.85f, -21.0f), new Vector3(0.12f, 1.2f, 18.0f), trim);
        OfficeBox("RoofSafetyRailRight", new Vector3(14.4f, 0.85f, -21.0f), new Vector3(0.12f, 1.2f, 18.0f), trim);
        OfficeGlass("RoofWindScreen", new Vector3(4.0f, 1.2f, -30.0f), new Vector2(20.5f, 1.8f), glass);

        // A neighbouring tower gives the office windows depth and makes the HQ read as a
        // distinct district rather than another corner of the combat map.
        OfficeBox("NeighbourTower", new Vector3(35.0f, -12.0f, -38.0f), new Vector3(21.0f, 62.0f, 22.0f), trim);
        for (var y = -2.0f; y <= 17.0f; y += 3.0f)
        {
            OfficeGlass(
                $"NeighbourWindowBand_{y:0}",
                new Vector3(24.4f, y, -38.0f),
                new Vector2(19.0f, 1.15f),
                glass,
                new Vector3(0, Mathf.Pi * 0.5f, 0));
        }
    }

    private MeshInstance3D OfficeGlass(
        string name,
        Vector3 position,
        Vector2 size,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var pane = new MeshInstance3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = new QuadMesh { Size = size },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _operationsOfficeScene.AddChild(pane);
        _operationsOfficeGlassPanes.Add(pane);
        return pane;
    }

    private MeshInstance3D OfficeBox(
        string name,
        Vector3 position,
        Vector3 size,
        Godot.Material material,
        Vector3 rotation = default)
        => OfficeBox(_operationsOfficeScene, name, position, size, material, rotation);

    private static MeshInstance3D OfficeBox(
        Node parent,
        string name,
        Vector3 position,
        Vector3 size,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = SharedBoxMesh(size),
            MaterialOverride = material
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private void InitializeOperationsOfficeState(string[] args)
    {
        if (_squadDeployed)
        {
            return;
        }

        EnterOperationsOffice();
        if (Array.Exists(args, value => value == "--capture-squad-lobby"))
        {
            OnOperationsQuickStartRequested();
        }
        else if (Array.Exists(args, value => value == "--capture-demolition-briefing"))
        {
            OnDemolitionModeRequested();
        }
    }

    private void EnterOperationsOffice()
    {
        _squadNetwork?.StopLanRoomBrowsing();
        _operationsOfficeActive = true;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        _operationsOfficeCamera.MakeCurrent();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _hud.ShowOperationsOffice(GameLocalization.Get(
            "operations_status_ready",
            _languageSetting,
            "FIELD TEAM STANDING BY  //  HELIPAD CLEAR"));
        GetTree().Paused = true;
    }

    private void ActivateBattlefieldFromOperationsOffice()
    {
        _squadNetwork.StopLanRoomBrowsing();
        GetTree().Paused = false;
        _operationsOfficeActive = false;
        _missionDirector.ProcessMode = ProcessModeEnum.Always;
        var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
        playerCamera?.MakeCurrent();
        _hud.HideOperationsMenus();
    }

    private void OnOperationsQuickStartRequested()
    {
        _squadNetwork.StartLanRoomBrowsing();
        _hud.ShowSquadLobby(GameLocalization.Get(
            "operations_quick_status",
            _languageSetting,
            "LOCAL SQUAD  //  3 OPERATORS  //  YOU PICK  //  AI FILLS THE REST"));
    }

    private void OnDemolitionModeRequested()
    {
        _squadNetwork.StartLanRoomBrowsing();
        _hud.ShowDemolitionBriefing();
    }

    private void OnDemolitionBackRequested() => EnterOperationsOffice();

    private void OnOperationsHomeRequested()
    {
        if (_missionEnded || _squadDeployed)
        {
            RestartMission();
            return;
        }
        EnterOperationsOffice();
    }

    private async void ValidateOperationsOffice()
    {
        await WaitFrames(5);
        var uiReady = _hud.OperationsOfficeUiReady;
        var packedUiReady = _hud.OperationsOfficeUsesPackedScene;
        var sceneReady = OperationsOfficeScenePartCount >= 50
            && _operationsOfficeScene.FindChild("TacticalMapTable", recursive: true, owned: false) is not null
            && _operationsOfficeScene.FindChild("HelipadRing", recursive: true, owned: false) is not null
            && _operationsOfficeScene.FindChild("CommandWall", recursive: true, owned: false) is not null
            && OperationsOfficeUsesSingleSurfaceGlass;
        var homeReady = _operationsOfficeActive
            && _hud.IsOperationsOfficeVisible
            && !_hud.IsSquadLobbyVisible
            && IsOperationsOfficeCameraCurrent
            && GetTree().Paused
            && _missionDirector.ProcessMode == ProcessModeEnum.Disabled;

        _hud.PressDemolitionModeForDiagnostics();
        var demolitionReady = _hud.IsDemolitionBriefingVisible
            && _hud.DemolitionBriefingUiReady
            && !_hud.IsOperationsOfficeVisible
            && _squadNetwork.IsLanRoomBrowsingRequested;
        _hud.PressDemolitionRoleForDiagnostics(OperatorRole.Recon);
        var roleReady = _hud.SelectedDemolitionRole == OperatorRole.Recon;
        _hud.PressDemolitionBackForDiagnostics();
        var backReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsDemolitionBriefingVisible
            && IsOperationsOfficeCameraCurrent
            && !_squadNetwork.IsLanRoomBrowsingRequested;

        _hud.SetLanguage("zh");
        var chineseReady = _hud.OperationsOfficeLanguageReady;
        _hud.SetLanguage("en");
        var englishReady = _hud.OperationsOfficeLanguageReady;

        _hud.PressOperationsQuickStartForDiagnostics();
        var loadoutReady = _hud.IsSquadLobbyVisible
            && !_hud.IsOperationsOfficeVisible
            && _hud.SquadLobbyHomeUiReady
            && _squadNetwork.IsLanRoomBrowsingRequested
            && GetTree().Paused;
        _hud.PressSquadLobbyHomeForDiagnostics();
        var loadoutBackReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsSquadLobbyVisible
            && IsOperationsOfficeCameraCurrent
            && !_squadNetwork.IsLanRoomBrowsingRequested
            && GetTree().Paused;
        var languageReady = chineseReady && englishReady;
        var valid = uiReady && packedUiReady && sceneReady && homeReady && demolitionReady && roleReady && backReady
            && languageReady && loadoutReady && loadoutBackReady;
        GD.Print($"OPERATIONS_OFFICE_CHECK valid={valid} ui={uiReady} packed_ui={packedUiReady} scene={sceneReady} parts={OperationsOfficeScenePartCount} glass={OperationsOfficeGlassPaneCount} single_surface={OperationsOfficeUsesSingleSurfaceGlass} home={homeReady} demolition={demolitionReady} role={roleReady} back={backReady} language={languageReady} loadout={loadoutReady} loadout_back={loadoutBackReady} paused={GetTree().Paused} camera={IsOperationsOfficeCameraCurrent}");
        GD.Print($"OPERATIONS_OFFICE_PASS valid={valid}");
        GetTree().Paused = false;
        await WaitFrames(180);
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureOperationsOffice()
    {
        await WaitFrames(18);
        SaveViewportImage("res://operations_office_validation.png");
        GD.Print("OPERATIONS_OFFICE_CAPTURE path=operations_office_validation.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }

    private async void CaptureDemolitionBriefing()
    {
        _hud.SetLanRoomBrowseAvailable(true);
        _hud.SetLanRooms(new[]
        {
            new LanRoomInfo(
                "capture-demolition-room",
                "STEEL-TIDE-HOST",
                "192.168.10.42",
                SquadNetwork.DefaultPort,
                LanRoomKind.Demolition,
                DemolitionMapCatalog.TideforgeId,
                2,
                SquadNetwork.MaximumPlayers)
        });
        _hud.SelectDemolitionNetworkForDiagnostics(
            SquadSessionMode.Join,
            DemolitionNetworkTeam.Alpha,
            string.Empty);
        await WaitFrames(18);
        SaveViewportImage("res://demolition_briefing_validation.png");
        GD.Print("DEMOLITION_BRIEFING_CAPTURE path=demolition_briefing_validation.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }
}
