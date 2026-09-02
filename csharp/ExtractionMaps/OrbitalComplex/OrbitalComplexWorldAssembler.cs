using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public sealed class OrbitalComplexGateRuntime
{
    internal OrbitalComplexGateRuntime(
        OrbitalComplexPowerGateDefinition definition,
        StaticBody3D collisionBody,
        CollisionShape3D collisionShape,
        Node3D? authoredVisual)
    {
        Definition = definition;
        CollisionBody = collisionBody;
        CollisionShape = collisionShape;
        AuthoredVisual = authoredVisual;
    }

    public OrbitalComplexPowerGateDefinition Definition { get; }
    public StaticBody3D CollisionBody { get; }
    public CollisionShape3D CollisionShape { get; }
    public Node3D? AuthoredVisual { get; }
    public bool IsOpen { get; internal set; }
    public bool IsVisualVisible => AuthoredVisual?.Visible ?? false;
}

public sealed class OrbitalComplexWorldBuildResult
{
    private readonly ulong _layoutSeed;
    private readonly Dictionary<string, float> _gateOverrideRemaining =
        new(StringComparer.Ordinal);

    internal OrbitalComplexWorldBuildResult(
        Node3D authoredArtRoot,
        Node3D gameplayRoot,
        int collisionShapeCount,
        IReadOnlyDictionary<string, Node3D> objectiveAnchors,
        IReadOnlyDictionary<string, OrbitalComplexGateRuntime> gates,
        IReadOnlyDictionary<string, Node3D> presentationNodes,
        OrbitalComplexValidationSnapshot layoutValidation,
        ulong layoutSeed)
    {
        AuthoredArtRoot = authoredArtRoot;
        GameplayRoot = gameplayRoot;
        CollisionShapeCount = collisionShapeCount;
        ObjectiveAnchors = objectiveAnchors;
        Gates = gates;
        PresentationNodes = presentationNodes;
        LayoutValidation = layoutValidation;
        _layoutSeed = layoutSeed;
        PowerState = OrbitalComplexPowerRules.Derive(0, layoutSeed);
    }

    public Node3D AuthoredArtRoot { get; }
    public Node3D GameplayRoot { get; }
    public int CollisionShapeCount { get; }
    public IReadOnlyDictionary<string, Node3D> ObjectiveAnchors { get; }
    public IReadOnlyDictionary<string, OrbitalComplexGateRuntime> Gates { get; }
    public IReadOnlyDictionary<string, Node3D> PresentationNodes { get; }
    public OrbitalComplexValidationSnapshot LayoutValidation { get; }
    public OrbitalComplexPowerState PowerState { get; private set; }

    /// <summary>
    /// Opens a normally locked gate for a short, high-risk maintenance window.  The
    /// override is deliberately kept in the assembled-world result rather than in the
    /// FreightTerminalWorld adapter so objective-stage changes cannot accidentally leave
    /// the collision and authored door out of sync.
    /// </summary>
    public bool ActivateGateOverride(string gateId, float durationSeconds)
    {
        if (!Gates.TryGetValue(gateId, out var gate)
            || durationSeconds <= 0.0f)
        {
            return false;
        }

        _gateOverrideRemaining[gateId] = Mathf.Max(
            durationSeconds,
            _gateOverrideRemaining.TryGetValue(gateId, out var remaining)
                ? remaining
                : 0.0f);
        ApplyGateState(gate, open: true, overrideActive: true);
        gate.CollisionBody.SetMeta(
            "falltide_gate_override_remaining",
            _gateOverrideRemaining[gateId]);
        ApplyPresentation(PowerState);
        return true;
    }

    public bool IsGateOverrideActive(string gateId)
        => _gateOverrideRemaining.TryGetValue(gateId, out var remaining)
            && remaining > 0.0f;

    public float GateOverrideRemaining(string gateId)
        => _gateOverrideRemaining.TryGetValue(gateId, out var remaining)
            ? Mathf.Max(0.0f, remaining)
            : 0.0f;

