using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaBuilder
{
    private void BuildBazaarCrossingLandmarks(Node3D root, DemolitionArenaLayout layout)
    {
        AddSign(
            root,
            "BazaarArenaTitle",
            layout.Origin + new Vector3(0.0f, 5.4f, -54.9f),
            "BAZAAR CROSSING  //  BC-06",
            0.0f,
            new Color(0.96f, 0.72f, 0.28f));
        AddSign(
            root,
            "BazaarSiteASign",
            layout.Origin + new Vector3(-43.0f, 4.5f, -30.4f),
            "A  //  GALLERY COURT",
            0.0f,
            new Color(1.0f, 0.53f, 0.17f));
        AddSign(
            root,
            "BazaarSiteBSign",
            layout.Origin + new Vector3(43.0f, 4.5f, -30.4f),
            "B  //  BALCONY MARKET",
            0.0f,
            new Color(0.28f, 0.84f, 1.0f));
        AddSign(
            root,
            "BazaarGallerySign",
            layout.Origin + new Vector3(-63.2f, 4.7f, -20.0f),
            "A GALLERY  +3.0M",
            Mathf.Pi * 0.5f,
            new Color(1.0f, 0.66f, 0.24f));
        AddSign(
            root,
            "BazaarBridgeSign",
            layout.Origin + new Vector3(0.0f, 4.7f, -1.72f),
            "MID BRIDGE  +3.0M",
            Mathf.Pi,
            new Color(0.94f, 0.84f, 0.54f));
        AddSign(
            root,
            "BazaarBalconySign",
            layout.Origin + new Vector3(63.2f, 4.3f, -22.0f),
            "B BALCONY  +2.6M",
            -Mathf.Pi * 0.5f,
            new Color(0.34f, 0.88f, 1.0f));
    }

    private static void BuildBazaarCrossingCoverDetails(
        Node3D root,
        DemolitionArenaLayout layout)
    {
        // Bazaar cover silhouettes, stairs, railings, and surface details are authored
        // in the DCC scene. Runtime construction intentionally adds no visible boxes.
        root.SetMeta("bazaar_authored_cover", true);
        root.SetMeta("bazaar_traversal_surface_count", layout.TraversalBoxes.Count);
    }

    private void BuildBazaarCrossingRouteGuidance(Node3D root, DemolitionArenaLayout layout)
    {
        AddFloorLabel(
            root,
            "BazaarAttackFloorLabel",
            layout.Origin + new Vector3(0.0f, 0.09f, 48.0f),
            "MARKET ENTRY",
            new Color(0.56f, 0.92f, 0.86f),
            82);
        AddFloorLabel(
            root,
            "BazaarRouteALabel",
            layout.Origin + new Vector3(-29.0f, 0.09f, 40.0f),
            "<  A LONG",
            new Color(1.0f, 0.58f, 0.18f),
            72);
        AddFloorLabel(
            root,
            "BazaarRouteMidLabel",
            layout.Origin + new Vector3(0.0f, 0.09f, 33.0f),
            "MID  ^",
            new Color(0.9f, 0.88f, 0.68f),
            64);
        AddFloorLabel(
            root,
            "BazaarRouteBLabel",
            layout.Origin + new Vector3(29.0f, 0.09f, 40.0f),
            "B BANANA  >",
            new Color(0.28f, 0.82f, 0.96f),
            72);
        AddFloorLabel(
            root,
            "BazaarDefendFloorLabel",
            layout.Origin + new Vector3(0.0f, 0.09f, -49.0f),
            "NORTH ARCADE",
            new Color(0.46f, 0.94f, 0.68f),
            78);
    }

    private void BuildBazaarCrossingLighting(Node3D root, DemolitionArenaLayout layout)
    {
        var floodlights = new[]
        {
            (Name: "BazaarFloodlightAttack", Position: new Vector3(0.0f, 10.5f, 42.0f), Color: new Color(1.0f, 0.77f, 0.52f), Shadows: false),
            (Name: "BazaarFloodlightALong", Position: new Vector3(-47.0f, 10.0f, 9.0f), Color: new Color(1.0f, 0.67f, 0.36f), Shadows: true),
            (Name: "BazaarFloodlightMid", Position: new Vector3(0.0f, 11.5f, -1.0f), Color: new Color(0.95f, 0.78f, 0.54f), Shadows: true),
            (Name: "BazaarFloodlightBBanana", Position: new Vector3(48.0f, 10.0f, 8.0f), Color: new Color(0.61f, 0.82f, 0.92f), Shadows: true),
            (Name: "BazaarFloodlightDefender", Position: new Vector3(0.0f, 10.5f, -44.0f), Color: new Color(0.69f, 0.86f, 0.78f), Shadows: false)
        };
        foreach (var light in floodlights)
        {
            root.AddChild(new SpotLight3D
            {
                Name = light.Name,
                Position = layout.Origin + light.Position,
                RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
                LightColor = light.Color,
                LightEnergy = 3.8f,
                SpotRange = 30.0f,
                SpotAngle = 54.0f,
                ShadowEnabled = light.Shadows
            });
            _visualPartCount++;
        }

        AddBazaarSiteLight(root, layout, "BazaarSiteAAccent", 0, new Color(1.0f, 0.43f, 0.13f));
        AddBazaarSiteLight(root, layout, "BazaarSiteBAccent", 1, new Color(0.18f, 0.68f, 1.0f));
    }

    private void AddBazaarSiteLight(
        Node3D root,
        DemolitionArenaLayout layout,
        string name,
        int siteIndex,
        Color color)
    {
        root.AddChild(new OmniLight3D
        {
            Name = name,
            Position = layout.SitePosition(siteIndex) + Vector3.Up * 2.6f,
            LightColor = color,
            LightEnergy = 1.15f,
            OmniRange = 8.0f,
            ShadowEnabled = false
        });
        _visualPartCount++;
    }
}
