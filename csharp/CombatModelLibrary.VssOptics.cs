using Godot;

namespace OperationSteelTide;

// Retained as a compatibility projection for the established combat-model
// diagnostic. New runtime code should consume IntegratedScopeInspection so all
// authored fixed-scope weapons share the same aperture contract.
internal readonly record struct VssIntegratedScopeInspection(
    bool Available,
    int GlassSurfaceCount,
    int RearApertureVertexCount,
    Vector3 RearApertureCenter,
    Vector2 RearApertureSize,
    bool ClearMaterialValid,
    bool MarkerAligned)
{
    public bool GeometryValid => Available
        && GlassSurfaceCount > 0
        && RearApertureVertexCount >= 4
        && RearApertureSize.X >= 0.02f
        && RearApertureSize.Y >= 0.02f
        && ClearMaterialValid;

    public bool Valid => GeometryValid && MarkerAligned;
}

internal static partial class CombatModelLibrary
{
    internal static VssIntegratedScopeInspection InspectVssIntegratedScope(
        Node3D root,
        Node3D? reticleAnchor = null)
        => AsVssInspection(InspectIntegratedScope(root, reticleAnchor));

    public static VssIntegratedScopeInspection InspectVssIntegratedScope()
        => AsVssInspection(InspectIntegratedScope(WeaponPlatform.VSS));

    private static VssIntegratedScopeInspection AsVssInspection(
        IntegratedScopeInspection inspection)
        => new(
            inspection.Available,
            inspection.GlassSurfaceCount,
            inspection.RearApertureVertexCount,
            inspection.RearApertureCenter,
            inspection.RearApertureSize,
            inspection.ClearMaterialValid,
            inspection.MarkerAligned && inspection.OpticalAxisAligned);
}