    /// <summary>Advances temporary gate overrides and restores normal stage authority.</summary>
    public void TickGateOverrides(float deltaSeconds)
    {
        if (deltaSeconds <= 0.0f || _gateOverrideRemaining.Count == 0)
        {
            return;
        }

        var expired = new List<string>();
        foreach (var pair in _gateOverrideRemaining.ToArray())
        {
            var remaining = pair.Value - deltaSeconds;
            if (remaining <= 0.0f)
            {
                expired.Add(pair.Key);
            }
            else
            {
                _gateOverrideRemaining[pair.Key] = remaining;
                if (Gates.TryGetValue(pair.Key, out var activeGate))
                {
                    activeGate.CollisionBody.SetMeta(
                        "falltide_gate_override_remaining",
                        remaining);
                }
            }
        }

        foreach (var gateId in expired)
        {
            _gateOverrideRemaining.Remove(gateId);
            if (Gates.TryGetValue(gateId, out var gate))
            {
                var stageOpen = OrbitalComplexPowerRules.IsGateOpen(gate.Definition, PowerState);
                ApplyGateState(gate, stageOpen, overrideActive: false);
            }
        }

        if (expired.Count > 0)
        {
            ApplyPresentation(PowerState);
        }
    }

    public OrbitalComplexPowerState ApplyObjectiveStage(int objectiveStage)
        => ApplyObjectiveStage(objectiveStage, _layoutSeed);

    public OrbitalComplexPowerState ApplyObjectiveStage(
        int objectiveStage,
        ulong sharedWorldSeed)
    {
        var state = OrbitalComplexPowerRules.Derive(objectiveStage, sharedWorldSeed);
        foreach (var gate in Gates.Values)
        {
            var stageOpen = OrbitalComplexPowerRules.IsGateOpen(gate.Definition, state);
            ApplyGateState(
                gate,
                stageOpen || IsGateOverrideActive(gate.Definition.Id),
                IsGateOverrideActive(gate.Definition.Id));
        }

        ApplyPresentation(state);
        PowerState = state;
        return state;
    }

    private static void ApplyGateState(
        OrbitalComplexGateRuntime gate,
        bool open,
        bool overrideActive = false)
    {
        gate.IsOpen = open;
        gate.CollisionShape.Disabled = open;
        gate.CollisionBody.SetMeta("falltide_gate_open", open);
        gate.CollisionBody.SetMeta("falltide_gate_stage", gate.Definition.OpensAtObjectiveStage);
        gate.CollisionBody.SetMeta("falltide_gate_override", overrideActive);
        gate.CollisionBody.SetMeta(
            "falltide_gate_override_remaining",
            overrideActive ? 1.0f : 0.0f);
        if (gate.AuthoredVisual is not { } visual)
        {
            return;
        }

        visual.SetMeta("falltide_target_open", open);
        visual.SetMeta("falltide_target_open_fraction", open ? 1.0f : 0.0f);
        visual.Visible = !gate.Definition.HideVisualWhenOpen || !open;
    }

    private void ApplyPresentation(OrbitalComplexPowerState state)
    {
        AuthoredArtRoot.SetMeta("falltide_objective_stage", state.ObjectiveStage);
        AuthoredArtRoot.SetMeta("falltide_power_mode", (int)state.Mode);
        AuthoredArtRoot.SetMeta(
            "falltide_qrf_activation_hint",
            state.Presentation.QrfActivationRecommended);
        AuthoredArtRoot.SetMeta(
            "falltide_boss_activation_hint",
            state.Presentation.BossActivationRecommended);
        AuthoredArtRoot.SetMeta(
            "falltide_response_hint",
            (int)state.Presentation.ResponseHint);

        ApplyDishTarget(state.Presentation.DishRotationSpeedRadiansPerSecond);
        ApplyDoorTarget(
            state.Presentation.TideGateOpeningFraction,
            "TideGate",
            "TideGateLeft",
            "TideGateRight");
        ApplyDoorTarget(
            IsGateOverrideActive("stormglass_vault")
                ? 1.0f
                : state.Presentation.VaultDoorOpeningFraction,
            "VaultDoor",
            "VaultDoorLeft",
            "VaultDoorRight");
        ApplyPowerZoneVisibility(state.Mode);
        ApplyAlarmPresentation(state.Presentation.AlarmState);

        foreach (var district in state.Presentation.DistrictLights)
        {
            ApplyDistrictPresentation(district);
        }
    }

