using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly string[] MetalSurfaceKeywords =
    [
        "metal", "steel", "iron", "container", "vehicle", "truck", "aircraft", "barrel",
        "pipe", "fence", "rail", "machine", "generator", "terminal", "roller", "shutter"
    ];

    private static readonly string[] WoodSurfaceKeywords =
    [
        "wood", "timber", "pallet", "crate", "cabinet", "furniture", "table", "chair"
    ];

    private MeleeImpactSurface ResolveMeleeImpactSurface(GodotObject? collider, int shape)
    {
        var shapeOwner = ResolveMeleeShapeOwner(collider, shape);
        if (TryResolveSurfaceMetadata(shapeOwner, out var metadataSurface)
            || TryResolveSurfaceMetadata(collider as Node, out metadataSurface))
        {
            return metadataSurface;
        }

        for (var current = collider as Node; current is not null; current = current.GetParent())
        {
            if (current is ExplosiveBarrel
                or DriveableVehicle
                or DestructibleAircraft
                or AircraftShell)
            {
                return MeleeImpactSurface.Metal;
            }
        }

        var identity = SurfaceIdentity(shapeOwner) + " " + SurfaceIdentity(collider as Node);
        if (ContainsSurfaceKeyword(identity, MetalSurfaceKeywords))
        {
            return MeleeImpactSurface.Metal;
        }
        if (ContainsSurfaceKeyword(identity, WoodSurfaceKeywords))
        {
            return MeleeImpactSurface.Wood;
        }
        return MeleeImpactSurface.Masonry;
    }

    private static Node? ResolveMeleeShapeOwner(GodotObject? collider, int shape)
    {
        if (collider is not CollisionObject3D collisionObject || shape < 0)
        {
            return null;
        }
        var ownerId = collisionObject.ShapeFindOwner(shape);
        return ownerId == uint.MaxValue
            ? null
            : collisionObject.ShapeOwnerGetOwner(ownerId) as Node;
    }

    private static bool TryResolveSurfaceMetadata(
        Node? node,
        out MeleeImpactSurface surface)
    {
        for (var current = node; current is not null; current = current.GetParent())
        {
            if (!current.HasMeta(MeleeSurfaceMetadataKey))
            {
                continue;
            }
            var value = current.GetMeta(MeleeSurfaceMetadataKey, string.Empty).AsString();
            if (TryParseMeleeSurface(value, out surface))
            {
                return true;
            }
        }
        surface = MeleeImpactSurface.Masonry;
        return false;
    }

    private static bool TryParseMeleeSurface(
        string value,
        out MeleeImpactSurface surface)
    {
        surface = value.Trim().ToLowerInvariant() switch
        {
            "metal" or "steel" or "iron" or "aluminum" or "aluminium"
                => MeleeImpactSurface.Metal,
            "wood" or "timber" or "plywood"
                => MeleeImpactSurface.Wood,
            "masonry" or "concrete" or "stone" or "brick" or "plaster"
                => MeleeImpactSurface.Masonry,
            _ => (MeleeImpactSurface)(-1)
        };
        var recognized = (int)surface >= 0;
        surface = recognized ? surface : MeleeImpactSurface.Masonry;
        return recognized;
    }

    private static string SurfaceIdentity(Node? node)
    {
        var identity = string.Empty;
        var depth = 0;
        for (var current = node;
             current is not null && current is not FreightTerminalWorld && depth < 6;
             current = current.GetParent(), depth++)
        {
            identity += " " + current.Name.ToString() + " " + current.GetType().Name;
        }
        return identity.ToLowerInvariant();
    }

    private static bool ContainsSurfaceKeyword(string identity, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (identity.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
