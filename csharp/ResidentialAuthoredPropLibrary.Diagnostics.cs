using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal static partial class ResidentialAuthoredPropLibrary
{
    internal static SceneLoadFailureCacheInspection InspectFailureCacheForDiagnostics()
    {
        const string missingPath = "res://__diagnostics__/missing_residential_authored_prop.glb";
        const string invalidRootPath = "res://__diagnostics__/invalid_residential_authored_prop.glb";
        var loadAttempts = 0;
        var loadReports = 0;
        PackedScene? LoadMissing(string _)
        {
            loadAttempts++;
            return null;
        }

        void RecordLoadFailure(string _, Exception? __) => loadReports++;

        ReleaseSharedResources();
        var firstLoaded = TryLoad(missingPath, LoadMissing, RecordLoadFailure, out _);
        var secondLoaded = TryLoad(missingPath, LoadMissing, RecordLoadFailure, out _);
        var repeatedFailureSuppressed = !firstLoaded
            && !secondLoaded
            && loadAttempts == 1
            && loadReports == 1
            && FailedScenePaths.Contains(missingPath);

        ReleaseSharedResources();
        var cacheCleared = Scenes.Count == 0 && FailedScenePaths.Count == 0;
        var loadedAfterRelease = TryLoad(missingPath, LoadMissing, RecordLoadFailure, out _);
        var retriedAfterRelease = !loadedAfterRelease
            && loadAttempts == 2
            && loadReports == 2
            && FailedScenePaths.Contains(missingPath);
        ReleaseSharedResources();

        var buildLoadAttempts = 0;
        var buildAttempts = 0;
        var buildReports = 0;
        var loadedScenes = new List<PackedScene>();
        var temporaryRoots = new List<Node>();
        PackedScene? LoadInvalidRootScene(string _)
        {
            buildLoadAttempts++;
            var scene = new PackedScene();
            loadedScenes.Add(scene);
            return scene;
        }

        Node? InstantiateInvalidRoot(PackedScene _)
        {
            buildAttempts++;
            var root = new Node { Name = "DiagnosticInvalidAuthoredRoot" };
            temporaryRoots.Add(root);
            return root;
        }

        void RecordBuildFailure(string _, Exception? __) => buildReports++;

        var firstBuilt = TryCreateVisual(
            invalidRootPath,
            Vector3.One,
            LoadInvalidRootScene,
            InstantiateInvalidRoot,
            RecordBuildFailure,
            out var firstVisual,
            out var firstMeshCount);
        var secondBuilt = TryCreateVisual(
            invalidRootPath,
            Vector3.One,
            LoadInvalidRootScene,
            InstantiateInvalidRoot,
            RecordBuildFailure,
            out var secondVisual,
            out var secondMeshCount);
        var buildFailureSuppressed = !firstBuilt
            && !secondBuilt
            && buildLoadAttempts == 1
            && buildAttempts == 1
            && buildReports == 1;
        var invalidSceneEvicted = !Scenes.ContainsKey(invalidRootPath)
            && FailedScenePaths.Contains(invalidRootPath);
        var temporaryRootsFreed = temporaryRoots.Count == 1
            && temporaryRoots.TrueForAll(root => !GodotObject.IsInstanceValid(root));
        var noFailedVisualReturned = firstVisual is null
            && secondVisual is null
            && firstMeshCount == 0
            && secondMeshCount == 0;

        ReleaseSharedResources();
        var buildCacheCleared = Scenes.Count == 0 && FailedScenePaths.Count == 0;
        var builtAfterRelease = TryCreateVisual(
            invalidRootPath,
            Vector3.One,
            LoadInvalidRootScene,
            InstantiateInvalidRoot,
            RecordBuildFailure,
            out var retryVisual,
            out var retryMeshCount);
        var buildRetriedAfterRelease = !builtAfterRelease
            && buildLoadAttempts == 2
            && buildAttempts == 2
            && buildReports == 2
            && FailedScenePaths.Contains(invalidRootPath)
            && !Scenes.ContainsKey(invalidRootPath);
        temporaryRootsFreed &= temporaryRoots.Count == 2
            && temporaryRoots.TrueForAll(root => !GodotObject.IsInstanceValid(root));
        noFailedVisualReturned &= retryVisual is null && retryMeshCount == 0;
        ReleaseSharedResources();
        foreach (var loadedScene in loadedScenes)
        {
            loadedScene.Dispose();
        }

        return new SceneLoadFailureCacheInspection(
            loadAttempts,
            loadReports,
            repeatedFailureSuppressed,
            cacheCleared,
            retriedAfterRelease,
            buildLoadAttempts,
            buildAttempts,
            buildReports,
            buildFailureSuppressed,
            invalidSceneEvicted,
            temporaryRootsFreed,
            noFailedVisualReturned,
            buildCacheCleared,
            buildRetriedAfterRelease);
    }

    internal readonly record struct SceneLoadFailureCacheInspection(
        int LoadAttempts,
        int FailureReports,
        bool RepeatedFailureSuppressed,
        bool CacheCleared,
        bool RetriedAfterRelease,
        int BuildLoadAttempts,
        int BuildAttempts,
        int BuildFailureReports,
        bool BuildFailureSuppressed,
        bool InvalidSceneEvicted,
        bool TemporaryRootsFreed,
        bool NoFailedVisualReturned,
        bool BuildCacheCleared,
        bool BuildRetriedAfterRelease)
    {
        public bool Valid => LoadAttempts == 2
            && FailureReports == 2
            && RepeatedFailureSuppressed
            && CacheCleared
            && RetriedAfterRelease
            && BuildLoadAttempts == 2
            && BuildAttempts == 2
            && BuildFailureReports == 2
            && BuildFailureSuppressed
            && InvalidSceneEvicted
            && TemporaryRootsFreed
            && NoFailedVisualReturned
            && BuildCacheCleared
            && BuildRetriedAfterRelease;
    }
}
