using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaBuilder
{
    private IReadOnlyList<BreakableGlassField> BuildBazaarCrossingGlass(
        Node3D root,
        DemolitionArenaLayout layout)
    {
        if (layout.MapId != DemolitionMapCatalog.BazaarCrossingId)
        {
            return Array.Empty<BreakableGlassField>();
        }

        var glassMaterial = _material(
            "bazaar_portal_breakable_glass",
            new Color(0.27f, 0.68f, 0.76f, 0.30f),
            0.18f,
            0.08f,
            new Color(0.012f, 0.045f, 0.052f));
        glassMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        glassMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        glassMaterial.VertexColorUseAsAlbedo = true;

        var field = new BreakableGlassField
        {
            Name = "BazaarPortalGlass"
        };
        root.AddChild(field);
        field.Configure(
            glassMaterial,
            glassMaterial,
            backingMaterial: null,
            visibilityRange: 145.0f,
            buildFrames: false,
            blocksMovementUntilShattered: true);
        field.SetWorldOcclusionRequired(true);

        foreach (var portal in layout.BazaarGlassPortals)
        {
            field.AddPane(
                // Layout centers share the arena root-local contract used by every
                // authored static box, despite the historical WorldCenter name.
                portal.WorldCenter,
                portal.Size,
                new Color(0.78f, 0.95f, 1.0f, 0.84f));
        }
        var portalIndexByName = layout.BazaarGlassPortals
            .Select((portal, index) => (portal.Name, Index: index))
            .ToDictionary(entry => entry.Name, entry => entry.Index, StringComparer.Ordinal);
        if (!field.LinkShatterGroup(
                portalIndexByName["Bazaar_Mid_NorthConnector_South_Portal00"],
                portalIndexByName["Bazaar_Mid_NorthTeaHall_North_Portal00"])
            || !field.LinkShatterGroup(
                portalIndexByName["Bazaar_Mid_NorthTeaHall_South_Portal00"],
                portalIndexByName["Bazaar_Mid_CenterProduceHall_North_Portal00"])
            || !field.LinkShatterGroup(
                portalIndexByName["Bazaar_Mid_CenterProduceHall_South_Portal00"],
                portalIndexByName["Bazaar_Mid_SouthCarpetHall_North_Portal01"]))
        {
            throw new InvalidOperationException(
                "Bazaar paired Mid portal glass could not be linked before commit.");
        }
        field.Commit();

        root.SetMeta("bazaar_glass_portal_count", layout.BazaarGlassPortals.Count);
        _visualPartCount++;
        return Array.AsReadOnly(new[] { field });
    }
}
