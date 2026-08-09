using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
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
    }

    private static void CountMapRuntimeNodes(Node node, ref MapRuntimeCounts counts, bool insideResidentialStair = false)
    {
        counts.Nodes++;
        if (node is StaticBody3D)
        {
            counts.StaticBodies++;
        }
        if (node is CollisionShape3D)
        {
            counts.CollisionShapes++;
        }
        if (node is MeshInstance3D)
        {
            counts.MeshInstances++;
        }
        if (node is MultiMeshInstance3D)
        {
            counts.MultiMeshInstances++;
        }
        if (node is Label3D)
        {
            counts.Labels++;
        }
        if (node is Light3D)
        {
            counts.Lights++;
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
            if (node is CollisionShape3D)
            {
                counts.ResidentialStairShapes++;
            }
            if (node is MeshInstance3D or MultiMeshInstance3D)
            {
                counts.ResidentialStairVisuals++;
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                CountMapRuntimeNodes(childNode, ref counts, insideStair);
            }
        }
    }

    private async void ValidateMapPerformance()
    {
        // Let deferred collision registration settle before taking the deterministic count.
        await WaitFrames(4);
        var counts = new MapRuntimeCounts();
        CountMapRuntimeNodes(this, ref counts);
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
        var nodeBudgetMet = counts.Nodes < 45000;
        var staticBodyBudgetMet = counts.StaticBodies < 7500;
        var valid = residentialReady
            && stairCollisionReady
            && stairBodiesConsolidated
            && stairVisualsBatched
            && nodeBudgetMet
            && staticBodyBudgetMet
            && counts.CollisionShapes > 0
            && counts.MeshInstances > 0;
        GD.Print($"PERFORMANCE_CHECK valid={valid} nodes={counts.Nodes} node_budget={nodeBudgetMet} static_bodies={counts.StaticBodies} static_budget={staticBodyBudgetMet} collision_shapes={counts.CollisionShapes} mesh_instances={counts.MeshInstances} multimesh_instances={counts.MultiMeshInstances} stair_batched={stairVisualsBatched} labels={counts.Labels} lights={counts.Lights} stair_bodies={counts.ResidentialStairBodies} stair_consolidated={stairBodiesConsolidated} stair_shapes={counts.ResidentialStairShapes} stair_visuals={counts.ResidentialStairVisuals} residential={residentialReady}");
        GD.Print($"PERFORMANCE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
