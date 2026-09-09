using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string ResidentialLifeClusterScenePath =
        "res://assets/models/residential_life_cluster/residential_life_cluster.glb";

    private readonly record struct ResidentialLifeClusterZone(
        string Id,
        string ChineseName,
        string EnglishName,
        Vector3 Anchor,
        Color Accent);

    private static readonly ResidentialLifeClusterZone[] ResidentialLifeClusterZones =
    {
        new("residential_life_supermarket", "\u793e\u533a\u8d85\u5e02", "COMMUNITY SUPERMARKET", new(-13.0f, 0.2f, 5.0f), new Color(0.38f, 0.78f, 0.65f)),
        new("residential_life_plaza", "\u90bb\u91cc\u8d2d\u7269\u5e7f\u573a", "NEIGHBORHOOD PLAZA", new(12.0f, 0.2f, 4.0f), new Color(0.84f, 0.58f, 0.27f)),
        new("residential_life_market_hall", "\u751f\u6d3b\u96c6\u5e02", "MARKET HALL", new(0.0f, 0.2f, -13.0f), new Color(0.48f, 0.64f, 0.86f))
    };

    private Node3D? _residentialLifeCluster;
    private int _residentialLifeClusterZoneCount;

    public int ResidentialLifeClusterZoneCount => _residentialLifeClusterZoneCount;

    /// <summary>
    /// Adds the standalone authored residential-life environment as a compact
    /// community shopping district. The GLB is the visible art; gameplay collision and loot
    /// anchors remain separate so the district can later be replaced by a dedicated
    /// residential-life Blender export without changing the map contract.
    /// </summary>
    private void BuildResidentialLifeCluster(Node3D community)
    {
        var scene = GD.Load<PackedScene>(ResidentialLifeClusterScenePath);
        if (scene is null)
        {
            GD.PushError($"Residential life cluster authored scene is missing: {ResidentialLifeClusterScenePath}");
            _residentialLifeClusterZoneCount = 0;
            return;
        }
        if (scene.Instantiate() is not Node3D authored)
        {
            GD.PushError($"Residential life cluster authored scene could not instantiate: {ResidentialLifeClusterScenePath}");
            _residentialLifeClusterZoneCount = 0;
            return;
        }

        var root = new Node3D
        {
            Name = "ResidentialLifeCluster",
            Position = new Vector3(-52.0f, 0.02f, 43.0f),
            Scale = Vector3.One * 0.46f
        };
        root.AddToGroup("residential_life_cluster");
        root.SetMeta("authored_scene_path", ResidentialLifeClusterScenePath);
        root.SetMeta("layout_version", "life-cluster-v1");
        root.SetMeta("gameplay_contract", "visual-authored-shell-with-separate-anchors");
        community.AddChild(root);
        authored.Name = "AuthoredShoppingEnvironment";
        authored.AddToGroup("residential_life_cluster_authored_art");
        authored.SetMeta("source_license", "CC0");
        root.AddChild(authored);
        AddResidentialLifeClusterCollision(root);
        _residentialLifeCluster = root;

        _residentialLifeClusterZoneCount = 0;
        foreach (var zone in ResidentialLifeClusterZones)
        {
            var anchor = new Node3D
            {
                Name = $"Zone_{zone.Id}",
                Position = zone.Anchor
            };
            anchor.AddToGroup("residential_commercial_zone");
            anchor.SetMeta("zone_id", zone.Id);
            anchor.SetMeta("zone_name_zh", zone.ChineseName);
            anchor.SetMeta("zone_name_en", zone.EnglishName);
            anchor.SetMeta("loot_profile", zone.Id.Contains("supermarket", StringComparison.Ordinal)
                ? "community_pantry"
                : zone.Id.Contains("market_hall", StringComparison.Ordinal)
                    ? "market_hall"
                    : "retail_plaza");
            root.AddChild(anchor);

            var sign = new Label3D
            {
                Name = "ZoneSign",
                Position = new Vector3(0, 3.8f, 0),
                FontSize = 22,
                OutlineSize = 6,
                Modulate = zone.Accent,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 65.0f
            };
            anchor.AddChild(RegisterResidentialLocalizedLabel(sign, language =>
                language == "zh" ? zone.ChineseName : zone.EnglishName));
            _residentialLifeClusterZoneCount++;
        }
    }

    private static void AddResidentialLifeClusterCollision(Node3D root)
    {
        var body = new StaticBody3D { Name = "LifeClusterGameplayCollision" };
        body.CollisionLayer = 1;
        body.CollisionMask = 1 | 2;
        root.AddChild(body);
        AddPerimeterWithOpening(body, new Vector3(-22, 7.0f, 2), new Vector3(15, 14, 20), 7.0f);
        AddPerimeterWithOpening(body, new Vector3(12, 4.0f, -3), new Vector3(26, 8, 18), 7.0f);
        AddPerimeterWithOpening(body, new Vector3(29, 2.7f, 1), new Vector3(8, 5.4f, 12), 3.0f);
        AddPerimeterWithOpening(body, new Vector3(39, 2.7f, 1), new Vector3(8, 5.4f, 12), 3.0f);
        AddPerimeterWithOpening(body, new Vector3(49, 2.7f, 1), new Vector3(8, 5.4f, 12), 3.0f);
    }

    private static void AddPerimeterWithOpening(StaticBody3D body, Vector3 center, Vector3 size, float openingWidth)
    {
        const float wall = 0.35f;
        var sideWidth = Mathf.Max(0.4f, (size.X - openingWidth) * 0.5f);
        AddCollision(body, center + new Vector3(-(openingWidth + sideWidth) * 0.5f, 0, size.Z * 0.5f), new Vector3(sideWidth, size.Y, wall));
        AddCollision(body, center + new Vector3((openingWidth + sideWidth) * 0.5f, 0, size.Z * 0.5f), new Vector3(sideWidth, size.Y, wall));
        AddCollision(body, center + new Vector3(0, 0, -size.Z * 0.5f), new Vector3(size.X, size.Y, wall));
        AddCollision(body, center + new Vector3(-size.X * 0.5f, 0, 0), new Vector3(wall, size.Y, size.Z));
        AddCollision(body, center + new Vector3(size.X * 0.5f, 0, 0), new Vector3(wall, size.Y, size.Z));
    }

    private static void AddCollision(StaticBody3D body, Vector3 position, Vector3 size)
    {
        body.AddChild(new CollisionShape3D
        {
            Position = position,
            Shape = new BoxShape3D { Size = size }
        });
    }
}
