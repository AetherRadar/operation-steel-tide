using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OperationSteelTide;

public sealed record DeploymentMapOffer(
    string Id,
    string Code,
    string LocalizationKey,
    string EnglishName,
    string SubtitleLocalizationKey,
    string EnglishSubtitle,
    bool Available);

public static class DeploymentMapCatalog
{
    public const string FreightTerminalId = "freight_terminal";
    public const string BlackwaterRefineryId = "blackwater_refinery";
    public const string OrbitalComplexId = "orbital_complex";
    public const string ResidentialSurvivalId = "residential_survival";
    public const string LanternCanalId = "lantern_canal";

    public static readonly IReadOnlyList<DeploymentMapOffer> Maps = new[]
    {
        new DeploymentMapOffer(
            FreightTerminalId,
            "MAP 01",
            "map_freight_terminal",
            "FREIGHT TERMINAL",
            "map_freight_terminal_subtitle",
            "HARBOR EXCLUSION ZONE",
            true),
        new DeploymentMapOffer(
            BlackwaterRefineryId,
            "MAP 02",
            "map_blackwater_refinery",
            "JIANGHAI OLD CITY",
            "map_blackwater_refinery_subtitle",
            "GUANGCHANG PAWNSHOP  //  RED STAR ELECTRONICS",
            true),
        new DeploymentMapOffer(
            OrbitalComplexId,
            "MAP 03",
            "map_orbital_complex",
            "FALLTIDE RECOVERY ARRAY",
            "map_orbital_complex_subtitle",
            "STORM-BARRIER QUARANTINE COMPLEX",
            true),
        new DeploymentMapOffer(
            ResidentialSurvivalId,
            "MAP 04",
            "map_residential_survival",
            "LASTLIGHT COMMUNITY",
            "map_residential_survival_subtitle",
            "RESIDENTIAL SURVIVAL ZONE  //  NORTHSTAR PLAZA",
            true),
        new DeploymentMapOffer(
            LanternCanalId,
            "MAP 05",
            "map_lantern_canal",
            "LANTERN CANAL DISTRICT",
            "map_lantern_canal_subtitle",
            "JIANGHAI WORKSHOPS  //  CERAMICS AND SILK",
            true)
    };

    public static bool TryResolve(
        string? id,
        [NotNullWhen(true)] out DeploymentMapOffer? map)
    {
        foreach (var candidate in Maps)
        {
            if (string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                map = candidate;
                return true;
            }
        }

        map = null;
        return false;
    }

    public static DeploymentMapOffer Resolve(string id)
        => TryResolve(id, out var map) ? map : Maps[0];

    public static bool IsAvailable(string id)
        => TryResolve(id, out var map) && map.Available;
}
