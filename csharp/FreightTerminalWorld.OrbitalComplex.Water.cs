using System.Linq;
using Godot;

namespace OperationSteelTide;

/// <summary>Runtime presentation for MAP 03's exterior sea and blackwater route.</summary>
public partial class FreightTerminalWorld
{
    private bool BuildOrbitalComplexRuntimeWaterPresentation()
    {
        if (_levelRoot is null || _orbitalComplexRuntimeBuild is null)
        {
            return false;
        }

        var pool = _orbitalComplexRuntimeBuild.AuthoredArtRoot.FindChild(
            "BlackwaterPoolSurface*", recursive: true, owned: false) as MeshInstance3D;
        if (pool is null)
        {
            return false;
        }

        var material = OceanBackdropFactory.BuildMaterial();
        material.SetShaderParameter("deep_color", new Color(0.006f, 0.025f, 0.034f, 1.0f));
        material.SetShaderParameter("shallow_color", new Color(0.018f, 0.105f, 0.125f, 1.0f));
        material.SetShaderParameter("foam_color", new Color(0.18f, 0.38f, 0.40f, 1.0f));
        material.SetShaderParameter("wave_scale", 2.4f);
        pool.MaterialOverride = material;
        pool.SetMeta("swimmable_water_surface", true);
        pool.SetMeta("water_surface_y", OrbitalComplexMapDefinition.BlackwaterSurfaceY);

        _levelRoot.SetMeta("falltide_blackwater_ready", true);
        var backdropCount = _levelRoot.GetChildren().OfType<MeshInstance3D>().Count(node =>
            node.GetMeta("ocean_backdrop", false).AsBool());
        _levelRoot.SetMeta("falltide_ocean_backdrop_count", backdropCount);
        return true;
    }
}
