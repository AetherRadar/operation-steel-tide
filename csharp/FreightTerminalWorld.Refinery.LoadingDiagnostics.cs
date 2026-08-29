using System.Diagnostics;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal static class JianghaiLoadingDiagnosticRuntime
{
    public static bool ReloadPending { get; private set; }
    public static long ReloadStartedAtMilliseconds { get; private set; }
    public static long PreloadWaitMilliseconds { get; private set; }
    public static JianghaiMapPreloadSnapshot? PreloadSnapshot { get; private set; }

    public static void BeginReload(
        long preloadWaitMilliseconds,
        JianghaiMapPreloadSnapshot preloadSnapshot)
    {
        PreloadWaitMilliseconds = preloadWaitMilliseconds;
        PreloadSnapshot = preloadSnapshot;
        ReloadStartedAtMilliseconds = (long)Time.GetTicksMsec();
        ReloadPending = true;
    }

    public static void Reset()
    {
        ReloadPending = false;
        ReloadStartedAtMilliseconds = 0;
        PreloadWaitMilliseconds = 0;
        PreloadSnapshot = null;
    }
}

public partial class FreightTerminalWorld
{
    private async void ValidateRefineryLoading()
    {
        if (!JianghaiLoadingDiagnosticRuntime.ReloadPending)
        {
            await BeginRefineryLoadingDiagnosticReload();
            return;
        }

        ValidateReloadedRefineryLoading();
    }

    private async System.Threading.Tasks.Task BeginRefineryLoadingDiagnosticReload()
    {
        var initial = JianghaiMapPreloadCache.Snapshot;
        var requestAccepted = JianghaiMapPreloadCache.Request();
        var requested = JianghaiMapPreloadCache.Snapshot;
        var waitClock = Stopwatch.StartNew();
        var ready = await JianghaiMapPreloadCache.EnsureReadyAsync(GetTree());
        waitClock.Stop();
        if (!IsInsideTree())
        {
            return;
        }
        var preloaded = JianghaiMapPreloadCache.Snapshot;
        var requestDelta = initial.State == JianghaiMapPreloadState.Ready ? 0 : 1;
        var preloadReady = requestAccepted
            && ready
            && preloaded.State == JianghaiMapPreloadState.Ready
            && preloaded.Progress >= 0.99f
            && preloaded.RequestCount == initial.RequestCount + requestDelta
            && preloaded.ElapsedMilliseconds <= 60_000
            && waitClock.ElapsedMilliseconds <= 60_500;
        if (!preloadReady)
        {
            GD.Print(
                $"REFINERY_LOADING_CHECK valid=False phase=preload initial={initial.State} "
                + $"requested={requested.State} ready={preloaded.State} "
                + $"requests={preloaded.RequestCount} polls={preloaded.PollCount} "
                + $"progress={preloaded.Progress:0.000} "
                + $"threaded_ms={preloaded.ElapsedMilliseconds} "
                + $"wait_ms={waitClock.ElapsedMilliseconds} error={preloaded.Error}");
            GD.Print("REFINERY_LOADING_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        JianghaiLoadingDiagnosticRuntime.BeginReload(waitClock.ElapsedMilliseconds, preloaded);
        DeploymentMapRuntime.SelectMapForDiagnostics(
            DeploymentMapCatalog.BlackwaterRefineryId);
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void ValidateReloadedRefineryLoading()
    {
        var scene = _jianghaiOldCityScene;
        var metrics = scene?.LoadMetrics;
        var collision = _jianghaiGameplayCollision;
        var expectedPlacements = RefineryLayout.Models.Count(placement => placement.HasCollision);
        var reloadToWorldReadyMilliseconds = (long)Time.GetTicksMsec()
            - JianghaiLoadingDiagnosticRuntime.ReloadStartedAtMilliseconds;
        var preload = JianghaiLoadingDiagnosticRuntime.PreloadSnapshot;
        var stageBudgetsReady = metrics is not null
            && metrics.PreloadReadyBeforeAcquire
            && metrics.UsedThreadedPreload
            && !metrics.DetailedInspectionEnabled
            && metrics.PackedSceneAcquireMilliseconds <= 100
            && metrics.InstantiationMilliseconds <= 5_000
            && metrics.RuntimeConfigurationMilliseconds <= 1_500
            && metrics.DetailedInspectionMilliseconds == 0
            && metrics.TotalMilliseconds <= 7_000;
        var collisionReady = collision is not null
            && _jianghaiGameplayCollisionError is null
            && collision.SourcePlacementCount == expectedPlacements
            && collision.AuthoredSourceMeshCount
                == JianghaiGameplayCollisionBuilder.ExpectedAuthoredProxyCount
            && collision.CollisionShapeCount
                == expectedPlacements + JianghaiGameplayCollisionBuilder.ExpectedAuthoredProxyCount
            && collision.BoxShapeCount == collision.CollisionShapeCount
            && collision.ConcaveShapeCount == 0;
        var worldReady = IsBlackwaterRefineryMap
            && scene is not null
            && IsInstanceValid(scene.Root)
            && scene.Root.GetParent() == _levelRoot
            && IsInstanceValid(_player)
            && IsInstanceValid(_hud)
            && _oldTownLandmarks is not null
            && _objectiveTerminals.Count == 2
            && _lootSources.Count >= 32
            && _enemies.Count >= RefineryLayout.GarrisonSpawns.Count
            && reloadToWorldReadyMilliseconds <= 20_000;
        var valid = preload is not null
            && preload.State == JianghaiMapPreloadState.Ready
            && stageBudgetsReady
            && collisionReady
            && worldReady;

        GD.Print(
            $"REFINERY_LOADING_CHECK valid={valid} phase=reload_to_world_ready "
            + $"preload_wait_ms={JianghaiLoadingDiagnosticRuntime.PreloadWaitMilliseconds} "
            + $"threaded_ms={preload?.ElapsedMilliseconds ?? -1} "
            + $"cache_ready_before={metrics?.PreloadReadyBeforeAcquire ?? false} "
            + $"acquire_ms={metrics?.PackedSceneAcquireMilliseconds ?? -1} "
            + $"instantiate_ms={metrics?.InstantiationMilliseconds ?? -1} "
            + $"runtime_config_ms={metrics?.RuntimeConfigurationMilliseconds ?? -1} "
            + $"inspection={metrics?.DetailedInspectionEnabled ?? true}:"
            + $"{metrics?.DetailedInspectionMilliseconds ?? -1} "
            + $"scene_total_ms={metrics?.TotalMilliseconds ?? -1} "
            + $"collision={collisionReady}:shapes={collision?.CollisionShapeCount ?? 0}/"
            + $"{expectedPlacements}+{JianghaiGameplayCollisionBuilder.ExpectedAuthoredProxyCount}:"
            + $"boxes={collision?.BoxShapeCount ?? 0}:"
            + $"authored={collision?.AuthoredSourceMeshCount ?? 0} "
            + $"world_ready={worldReady}:ms={reloadToWorldReadyMilliseconds} "
            + $"loot={_lootSources.Count} enemies={_enemies.Count} "
            + $"error={(_jianghaiGameplayCollisionError is null ? "none" : "collision_fallback")}");
        GD.Print($"REFINERY_LOADING_PASS valid={valid}");
        JianghaiLoadingDiagnosticRuntime.Reset();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