    private void ApplyDishTarget(float rotationSpeed)
    {
        foreach (var name in new[] { "DishRotor", "DishYaw" })
        {
            if (PresentationNodes.TryGetValue(name, out var dish))
            {
                dish.SetMeta("falltide_rotation_speed_radians_per_second", rotationSpeed);
            }
        }
    }

    private void ApplyDoorTarget(float fraction, params string[] nodeNames)
    {
        for (var index = 0; index < nodeNames.Length; index++)
        {
            if (!PresentationNodes.TryGetValue(nodeNames[index], out var door))
            {
                continue;
            }
            door.SetMeta("falltide_target_open_fraction", fraction);
            if (nodeNames[index].EndsWith("Left", StringComparison.Ordinal))
            {
                door.SetMeta("falltide_open_direction", -1.0f);
            }
            else if (nodeNames[index].EndsWith("Right", StringComparison.Ordinal))
            {
                door.SetMeta("falltide_open_direction", 1.0f);
            }
        }
    }

    private void ApplyPowerZoneVisibility(OrbitalComplexPowerMode mode)
    {
        if (PresentationNodes.TryGetValue("PowerZone_Blackout", out var blackout))
        {
            blackout.Visible = mode == OrbitalComplexPowerMode.Blackout;
        }
        if (PresentationNodes.TryGetValue("PowerZone_Powered", out var powered))
        {
            powered.Visible = mode != OrbitalComplexPowerMode.Blackout;
        }
    }

    private void ApplyAlarmPresentation(OrbitalComplexAlarmState alarmState)
    {
        var active = alarmState != OrbitalComplexAlarmState.Off;
        var color = alarmState == OrbitalComplexAlarmState.VaultBreach
            ? new Color(1.0f, 0.08f, 0.03f)
            : new Color(1.0f, 0.42f, 0.06f);
        foreach (var name in new[]
                 {
                     "AlarmLights",
                     "AlarmLight_Archive",
                     "AlarmLight_Breaker",
                     "AlarmLight_Central",
                     "AlarmLight_TideGate"
                 })
        {
            if (!PresentationNodes.TryGetValue(name, out var alarmRoot))
            {
                continue;
            }
            alarmRoot.Visible = active;
            alarmRoot.SetMeta("falltide_alarm_state", (int)alarmState);
            ApplyLights(alarmRoot, color, active ? 2.4f : 0.0f);
        }
    }

    private void ApplyDistrictPresentation(
        OrbitalComplexDistrictLightPresentation district)
    {
        var nodeNames = district.District switch
        {
            "BreakerYard" => new[] { "BreakerYardLights", "AlarmLight_Breaker", "PoweredBreakerStrip" },
            "QuarantineArchive" => new[] { "QuarantineArchiveLights", "AlarmLight_Archive", "PoweredArchiveStrip" },
            "StormglassArray" => new[] { "StormglassArrayLights", "AlarmLight_Central", "PoweredSpineStrip", "PoweredVaultStrip" },
            "TideGate" => new[] { "TideGateLights", "AlarmLight_TideGate", "PoweredNorthStrip" },
            _ => Array.Empty<string>()
        };
        foreach (var nodeName in nodeNames)
        {
            if (!PresentationNodes.TryGetValue(nodeName, out var lightRoot))
            {
                continue;
            }
            lightRoot.SetMeta("falltide_light_color", district.Color);
            lightRoot.SetMeta("falltide_light_energy", district.Energy);
            ApplyLights(lightRoot, district.Color, district.Energy);
        }
    }

    private static void ApplyLights(Node node, Color color, float energy)
    {
        if (node is Light3D light)
        {
            light.LightColor = color;
            light.LightEnergy = energy;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ApplyLights(childNode, color, energy);
            }
        }
    }
}

/// <summary>
/// Loads the DCC-authored Falltide scene and adds deterministic, invisible gameplay collision only.
/// </summary>
public sealed class OrbitalComplexWorldAssembler
{
    public const string DefaultScenePath =
        "res://assets/models/orbital_complex/orbital_complex.glb";
    public const string AuthoredSceneGroup = "orbital_complex_authored_scene";
    public const string GameplayCollisionGroup = "orbital_complex_gameplay_collision";

