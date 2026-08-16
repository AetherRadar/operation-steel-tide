using Godot;

namespace OperationSteelTide;

internal static class GrenadeVisualFactory
{
    public static Node3D CreateFragmentationGrenade(bool firstPerson)
    {
        var root = new Node3D { Name = "FragmentationGrenadeVisual" };
        var body = Material(new Color(0.11f, 0.15f, 0.09f), 0.42f, 0.58f);
        var metal = Material(new Color(0.25f, 0.28f, 0.24f), 0.82f, 0.27f);
        var dark = Material(new Color(0.045f, 0.055f, 0.04f), 0.28f, 0.68f);

        AddPart(root, new SphereMesh
        {
            Radius = 0.09f,
            Height = 0.18f,
            RadialSegments = 16,
            Rings = 8
        }, Vector3.Zero, Vector3.Zero, body, firstPerson);
        for (var ring = -2; ring <= 2; ring++)
        {
            AddPart(root, new CylinderMesh
            {
                TopRadius = 0.094f,
                BottomRadius = 0.094f,
                Height = 0.012f,
                RadialSegments = 16
            }, new Vector3(0, ring * 0.031f, 0), Vector3.Zero, dark, firstPerson);
        }
        for (var rib = 0; rib < 8; rib++)
        {
            var angle = rib * Mathf.Tau / 8.0f;
            AddPart(root, new BoxMesh { Size = new Vector3(0.012f, 0.14f, 0.018f) },
                new Vector3(Mathf.Cos(angle) * 0.087f, 0, Mathf.Sin(angle) * 0.087f),
                new Vector3(0, -angle, 0), dark, firstPerson);
        }
        AddPart(root, new CylinderMesh
        {
            TopRadius = 0.041f,
            BottomRadius = 0.045f,
            Height = 0.055f,
            RadialSegments = 12
        }, new Vector3(0, 0.105f, 0), Vector3.Zero, metal, firstPerson);
        AddPart(root, new BoxMesh { Size = new Vector3(0.035f, 0.016f, 0.13f) },
            new Vector3(0.025f, 0.145f, 0.035f), new Vector3(0.12f, 0, -0.18f), metal, firstPerson);
        AddPart(root, new CylinderMesh
        {
            TopRadius = 0.032f,
            BottomRadius = 0.032f,
            Height = 0.008f,
            RadialSegments = 18
        }, new Vector3(-0.052f, 0.142f, 0), new Vector3(0, 0, Mathf.Pi / 2), metal, firstPerson);
        return root;
    }

    public static Node3D CreateSmokeGrenade(bool firstPerson)
    {
        var root = new Node3D { Name = "SmokeGrenadeVisual" };
        var body = Material(new Color(0.64f, 0.68f, 0.62f), 0.34f, 0.5f);
        var band = Material(new Color(0.16f, 0.32f, 0.31f), 0.2f, 0.66f);
        var metal = Material(new Color(0.3f, 0.33f, 0.3f), 0.82f, 0.25f);

        AddPart(root, new CapsuleMesh
        {
            Radius = 0.075f,
            Height = 0.205f,
            RadialSegments = 16,
            Rings = 6
        }, Vector3.Zero, Vector3.Zero, body, firstPerson);
        for (var bandIndex = -1; bandIndex <= 1; bandIndex++)
        {
            AddPart(root, new CylinderMesh
            {
                TopRadius = 0.079f,
                BottomRadius = 0.079f,
                Height = 0.014f,
                RadialSegments = 16
            }, new Vector3(0, bandIndex * 0.052f, 0), Vector3.Zero, band, firstPerson);
        }
        AddPart(root, new CylinderMesh
        {
            TopRadius = 0.038f,
            BottomRadius = 0.042f,
            Height = 0.05f,
            RadialSegments = 12
        }, new Vector3(0, 0.126f, 0), Vector3.Zero, metal, firstPerson);
        AddPart(root, new BoxMesh { Size = new Vector3(0.032f, 0.014f, 0.145f) },
            new Vector3(0.02f, 0.159f, 0.04f), new Vector3(0.08f, 0, -0.12f), metal, firstPerson);
        AddPart(root, new CylinderMesh
        {
            TopRadius = 0.03f,
            BottomRadius = 0.03f,
            Height = 0.008f,
            RadialSegments = 18
        }, new Vector3(-0.05f, 0.153f, 0), new Vector3(0, 0, Mathf.Pi / 2), metal, firstPerson);
        return root;
    }

    private static void AddPart(
        Node3D root,
        PrimitiveMesh mesh,
        Vector3 position,
        Vector3 rotation,
        Material material,
        bool firstPerson)
    {
        root.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material,
            CastShadow = firstPerson
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : GeometryInstance3D.ShadowCastingSetting.On
        });
    }

    private static StandardMaterial3D Material(Color color, float metallic, float roughness)
        => new()
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
}
