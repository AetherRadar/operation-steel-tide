using Godot;

namespace OperationSteelTide;

internal static class FirstPersonMeshFactory
{
    private readonly record struct LoftSection(
        float Along,
        float RadiusA,
        float RadiusB,
        float OffsetA = 0.0f,
        float OffsetB = 0.0f);

    public static ArrayMesh Palm(bool left)
    {
        var side = left ? -1.0f : 1.0f;
        return Loft(
            Vector3.Back,
            Vector3.Right,
            Vector3.Up,
            20,
            new LoftSection(-0.125f, 0.063f, 0.031f, side * 0.004f, -0.004f),
            new LoftSection(-0.095f, 0.079f, 0.039f, side * 0.002f, 0.001f),
            new LoftSection(-0.045f, 0.089f, 0.047f, 0.0f, 0.006f),
            new LoftSection(0.015f, 0.092f, 0.049f, -side * 0.003f, 0.008f),
            new LoftSection(0.07f, 0.086f, 0.044f, -side * 0.005f, 0.004f),
            new LoftSection(0.118f, 0.073f, 0.037f, -side * 0.004f, -0.002f));
    }

    public static ArrayMesh Finger(float length, float width, float thickness, float curl)
    {
        return Loft(
            Vector3.Forward,
            Vector3.Right,
            Vector3.Up,
            18,
            new LoftSection(0.006f, width * 0.48f, thickness * 0.48f),
            new LoftSection(length * 0.18f, width * 0.51f, thickness * 0.5f, 0.0f, -curl * 0.03f),
            new LoftSection(length * 0.38f, width * 0.47f, thickness * 0.47f, 0.0f, -curl * 0.14f),
            new LoftSection(length * 0.56f, width * 0.45f, thickness * 0.44f, 0.0f, -curl * 0.33f),
            new LoftSection(length * 0.73f, width * 0.42f, thickness * 0.4f, 0.0f, -curl * 0.57f),
            new LoftSection(length * 0.89f, width * 0.36f, thickness * 0.34f, 0.0f, -curl * 0.84f),
            new LoftSection(length, width * 0.18f, thickness * 0.18f, 0.0f, -curl));
    }

    public static ArrayMesh Forearm()
    {
        return Loft(
            Vector3.Up,
            Vector3.Right,
            Vector3.Back,
            20,
            new LoftSection(-0.24f, 0.106f, 0.096f, 0.0f, 0.012f),
            new LoftSection(-0.17f, 0.101f, 0.091f, 0.002f, 0.009f),
            new LoftSection(-0.08f, 0.093f, 0.084f, 0.0f, 0.004f),
            new LoftSection(0.02f, 0.087f, 0.078f),
            new LoftSection(0.11f, 0.081f, 0.072f, -0.002f, -0.002f),
            new LoftSection(0.2f, 0.074f, 0.066f, 0.0f, -0.004f),
            new LoftSection(0.235f, 0.071f, 0.063f, 0.0f, -0.004f));
    }

    public static ArrayMesh Cuff()
    {
        return Loft(
            Vector3.Up,
            Vector3.Right,
            Vector3.Back,
            20,
            new LoftSection(-0.052f, 0.079f, 0.071f),
            new LoftSection(-0.038f, 0.086f, 0.077f),
            new LoftSection(0.036f, 0.084f, 0.075f),
            new LoftSection(0.052f, 0.077f, 0.069f));
    }

    public static ArrayMesh BackPlate(float width, float length, float thickness)
    {
        return Loft(
            Vector3.Back,
            Vector3.Right,
            Vector3.Up,
            18,
            new LoftSection(-length * 0.5f, width * 0.28f, thickness * 0.12f),
            new LoftSection(-length * 0.36f, width * 0.46f, thickness * 0.43f),
            new LoftSection(0.0f, width * 0.5f, thickness * 0.5f),
            new LoftSection(length * 0.36f, width * 0.46f, thickness * 0.43f),
            new LoftSection(length * 0.5f, width * 0.28f, thickness * 0.12f));
    }

    private static ArrayMesh Loft(
        Vector3 axis,
        Vector3 basisA,
        Vector3 basisB,
        int sides,
        params LoftSection[] sections)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var section = sections[sectionIndex];
            for (var side = 0; side < sides; side++)
            {
                var angle = Mathf.Tau * side / sides;
                var point = axis * section.Along
                    + basisA * (section.OffsetA + Mathf.Cos(angle) * section.RadiusA)
                    + basisB * (section.OffsetB + Mathf.Sin(angle) * section.RadiusB);
                surface.SetUV(new Vector2((float)side / sides, (float)sectionIndex / (sections.Length - 1)));
                surface.AddVertex(point);
            }
        }

        for (var sectionIndex = 0; sectionIndex < sections.Length - 1; sectionIndex++)
        {
            var rightHanded = basisA.Cross(basisB).Dot(axis) > 0.0f;
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                var currentA = sectionIndex * sides + side;
                var currentB = sectionIndex * sides + next;
                var nextA = (sectionIndex + 1) * sides + side;
                var nextB = (sectionIndex + 1) * sides + next;
                if (rightHanded)
                {
                    surface.AddIndex(currentA);
                    surface.AddIndex(currentB);
                    surface.AddIndex(nextA);
                    surface.AddIndex(currentB);
                    surface.AddIndex(nextB);
                    surface.AddIndex(nextA);
                }
                else
                {
                    surface.AddIndex(currentA);
                    surface.AddIndex(nextA);
                    surface.AddIndex(currentB);
                    surface.AddIndex(currentB);
                    surface.AddIndex(nextA);
                    surface.AddIndex(nextB);
                }
            }
        }

        var axisMatchesBasis = basisA.Cross(basisB).Dot(axis) > 0.0f;
        var ringVertexCount = sections.Length * sides;
        AddCap(surface, axis, basisA, basisB, sides, sections[0], 0, ringVertexCount, axisMatchesBasis);
        AddCap(surface, axis, basisA, basisB, sides, sections[^1], (sections.Length - 1) * sides, ringVertexCount + 1, !axisMatchesBasis);
        surface.GenerateNormals();
        return surface.Commit();
    }

    private static void AddCap(
        SurfaceTool surface,
        Vector3 axis,
        Vector3 basisA,
        Vector3 basisB,
        int sides,
        LoftSection section,
        int ringStart,
        int centerIndex,
        bool reverse)
    {
        surface.SetUV(new Vector2(0.5f, 0.5f));
        surface.AddVertex(axis * section.Along + basisA * section.OffsetA + basisB * section.OffsetB);
        for (var side = 0; side < sides; side++)
        {
            var next = (side + 1) % sides;
            surface.AddIndex(centerIndex);
            surface.AddIndex(ringStart + (reverse ? next : side));
            surface.AddIndex(ringStart + (reverse ? side : next));
        }
    }
}
