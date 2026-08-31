using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int MaximumActiveSmokeGrenades = 4;
    private readonly List<SmokeGrenade> _activeSmokeGrenades = new();

    public void ThrowSmokeGrenade(Vector3 origin, Vector3 direction, Node source)
        => ThrowSmokeGrenade(origin, direction, source, 14.0f, 5.0f);

    public void ThrowSmokeGrenade(
        Vector3 origin,
        Vector3 direction,
        Node source,
        float speed,
        float loft)
    {
        var grenade = new SmokeGrenade
        {
            Position = origin,
            OwnerBody = source
        };
        AddChild(grenade);
        grenade.Arm(direction, speed, loft);
        NotifyHostDemolitionUtilitySpawned(
            DemolitionNetworkUtilityKind.Smoke,
            origin,
            direction,
            source,
            speed,
            loft);
    }

    public bool IsLineObscuredBySmoke(Vector3 from, Vector3 to)
    {
        for (var index = _activeSmokeGrenades.Count - 1; index >= 0; index--)
        {
            var smoke = _activeSmokeGrenades[index];
            if (!IsInstanceValid(smoke))
            {
                _activeSmokeGrenades.RemoveAt(index);
                continue;
            }
            if (smoke.ObscuresSegment(from, to))
            {
                return true;
            }
        }
        return false;
    }

    internal void RegisterActiveSmokeGrenade(SmokeGrenade smoke)
    {
        if (_activeSmokeGrenades.Contains(smoke))
        {
            return;
        }
        for (var index = _activeSmokeGrenades.Count - 1; index >= 0; index--)
        {
            if (!IsInstanceValid(_activeSmokeGrenades[index]))
            {
                _activeSmokeGrenades.RemoveAt(index);
            }
        }
        while (_activeSmokeGrenades.Count >= MaximumActiveSmokeGrenades)
        {
            var oldest = _activeSmokeGrenades[0];
            _activeSmokeGrenades.RemoveAt(0);
            if (IsInstanceValid(oldest))
            {
                oldest.QueueFree();
            }
        }
        _activeSmokeGrenades.Add(smoke);
    }

    internal void UnregisterActiveSmokeGrenade(SmokeGrenade smoke)
        => _activeSmokeGrenades.Remove(smoke);
}
