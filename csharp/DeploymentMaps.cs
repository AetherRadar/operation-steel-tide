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
            "tidal_prison",
            "MAP 02",
            "map_tidal_prison",
            "TIDAL PRISON",
            "map_tidal_prison_subtitle",
            "VERTICAL DETENTION COMPLEX  //  LOCKED",
            false),
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
