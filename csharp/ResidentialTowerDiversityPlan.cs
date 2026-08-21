using System;
using System.Collections.Generic;

namespace OperationSteelTide;

internal enum ResidentialTowerUse
{
    FamilyCourtyard,
    MedicalResidences,
    EvacuationHousing,
    WorkshopLofts,
    SecurityResidences,
    MarketResidences,
    CommunityHub
}

internal enum ResidentialFloorLayout
{
    FamilySplit,
    OffsetApartment,
    OpenLoft,
    ClinicWard,
    ShelterDormitory,
    SecuritySuite,
    CommunityKitchen,
    WorkshopLoft
}

internal enum ResidentialFacadeStyle
{
    RecessedGrid,
    RibbonGlass,
    VerticalBays,
    StaggeredGrid,
    ServiceBands,
    TerracedWindows
}

internal enum ResidentialRoofStyle
{
    GardenServices,
    ClinicMechanical,
    ShelterCrown,
    WorkshopPlant,
    SecurityRelay,
    MarketCanopy
}

internal enum ResidentialArtTheme
{
    FamilyGarden,
    ClinicServices,
    ShelterUtilities,
    WorkshopPlant,
    SecurityRelay,
    MarketPodium
}

internal sealed record ResidentialTowerDiversityProfile(
    int TowerIndex,
    string Signature,
    ResidentialTowerUse Use,
    ResidentialFacadeStyle Facade,
    ResidentialRoofStyle Roof,
    ResidentialArtTheme ArtTheme,
    ResidentialFloorLayout GroundLayout,
    ResidentialFloorLayout PrimaryLayout,
    ResidentialFloorLayout SecondaryLayout,
    ResidentialFloorLayout AccentLayout);

