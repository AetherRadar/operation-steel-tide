using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed partial class OldTownLandmarksBuilder
{
    private static void AddPawnshopGameplayCollision(
        StaticBody3D collisionBody,
        BuildCounts counts)
    {
        const float wallHeight = 2.2f;
        AddCollision(
            collisionBody,
            "PawnshopNorthWall",
            HotelCenter + new Vector3(0, wallHeight * 0.5f, -12.0f),
            new Vector3(24.5f, wallHeight, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopWestWall",
            HotelCenter + new Vector3(-12.0f, wallHeight * 0.5f, 0),
            new Vector3(0.5f, wallHeight, 24.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopWestFacadePanel",
            new Vector3(-98.0f, wallHeight * 0.5f, -122.5f),
            new Vector3(0.6f, wallHeight, 0.3f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopEastWall",
            HotelCenter + new Vector3(12.0f, wallHeight * 0.5f, 0),
            new Vector3(0.5f, wallHeight, 24.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopEntryLeft",
            HotelCenter + new Vector3(-6.5f, wallHeight * 0.5f, 12.0f),
            new Vector3(11.0f, wallHeight, 1.0f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopEntryRight",
            HotelCenter + new Vector3(6.5f, wallHeight * 0.5f, 12.0f),
            new Vector3(11.0f, wallHeight, 1.0f),
            Vector3.Zero,
            counts);
    }

    private static void AddFactoryGateGameplayCollision(
        StaticBody3D collisionBody,
        BuildCounts counts)
    {
        const float gateZ = -7.924f;
        AddCollision(
            collisionBody,
            "FactoryGateLeft",
            new Vector3(82.4f, 2.0f, gateZ),
            new Vector3(1.0f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateLeftFacade",
            new Vector3(84.1f, 2.0f, gateZ),
            new Vector3(2.2f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateRight",
            new Vector3(89.8f, 2.0f, gateZ),
            new Vector3(1.0f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateRightFacade",
            new Vector3(87.9f, 2.0f, gateZ),
            new Vector3(2.2f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateLintel",
            new Vector3(86.0f, 4.5f, gateZ),
            new Vector3(7.0f, 1.0f, 0.5f),
            Vector3.Zero,
            counts);
    }

    private static IReadOnlyList<Vector3> AddMarketGameplayCollision(
        StaticBody3D collisionBody,
        BuildCounts counts)
    {
        AddCollision(
            collisionBody,
            "MarketDeck",
            new Vector3(0, 4.14f, RooftopZ),
            new Vector3(45.0f, 0.34f, 4.4f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "MarketWestRamp",
            new Vector3(-29.0f, 2.05f, RooftopZ),
            new Vector3(12.5f, 0.38f, 4.0f),
            new Vector3(0, 0, 18.7f),
            counts);
        AddCollision(
            collisionBody,
            "MarketEastRamp",
            new Vector3(29.0f, 2.05f, RooftopZ),
            new Vector3(12.5f, 0.38f, 4.0f),
            new Vector3(0, 0, -18.7f),
            counts);
        foreach (var z in new[] { RooftopZ - 2.15f, RooftopZ + 2.15f })
        {
            foreach (var y in new[] { 4.845f, 5.445f })
            {
                AddCollision(
                    collisionBody,
                    $"MarketRail_{z:0.00}_{y:0.000}",
                    new Vector3(0, y, z),
                    new Vector3(45.0f, 0.18f, 0.3f),
                    Vector3.Zero,
                    counts);
            }
            AddCollision(
                collisionBody,
                $"MarketRailPost_{z:0.00}",
                new Vector3(0, 5.15f, z),
                new Vector3(0.3f, 1.2f, 0.3f),
                Vector3.Zero,
                counts);
        }
        return MarketRooftopRoute();
    }
}
