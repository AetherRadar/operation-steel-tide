using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly StringName MapDetailVisualGroup = "map_detail_visuals";
    private static readonly float[] MapDetailVisibilityRanges = { 34.0f, 58.0f, 84.0f };
    private static readonly StringName LowPolyBuildingVisualGroup = "low_poly_building_visuals";
    private static readonly float[] LowPolyBuildingVisibilityRanges = { 190.0f, 275.0f, 360.0f };

    private struct MapRuntimeCounts
    {
        public int Nodes;
        public int StaticBodies;
        public int CollisionShapes;
        public int MeshInstances;
        public int MultiMeshInstances;
        public int Labels;
        public int Lights;
        public int ResidentialStairBodies;
        public int ResidentialStairShapes;
        public int ResidentialStairVisuals;
        public int MapDetailVisuals;
        public int LowPolyBuildingVisuals;
        public int BreakableGlassFields;
        public int BreakableGlassPanes;
    }

    private static bool IsDistanceCulledBox(string name)
    {
        if (name.StartsWith("Apartment", System.StringComparison.Ordinal))
        {
            return !name.StartsWith("ApartmentDivider", System.StringComparison.Ordinal)
                && !name.StartsWith("ApartmentCorridorWall", System.StringComparison.Ordinal)
                && !name.StartsWith("ApartmentDoorHeader", System.StringComparison.Ordinal);
        }
        return name.StartsWith("Corridor", System.StringComparison.Ordinal)
            || name.StartsWith("Clinic", System.StringComparison.Ordinal)
            || name.StartsWith("Evac", System.StringComparison.Ordinal)
            || name.StartsWith("Workshop", System.StringComparison.Ordinal)
            || name.StartsWith("Security", System.StringComparison.Ordinal)
            || name.StartsWith("Contraband", System.StringComparison.Ordinal)
            || name.StartsWith("Smuggler", System.StringComparison.Ordinal)
            || name.StartsWith("Kitchen", System.StringComparison.Ordinal)
            || name.StartsWith("Family", System.StringComparison.Ordinal)
            || name.StartsWith("ComplexDesk", System.StringComparison.Ordinal)
            || name.StartsWith("ComplexCabinet", System.StringComparison.Ordinal)
            || name.StartsWith("ComplexCrate", System.StringComparison.Ordinal)
            || name.StartsWith("HangarWorkBay", System.StringComparison.Ordinal)
            || name.StartsWith("HangarPartsShelf", System.StringComparison.Ordinal)
            || name.StartsWith("MaintenanceBench", System.StringComparison.Ordinal)
            || name.StartsWith("MaintenanceRack", System.StringComparison.Ordinal)
            || name.StartsWith("RepairStand", System.StringComparison.Ordinal)
            || name.StartsWith("BarracksBunk", System.StringComparison.Ordinal)
            || name.StartsWith("BarracksLocker", System.StringComparison.Ordinal)
            || name.StartsWith("WarehouseCrate", System.StringComparison.Ordinal)
            || name.StartsWith("ArmoryBench", System.StringComparison.Ordinal)
            || name.StartsWith("CustomsDesk", System.StringComparison.Ordinal)
            || name.StartsWith("ResidentialInfillCrate", System.StringComparison.Ordinal);
    }

    private void RegisterMapDetailVisual(GeometryInstance3D visual)
    {
        visual.AddToGroup(MapDetailVisualGroup);
        ConfigureMapDetailVisual(visual);
    }

    private void ConfigureMapDetailVisual(GeometryInstance3D visual)
    {
        visual.VisibilityRangeEnd = MapDetailVisibilityRanges[Mathf.Clamp(_qualitySetting, 0, 2)];
        visual.VisibilityRangeEndMargin = _qualitySetting == 0 ? 4.0f : 8.0f;
        visual.CastShadow = _qualitySetting >= 2
            ? GeometryInstance3D.ShadowCastingSetting.On
            : GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private void ApplyMapDetailQuality()
    {
        if (!IsInsideTree())
        {
            return;
        }
        var detailNodes = GetTree().GetNodesInGroup(MapDetailVisualGroup);
        using var detailNodesBacking = detailNodes.AsDisposable();
        foreach (var node in detailNodes)
        {
            if (node is GeometryInstance3D visual && IsInstanceValid(visual))
            {
                ConfigureMapDetailVisual(visual);
            }
        }
    }

    private void ConfigureLowPolyBuildingVisual(GeometryInstance3D visual)
    {
        var maximumRange = (float)visual
            .GetMeta("low_poly_max_visibility_range", 360.0f)
            .AsDouble();
        visual.VisibilityRangeEnd = Mathf.Min(
            maximumRange,
            LowPolyBuildingVisibilityRanges[Mathf.Clamp(_qualitySetting, 0, 2)]);
        visual.VisibilityRangeEndMargin = _qualitySetting == 0 ? 8.0f : 16.0f;
        visual.CastShadow = _qualitySetting >= 2
            ? GeometryInstance3D.ShadowCastingSetting.On
            : GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private void ApplyLowPolyBuildingQuality()
    {
        if (!IsInsideTree())
        {
            return;
        }
        var visualNodes = GetTree().GetNodesInGroup(LowPolyBuildingVisualGroup);
        using var visualNodesBacking = visualNodes.AsDisposable();
        foreach (var node in visualNodes)
        {
            if (node is GeometryInstance3D visual && IsInstanceValid(visual))
            {
                ConfigureLowPolyBuildingVisual(visual);
            }
        }
    }

    private static void CountMapRuntimeNodes(
        Node node,
        ref MapRuntimeCounts counts,
        HashSet<ulong> boxMeshResources,
        bool insideResidentialStair = false)
    {
        counts.Nodes++;
        if (node is StaticBody3D)
        {
            counts.StaticBodies++;
        }
        if (node is CollisionObject3D collisionObject)
        {
            var owners = collisionObject.GetShapeOwners();
            foreach (var owner in owners)
            {
                counts.CollisionShapes += collisionObject.ShapeOwnerGetShapeCount((uint)owner);
            }
        }
        if (node is MeshInstance3D)
        {
            counts.MeshInstances++;
        }
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is BoxMesh boxMesh)
        {
            boxMeshResources.Add(boxMesh.GetInstanceId());
        }
        if (node is MultiMeshInstance3D)
        {
            counts.MultiMeshInstances++;
        }
        if (node is MultiMeshInstance3D multiMeshInstance
            && multiMeshInstance.Multimesh?.Mesh is BoxMesh multiBoxMesh)
        {
            boxMeshResources.Add(multiBoxMesh.GetInstanceId());
        }
        if (node is Label3D)
        {
            counts.Labels++;
        }
        if (node is Light3D)
        {
            counts.Lights++;
        }
        if (node.IsInGroup(MapDetailVisualGroup))
        {
            counts.MapDetailVisuals++;
        }
        if (node.IsInGroup(LowPolyBuildingVisualGroup))
        {
            counts.LowPolyBuildingVisuals++;
        }
        if (node is BreakableGlassField glassField)
        {
            counts.BreakableGlassFields++;
            counts.BreakableGlassPanes += glassField.PaneCount;
        }
        var nodeName = node.Name.ToString();
        var insideStair = insideResidentialStair
            || nodeName.Contains("ResidentialStair", System.StringComparison.Ordinal);
        if (insideStair)
        {
            if (node is StaticBody3D)
            {
                counts.ResidentialStairBodies++;
            }
            if (node is CollisionObject3D stairCollision)
            {
                var owners = stairCollision.GetShapeOwners();
                foreach (var owner in owners)
                {
                    counts.ResidentialStairShapes += stairCollision.ShapeOwnerGetShapeCount((uint)owner);
                }
            }
            if (node is MeshInstance3D or MultiMeshInstance3D)
            {
                counts.ResidentialStairVisuals++;
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CountMapRuntimeNodes(childNode, ref counts, boxMeshResources, insideStair);
            }
        }
    }

    private async void ValidateMapPerformance()
    {
        // Let deferred collision registration settle before taking the deterministic count.
        await WaitFrames(4);
        var counts = new MapRuntimeCounts();
        var boxMeshResources = new HashSet<ulong>();
        CountMapRuntimeNodes(this, ref counts, boxMeshResources);
        var expectedFloors = 0;
        foreach (var spec in ResidentialTowerSpecs)
        {
            expectedFloors += spec.Floors;
        }

        var residentialReady = ResidentialTowerCount == ResidentialTowerSpecs.Length
            && _residentialFloorCount == expectedFloors
            && _residentialStairFlightCount == expectedFloors * 2;
        var stairCollisionReady = counts.ResidentialStairShapes >= expectedFloors * 32;
        var stairBodiesConsolidated = counts.ResidentialStairBodies <= expectedFloors * 2;
        var stairVisualsBatched = counts.MultiMeshInstances >= expectedFloors;
        var expectedDetailRange = MapDetailVisibilityRanges[Mathf.Clamp(_qualitySetting, 0, 2)];
        var detailQualityReady = true;
        var detailNodes = GetTree().GetNodesInGroup(MapDetailVisualGroup);
        using var detailNodesBacking = detailNodes.AsDisposable();
        foreach (var node in detailNodes)
        {
            if (node is not GeometryInstance3D visual)
            {
                detailQualityReady = false;
                break;
            }
            detailQualityReady &= Mathf.IsEqualApprox(visual.VisibilityRangeEnd, expectedDetailRange)
                && visual.CastShadow == (_qualitySetting >= 2
                    ? GeometryInstance3D.ShadowCastingSetting.On
                    : GeometryInstance3D.ShadowCastingSetting.Off);
        }
        var detailCullingReady = counts.MapDetailVisuals >= expectedFloors * 20 && detailQualityReady;
        var lowPolyQualityReady = true;
        var lowPolyNodes = GetTree().GetNodesInGroup(LowPolyBuildingVisualGroup);
        using var lowPolyNodesBacking = lowPolyNodes.AsDisposable();
        foreach (var node in lowPolyNodes)
        {
            if (node is not GeometryInstance3D visual)
            {
                lowPolyQualityReady = false;
                break;
            }
            var maximumRange = (float)visual
                .GetMeta("low_poly_max_visibility_range", 360.0f)
                .AsDouble();
            var expectedRange = Mathf.Min(
                maximumRange,
                LowPolyBuildingVisibilityRanges[Mathf.Clamp(_qualitySetting, 0, 2)]);
            lowPolyQualityReady &= Mathf.IsEqualApprox(visual.VisibilityRangeEnd, expectedRange)
                && visual.CastShadow == (_qualitySetting >= 2
                    ? GeometryInstance3D.ShadowCastingSetting.On
                    : GeometryInstance3D.ShadowCastingSetting.Off);
        }
        var lowPolyCullingReady = counts.LowPolyBuildingVisuals >= 51
            && lowPolyQualityReady;
        const int nodeBudget = 41000;
        var nodeBudgetMet = counts.Nodes < nodeBudget;
        var staticBodyBudgetMet = counts.StaticBodies < 7500;
        var boxMeshBudgetMet = boxMeshResources.Count < 3000;
        var expectedScale = new[] { 0.74f, 0.88f, 1.0f }[Mathf.Clamp(_qualitySetting, 0, 2)];
        var qualityScaleReady = Mathf.IsEqualApprox(GetViewport().Scaling3DScale, expectedScale);
        var expectedRadiance = new[]
        {
            Sky.RadianceSizeEnum.Size64,
            Sky.RadianceSizeEnum.Size128,
            Sky.RadianceSizeEnum.Size256
        }[Mathf.Clamp(_qualitySetting, 0, 2)];
        var skyQualityReady = _environmentRef.Sky is Sky sky
            && sky.ProcessMode == (_qualitySetting >= 2 ? Sky.ProcessModeEnum.Realtime : Sky.ProcessModeEnum.Incremental)
            && sky.RadianceSize == expectedRadiance;
        var valid = residentialReady
            && stairCollisionReady
            && stairBodiesConsolidated
            && stairVisualsBatched
            && detailCullingReady
            && lowPolyCullingReady
            && nodeBudgetMet
            && staticBodyBudgetMet
            && boxMeshBudgetMet
            && qualityScaleReady
            && skyQualityReady
            && counts.CollisionShapes > 0
            && counts.MeshInstances > 0;
        GD.Print($"PERFORMANCE_CHECK valid={valid} nodes={counts.Nodes} node_limit={nodeBudget} node_budget={nodeBudgetMet} static_bodies={counts.StaticBodies} static_budget={staticBodyBudgetMet} collision_shapes={counts.CollisionShapes} mesh_instances={counts.MeshInstances} multimesh_instances={counts.MultiMeshInstances} box_mesh_resources={boxMeshResources.Count} box_mesh_budget={boxMeshBudgetMet} detail_visuals={counts.MapDetailVisuals} detail_culling={detailCullingReady} low_poly_visuals={counts.LowPolyBuildingVisuals} low_poly_culling={lowPolyCullingReady} quality_scale={qualityScaleReady} sky_quality={skyQualityReady} stair_batched={stairVisualsBatched} glass_fields={counts.BreakableGlassFields} glass_panes={counts.BreakableGlassPanes} labels={counts.Labels} lights={counts.Lights} industrial_guards={_industrialInteriorGuards.Count} stair_bodies={counts.ResidentialStairBodies} stair_consolidated={stairBodiesConsolidated} stair_shapes={counts.ResidentialStairShapes} stair_visuals={counts.ResidentialStairVisuals} residential={residentialReady}");
        GD.Print($"PERFORMANCE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