    // Godot's glTF importer keeps arbitrary Blender custom properties in the
    // source asset but does not expose them as Node metadata on every 4.6
    // import path.  Keep the authored placement manifest beside the importer
    // contract so a valid GLB cannot silently lose its architecture collision
    // when the editor cache is rebuilt.  These are the 36 architecture roots
    // emitted by build_orbital_complex(_underground).py; decorative landmark
    // groups intentionally are not included.
    private static readonly HashSet<string> AuthoredArchitectureAssemblyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "BreakerBoilerHall",
            "BreakerControlAnnex",
            "BreakerSwitchgearHall",
            "BreakerTransformerGallery",
            "BreakerTurbineHall",
            "CathodeCompressorWest",
            "CathodeLoadingBayWest",
            "CathodeSawtoothEast",
            "CathodeUtilityEast",
            "CoolantPipeStoresEast",
            "CoolantPipeStoresWest",
            "CoolantTunnelEast",
            "CoolantTunnelWest",
            "OssuaryInspectionLock",
            "OssuaryShiftLock",
            "OssuaryWindowGallery",
            "IntakeCrewMess",
            "IntakeCustomsWest",
            "IntakePressureArch",
            "IntakeServiceEast",
            "LaunchCatwalkEast",
            "LaunchCatwalkWest",
            "LaunchSiloCapsuleBay",
            "LaunchSiloEastStores",
            "LaunchSiloPressureGate",
            "LaunchSiloWestStores",
            "ArchiveCommandHall",
            "ArchiveCryoHall",
            "ArchiveDeconMess",
            "ArchiveGlassLab",
            "ArchiveObservation",
            "ReactorBridgeEast",
            "ReactorBridgeWest",
            "ReactorControlGallery",
            "ReactorPumpEast",
            "ReactorPumpWest"
        };

    private static readonly string[] PresentationNodeNames =
    {
        "DishRotor",
        "DishYaw",
        "DishPitch",
        "TideGate",
        "TideGateLeft",
        "TideGateRight",
        "VaultDoor",
        "VaultDoorLeft",
        "VaultDoorRight",
        "AlarmLights",
        "AlarmLight_Archive",
        "AlarmLight_Breaker",
        "AlarmLight_Central",
        "AlarmLight_TideGate",
        "BreakerYardLights",
        "QuarantineArchiveLights",
        "StormglassArrayLights",
        "TideGateLights",
        "PowerZone_Blackout",
        "PowerZone_Powered",
        "PoweredArchiveStrip",
        "PoweredBreakerStrip",
        "PoweredNorthStrip",
        "PoweredSpineStrip",
        "PoweredVaultStrip"
    };

    private readonly string _scenePath;

    public OrbitalComplexWorldAssembler(string scenePath = DefaultScenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            throw new ArgumentException("An authored Falltide scene path is required.", nameof(scenePath));
        }
        _scenePath = scenePath;
    }

    public OrbitalComplexWorldBuildResult Build(
        Node3D parent,
        OrbitalComplexMapLayout layout,
        int objectiveStage = 0,
        ulong? sharedWorldSeed = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(layout);
        var layoutValidation = OrbitalComplexLayoutValidator.Validate(layout);
        if (!layoutValidation.Valid)
        {
            throw new InvalidOperationException(
                "The Falltide gameplay layout failed validation: "
                + layoutValidation.MachineSummary);
        }
        EnsureRootNamesAvailable(parent);

        var packedScene = GD.Load<PackedScene>(_scenePath)
            ?? throw new InvalidOperationException(
                $"Unable to load the authored Falltide scene at '{_scenePath}'.");
        var instance = packedScene.Instantiate();
        if (instance is not Node3D authoredRoot)
        {
            instance.Free();
            throw new InvalidOperationException(
                $"The authored Falltide scene at '{_scenePath}' must have a Node3D root.");
        }

        authoredRoot.Name = "FalltideRecoveryArrayAuthored";
        authoredRoot.AddToGroup(AuthoredSceneGroup);
        parent.AddChild(authoredRoot);
        var gameplayRoot = new Node3D { Name = "FalltideRecoveryArrayGameplay" };
        gameplayRoot.AddToGroup(GameplayCollisionGroup);
        parent.AddChild(gameplayRoot);

        try
        {
            var collisionCount = BuildStaticCollision(gameplayRoot, layout);
            collisionCount += BuildAuthoredAssemblyCollision(
                gameplayRoot,
                authoredRoot);
            var anchors = BuildObjectiveAnchors(gameplayRoot, layout);
            var gates = BuildPowerGates(gameplayRoot, authoredRoot, layout);
            collisionCount += gates.Count;
            var presentationNodes = FindPresentationNodes(authoredRoot);
            var result = new OrbitalComplexWorldBuildResult(
                authoredRoot,
                gameplayRoot,
                collisionCount,
                new ReadOnlyDictionary<string, Node3D>(anchors),
                new ReadOnlyDictionary<string, OrbitalComplexGateRuntime>(gates),
                new ReadOnlyDictionary<string, Node3D>(presentationNodes),
                layoutValidation,
                layout.SharedWorldSeed);
            result.ApplyObjectiveStage(
                objectiveStage,
                sharedWorldSeed ?? layout.SharedWorldSeed);
            gameplayRoot.SetMeta("falltide_collision_shape_count", collisionCount);
            gameplayRoot.SetMeta("falltide_objective_anchor_count", anchors.Count);
            gameplayRoot.SetMeta("falltide_power_gate_count", gates.Count);
            return result;
        }
        catch
        {
            gameplayRoot.QueueFree();
            authoredRoot.QueueFree();
            throw;
        }
    }

    private static void EnsureRootNamesAvailable(Node3D parent)
    {
        if (parent.GetNodeOrNull<Node3D>("FalltideRecoveryArrayAuthored") is not null
            || parent.GetNodeOrNull<Node3D>("FalltideRecoveryArrayGameplay") is not null)
        {
            throw new InvalidOperationException(
                "The Falltide world is already assembled under the supplied parent.");
        }
    }

    private static int BuildStaticCollision(
        Node3D gameplayRoot,
        OrbitalComplexMapLayout layout)
    {
        var body = new StaticBody3D
        {
            Name = "FalltideStaticCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup(GameplayCollisionGroup);
        gameplayRoot.AddChild(body);

        var collisionCount = 0;
        foreach (var box in layout.CollisionBoxes)
        {
            body.AddChild(CreateCollisionShape(
                $"Collision_{++collisionCount:000}_{box.Id}",
                box.Position,
                box.Size,
                box.RotationRadians,
                box.Id,
                box.Purpose));
        }
        foreach (var ramp in layout.Ramps)
        {
            body.AddChild(CreateCollisionShape(
                $"Collision_{++collisionCount:000}_{ramp.Id}",
                ramp.Position,
                ramp.Size,
                ramp.RotationRadians,
                ramp.Id,
                "ramp"));
        }
        return collisionCount;
    }

    /// <summary>
    /// Gives the imported Trey/Majadroid room shells real player and ballistic
    /// collision without turning the whole GLB into one opaque convex block.
    /// Each DCC placement is exported with <c>dcc_assembly=true</c>; collecting
    /// one concave shape per placement preserves its authored door openings and
    /// keeps animated dish/pressure-door meshes out of the static body.
    /// </summary>
    private static int BuildAuthoredAssemblyCollision(
        Node3D gameplayRoot,
        Node3D authoredRoot)
    {
        var assemblies = new List<Node3D>();
        CollectAuthoredAssemblies(authoredRoot, assemblies);
        var shapeCount = 0;
        var triangleCount = 0;
        foreach (var assembly in assemblies)
        {
            var faces = new List<Vector3>();
            CollectAssemblyMeshFaces(assembly, faces);
            if (faces.Count < 3 || faces.Count % 3 != 0)
            {
                continue;
            }

            var shape = new ConcavePolygonShape3D
            {
                BackfaceCollision = true
            };
            shape.SetFaces(faces.ToArray());
            var body = new StaticBody3D
            {
                Name = $"AuthoredAssemblyCollision_{assembly.Name}",
                CollisionLayer = 1,
                CollisionMask = 0
            };
            body.AddToGroup(GameplayCollisionGroup);
            body.SetMeta("falltide_authored_assembly", assembly.Name);
            body.SetMeta("falltide_authored_triangle_count", faces.Count / 3);
            gameplayRoot.AddChild(body);
            body.GlobalTransform = assembly.GlobalTransform;
            body.AddChild(new CollisionShape3D
            {
                Name = "AuthoredAssemblyConcaveShape",
                Shape = shape
            });
            shapeCount++;
            triangleCount += faces.Count / 3;
        }

        gameplayRoot.SetMeta("falltide_authored_assembly_count", shapeCount);
        gameplayRoot.SetMeta("falltide_authored_collision_triangles", triangleCount);
        return shapeCount;
    }

    private static void CollectAuthoredAssemblies(
        Node node,
        List<Node3D> assemblies)
    {
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is not Node childNode)
            {
                continue;
            }
            if (childNode is Node3D spatial
                && IsAuthoredCollisionAssembly(spatial)
                && !IsAnimatedAssembly(spatial))
            {
                assemblies.Add(spatial);
                // DCC placement roots are unique and their descendants do not
                // carry dcc_assembly, so there is no need to recurse into them.
                continue;
            }
            CollectAuthoredAssemblies(childNode, assemblies);
        }
    }

    private static bool IsAuthoredCollisionAssembly(Node3D spatial)
    {
        // New exports classify every root explicitly.  Honour that role so a
        // landmark group tagged minor_prop never turns its decorative spires
        // into a large concave blocker.  The dcc_assembly fallback keeps older
        // revisions of the authored GLB playable during content iteration.
        if (TryReadAuthoredMeta(spatial, "collision_role", out var collisionRole))
        {
            return string.Equals(
                collisionRole.AsString(),
                "architecture_shell",
                StringComparison.OrdinalIgnoreCase);
        }
        return (TryReadAuthoredMeta(spatial, "dcc_assembly", out var dccAssembly)
            && dccAssembly.AsBool())
            || AuthoredArchitectureAssemblyNames.Contains(spatial.Name.ToString());
    }

    private static bool IsAnimatedAssembly(Node3D assembly)
    {
        // The authored GLB marks movable roots with the same metadata used by
        // the presentation adapter.  Prefer that contract over name matching:
        // a static module is allowed to contain words such as "Capsule" (for
        // example LaunchSiloCapsuleBay), while a future gate can be renamed
        // without silently acquiring a second, immovable collision shell.
        if (TryReadAuthoredMeta(assembly, "animation_motion", out _)
            || TryReadAuthoredMeta(assembly, "pivot_role", out _))
        {
            return true;
        }

        // Keep a narrow compatibility list for older GLB revisions that did
        // not carry the extras.  Do not use broad Contains checks here: those
        // would incorrectly drop legitimate static architecture assemblies.
        var name = assembly.Name.ToString();
        return name.Equals("TideGateLeft", StringComparison.OrdinalIgnoreCase)
            || name.Equals("TideGateRight", StringComparison.OrdinalIgnoreCase)
            || name.Equals("VaultDoorLeft", StringComparison.OrdinalIgnoreCase)
            || name.Equals("VaultDoorRight", StringComparison.OrdinalIgnoreCase)
            || name.Equals("UpperBypassBarrier", StringComparison.OrdinalIgnoreCase)
            || name.Equals("DishYaw", StringComparison.OrdinalIgnoreCase)
            || name.Equals("DishPitch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadAuthoredMeta(
        Node node,
        string key,
        out Variant value)
    {
        value = default;
        if (node.HasMeta(key))
        {
            value = node.GetMeta(key);
            return true;
        }

        // Godot 4.6 preserves glTF node extras as one Dictionary metadata
        // value.  Accept the flattened form too so the contract remains
        // compatible with older importer revisions and hand-authored scenes.
        if (!node.HasMeta("extras"))
        {
            return false;
        }
        var extrasValue = node.GetMeta("extras");
        if (extrasValue.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        var extras = extrasValue.AsGodotDictionary();
        if (!extras.ContainsKey(key))
        {
            return false;
        }
        value = extras[key];
        return true;
    }

    private static void CollectAssemblyMeshFaces(
        Node3D assembly,
        List<Vector3> faces)
    {
        if (assembly is MeshInstance3D { Mesh: not null } rootMesh)
        {
            foreach (var vertex in rootMesh.Mesh.GetFaces())
            {
                // The assembly's transform is assigned to the collision body
                // below, so vertices on a mesh-root assembly stay in that
                // assembly-local space (child meshes are transformed during
                // recursive collection).
                faces.Add(vertex);
            }
        }
        var children = assembly.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CollectAssemblyMeshFacesRecursive(
                    childNode,
                    Transform3D.Identity,
                    faces);
            }
        }
    }

    private static void CollectAssemblyMeshFacesRecursive(
        Node node,
        Transform3D parentTransform,
        List<Vector3> faces)
    {
        var transform = node is Node3D spatial
            ? parentTransform * spatial.Transform
            : parentTransform;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            foreach (var vertex in mesh.Mesh.GetFaces())
            {
                faces.Add(transform * vertex);
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CollectAssemblyMeshFacesRecursive(childNode, transform, faces);
            }
        }
    }

    private static CollisionShape3D CreateCollisionShape(
        string name,
        Vector3 position,
        Vector3 size,
        Vector3 rotation,
        string sourceId,
        string purpose)
    {
        var shape = new CollisionShape3D
        {
            Name = name,
            Shape = new BoxShape3D { Size = size },
            Transform = new Transform3D(Basis.FromEuler(rotation), position)
        };
        shape.SetMeta("falltide_source_id", sourceId);
        shape.SetMeta("falltide_collision_purpose", purpose);
        return shape;
    }

    private static Dictionary<string, Node3D> BuildObjectiveAnchors(
        Node3D gameplayRoot,
        OrbitalComplexMapLayout layout)
    {
        var anchors = new Dictionary<string, Node3D>(StringComparer.Ordinal);
        foreach (var objective in layout.Objectives)
        {
            var anchor = new Node3D
            {
                Name = $"ObjectiveAnchor_{objective.Id}",
                Position = objective.Position,
                Rotation = new Vector3(0, objective.YawRadians, 0)
            };
            anchor.SetMeta("falltide_objective_id", objective.Id);
            anchor.SetMeta("falltide_objective_signal", objective.CompletionSignal);
            gameplayRoot.AddChild(anchor);
            if (!anchors.TryAdd(objective.Id, anchor))
            {
                throw new InvalidOperationException(
                    $"Duplicate Falltide objective id '{objective.Id}'.");
            }
        }
        return anchors;
    }

    private static Dictionary<string, OrbitalComplexGateRuntime> BuildPowerGates(
        Node3D gameplayRoot,
        Node3D authoredRoot,
        OrbitalComplexMapLayout layout)
    {
        var gates = new Dictionary<string, OrbitalComplexGateRuntime>(StringComparer.Ordinal);
        foreach (var definition in layout.PowerGates)
        {
            var body = new StaticBody3D
            {
                Name = $"PowerGate_{definition.Id}",
                CollisionLayer = 1,
                CollisionMask = 0
            };
            body.AddToGroup(GameplayCollisionGroup);
            var shape = CreateCollisionShape(
                "Collision",
                definition.Position,
                definition.Size,
                definition.RotationRadians,
                definition.Id,
                "power_gate");
            body.AddChild(shape);
            gameplayRoot.AddChild(body);
            var visual = authoredRoot.FindChild(
                definition.AuthoredVisualNodeName,
                recursive: true,
                owned: false) as Node3D;
            var runtime = new OrbitalComplexGateRuntime(definition, body, shape, visual);
            if (!gates.TryAdd(definition.Id, runtime))
            {
                throw new InvalidOperationException(
                    $"Duplicate Falltide power-gate id '{definition.Id}'.");
            }
        }
        return gates;
    }

    private static Dictionary<string, Node3D> FindPresentationNodes(Node3D authoredRoot)
    {
        var nodes = new Dictionary<string, Node3D>(StringComparer.Ordinal);
        foreach (var name in PresentationNodeNames)
        {
            if (authoredRoot.FindChild(name, recursive: true, owned: false) is Node3D node)
            {
                nodes.Add(name, node);
            }
        }
        return nodes;
    }
}
