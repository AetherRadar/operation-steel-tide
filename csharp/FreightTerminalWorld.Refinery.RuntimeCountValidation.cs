using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static void CountRefineryNodes(
        Node node,
        bool insideAuthoredScene,
        bool insideLegacyVisualScaffold,
        bool insideLegacyCollisionProxy,
        bool insideGameplayCollision,
        ref RefineryRuntimeCounts counts)
    {
        counts.Nodes++;
        var authoredRoot = node.IsInGroup(JianghaiOldCitySceneLoader.AuthoredSceneGroup);
        var authored = insideAuthoredScene || authoredRoot;
        var legacyVisual = insideLegacyVisualScaffold
            || node.IsInGroup("refinery_legacy_visual_scaffold");
        var legacyCollision = insideLegacyCollisionProxy
            || node.IsInGroup("refinery_legacy_collision_proxy");
        var gameplayCollisionRoot = node.IsInGroup(
            JianghaiGameplayCollisionBuilder.CollisionGroup);
        var gameplayCollision = insideGameplayCollision || gameplayCollisionRoot;
        if (authoredRoot)
        {
            counts.AuthoredSceneRoots++;
        }
        if (node is StaticBody3D)
        {
            counts.StaticBodies++;
            if (gameplayCollisionRoot)
            {
                counts.GameplayCollisionBodies++;
            }
            if (legacyCollision)
            {
                counts.LegacyCollisionBodies++;
            }
        }
        if (node is Light3D)
        {
            counts.Lights++;
        }
        if (node is MeshInstance3D mesh)
        {
            counts.MeshInstances++;
            if (authored)
            {
                counts.AuthoredSceneMeshes++;
                if (mesh.IsVisibleInTree())
                {
                    counts.VisibleAuthoredSceneMeshes++;
                }
            }
        }
        if (legacyVisual && node is GeometryInstance3D legacyGeometry
            && legacyGeometry.IsVisibleInTree())
        {
            counts.VisibleLegacyScaffoldGeometry++;
        }
        if (legacyCollision && node is CollisionShape3D collision)
        {
            counts.LegacyCollisionShapes++;
            if (collision.Shape is not BoxShape3D)
            {
                counts.NonBoxLegacyCollisionShapes++;
            }
        }
        if (gameplayCollision && node is CollisionShape3D gameplayCollisionShape)
        {
            counts.GameplayCollisionShapes++;
            if (gameplayCollisionShape.Shape is BoxShape3D)
            {
                counts.GameplayBoxCollisionShapes++;
            }
            else
            {
                counts.GameplayNonBoxCollisionShapes++;
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CountRefineryNodes(
                    childNode,
                    authored,
                    legacyVisual,
                    legacyCollision,
                    gameplayCollision,
                    ref counts);
            }
        }
    }

    private struct RefineryRuntimeCounts
    {
        public int Nodes;
        public int StaticBodies;
        public int MeshInstances;
        public int Lights;
        public int AuthoredSceneRoots;
        public int AuthoredSceneMeshes;
        public int VisibleAuthoredSceneMeshes;
        public int VisibleLegacyScaffoldGeometry;
        public int LegacyCollisionShapes;
        public int LegacyCollisionBodies;
        public int NonBoxLegacyCollisionShapes;
        public int GameplayCollisionBodies;
        public int GameplayCollisionShapes;
        public int GameplayBoxCollisionShapes;
        public int GameplayNonBoxCollisionShapes;
    }
}
