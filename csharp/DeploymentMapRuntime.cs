using System;

namespace OperationSteelTide;

public sealed record PendingExtractionDeployment(
    string MapId,
    OperatorRole Role,
    SquadSessionMode SessionMode,
    string Address,
    DeploymentLoadoutSelection Loadout,
    long WorldSeed = 0,
    int SquadSlot = 0);

/// <summary>
/// Carries the selected extraction map across a scene reload without persisting it to disk.
/// Only one extraction world is built at a time.
/// </summary>
public static class DeploymentMapRuntime
{
    private static string _selectedMapId = DeploymentMapCatalog.FreightTerminalId;
    private static PendingExtractionDeployment? _pendingDeployment;
    private static long _selectedWorldSeed;

    public static long CurrentWorldSeed => _selectedWorldSeed;

    public static string ResolveStartupMap(string[] args)
    {
        foreach (var argument in args)
        {
            const string prefix = "--map=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                SelectMap(argument[prefix.Length..]);
                break;
            }
        }

        if (Array.Exists(args, value =>
                value is "--validate-refinery-map"
                    or "--validate-refinery-collision"
                    or "--validate-refinery-doors"
                    or "--validate-jianghai-interiors"
                    or "--validate-refinery-atmosphere"
                    or "--capture-refinery-map"
                    or "--capture-promotion"
                    or "--capture-readme-zh"))
        {
            SelectMap(DeploymentMapCatalog.BlackwaterRefineryId);
        }

        if (Array.Exists(args, value =>
                value is "--validate-orbital-map"
                    or "--validate-orbital-collision"
                    or "--validate-orbital-gameplay"
                    or "--validate-orbital-spawns"
                    or "--validate-orbital-loading"
                    or "--validate-orbital-atmosphere"
                    or "--validate-orbital-water"
                    or "--validate-orbital-integration"
                    or "--capture-orbital-map"
                    or "--capture-orbital-coast"))
        {
            SelectMap(DeploymentMapCatalog.OrbitalComplexId);
        }

        return _selectedMapId;
    }

    public static void StageDeployment(PendingExtractionDeployment deployment)
    {
        SelectMap(deployment.MapId);
        _selectedWorldSeed = deployment.WorldSeed;
        _pendingDeployment = deployment with { MapId = _selectedMapId };
    }

    public static bool TryConsumePending(
        string activeMapId,
        out PendingExtractionDeployment deployment)
    {
        if (_pendingDeployment is not null
            && string.Equals(_pendingDeployment.MapId, activeMapId, StringComparison.OrdinalIgnoreCase))
        {
            deployment = _pendingDeployment;
            _pendingDeployment = null;
            return true;
        }

        deployment = null!;
        return false;
    }

    public static void ClearTransientDeployment()
    {
        _pendingDeployment = null;
        _selectedWorldSeed = 0;
    }

    internal static void SelectMapForDiagnostics(string mapId) => SelectMap(mapId);

    private static void SelectMap(string mapId)
    {
        _selectedMapId = DeploymentMapCatalog.TryResolve(mapId, out var map) && map.Available
            ? map.Id
            : DeploymentMapCatalog.FreightTerminalId;
    }
}
