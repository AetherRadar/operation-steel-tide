using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Deterministic pane-state helpers used by host-authoritative game modes. The
/// default remains locally authoritative so existing residential glass is unchanged.
/// </summary>
public partial class BreakableGlassField
{
    private bool _hasLocalShatterAuthority = true;
    private bool _worldOcclusionRequired;
    private bool _suppressPaneShatteredEvent;
    private readonly Dictionary<int, uint> _linkedShatterMaskByPane = new();

    /// <summary>Raised only for an actual local intact-to-shattered transition.</summary>
    public event Action<BreakableGlassField, int, uint>? PaneShattered;

    public bool HasLocalShatterAuthority => _hasLocalShatterAuthority;
    public bool WorldOcclusionRequired => _worldOcclusionRequired;

    public uint ShatteredPaneMask
    {
        get
        {
            var mask = 0u;
            var count = Math.Min(_panes.Count, sizeof(uint) * 8);
            for (var index = 0; index < count; index++)
            {
                if (_panes[index].Shattered)
                {
                    mask |= 1u << index;
                }
            }
            return mask;
        }
    }

    public uint ValidPaneMask
        => _panes.Count switch
        {
            <= 0 => 0u,
            >= sizeof(uint) * 8 => uint.MaxValue,
            _ => (1u << _panes.Count) - 1u
        };

    /// <summary>
    /// When disabled, local rays still report that intact glass stopped the attack,
    /// but only ApplyShatteredPaneMask may mutate pane state.
    /// </summary>
    public void SetLocalShatterAuthority(bool authoritative)
        => _hasLocalShatterAuthority = authoritative;

    /// <summary>
    /// Requires the glass area to be the first reachable hit along a shatter ray.
    /// This is opt-in because legacy residential panes intentionally overlap older
    /// facade collision; new traversable glass portals must enable it.
    /// </summary>
    public void SetWorldOcclusionRequired(bool required)
        => _worldOcclusionRequired = required;

    /// <summary>
    /// Links panes that represent one short portal threshold. Hitting any member
    /// shatters every intact member before a single final-mask event is emitted.
    /// Unlinked panes, including all existing residential panes, retain old behavior.
    /// </summary>
    public bool LinkShatterGroup(params int[] paneIndices)
    {
        if (_committed || paneIndices is null || paneIndices.Length < 2)
        {
            return false;
        }
        var mask = 0u;
        foreach (var paneIndex in paneIndices)
        {
            if (paneIndex < 0 || paneIndex >= _panes.Count || paneIndex >= sizeof(uint) * 8)
            {
                return false;
            }
            mask |= 1u << paneIndex;
        }
        if (System.Numerics.BitOperations.PopCount(mask) < 2)
        {
            return false;
        }
        foreach (var paneIndex in paneIndices)
        {
            _linkedShatterMaskByPane[paneIndex] = mask;
        }
        return true;
    }

    private uint ShatterMaskForPane(int paneIndex)
        => _linkedShatterMaskByPane.GetValueOrDefault(paneIndex, 1u << paneIndex);

    /// <summary>
    /// Applies an exact authoritative state without raising PaneShattered. The optional
    /// effect pane is the one transition allowed to produce a local shard burst.
    /// </summary>
    public bool ApplyShatteredPaneMask(
        uint mask,
        int effectPaneIndex = -1,
        Vector3 effectPosition = default,
        bool spawnEffects = false)
    {
        if (!_committed)
        {
            return false;
        }

        mask &= ValidPaneMask;
        var changed = false;
        _suppressPaneShatteredEvent = true;
        try
        {
            for (var index = 0; index < _panes.Count; index++)
            {
                var pane = _panes[index];
                var shouldBeShattered = index < sizeof(uint) * 8
                    && (mask & (1u << index)) != 0u;
                if (pane.Shattered == shouldBeShattered)
                {
                    continue;
                }

                changed = true;
                if (shouldBeShattered)
                {
                    var worldPosition = index == effectPaneIndex
                        && effectPosition.IsFinite()
                        ? effectPosition
                        : ToGlobal(pane.Position);
                    var localNormal = Vector3.Zero;
                    localNormal[ThinAxis(pane.Size)] = 1.0f;
                    var worldNormal = (GlobalBasis
                        * (Basis.FromEuler(pane.Rotation) * localNormal)).Normalized();
                    ShatterSinglePane(
                        index,
                        worldPosition,
                        worldNormal,
                        -worldNormal,
                        spawnEffects && index == effectPaneIndex,
                        requireFieldActive: false);
                    continue;
                }

                RestorePane(index);
            }
        }
        finally
        {
            _suppressPaneShatteredEvent = false;
        }

        RecountShatteredPanes();
        ApplyFieldCollisionState();
        return changed;
    }

    private void RestorePane(int paneIndex)
    {
        var pane = _panes[paneIndex];
        pane.Shattered = false;
        _glassMultiMesh?.SetInstanceTransform(paneIndex, PaneSurfaceTransform(pane));
    }

    private void RecountShatteredPanes()
    {
        var count = 0;
        foreach (var pane in _panes)
        {
            count += pane.Shattered ? 1 : 0;
        }
        ShatteredCount = count;
        if (count == 0)
        {
            LastShatterPosition = Vector3.Zero;
        }
    }

    private void NotifyPaneShattered(int paneIndex)
    {
        if (!_suppressPaneShatteredEvent)
        {
            PaneShattered?.Invoke(this, paneIndex, ShatteredPaneMask);
        }
    }

    public static bool TryFindIntactPaneAlongRay(
        World3D world,
        Vector3 from,
        Vector3 to,
        out BreakableGlassField? field,
        out int paneIndex,
        out Vector3 hitPosition,
        out Vector3 hitNormal)
    {
        field = null;
        paneIndex = -1;
        hitPosition = to;
        hitNormal = Vector3.Zero;
        if (world is null
            || !from.IsFinite()
            || !to.IsFinite()
            || !PhysicsRaycast.TryHit(
                world,
                from,
                to,
                GlassCollisionLayer,
                out var hit,
                collideWithAreas: true,
                collideWithBodies: false)
            || hit.Collider is not BreakableGlassField glass
            || !glass.TryResolveIntactPane(hit.Shape, out paneIndex))
        {
            return false;
        }

        field = glass;
        hitPosition = hit.Position;
        hitNormal = hit.Normal;
        return true;
    }

    private bool TryResolveIntactPane(int shapeIndex, out int paneIndex)
    {
        paneIndex = -1;
        if (!_committed || !_fieldActive || shapeIndex < 0)
        {
            return false;
        }
        var owner = ShapeFindOwner(shapeIndex);
        return _paneByShapeOwner.TryGetValue(owner, out paneIndex)
            && paneIndex >= 0
            && paneIndex < _panes.Count
            && !_panes[paneIndex].Shattered;
    }
}
