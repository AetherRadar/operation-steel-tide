using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string DemolitionBriefingScenePath = "res://ui/DemolitionBriefingView.tscn";
    private const int HudResourceLifecycleRounds = 16;
    private const int HudResourceLifecycleRoundsBeforeReload = 8;
    private static readonly string[] ProductionHudScenePaths =
    {
        "res://ui/QuickSlotBarView.tscn",
        LootWeaponRackView.ScenePath,
        LootItemActionMenuView.ScenePath,
        LanRoomBrowserView.ScenePath,
        DemolitionTeamStatusView.ScenePath,
        DemolitionTeamMemberCard.ScenePath,
        "res://ui/DemolitionRoundResultView.tscn",
        "res://ui/DemolitionBuyView.tscn",
        "res://ui/OperationsOfficeView.tscn",
        DemolitionBriefingScenePath,
        "res://ui/PauseMenuView.tscn"
    };

    private static HudResourceLifecycleDiagnosticState? _hudResourceLifecycleDiagnosticState;

    private async void ValidateHudResourceLifecycle()
    {
        var reloadedState = _hudResourceLifecycleDiagnosticState;
        if (reloadedState is not null)
        {
            await ContinueHudResourceLifecycleAfterReload(reloadedState);
            return;
        }

        await WaitFrames(3);

        if (!ProductionHudCacheWasBuiltNormally())
        {
            CompleteHudResourceLifecycleDiagnostic(
                null,
                "production_cache_missing");
            return;
        }

        HudResourceLifecycleDiagnosticState state;
        try
        {
            state = new HudResourceLifecycleDiagnosticState(
                CaptureHudResourceCacheBaseline());
            _hudResourceLifecycleDiagnosticState = state;
            await ExerciseHudResourceLifecycleRounds(
                state,
                startRound: 0,
                HudResourceLifecycleRoundsBeforeReload);
        }
        catch (Exception exception)
        {
            CompleteHudResourceLifecycleDiagnostic(
                _hudResourceLifecycleDiagnosticState,
                exception.GetType().Name);
            return;
        }

        state.ReloadRequested = true;
        GetTree().Paused = false;
        var reloadError = GetTree().ReloadCurrentScene();
        if (reloadError != Error.Ok)
        {
            CompleteHudResourceLifecycleDiagnostic(
                state,
                $"reload_{reloadError}");
        }
    }

    private async Task ContinueHudResourceLifecycleAfterReload(
        HudResourceLifecycleDiagnosticState state)
    {
        state.ReloadObserved = true;
        await WaitFrames(3);

        try
        {
            state.PostReloadProductionCacheReady = ProductionHudCacheWasBuiltNormally();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await WaitFrames(2);
            MergeHudResourceCacheProbe(state, ProbeHudResourceCache(state.Baseline));

            await ExerciseHudResourceLifecycleRounds(
                state,
                HudResourceLifecycleRoundsBeforeReload,
                HudResourceLifecycleRounds - HudResourceLifecycleRoundsBeforeReload);
        }
        catch (Exception exception)
        {
            state.Failure = exception.GetType().Name;
        }

        CompleteHudResourceLifecycleDiagnostic(state, state.Failure);
    }

    private async Task ExerciseHudResourceLifecycleRounds(
        HudResourceLifecycleDiagnosticState state,
        int startRound,
        int roundCount)
    {
        for (var offset = 0; offset < roundCount; offset++)
        {
            var result = await ExerciseHudResourceLifecycleRound(startRound + offset);
            state.AllUiReady &= result.UiReady;
            state.AllNestedBrowsersReady &= result.NestedBrowserReady;
            state.AllNodesFreed &= result.NodesFreed;
            state.CompletedRounds++;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await WaitFrames(2);

            MergeHudResourceCacheProbe(state, ProbeHudResourceCache(state.Baseline));
        }
    }

    private void CompleteHudResourceLifecycleDiagnostic(
        HudResourceLifecycleDiagnosticState? state,
        string failure)
    {
        var completedRounds = state?.CompletedRounds ?? 0;
        var allUiReady = state?.AllUiReady ?? false;
        var allNestedBrowsersReady = state?.AllNestedBrowsersReady ?? false;
        var allNodesFreed = state?.AllNodesFreed ?? false;
        var cacheReady = state?.CacheEntriesReady ?? false;
        var cacheIdentityStable = state?.CacheIdentityStable ?? false;
        var cacheCountStable = state?.CacheCountStable ?? false;
        var productionCacheReady = state?.ProductionCacheReady ?? false;
        var postReloadProductionCacheReady = state?.PostReloadProductionCacheReady ?? false;
        var reloadReady = state is { ReloadRequested: true, ReloadObserved: true };
        var resolvedFailure = string.IsNullOrWhiteSpace(failure) ? "none" : failure;
        var valid = resolvedFailure == "none"
            && completedRounds == HudResourceLifecycleRounds
            && allUiReady
            && allNestedBrowsersReady
            && allNodesFreed
            && cacheReady
            && cacheIdentityStable
            && cacheCountStable
            && productionCacheReady
            && postReloadProductionCacheReady
            && reloadReady;

        GD.Print(
            $"HUD_RESOURCE_LIFECYCLE_CHECK valid={valid} rounds={completedRounds}/{HudResourceLifecycleRounds} "
            + $"ui={allUiReady} nested_lan={allNestedBrowsersReady} freed={allNodesFreed} "
            + $"cache={cacheReady} identity={cacheIdentityStable} count_stable={cacheCountStable} "
            + $"production_paths={productionCacheReady} post_reload_paths={postReloadProductionCacheReady} "
            + $"reload={reloadReady} expected_paths={ProductionHudScenePaths.Length} "
            + $"cache_count={HudPackedSceneCache.Count} failure={resolvedFailure}");
        GD.Print($"HUD_RESOURCE_LIFECYCLE_PASS valid={valid}");
        _hudResourceLifecycleDiagnosticState = null;
        GetTree().Paused = false;
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProductionHudCacheWasBuiltNormally()
    {
        if (HudPackedSceneCache.Count != ProductionHudScenePaths.Length)
        {
            return false;
        }

        foreach (var scenePath in ProductionHudScenePaths)
        {
            if (!HudPackedSceneCache.IsCached(scenePath))
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static HudResourceCacheBaseline CaptureHudResourceCacheBaseline()
    {
        var identities = new HudResourceSceneIdentity[ProductionHudScenePaths.Length];
        for (var index = 0; index < ProductionHudScenePaths.Length; index++)
        {
            var scenePath = ProductionHudScenePaths[index];
            var scene = HudPackedSceneCache.Load(scenePath);
            identities[index] = new HudResourceSceneIdentity(
                scenePath,
                new WeakReference<PackedScene>(scene),
                scene.GetInstanceId());
        }

        return new HudResourceCacheBaseline(identities, HudPackedSceneCache.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static HudResourceCacheProbe ProbeHudResourceCache(
        HudResourceCacheBaseline baseline)
    {
        var entriesReady = true;
        var identityStable = true;
        foreach (var identity in baseline.Identities)
        {
            var scene = HudPackedSceneCache.Load(identity.ScenePath);
            var weakReady = identity.Scene.TryGetTarget(out var originalScene);
            entriesReady &= weakReady
                && GodotObject.IsInstanceValid(scene)
                && scene.ResourcePath == identity.ScenePath
                && HudPackedSceneCache.IsCached(identity.ScenePath);
            identityStable &= weakReady
                && ReferenceEquals(originalScene, scene)
                && scene.GetInstanceId() == identity.InstanceId;
        }

        return new HudResourceCacheProbe(
            entriesReady,
            identityStable,
            HudPackedSceneCache.Count);
    }

    private static void MergeHudResourceCacheProbe(
        HudResourceLifecycleDiagnosticState state,
        HudResourceCacheProbe probe)
    {
        state.CacheEntriesReady &= probe.EntriesReady;
        state.CacheIdentityStable &= probe.IdentityStable;
        state.CacheCountStable &= probe.CacheCount == state.Baseline.CacheCount;
    }

    private async Task<HudResourceLifecycleRound> ExerciseHudResourceLifecycleRound(int round)
    {
        var browser = HudPackedSceneCache.Instantiate<LanRoomBrowserView>(
            LanRoomBrowserView.ScenePath);
        var briefing = HudPackedSceneCache.Instantiate<DemolitionBriefingView>(
            DemolitionBriefingScenePath);
        browser.Name = $"HudResourceBrowser{round:D2}";
        briefing.Name = $"HudResourceBriefing{round:D2}";
        browser.Visible = false;
        briefing.Visible = false;
        _hud.AddChild(browser);
        _hud.AddChild(briefing);
        await WaitFrames(2);

        var room = new LanRoomInfo(
            $"hud-resource-{round:D2}",
            "HUD RESOURCE DIAGNOSTIC",
            "192.0.2.25",
            30118,
            LanRoomKind.Demolition,
            DemolitionMapCatalog.BazaarCrossingId,
            1,
            SquadNetwork.DemolitionCapacity);

        LanRoomInfo? selectedRoom = null;
        browser.RoomSelected += selected => selectedRoom = selected;
        browser.SetContext(LanRoomKind.Demolition);
        browser.SetDiscoveryAvailable(true);
        browser.SetRooms(new[] { room });
        browser.SelectRoomForDiagnostics(0);

        var nestedBrowser = briefing.GetNodeOrNull<LanRoomBrowserView>("Band/LanRoomBrowser");
        briefing.SetLanRoomBrowseAvailable(true);
        briefing.SetLanRooms(new[] { room });
        briefing.SelectLanRoomForDiagnostics(0);

        var browserReady = browser.SceneFilePath == LanRoomBrowserView.ScenePath
            && browser.UiReady
            && browser.IntentSignalsConnected
            && browser.VisibleRoomCount == 1
            && selectedRoom == room;
        var briefingReady = briefing.SceneFilePath == DemolitionBriefingScenePath
            && briefing.UiReady
            && briefing.LanRoomBrowserUiReady;
        var nestedBrowserReady = nestedBrowser is not null
            && nestedBrowser.SceneFilePath == LanRoomBrowserView.ScenePath
            && nestedBrowser.UiReady
            && nestedBrowser.IntentSignalsConnected
            && briefing.VisibleLanRoomCount == 1
            && briefing.SelectedSessionMode == SquadSessionMode.Join
            && briefing.NetworkAddress == room.Endpoint;

        browser.QueueFree();
        briefing.QueueFree();
        await WaitFrames(3);
        var nodesFreed = !GodotObject.IsInstanceValid(browser)
            && !GodotObject.IsInstanceValid(briefing);

        return new HudResourceLifecycleRound(
            browserReady && briefingReady,
            nestedBrowserReady,
            nodesFreed);
    }

    private readonly record struct HudResourceLifecycleRound(
        bool UiReady,
        bool NestedBrowserReady,
        bool NodesFreed);

    private readonly record struct HudResourceSceneIdentity(
        string ScenePath,
        WeakReference<PackedScene> Scene,
        ulong InstanceId);

    private readonly record struct HudResourceCacheBaseline(
        HudResourceSceneIdentity[] Identities,
        int CacheCount);

    private readonly record struct HudResourceCacheProbe(
        bool EntriesReady,
        bool IdentityStable,
        int CacheCount);

    private sealed class HudResourceLifecycleDiagnosticState
    {
        public HudResourceLifecycleDiagnosticState(HudResourceCacheBaseline baseline)
        {
            Baseline = baseline;
        }

        public HudResourceCacheBaseline Baseline { get; }
        public int CompletedRounds { get; set; }
        public bool AllUiReady { get; set; } = true;
        public bool AllNestedBrowsersReady { get; set; } = true;
        public bool AllNodesFreed { get; set; } = true;
        public bool CacheEntriesReady { get; set; } = true;
        public bool CacheIdentityStable { get; set; } = true;
        public bool CacheCountStable { get; set; } = true;
        public bool ProductionCacheReady { get; } = true;
        public bool PostReloadProductionCacheReady { get; set; }
        public bool ReloadRequested { get; set; }
        public bool ReloadObserved { get; set; }
        public string Failure { get; set; } = "none";
    }
}
