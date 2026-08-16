using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DistrictRouteDeckHeight = 5.2f;
    private const float DistrictRouteDeckWidth = 3.2f;
    private const float DistrictRouteDeckThickness = 0.22f;
    private const float DistrictRouteHubSize = 5.2f;
    private const float DistrictRouteStairRun = 14.4f;
    private const float DistrictRouteStairRise = 5.05f;
    private const int DistrictRouteStairSteps = 32;

    private readonly record struct DistrictRouteHub(
        string Id,
        Vector3 DeckCenter,
        Vector3 StairStart,
        Vector3 StairDirection,
        bool ResidentialGateway = false);

    private readonly record struct DistrictRouteLink(
        string From,
        string To,
        Vector3[] Points);

    private Node3D? _districtRouteRoot;
    private readonly List<DistrictRouteHub> _districtRouteHubs = new();
    private readonly List<DistrictRouteLink> _districtRouteLinks = new();
    private readonly List<Vector3> _districtRouteDeckSamples = new();
    private readonly List<Vector3> _districtRouteSupportPositions = new();
    private int _districtRouteDeckCount;
    private int _districtRouteStairCount;
    private int _districtRouteSupportCount;
    private int _districtRouteGatewayCount;

    public int DistrictRouteHubCount => _districtRouteHubs.Count;
    public int DistrictRouteLinkCount => _districtRouteLinks.Count;
    public int DistrictRouteVerticalAccessCount => _districtRouteStairCount;

    private void BuildInterdistrictRouteNetwork(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material yellow)
    {
        _districtRouteHubs.Clear();
        _districtRouteLinks.Clear();
        _districtRouteDeckSamples.Clear();
        _districtRouteSupportPositions.Clear();
        _districtRouteDeckCount = 0;
        _districtRouteStairCount = 0;
        _districtRouteSupportCount = 0;
        _districtRouteGatewayCount = 0;

        _districtRouteRoot = new Node3D { Name = "InterdistrictRouteNetwork" };
        _districtRouteRoot.AddToGroup("district_route_network");
        _levelRoot.AddChild(_districtRouteRoot);

        var deck = Mat("district_route_deck", new Color(0.25f, 0.3f, 0.3f), 0.58f, 0.48f);
        var guard = Mat("district_route_guard", new Color(0.09f, 0.12f, 0.13f), 0.72f, 0.32f);
        var support = Mat("district_route_support", new Color(0.18f, 0.22f, 0.22f), 0.68f, 0.4f);
        var safety = Mat(
            "district_route_safety",
            new Color(0.94f, 0.54f, 0.12f),
            0.18f,
            0.42f,
            new Color(0.42f, 0.12f, 0.015f));

        AddDistrictRouteHub(CreateDistrictRouteHub("BazaarGate", new Vector2(-59.0f, 4.0f), Vector3.Forward), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("CustomsGate", new Vector2(-55.0f, -14.0f), Vector3.Left), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("RailGate", new Vector2(-91.0f, -52.0f), Vector3.Right), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("OpsGate", new Vector2(48.0f, -34.0f), Vector3.Forward), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("TideglassGate", new Vector2(96.0f, 23.0f), Vector3.Left), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("MaintenanceGate", new Vector2(25.0f, -66.0f), Vector3.Forward), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("FuelGate", new Vector2(39.0f, -112.0f), Vector3.Right), deck, guard, support, safety);
        AddDistrictRouteHub(CreateDistrictRouteHub("DrydockGate", new Vector2(77.0f, -134.0f), Vector3.Forward), deck, guard, support, safety);

        if (_residentialEntrances.Count >= 2)
        {
            AddDistrictRouteHub(CreateResidentialDistrictGateway("WestResidentialGateway", 0), deck, guard, support, safety);
            AddDistrictRouteHub(CreateResidentialDistrictGateway("EastResidentialGateway", 1), deck, guard, support, safety);
        }

        var hubs = _districtRouteHubs.ToDictionary(hub => hub.Id, StringComparer.Ordinal);
        AddDistrictRouteLink(hubs, "BazaarGate", "CustomsGate", deck, guard, support, safety);
        AddDistrictRouteLink(hubs, "CustomsGate", "RailGate", deck, guard, support, safety,
            DistrictRoutePoint(-75.0f, -14.0f), DistrictRoutePoint(-75.0f, -42.0f));
        AddDistrictRouteLink(hubs, "CustomsGate", "OpsGate", deck, guard, support, safety,
            DistrictRoutePoint(-38.0f, -14.0f), DistrictRoutePoint(-38.0f, -33.5f), DistrictRoutePoint(20.0f, -33.5f));
        AddDistrictRouteLink(hubs, "RailGate", "MaintenanceGate", deck, guard, support, safety,
            DistrictRoutePoint(-72.0f, -82.0f), DistrictRoutePoint(-24.0f, -90.0f),
            DistrictRoutePoint(6.0f, -110.0f), DistrictRoutePoint(44.0f, -110.0f),
            DistrictRoutePoint(44.0f, -70.0f));
        AddDistrictRouteLink(hubs, "OpsGate", "MaintenanceGate", deck, guard, support, safety,
            DistrictRoutePoint(62.0f, -34.0f), DistrictRoutePoint(62.0f, -62.0f),
            DistrictRoutePoint(44.0f, -66.0f));
        AddDistrictRouteLink(hubs, "OpsGate", "TideglassGate", deck, guard, support, safety,
            DistrictRoutePoint(82.0f, -36.0f), DistrictRoutePoint(90.0f, -10.0f));
        AddDistrictRouteLink(hubs, "MaintenanceGate", "FuelGate", deck, guard, support, safety,
            DistrictRoutePoint(44.0f, -70.0f), DistrictRoutePoint(42.0f, -101.0f));
        AddDistrictRouteLink(hubs, "FuelGate", "DrydockGate", deck, guard, support, safety,
            DistrictRoutePoint(38.0f, -130.0f));

        if (hubs.ContainsKey("WestResidentialGateway") && hubs.ContainsKey("EastResidentialGateway"))
        {
            AddDistrictRouteLink(hubs, "WestResidentialGateway", "BazaarGate", deck, guard, support, safety,
                DistrictRoutePoint(-80.0f, -8.0f), DistrictRoutePoint(-66.0f, -8.0f));
            AddDistrictRouteLink(hubs, "WestResidentialGateway", "RailGate", deck, guard, support, safety);
            AddDistrictRouteLink(hubs, "WestResidentialGateway", "CustomsGate", deck, guard, support, safety,
                DistrictRoutePoint(-78.0f, -14.0f));
            AddDistrictRouteLink(hubs, "DrydockGate", "EastResidentialGateway", deck, guard, support, safety,
                DistrictRoutePoint(114.0f, -130.0f), DistrictRoutePoint(114.0f, -90.0f),
                DistrictRoutePoint(99.0f, -90.0f));
            AddDistrictRouteLink(hubs, "EastResidentialGateway", "OpsGate", deck, guard, support, safety,
                DistrictRoutePoint(84.0f, -54.0f), DistrictRoutePoint(64.0f, -40.0f));
            AddDistrictRouteLink(hubs, "EastResidentialGateway", "TideglassGate", deck, guard, support, safety,
                DistrictRoutePoint(92.0f, -52.0f), DistrictRoutePoint(90.0f, -10.0f));
        }

        // Existing concrete and structural materials remain visible at the gateway joins.
        _ = concrete;
        _ = steel;
        _ = steelDark;
        _ = yellow;
    }

    private static DistrictRouteHub CreateDistrictRouteHub(
        string id,
        Vector2 position,
        Vector3 stairDirection)
    {
        var direction = stairDirection.Normalized();
        var deckCenter = new Vector3(position.X, DistrictRouteDeckHeight, position.Y);
        var stairStart = deckCenter - direction * DistrictRouteStairRun;
        stairStart.Y = 0.08f;
        return new DistrictRouteHub(id, deckCenter, stairStart, direction);
    }

    private DistrictRouteHub CreateResidentialDistrictGateway(string id, int entranceIndex)
    {
        var entry = _residentialEntrances[entranceIndex];
        var inward = new Vector3(-entry.X, 0.0f, MapCenterZ - entry.Z).Normalized();
        var deckCenter = entry + inward * 4.2f;
        deckCenter.Y = DistrictRouteDeckHeight;
        var tangent = new Vector3(-inward.Z, 0.0f, inward.X).Normalized();
        var stairStart = deckCenter - tangent * DistrictRouteStairRun;
        stairStart.Y = 0.08f;
        return new DistrictRouteHub(id, deckCenter, stairStart, tangent, true);
    }

    private static Vector3 DistrictRoutePoint(float x, float z)
        => new(x, DistrictRouteDeckHeight, z);

    private void AddDistrictRouteHub(
        DistrictRouteHub hub,
        Godot.Material deck,
        Godot.Material guard,
        Godot.Material support,
        Godot.Material safety)
    {
        if (_districtRouteRoot is null)
        {
            return;
        }

        _districtRouteHubs.Add(hub);
        if (hub.ResidentialGateway)
        {
            _districtRouteGatewayCount++;
        }

        var platform = ExpansionBox(
            _districtRouteRoot,
            $"DistrictHub_{hub.Id}",
            hub.DeckCenter,
            new Vector3(DistrictRouteHubSize, DistrictRouteDeckThickness, DistrictRouteHubSize),
            deck);
        platform.AddToGroup("district_route_collision");
        platform.AddToGroup("district_route_deck");
        platform.AddToGroup("district_route_hub");
        if (hub.ResidentialGateway)
        {
            platform.AddToGroup("district_route_residential_gateway");
        }

        foreach (var lateral in new[] { -1.0f, 1.0f })
        {
            var edge = MeshBox(
                _districtRouteRoot,
                hub.DeckCenter + new Vector3(lateral * 2.45f, 0.16f, 0),
                new Vector3(0.12f, 0.12f, 4.6f),
                safety);
            edge.Name = $"DistrictHubEdge_{hub.Id}_{(lateral < 0 ? "L" : "R")}";
            RegisterMapDetailVisual(edge);
        }

        BuildDistrictRouteStair(hub, deck, guard, safety);
        var lateralAxis = new Vector3(hub.StairDirection.Z, 0.0f, -hub.StairDirection.X).Normalized();
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var supportPosition = hub.DeckCenter + lateralAxis * side * 2.05f;
            supportPosition.Y = 0.0f;
            BuildDistrictRouteSupport($"Hub_{hub.Id}_{(side < 0 ? "L" : "R")}", supportPosition, support, safety);
        }

        _districtRouteRoot.AddChild(new Label3D
        {
            Name = $"DistrictHubLabel_{hub.Id}",
            Position = hub.DeckCenter + Vector3.Up * 2.15f,
            Text = hub.Id.Replace("Gate", " ACCESS", StringComparison.Ordinal).Replace("Gateway", " GATEWAY", StringComparison.Ordinal).ToUpperInvariant(),
            FontSize = 13,
            OutlineSize = 4,
            Modulate = hub.ResidentialGateway ? new Color(0.35f, 0.92f, 0.78f) : new Color(1.0f, 0.69f, 0.28f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 40.0f
        });
    }

    private void AddDistrictRouteLink(
        IReadOnlyDictionary<string, DistrictRouteHub> hubs,
        string from,
        string to,
        Godot.Material deck,
        Godot.Material guard,
        Godot.Material support,
        Godot.Material safety,
        params Vector3[] bends)
    {
        if (_districtRouteRoot is null || !hubs.TryGetValue(from, out var fromHub) || !hubs.TryGetValue(to, out var toHub))
        {
            return;
        }

        var points = new List<Vector3>(bends.Length + 2) { fromHub.DeckCenter };
        points.AddRange(bends);
        points.Add(toHub.DeckCenter);
        var link = new DistrictRouteLink(from, to, points.ToArray());
        _districtRouteLinks.Add(link);
        var navigationPoints = new Vector3[link.Points.Length];
        for (var index = 0; index < link.Points.Length; index++)
        {
            navigationPoints[index] = link.Points[index]
                + Vector3.Up * (DistrictRouteDeckThickness * 0.5f + 0.12f);
        }
        RegisterSquadTraversalLink(
            $"district_deck:{from}:{to}",
            SquadTraversalKind.Walk,
            bidirectional: true,
            navigationPoints);
        for (var segment = 0; segment < link.Points.Length - 1; segment++)
        {
            BuildDistrictRouteSegment(
                $"DistrictLink_{from}_{to}_{segment:00}",
                link.Points[segment],
                link.Points[segment + 1],
                deck,
                guard,
                support,
                safety);
        }
    }

    private void BuildDistrictRouteSegment(
        string name,
        Vector3 from,
        Vector3 to,
        Godot.Material deck,
        Godot.Material guard,
        Godot.Material support,
        Godot.Material safety)
    {
        if (_districtRouteRoot is null)
        {
            return;
        }

        var delta = to - from;
        delta.Y = 0.0f;
        var length = delta.Length();
        if (length < 1.0f)
        {
            return;
        }

        var direction = delta / length;
        var yaw = Mathf.Atan2(direction.X, direction.Z);
        var lateral = new Vector3(direction.Z, 0.0f, -direction.X);
        var center = (from + to) * 0.5f;
        center.Y = DistrictRouteDeckHeight;
        var deckBody = ExpansionBox(
            _districtRouteRoot,
            name + "_Deck",
            center,
            new Vector3(DistrictRouteDeckWidth, DistrictRouteDeckThickness, length + 0.22f),
            deck,
            new Vector3(0, yaw, 0));
        deckBody.AddToGroup("district_route_collision");
        deckBody.AddToGroup("district_route_deck");
        _districtRouteDeckSamples.Add(center);
        _districtRouteDeckCount++;

        var railLength = Mathf.Max(0.8f, length - 4.2f);
        var guardPosts = new List<Transform3D>();
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var sideCenter = center + lateral * side * (DistrictRouteDeckWidth * 0.5f - 0.04f);
            var topRail = ExpansionBox(
                _districtRouteRoot,
                name + (side < 0 ? "_GuardTopL" : "_GuardTopR"),
                sideCenter + Vector3.Up * 1.02f,
                new Vector3(0.1f, 0.1f, railLength),
                safety,
                new Vector3(0, yaw, 0));
            topRail.AddToGroup("district_route_collision");
            topRail.AddToGroup("district_route_guard");
            var midRail = ExpansionBox(
                _districtRouteRoot,
                name + (side < 0 ? "_GuardMidL" : "_GuardMidR"),
                sideCenter + Vector3.Up * 0.53f,
                new Vector3(0.08f, 0.08f, railLength),
                guard,
                new Vector3(0, yaw, 0));
            midRail.AddToGroup("district_route_collision");
            midRail.AddToGroup("district_route_guard");

            var postCount = Mathf.Max(2, Mathf.CeilToInt(railLength / 3.4f) + 1);
            for (var post = 0; post < postCount; post++)
            {
                var t = post / (float)(postCount - 1);
                var along = Mathf.Lerp(-railLength * 0.5f, railLength * 0.5f, t);
                guardPosts.Add(new Transform3D(
                    Basis.Identity,
                    sideCenter + direction * along + Vector3.Up * 0.56f));
            }
        }
        AddDistrictRouteBatch(
            name + "_GuardPosts",
            new Vector3(0.08f, 1.08f, 0.08f),
            guard,
            guardPosts);

        var supportSlots = Mathf.FloorToInt(length / 24.0f);
        for (var slot = 0; slot < supportSlots; slot++)
        {
            var t = (slot + 1.0f) / (supportSlots + 1.0f);
            var supportPosition = from.Lerp(to, t) + lateral * (slot % 2 == 0 ? -1.72f : 1.72f);
            supportPosition.Y = 0.0f;
            BuildDistrictRouteSupport($"{name}_{slot:00}", supportPosition, support, safety);
        }
    }

    private void BuildDistrictRouteSupport(
        string name,
        Vector3 groundPosition,
        Godot.Material support,
        Godot.Material safety)
    {
        if (_districtRouteRoot is null || !IsDistrictRouteSupportPositionClear(groundPosition))
        {
            return;
        }

        var height = DistrictRouteDeckHeight - DistrictRouteDeckThickness * 0.5f;
        var body = ExpansionBox(
            _districtRouteRoot,
            $"DistrictSupport_{name}",
            groundPosition + Vector3.Up * height * 0.5f,
            new Vector3(0.28f, height, 0.28f),
            support);
        body.AddToGroup("district_route_collision");
        body.AddToGroup("district_route_support");
        var cap = MeshBox(
            _districtRouteRoot,
            groundPosition + Vector3.Up * (height - 0.12f),
            new Vector3(1.2f, 0.18f, 0.38f),
            safety);
        cap.Name = $"DistrictSupportCap_{name}";
        RegisterMapDetailVisual(cap);
        _districtRouteSupportPositions.Add(groundPosition);
        _districtRouteSupportCount++;
    }

    private static bool IsDistrictRouteSupportPositionClear(Vector3 position)
    {
        var extractOffset = new Vector2(position.X - ExtractionPoint.X, position.Z - ExtractionPoint.Z);
        if (extractOffset.LengthSquared() < 19.0f * 19.0f)
        {
            return false;
        }
        if (Mathf.Abs(position.X) < 5.0f && position.Z <= -7.0f && position.Z >= -44.0f)
        {
            return false;
        }
        foreach (var pad in ExtractionSpawnPads.Pads)
        {
            if (new Vector2(position.X - pad.X, position.Z - pad.Z).LengthSquared() < 10.0f * 10.0f)
            {
                return false;
            }
        }
        return true;
    }

    private void BuildDistrictRouteStair(
        DistrictRouteHub hub,
        Godot.Material deck,
        Godot.Material guard,
        Godot.Material safety)
    {
        if (_districtRouteRoot is null)
        {
            return;
        }

        var direction = hub.StairDirection.Normalized();
        var yaw = Mathf.Atan2(direction.X, direction.Z);
        var axisExtent = Mathf.Max(Mathf.Abs(direction.X), Mathf.Abs(direction.Z));
        var platformEdgeDistance = DistrictRouteHubSize * 0.5f / Mathf.Max(0.001f, axisExtent);
        var stairRun = DistrictRouteStairRun - platformEdgeDistance;
        var stepRun = stairRun / DistrictRouteStairSteps;
        var stepRise = DistrictRouteStairRise / DistrictRouteStairSteps;
        var stairBody = new StaticBody3D
        {
            Name = $"DistrictStair_{hub.Id}",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        stairBody.AddToGroup("district_route_collision");
        stairBody.AddToGroup("district_route_stair");
        _districtRouteRoot.AddChild(stairBody);

        var stepTransforms = new List<Transform3D>(DistrictRouteStairSteps);
        var navigationPoints = new List<Vector3>(DistrictRouteStairSteps + 2)
        {
            hub.StairStart - direction * 0.65f + Vector3.Up * 0.12f
        };
        var rotation = Basis.FromEuler(new Vector3(0, yaw, 0));
        const float treadThickness = 0.14f;
        for (var step = 0; step < DistrictRouteStairSteps; step++)
        {
            var top = hub.StairStart.Y + stepRise * (step + 1);
            var center = hub.StairStart + direction * stepRun * (step + 0.5f);
            center.Y = top - treadThickness * 0.5f;
            var size = new Vector3(2.35f, treadThickness, stepRun * 1.06f);
            stairBody.AddChild(new CollisionShape3D
            {
                Name = $"DistrictStairShape_{step:00}",
                Position = center,
                Rotation = new Vector3(0, yaw, 0),
                Shape = new BoxShape3D { Size = size }
            });
            stepTransforms.Add(new Transform3D(rotation.Scaled(size), center));
            navigationPoints.Add(new Vector3(center.X, top + 0.12f, center.Z));
        }

        navigationPoints.Add(
            hub.DeckCenter + Vector3.Up * (DistrictRouteDeckThickness * 0.5f + 0.12f));
        RegisterSquadTraversalLink(
            $"district_stair:{hub.Id}",
            SquadTraversalKind.Step,
            bidirectional: true,
            navigationPoints,
            costMultiplier: 1.08f);

        AddDistrictRouteBatch(
            $"DistrictStairVisual_{hub.Id}",
            Vector3.One,
            deck,
            stepTransforms);

        var lateral = new Vector3(direction.Z, 0.0f, -direction.X).Normalized();
        var railStartCenter = hub.StairStart + direction * stepRun * 0.5f + Vector3.Up * (stepRise + 0.94f);
        var railEndCenter = hub.StairStart + direction * (stairRun - stepRun * 0.5f)
            + Vector3.Up * (DistrictRouteStairRise + 0.94f);
        var balusters = new List<Transform3D>(18);
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var offset = lateral * side * 1.13f;
            BuildDistrictSlopedRail(
                $"DistrictStairRail_{hub.Id}_{(side < 0 ? "L" : "R")}",
                railStartCenter + offset,
                railEndCenter + offset,
                guard);
            for (var post = 0; post < 9; post++)
            {
                var t = post / 8.0f;
                var position = hub.StairStart + direction * Mathf.Lerp(stepRun * 0.5f, stairRun - stepRun * 0.5f, t) + offset;
                var surfaceY = hub.StairStart.Y + Mathf.Lerp(stepRise, DistrictRouteStairRise, t);
                position.Y = surfaceY + 0.46f;
                balusters.Add(new Transform3D(Basis.Identity, position));
            }
        }
        AddDistrictRouteBatch(
            $"DistrictStairBalusters_{hub.Id}",
            new Vector3(0.065f, 0.92f, 0.065f),
            safety,
            balusters);
        _districtRouteStairCount++;
    }

    private void BuildDistrictSlopedRail(
        string name,
        Vector3 from,
        Vector3 to,
        Godot.Material material)
    {
        if (_districtRouteRoot is null)
        {
            return;
        }

        var delta = to - from;
        var length = delta.Length();
        var horizontalLength = new Vector2(delta.X, delta.Z).Length();
        if (length < 0.1f || horizontalLength < 0.1f)
        {
            return;
        }
        var yaw = Mathf.Atan2(delta.X, delta.Z);
        var angle = Mathf.Atan2(delta.Y, horizontalLength);
        var body = ExpansionBox(
            _districtRouteRoot,
            name,
            (from + to) * 0.5f,
            new Vector3(0.11f, 0.11f, length),
            material,
            new Vector3(-angle, yaw, 0));
        body.AddToGroup("district_route_collision");
        body.AddToGroup("district_route_guard");
    }

    private MultiMeshInstance3D AddDistrictRouteBatch(
        string name,
        Vector3 size,
        Godot.Material material,
        IReadOnlyList<Transform3D> transforms)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = SharedBoxMesh(size),
            InstanceCount = transforms.Count
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }
        var batch = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            MaterialOverride = material
        };
        _districtRouteRoot!.AddChild(batch);
        RegisterMapDetailVisual(batch);
        return batch;
    }

    private async void ValidateDistrictNetwork()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(5);

        var expectedHubCount = 10;
        var expectedMinimumLinks = 14;
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var hub in _districtRouteHubs)
        {
            adjacency[hub.Id] = new HashSet<string>(StringComparer.Ordinal);
        }
        foreach (var link in _districtRouteLinks)
        {
            if (!adjacency.TryGetValue(link.From, out var fromSet) || !adjacency.TryGetValue(link.To, out var toSet))
            {
                continue;
            }
            fromSet.Add(link.To);
            toSet.Add(link.From);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (_districtRouteHubs.Count > 0)
        {
            var queue = new Queue<string>();
            queue.Enqueue(_districtRouteHubs[0].Id);
            visited.Add(_districtRouteHubs[0].Id);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in adjacency[current])
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }
        var graphConnected = visited.Count == _districtRouteHubs.Count;
        var redundantRoutes = adjacency.Count == expectedHubCount && adjacency.Values.All(neighbors => neighbors.Count >= 2);
        var gatewayLinks = _districtRouteLinks.Count(link =>
            link.From.Contains("ResidentialGateway", StringComparison.Ordinal)
            || link.To.Contains("ResidentialGateway", StringComparison.Ordinal));
        var gatewaysReady = _districtRouteGatewayCount == 2 && gatewayLinks >= 4;

        var collisionNodes = GetTree().GetNodesInGroup("district_route_collision");
        var collisionReady = collisionNodes.Count > 0;
        foreach (var node in collisionNodes)
        {
            if (node is not StaticBody3D body || body.CollisionLayer != 1)
            {
                collisionReady = false;
                break;
            }
            collisionReady &= body.GetChildren().OfType<CollisionShape3D>().Any(shape => shape.Shape is not null);
        }

        var stairNodes = GetTree().GetNodesInGroup("district_route_stair");
        var stairShapeCount = 0;
        foreach (var node in stairNodes)
        {
            if (node is StaticBody3D stair)
            {
                stairShapeCount += stair.GetChildren().OfType<CollisionShape3D>().Count(shape => shape.Shape is BoxShape3D);
            }
        }
        var verticalRoutesReady = stairNodes.Count == expectedHubCount
            && _districtRouteStairCount == expectedHubCount
            && stairShapeCount == expectedHubCount * DistrictRouteStairSteps;

        var deckSamplesReady = _districtRouteDeckSamples.Count == _districtRouteDeckCount && _districtRouteDeckCount >= 24;
        var headClearanceReady = true;
        var deckHits = 0;
        foreach (var sample in _districtRouteDeckSamples)
        {
            var down = PhysicsRayQueryParameters3D.Create(sample + Vector3.Up * 1.25f, sample - Vector3.Up * 0.7f);
            down.CollisionMask = 1;
            down.CollideWithAreas = false;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(down);
            if (hit.Count > 0
                && hit["collider"].AsGodotObject() is Node collider
                && collider.IsInGroup("district_route_deck"))
            {
                deckHits++;
            }

            var up = PhysicsRayQueryParameters3D.Create(
                sample + Vector3.Up * (DistrictRouteDeckThickness * 0.5f + 0.12f),
                sample + Vector3.Up * 2.15f);
            up.CollisionMask = 1;
            up.CollideWithAreas = false;
            headClearanceReady &= GetWorld3D().DirectSpaceState.IntersectRay(up).Count == 0;
        }
        deckSamplesReady &= deckHits == _districtRouteDeckSamples.Count;

        var supportClearanceReady = _districtRouteSupportPositions.Count == _districtRouteSupportCount
            && _districtRouteSupportPositions.All(IsDistrictRouteSupportPositionClear);
        var spawnClearanceReady = true;
        var extractionAirspaceReady = true;
        foreach (var hub in _districtRouteHubs)
        {
            foreach (var pad in ExtractionSpawnPads.Pads)
            {
                spawnClearanceReady &= HorizontalDistance(hub.DeckCenter, pad) >= 10.0f;
            }
            extractionAirspaceReady &= HorizontalDistance(hub.DeckCenter, ExtractionPoint) >= 19.0f;
        }
        foreach (var link in _districtRouteLinks)
        {
            for (var segment = 0; segment < link.Points.Length - 1; segment++)
            {
                extractionAirspaceReady &= HorizontalPointSegmentDistance(
                    ExtractionPoint,
                    link.Points[segment],
                    link.Points[segment + 1]) >= 19.0f;
            }
        }
        var truckClearanceReady = DistrictRouteDeckHeight - DistrictRouteDeckThickness * 0.5f >= 4.5f
            && _districtRouteSupportPositions.All(position =>
                Mathf.Abs(position.X) >= 5.0f || position.Z > -7.0f || position.Z < -44.0f);

        var routeBlockers = new HashSet<string>(StringComparer.Ordinal);
        var clearanceHeight = 1.62f;
        var routeClearanceShape = new CapsuleShape3D { Radius = 0.32f, Height = clearanceHeight };
        var routeClearanceQuery = new PhysicsShapeQueryParameters3D
        {
            Shape = routeClearanceShape,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f,
            Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() }
        };
        foreach (var link in _districtRouteLinks)
        {
            for (var segment = 0; segment < link.Points.Length - 1; segment++)
            {
                var from = link.Points[segment];
                var to = link.Points[segment + 1];
                var samples = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 1.2f));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var feet = from.Lerp(to, sample / (float)samples);
                    feet.Y = DistrictRouteDeckHeight + DistrictRouteDeckThickness * 0.5f + 0.02f;
                    routeClearanceQuery.Transform = new Transform3D(
                        Basis.Identity,
                        feet + Vector3.Up * (clearanceHeight * 0.5f + 0.04f));
                    var hits = GetWorld3D().DirectSpaceState.IntersectShape(routeClearanceQuery, 12);
                    foreach (var hit in hits)
                    {
                        var collider = hit.TryGetValue("collider", out var value)
                            ? value.AsGodotObject() as Node
                            : null;
                        if (collider?.IsInGroup("district_route_collision") == true)
                        {
                            continue;
                        }
                        var blockerDescription = collider?.Name.ToString() ?? "unknown";
                        if (collider is Node3D blocker3D)
                        {
                            blockerDescription += $"[{blocker3D.GlobalPosition.X:0.0},{blocker3D.GlobalPosition.Y:0.0},{blocker3D.GlobalPosition.Z:0.0}]";
                            var shape = blocker3D.GetChildren().OfType<CollisionShape3D>().FirstOrDefault()?.Shape;
                            if (shape is BoxShape3D box)
                            {
                                blockerDescription += $"box({box.Size.X:0.0},{box.Size.Y:0.0},{box.Size.Z:0.0})";
                            }
                            else if (shape is CylinderShape3D cylinder)
                            {
                                blockerDescription += $"cyl({cylinder.Radius:0.0},{cylinder.Height:0.0})";
                            }
                        }
                        routeBlockers.Add($"{link.From}-{link.To}:{blockerDescription}@{feet.X:0.0},{feet.Z:0.0}");
                    }
                }
            }
        }
        var routeClearanceReady = routeBlockers.Count == 0;

        var walkHub = _districtRouteHubs.First(hub => hub.Id == "OpsGate");
        _player.GlobalPosition = walkHub.StairStart - walkHub.StairDirection * 0.65f + Vector3.Up * 0.25f;
        _player.RestoreMovementInput();
        await WaitFrames(10);
        var climbStartY = _player.GlobalPosition.Y;
        var climbed = false;
        try
        {
            Input.ActionPress("move_forward");
            Input.ActionPress("sprint");
            for (var frame = 0; frame < 520 && !climbed; frame++)
            {
                if (frame % 5 == 0)
                {
                    _player.FaceWorldPointForDiagnostics(walkHub.DeckCenter + Vector3.Up * 0.4f);
                }
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                climbed = _player.GlobalPosition.Y - climbStartY > 3.6f;
            }
        }
        finally
        {
            Input.ActionRelease("sprint");
            Input.ActionRelease("move_forward");
        }

        var valid = _districtRouteRoot is not null
            && DistrictRouteHubCount == expectedHubCount
            && DistrictRouteLinkCount >= expectedMinimumLinks
            && graphConnected
            && redundantRoutes
            && gatewaysReady
            && collisionReady
            && verticalRoutesReady
            && deckSamplesReady
            && headClearanceReady
            && supportClearanceReady
            && spawnClearanceReady
            && extractionAirspaceReady
            && truckClearanceReady
            && routeClearanceReady
            && climbed;
        GD.Print($"DISTRICT_NETWORK_CHECK valid={valid} hubs={DistrictRouteHubCount}/{expectedHubCount} links={DistrictRouteLinkCount}/{expectedMinimumLinks} connected={graphConnected} redundant={redundantRoutes} gateways={_districtRouteGatewayCount}/2 gateway_links={gatewayLinks} decks={_districtRouteDeckCount} deck_hits={deckHits}/{_districtRouteDeckSamples.Count} collisions={collisionNodes.Count} collision_ready={collisionReady} stairs={_districtRouteStairCount}/{expectedHubCount} stair_shapes={stairShapeCount} climb={climbed} climb_y={_player.GlobalPosition.Y - climbStartY:0.00} head_clear={headClearanceReady} route_clear={routeClearanceReady} route_blocked={string.Join(';', routeBlockers)} supports={_districtRouteSupportCount} support_clear={supportClearanceReady} spawn_clear={spawnClearanceReady} extract_clear={extractionAirspaceReady} truck_clear={truckClearanceReady}");
        GD.Print($"DISTRICT_NETWORK_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static float HorizontalPointSegmentDistance(Vector3 point, Vector3 from, Vector3 to)
    {
        var p = new Vector2(point.X, point.Z);
        var a = new Vector2(from.X, from.Z);
        var b = new Vector2(to.X, to.Z);
        var delta = b - a;
        var lengthSquared = delta.LengthSquared();
        if (lengthSquared < 0.0001f)
        {
            return p.DistanceTo(a);
        }
        var t = Mathf.Clamp((p - a).Dot(delta) / lengthSquared, 0.0f, 1.0f);
        return p.DistanceTo(a + delta * t);
    }
}
