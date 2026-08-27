using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record JianghaiOldCitySceneLoadResult(
    Node3D Root,
    string ScenePath,
    int MeshInstanceCount,
    int SurfaceCount,
    int MaterialSurfaceCount,
    long InstanceTriangleCount,
    int RequiredAnchorCount,
    int RequiredAnchorTotal,
    int AuthoredTerminalCount,
    int VisibleAuthoredTerminalCount,
    int AlignedAuthoredTerminalCount,
    int AuthoredTerminalTotal,
    int AuthoredStatusScreenCount,
    int AuthoredStatusScreenTotal,
    int DetailMeshCount,
    IReadOnlyDictionary<string, Aabb> AuthoredTerminalWorldBounds)
{
    public int QualityTier { get; internal set; } = 2;
    public int ShadowCasterMeshCount { get; internal set; }
}

/// <summary>Owns the single runtime instance of the DCC-authored Jianghai old-city scene.</summary>
internal sealed class JianghaiOldCitySceneLoader
{
    private const int DefaultQualityTier = 2;

    public const string DefaultScenePath =
        "res://assets/models/jianghai_old_city/jianghai_old_city.glb";
    public const string AuthoredSceneGroup = "jianghai_old_city_authored_scene";

    private static readonly Color CompletedScreenAlbedo = new(0.04f, 0.78f, 0.34f);
    private static readonly Color CompletedScreenEmission = new(0.03f, 1.0f, 0.42f);

    private static readonly HashSet<string> RequiredAnchorNames = new(StringComparer.Ordinal)
    {
        "AuthoredStreetNetwork",
        "JianghaiTenementDistrict",
        "RedStarElectronicsFactory",
        "GuangchangPawnshop",
        "OldCityMarketBridge",
        "GrandHotelSecurityTerminalVisual",
        "MunicipalTreasuryManifestTerminalVisual"
    };
    private static readonly Dictionary<string, Vector3> ExpectedTerminalPositions = new(StringComparer.Ordinal)
    {
        ["GrandHotelSecurityTerminalVisual"] = new Vector3(-86.0f, 0.0f, -107.0f),
        ["MunicipalTreasuryManifestTerminalVisual"] = new Vector3(86.0f, 0.0f, -13.0f)
    };
    private static readonly Dictionary<string, string> ExpectedTerminalScreens = new(StringComparer.Ordinal)
    {
        ["GrandHotelSecurityTerminalVisual"] = "GrandHotelSecurityTerminalVisual_AuthoredStatusScreen",
        ["MunicipalTreasuryManifestTerminalVisual"] = "MunicipalTreasuryManifestTerminalVisual_AuthoredStatusScreen"
    };
    private static readonly Dictionary<string, Vector3> ExpectedTerminalFacing = new(StringComparer.Ordinal)
    {
        ["GrandHotelSecurityTerminalVisual"] = Vector3.Back,
        ["MunicipalTreasuryManifestTerminalVisual"] = Vector3.Forward
    };
    private static readonly Dictionary<string, int> AuthoredStatusScreenIndices = new(StringComparer.Ordinal)
    {
        ["GrandHotelSecurityTerminalVisual_AuthoredStatusScreen"] = 0,
        ["MunicipalTreasuryManifestTerminalVisual_AuthoredStatusScreen"] = 1
    };

    private readonly string _scenePath;
    private readonly List<MeshQualityProfile> _meshQualityProfiles = new();
    private readonly List<TerminalScreenMaterialBinding>[] _terminalScreenBindings =
    {
        new(),
        new()
    };
    private JianghaiOldCitySceneLoadResult? _loadedScene;
    private int _qualityTier = DefaultQualityTier;

    public JianghaiOldCitySceneLoader(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            throw new ArgumentException("An authored city scene path is required.", nameof(scenePath));
        }

