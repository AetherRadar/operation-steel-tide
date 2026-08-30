using System;
using System.Collections.Generic;

namespace OperationSteelTide;

public sealed record DemolitionMapOffer(
    string Id,
    string Code,
    string LocalizationKey,
    string EnglishName,
    string SubtitleLocalizationKey,
    string EnglishSubtitle,
    bool Available,
    string ProfileLocalizationKey = "demolition_arena_profile",
    string EnglishProfile = "DIAGONAL SITES  //  MID ROTATION  //  TWO FLOORS OF COVER\nA  SOUTHWEST FOUNDRY YARD  //  B  NORTHEAST ASSEMBLY HALL");

/// <summary>
/// Open demolition map pool. Every entry is selectable from the briefing; only maps with
/// a finished arena report <see cref="Available"/>, and locked maps explain why.
/// </summary>
public static class DemolitionMapCatalog
{
    public const string BazaarCrossingId = "bazaar_crossing";
    public const string TideglassReactorId = "tideglass_reactor";
    public const string TideforgeId = "tideforge";
    public const string HarborLocksId = "harbor_locks";
    public const int PoolSize = 12;

    public static readonly IReadOnlyList<DemolitionMapOffer> Maps = new[]
    {
        new DemolitionMapOffer(
            BazaarCrossingId,
            "MAP 01",
            "demolition_map_bazaar_crossing",
            "BAZAAR CROSSING",
            "demolition_map_bazaar_crossing_subtitle",
            "OLD-CITY MARKET  //  GALLERIES AND BRIDGES",
            true,
            "demolition_map_bazaar_crossing_profile",
            "THREE-LANE OLD-CITY ASSAULT  //  BROKEN SIGHTLINES  //  THREE PLAYABLE ELEVATIONS\nA  WEST GALLERY COURT  //  B  EAST BALCONY MARKET"),
        new DemolitionMapOffer(
            TideglassReactorId,
            "MAP 02",
            "demolition_map_tideglass_reactor",
            "TIDEGLASS REACTOR",
            "demolition_map_tideglass_reactor_subtitle",
            "CONSTRUCTION QUARTER  //  OLD BRICK WORKS",
            true,
            "demolition_map_tideglass_reactor_profile",
            "FULL-SCALE THREE-LANE ASSAULT  //  COMPACT MID  //  LONG-RANGE WINGS\nA  CONSTRUCTION COURT  //  B  OLD REACTOR LOADING YARD"),
        new DemolitionMapOffer(
            TideforgeId,
            "MAP 03",
            "demolition_map_tideforge",
            "TIDEFORGE ARENA",
            "demolition_map_tideforge_subtitle",
            "DIAGONAL SITES  //  MID ROTATION",
            true),
        new DemolitionMapOffer(
            HarborLocksId,
            "MAP 04",
            "demolition_map_harbor_locks",
            "HARBOR LOCKS",
            "demolition_map_harbor_locks_subtitle",
            "LOCK GATES  //  PUMP STATIONS",
            true,
            "demolition_map_harbor_locks_profile",
            "THREE LOCK LANES  //  HARD COVER ROTATIONS  //  LONG QUAYSIDE ANGLES\nA  WEST CONTROL YARD  //  B  EAST PUMP ANNEX"),
        new DemolitionMapOffer(
            "drydock_yard",
            "MAP 05",
            "demolition_map_drydock_yard",
            "DRYDOCK YARD",
            "demolition_map_locked_subtitle",
            "SHIP BREAKING BERMS  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "observatory_ridge",
            "MAP 06",
            "demolition_map_observatory_ridge",
            "OBSERVATORY RIDGE",
            "demolition_map_locked_subtitle",
            "HILLSIDE RELAY  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "skybridge_terminal",
            "MAP 07",
            "demolition_map_skybridge_terminal",
            "SKYBRIDGE TERMINAL",
            "demolition_map_locked_subtitle",
            "ELEVATED CONCOURSE  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "tidal_prison",
            "MAP 08",
            "demolition_map_tidal_prison",
            "TIDAL PRISON",
            "demolition_map_locked_subtitle",
            "VERTICAL DETENTION  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "residential_block",
            "MAP 09",
            "demolition_map_residential_block",
            "RESIDENTIAL BLOCK",
            "demolition_map_locked_subtitle",
            "LOW RISE COURTYARDS  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "customs_house",
            "MAP 10",
            "demolition_map_customs_house",
            "CUSTOMS HOUSE",
            "demolition_map_locked_subtitle",
            "BORDER CLEARANCE  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "sparrow_depot",
            "MAP 11",
            "demolition_map_sparrow_depot",
            "SPARROW DEPOT",
            "demolition_map_locked_subtitle",
            "RAIL FREIGHT HUB  //  IN CONSTRUCTION",
            false),
        new DemolitionMapOffer(
            "lighthouse_point",
            "MAP 12",
            "demolition_map_lighthouse_point",
            "LIGHTHOUSE POINT",
            "demolition_map_locked_subtitle",
            "SHORELINE SIGNAL  //  IN CONSTRUCTION",
            false)
    };

    public static DemolitionMapOffer Resolve(string id)
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
