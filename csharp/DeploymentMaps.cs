using System;
using System.Collections.Generic;

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
            "orbital_complex",
            "MAP 03",
            "map_orbital_complex",
            "ORBITAL COMPLEX",
            "map_orbital_complex_subtitle",
            "GLASS SKYBRIDGE DISTRICT  //  LOCKED",
            false)
    };

    public static DeploymentMapOffer Resolve(string id)
    {
        foreach (var map in Maps)
        {
            if (string.Equals(map.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return map;
            }
        }
        return Maps[0];
    }

    public static bool IsAvailable(string id)
    {
        foreach (var map in Maps)
        {
            if (string.Equals(map.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return map.Available;
            }
        }
        return false;
    }
}