        _scenePath = scenePath;
    }

    public JianghaiOldCitySceneLoadResult LoadOnce(Node3D parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (_loadedScene is { } existing && GodotObject.IsInstanceValid(existing.Root))
        {
            if (existing.Root.GetParent() != parent)
            {
                throw new InvalidOperationException(
                    "The Jianghai old-city scene is already owned by a different parent.");
            }

            ApplyQuality(_qualityTier);
            return existing;
        }

        _meshQualityProfiles.Clear();
        ClearTerminalScreenBindings();

        var packedScene = GD.Load<PackedScene>(_scenePath)
            ?? throw new InvalidOperationException(
                $"Unable to load the authored Jianghai old-city scene at '{_scenePath}'.");
        var instance = packedScene.Instantiate();
        if (instance is not Node3D cityRoot)
        {
            instance.Free();
            throw new InvalidOperationException(
                $"The authored Jianghai old-city scene at '{_scenePath}' must have a Node3D root.");
        }

        cityRoot.Name = "JianghaiOldCityAuthored";
        cityRoot.AddToGroup(AuthoredSceneGroup);
        parent.AddChild(cityRoot);

        var statistics = new SceneStatistics();
        InspectScene(cityRoot, statistics);
        var terminalCount = 0;
        var visibleTerminalCount = 0;
        var alignedTerminalCount = 0;
        var terminalWorldBounds = new Dictionary<string, Aabb>(StringComparer.Ordinal);
        foreach (var expected in ExpectedTerminalPositions)
        {
            if (!statistics.AnchorNodes.TryGetValue(expected.Key, out var terminal))
            {
                continue;
            }

            terminalCount++;
            if (HasVisibleMesh(terminal))
            {
                visibleTerminalCount++;
            }
            terminalWorldBounds[expected.Key] = CalculateVisibleMeshWorldBounds(terminal);
            MeshInstance3D? screen = null;
            var screenReady = ExpectedTerminalScreens.TryGetValue(expected.Key, out var screenName)
                && statistics.AuthoredStatusScreenNodes.TryGetValue(screenName, out screen);
            var screenOffset = screenReady && screen is not null
                ? screen.GlobalPosition - terminal.GlobalPosition
                : Vector3.Zero;
            screenOffset.Y = 0.0f;
            var facingReady = screenOffset.LengthSquared() > 0.001f
                && ExpectedTerminalFacing.TryGetValue(expected.Key, out var expectedFacing)
                && screenOffset.Normalized().Dot(expectedFacing) >= 0.95f;
            if (terminal.GlobalPosition.DistanceTo(expected.Value) <= 0.05f && facingReady)
            {
                alignedTerminalCount++;
            }
        }
        _loadedScene = new JianghaiOldCitySceneLoadResult(
            cityRoot,
            _scenePath,
            statistics.MeshInstanceCount,
            statistics.SurfaceCount,
            statistics.MaterialSurfaceCount,
            statistics.InstanceTriangleCount,
            statistics.RequiredAnchors.Count,
            RequiredAnchorNames.Count,
            terminalCount,
            visibleTerminalCount,
            alignedTerminalCount,
            ExpectedTerminalPositions.Count,
            statistics.AuthoredStatusScreens.Count,
            AuthoredStatusScreenIndices.Count,
            statistics.DetailMeshCount,
            terminalWorldBounds);
        ApplyQuality(_qualityTier);
        ResetTerminalStatuses();
        return _loadedScene;
    }

    public void ReleaseReferences()
    {
        _meshQualityProfiles.Clear();
        ClearTerminalScreenBindings();
        _loadedScene = null;
    }

    /// <summary>Applies authored-city visibility and shadow policy for the selected quality tier.</summary>
    public void ApplyQuality(int qualityTier)
    {
        _qualityTier = Mathf.Clamp(qualityTier, 0, 2);
        var distanceScale = _qualityTier switch
        {
            0 => 0.68f,
            1 => 0.84f,
            _ => 1.0f
        };
        var shadowCasterCount = 0;
        foreach (var profile in _meshQualityProfiles)
        {
            if (!GodotObject.IsInstanceValid(profile.MeshInstance))
            {
                continue;
            }

            var meshInstance = profile.MeshInstance;
            var endDistance = profile.BaseVisibilityRange * distanceScale;
            meshInstance.VisibilityRangeEnd = endDistance;
            meshInstance.VisibilityRangeEndMargin = Mathf.Min(28.0f, endDistance * 0.12f);
            meshInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;

            var allowAuthoredShadow = _qualityTier switch
            {
                0 => !profile.IsDetail,
                1 => !profile.IsFineDetail && profile.WorldDiagonal > 4.0f,
                _ => !profile.IsFineDetail
            };
            meshInstance.CastShadow = profile.AlwaysDisableShadow || !allowAuthoredShadow
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : profile.AuthoredShadowSetting;
            if (meshInstance.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off)
            {
                shadowCasterCount++;
            }
        }

        if (_loadedScene is not null)
        {
            _loadedScene.QualityTier = _qualityTier;
            _loadedScene.ShadowCasterMeshCount = shadowCasterCount;
        }
    }

    /// <summary>Marks one authored objective terminal complete without sharing its material state.</summary>
    public void SetTerminalCompleted(int terminalIndex)
    {
        if (terminalIndex < 0 || terminalIndex >= _terminalScreenBindings.Length)
        {
            return;
        }

        SetTerminalScreenState(terminalIndex, completed: true);
    }

    /// <summary>Applies an authoritative completed-objective count to both authored terminal screens.</summary>
    public void ApplyTerminalStatuses(int completedCount)
    {
        var normalizedCount = Math.Clamp(completedCount, 0, _terminalScreenBindings.Length);
        for (var terminalIndex = 0;
             terminalIndex < _terminalScreenBindings.Length;
             terminalIndex++)
        {
            SetTerminalScreenState(terminalIndex, terminalIndex < normalizedCount);
        }
    }

    /// <summary>Restores the authored materials captured when the city scene was instantiated.</summary>
    public void ResetTerminalStatuses()
        => ApplyTerminalStatuses(0);

    /// <summary>Returns a read-only snapshot of the material state applied to each authored screen.</summary>
    public IReadOnlyList<bool> TerminalCompletionStates
    {
        get
        {
            var states = new bool[_terminalScreenBindings.Length];
            for (var terminalIndex = 0;
                 terminalIndex < _terminalScreenBindings.Length;
                 terminalIndex++)
            {
                var bindings = _terminalScreenBindings[terminalIndex];
                states[terminalIndex] = bindings.Count > 0;
                foreach (var binding in bindings)
                {
                    states[terminalIndex] &= binding.IsCompleted;
                }
            }
            return Array.AsReadOnly(states);
        }
    }

    private void InspectScene(Node node, SceneStatistics statistics)
    {
        var nodeName = node.Name.ToString();
        if (RequiredAnchorNames.Contains(nodeName))
        {
            statistics.RequiredAnchors.Add(nodeName);
            if (node is Node3D anchor)
            {
                statistics.AnchorNodes[nodeName] = anchor;
            }
        }

        if (node is MeshInstance3D { Mesh: { } mesh } meshInstance)
        {
            statistics.MeshInstanceCount++;
            var profile = CreateQualityProfile(meshInstance);
            _meshQualityProfiles.Add(profile);
            if (profile.IsDetail)
            {
                statistics.DetailMeshCount++;
            }
            var surfaceCount = mesh.GetSurfaceCount();
            statistics.SurfaceCount += surfaceCount;
            for (var surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
            {
                if (mesh is ArrayMesh arrayMesh)
                {
                    var indexCount = arrayMesh.SurfaceGetArrayIndexLen(surfaceIndex);
                    var vertexCount = arrayMesh.SurfaceGetArrayLen(surfaceIndex);
                    statistics.InstanceTriangleCount += (indexCount > 0 ? indexCount : vertexCount) / 3;
                }
                if (mesh.SurfaceGetMaterial(surfaceIndex) is not null)
                {
                    statistics.MaterialSurfaceCount++;
                }
            }

            if (AuthoredStatusScreenIndices.TryGetValue(nodeName, out var terminalIndex)
                && BindTerminalStatusScreen(meshInstance, terminalIndex))
            {
                statistics.AuthoredStatusScreens.Add(nodeName);
                statistics.AuthoredStatusScreenNodes[nodeName] = meshInstance;
            }
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                InspectScene(childNode, statistics);
            }
        }
    }

    private static MeshQualityProfile CreateQualityProfile(MeshInstance3D meshInstance)
    {
        var localSize = meshInstance.GetAabb().Size;
        var globalScale = meshInstance.GlobalTransform.Basis.Scale.Abs();
        var worldSize = new Vector3(
            localSize.X * globalScale.X,
            localSize.Y * globalScale.Y,
            localSize.Z * globalScale.Z);
        var diagonal = worldSize.Length();
        var baseVisibilityRange = diagonal switch
        {
            <= 1.2f => 105.0f,
            <= 4.0f => 180.0f,
            <= 12.0f => 285.0f,
            _ => 460.0f
        };
        var name = meshInstance.Name.ToString();
        var isFineDetail = diagonal <= 1.2f
            || ContainsAny(name, "Screen", "Indicator", "Fastener", "Text", "Cable", "Lens");
        var isDetail = diagonal <= 12.0f
            || ContainsAny(
                name,
                "Aircon",
                "Rollershutter",
                "Trashbag",
                "UtilityBox",
                "Barrel",
                "Crate",
                "SecurityCamera",
                "Television");
        var alwaysDisableShadow = diagonal <= 0.45f
            || ContainsAny(name, "ScreenTrace", "StatusScreen", "Indicator", "Fastener", "Text");
        return new MeshQualityProfile(
            meshInstance,
            diagonal,
            baseVisibilityRange,
            isDetail,
            isFineDetail,
            alwaysDisableShadow,
            meshInstance.CastShadow);
    }

    private bool BindTerminalStatusScreen(MeshInstance3D screen, int terminalIndex)
    {
        if (terminalIndex < 0 || terminalIndex >= _terminalScreenBindings.Length
            || screen.Mesh is not { } mesh)
        {
            return false;
        }

        var surfaceCount = mesh.GetSurfaceCount();
        if (surfaceCount <= 0)
        {
            return false;
        }

        var bindings = _terminalScreenBindings[terminalIndex];
        for (var surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
        {
            var sourceMaterial = screen.GetActiveMaterial(surfaceIndex) as StandardMaterial3D;
            var localMaterial = sourceMaterial?.Duplicate() as StandardMaterial3D
                ?? new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.18f, 0.08f, 0.035f),
                    EmissionEnabled = true,
                    Emission = new Color(0.75f, 0.16f, 0.035f),
                    EmissionEnergyMultiplier = 1.6f
                };
            screen.SetSurfaceOverrideMaterial(surfaceIndex, localMaterial);
            bindings.Add(new TerminalScreenMaterialBinding(localMaterial));
        }
        return bindings.Count > 0;
    }

    private void SetTerminalScreenState(int terminalIndex, bool completed)
    {
        foreach (var binding in _terminalScreenBindings[terminalIndex])
        {
            binding.SetCompleted(completed);
        }
    }

    private void ClearTerminalScreenBindings()
    {
        foreach (var bindings in _terminalScreenBindings)
        {
            bindings.Clear();
        }
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasVisibleMesh(Node node)
    {
        if (node is MeshInstance3D { Visible: true, Mesh: not null })
        {
            return true;
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode && HasVisibleMesh(childNode))
            {
                return true;
            }
        }
        return false;
    }

    private static Aabb CalculateVisibleMeshWorldBounds(Node node)
    {
        var initialized = false;
        var minimum = Vector3.Zero;
        var maximum = Vector3.Zero;
        AccumulateVisibleMeshWorldBounds(node, ref initialized, ref minimum, ref maximum);
        if (!initialized)
        {
            throw new InvalidOperationException(
                $"Authored terminal '{node.Name}' has no visible mesh bounds.");
        }
        return new Aabb(minimum, maximum - minimum);
    }

    private static void AccumulateVisibleMeshWorldBounds(
        Node node,
        ref bool initialized,
        ref Vector3 minimum,
        ref Vector3 maximum)
    {
        if (node is MeshInstance3D { Visible: true, Mesh: not null } meshInstance)
        {
            var localBounds = meshInstance.GetAabb();
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    for (var z = 0; z <= 1; z++)
                    {
                        var corner = localBounds.Position + new Vector3(
                            localBounds.Size.X * x,
                            localBounds.Size.Y * y,
                            localBounds.Size.Z * z);
                        var worldCorner = meshInstance.GlobalTransform * corner;
                        if (!initialized)
                        {
                            minimum = worldCorner;
                            maximum = worldCorner;
                            initialized = true;
                        }
                        else
                        {
                            minimum = minimum.Min(worldCorner);
                            maximum = maximum.Max(worldCorner);
                        }
                    }
                }
            }
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                AccumulateVisibleMeshWorldBounds(
                    childNode,
                    ref initialized,
                    ref minimum,
                    ref maximum);
            }
        }
    }

    private sealed class SceneStatistics
    {
        public int MeshInstanceCount;
        public int SurfaceCount;
        public int MaterialSurfaceCount;
        public long InstanceTriangleCount;
        public int DetailMeshCount;
        public HashSet<string> RequiredAnchors { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Node3D> AnchorNodes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> AuthoredStatusScreens { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, MeshInstance3D> AuthoredStatusScreenNodes { get; } = new(StringComparer.Ordinal);
    }

    private sealed record MeshQualityProfile(
        MeshInstance3D MeshInstance,
        float WorldDiagonal,
        float BaseVisibilityRange,
        bool IsDetail,
        bool IsFineDetail,
        bool AlwaysDisableShadow,
        GeometryInstance3D.ShadowCastingSetting AuthoredShadowSetting);

    private sealed class TerminalScreenMaterialBinding
    {
        private readonly StandardMaterial3D _material;
        private readonly Color _authoredAlbedo;
        private readonly bool _authoredEmissionEnabled;
        private readonly Color _authoredEmission;
        private readonly float _authoredEmissionEnergy;

        public bool IsCompleted { get; private set; }

        public TerminalScreenMaterialBinding(StandardMaterial3D material)
        {
            _material = material;
            _authoredAlbedo = material.AlbedoColor;
            _authoredEmissionEnabled = material.EmissionEnabled;
            _authoredEmission = material.Emission;
            _authoredEmissionEnergy = material.EmissionEnergyMultiplier;
        }

        public void SetCompleted(bool completed)
        {
            IsCompleted = completed;
            if (!completed)
            {
                _material.AlbedoColor = _authoredAlbedo;
                _material.EmissionEnabled = _authoredEmissionEnabled;
                _material.Emission = _authoredEmission;
                _material.EmissionEnergyMultiplier = _authoredEmissionEnergy;
                return;
            }

            _material.AlbedoColor = new Color(
                CompletedScreenAlbedo.R,
                CompletedScreenAlbedo.G,
                CompletedScreenAlbedo.B,
                _authoredAlbedo.A);
            _material.EmissionEnabled = true;
            _material.Emission = CompletedScreenEmission;
            _material.EmissionEnergyMultiplier = Mathf.Max(3.2f, _authoredEmissionEnergy);
        }
    }
}