/// <summary>Deterministic tower identities shared by residential layout, art, and diagnostics.</summary>
internal static class ResidentialTowerDiversityPlan
{
    private static readonly ResidentialTowerDiversityProfile[] Profiles =
    {
        new(0, "harbor-family-grid", ResidentialTowerUse.FamilyCourtyard,
            ResidentialFacadeStyle.RecessedGrid, ResidentialRoofStyle.GardenServices,
            ResidentialArtTheme.FamilyGarden, ResidentialFloorLayout.CommunityKitchen,
            ResidentialFloorLayout.FamilySplit, ResidentialFloorLayout.OffsetApartment,
            ResidentialFloorLayout.CommunityKitchen),
        new(1, "harbor-clinic-ribbon", ResidentialTowerUse.MedicalResidences,
            ResidentialFacadeStyle.RibbonGlass, ResidentialRoofStyle.ClinicMechanical,
            ResidentialArtTheme.ClinicServices, ResidentialFloorLayout.ClinicWard,
            ResidentialFloorLayout.ClinicWard, ResidentialFloorLayout.FamilySplit,
            ResidentialFloorLayout.OpenLoft),
        new(2, "quay-security-bays", ResidentialTowerUse.SecurityResidences,
            ResidentialFacadeStyle.VerticalBays, ResidentialRoofStyle.SecurityRelay,
            ResidentialArtTheme.SecurityRelay, ResidentialFloorLayout.SecuritySuite,
            ResidentialFloorLayout.SecuritySuite, ResidentialFloorLayout.OffsetApartment,
            ResidentialFloorLayout.FamilySplit),
        new(3, "quay-market-wide", ResidentialTowerUse.MarketResidences,
            ResidentialFacadeStyle.TerracedWindows, ResidentialRoofStyle.MarketCanopy,
            ResidentialArtTheme.MarketPodium, ResidentialFloorLayout.CommunityKitchen,
            ResidentialFloorLayout.OpenLoft, ResidentialFloorLayout.CommunityKitchen,
            ResidentialFloorLayout.OffsetApartment),
        new(4, "quay-workshop-service", ResidentialTowerUse.WorkshopLofts,
            ResidentialFacadeStyle.ServiceBands, ResidentialRoofStyle.WorkshopPlant,
            ResidentialArtTheme.WorkshopPlant, ResidentialFloorLayout.WorkshopLoft,
            ResidentialFloorLayout.WorkshopLoft, ResidentialFloorLayout.OpenLoft,
            ResidentialFloorLayout.FamilySplit),
        new(5, "west-shelter-stack", ResidentialTowerUse.EvacuationHousing,
            ResidentialFacadeStyle.VerticalBays, ResidentialRoofStyle.ShelterCrown,
            ResidentialArtTheme.ShelterUtilities, ResidentialFloorLayout.ShelterDormitory,
            ResidentialFloorLayout.ShelterDormitory, ResidentialFloorLayout.FamilySplit,
            ResidentialFloorLayout.ClinicWard),
        new(6, "east-security-staggered", ResidentialTowerUse.SecurityResidences,
            ResidentialFacadeStyle.StaggeredGrid, ResidentialRoofStyle.SecurityRelay,
            ResidentialArtTheme.SecurityRelay, ResidentialFloorLayout.SecuritySuite,
            ResidentialFloorLayout.OpenLoft, ResidentialFloorLayout.SecuritySuite,
            ResidentialFloorLayout.OffsetApartment),
        new(7, "south-family-terraces", ResidentialTowerUse.FamilyCourtyard,
            ResidentialFacadeStyle.TerracedWindows, ResidentialRoofStyle.GardenServices,
            ResidentialArtTheme.FamilyGarden, ResidentialFloorLayout.FamilySplit,
            ResidentialFloorLayout.OffsetApartment, ResidentialFloorLayout.FamilySplit,
            ResidentialFloorLayout.CommunityKitchen),
        new(8, "south-evac-ribbon", ResidentialTowerUse.EvacuationHousing,
            ResidentialFacadeStyle.RibbonGlass, ResidentialRoofStyle.ShelterCrown,
            ResidentialArtTheme.ShelterUtilities, ResidentialFloorLayout.ShelterDormitory,
            ResidentialFloorLayout.ShelterDormitory, ResidentialFloorLayout.OpenLoft,
            ResidentialFloorLayout.ClinicWard),
        new(9, "south-community-grid", ResidentialTowerUse.CommunityHub,
            ResidentialFacadeStyle.RecessedGrid, ResidentialRoofStyle.MarketCanopy,
            ResidentialArtTheme.MarketPodium, ResidentialFloorLayout.CommunityKitchen,
            ResidentialFloorLayout.FamilySplit, ResidentialFloorLayout.CommunityKitchen,
            ResidentialFloorLayout.OpenLoft),
        new(10, "south-workshop-crown", ResidentialTowerUse.WorkshopLofts,
            ResidentialFacadeStyle.ServiceBands, ResidentialRoofStyle.WorkshopPlant,
            ResidentialArtTheme.WorkshopPlant, ResidentialFloorLayout.WorkshopLoft,
            ResidentialFloorLayout.OpenLoft, ResidentialFloorLayout.WorkshopLoft,
            ResidentialFloorLayout.SecuritySuite)
    };

    public static IReadOnlyList<ResidentialTowerDiversityProfile> All => Profiles;

    public static ResidentialTowerDiversityProfile ForTower(int towerIndex)
    {
        if ((uint)towerIndex >= Profiles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(towerIndex));
        }
        return Profiles[towerIndex];
    }

    public static ResidentialFloorLayout LayoutFor(int towerIndex, int floor, int floorCount)
    {
        var profile = ForTower(towerIndex);
        if (floor <= 0)
        {
            return profile.GroundLayout;
        }
        if (floor == floorCount - 1 || floor == floorCount / 2)
        {
            return profile.AccentLayout;
        }
        return ((floor - 1) % 3) switch
        {
            0 => profile.PrimaryLayout,
            1 => profile.SecondaryLayout,
            _ => profile.AccentLayout
        };
    }
}
