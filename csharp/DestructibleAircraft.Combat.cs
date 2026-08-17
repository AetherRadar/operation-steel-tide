using Godot;

namespace OperationSteelTide;

public partial class DestructibleAircraft
{
    public bool IsAttackOrbitActive { get; private set; }
    public float AttackHorizontalDistance { get; private set; } = float.PositiveInfinity;
    public float LastAttackAngleDegrees { get; private set; } = 90.0f;
    public bool LastAttackPathClear { get; private set; }

    public const float AttackAltitude = 36.0f;
    public const float AttackOrbitRadius = 9.0f;
    public const float MaximumAttackAngleDegrees = 20.0f;
    private const float EngageRange = 118.0f;
    private const float AttackCruiseSpeed = 21.0f;
    private const float AttackOrbitAngularSpeed = 0.75f;
    private const float AttackFireRadius = AttackOrbitRadius + 3.0f;
    private const float MinimumAttackVerticalDrop = 24.0f;
    private const float AttackLeadSeconds = 0.16f;
    private const float AttackHorizontalSpread = 0.7f;
    private const float OpenSkyProbeHeight = 52.0f;
    private const float FireCooldown = 3.1f;
    private const float ShellDamage = 48.0f;
    private const float ShellBlastRadius = 12.5f;

    private float _attackOrbitPhase;
    private float _fireCooldown;
    private float _acquireTimer;
    private Node3D? _currentTarget;

    internal void SetAttackTargetForDiagnostics(Node3D target, Vector3 horizontalOffset)
    {
        _currentTarget = target;
        _acquireTimer = 10.0f;
        _fireCooldown = Mathf.Max(_fireCooldown, 10.0f);
        IsAttackOrbitActive = false;
        _rejoiningPatrol = false;
        GlobalPosition = target.GlobalPosition + horizontalOffset + Vector3.Up * AttackAltitude;
        LastPatrolStepDistance = 0.0f;
    }

    private void UpdateTargeting(float dt)
    {
        _fireCooldown = Mathf.Max(0.0f, _fireCooldown - dt);
        _acquireTimer -= dt;
        if (_acquireTimer <= 0.0f)
        {
            _acquireTimer = 0.45f;
            _currentTarget = AcquireTarget();
        }
    }

    private void UpdateCombat()
    {
        if (_currentTarget is null
            || !GodotObject.IsInstanceValid(_currentTarget)
            || !IsAttackOrbitActive
            || AttackHorizontalDistance > AttackFireRadius)
        {
            return;
        }

        if (_fireCooldown <= 0.0f)
        {
            FireAt(_currentTarget);
        }
    }

    private Node3D? AcquireTarget()
    {
        if (Main is null || !IsHostile)
        {
            return null;
        }

        Node3D? best = null;
        var bestDistance = EngageRange;
        foreach (var combatant in Main.GetHostileAircraftTargets())
        {
            if (combatant is null || !GodotObject.IsInstanceValid(combatant))
            {
                continue;
            }
            var distance = GlobalPosition.DistanceTo(combatant.GlobalPosition);
            if (distance < bestDistance && HasOpenSkyAbove(combatant.GlobalPosition, combatant))
            {
                bestDistance = distance;
                best = combatant;
            }
        }
        return best;
    }

    private bool FireAt(Node3D target)
    {
        if (Main is null || !TryBuildAttackSolution(target, out var muzzle, out var aim))
        {
            return false;
        }

        var damage = ShellDamage * _rng.RandfRange(0.92f, 1.08f);
        LastAttackDamage = damage;
        AttackSalvosFired++;
        _fireCooldown = FireCooldown;

        // Physical bomb: deliberately slow enough to dodge, but impossible to shoot down.
        Main.SpawnAircraftShell(muzzle, aim, damage, ShellBlastRadius, this);
        Main.SpawnTracer(muzzle, aim, new Color(1.0f, 0.4f, 0.12f));
        return true;
    }

    private bool TryBuildAttackSolution(Node3D target, out Vector3 muzzle, out Vector3 aim)
    {
        muzzle = GlobalPosition + Vector3.Down * 1.9f + _flightDirection * 2.2f;
        aim = target.GlobalPosition + Vector3.Up * 0.4f;
        if (target is CharacterBody3D body)
        {
            aim += body.Velocity * AttackLeadSeconds;
        }
        aim += new Vector3(
            _rng.RandfRange(-AttackHorizontalSpread, AttackHorizontalSpread),
            _rng.RandfRange(-0.05f, 0.18f),
            _rng.RandfRange(-AttackHorizontalSpread, AttackHorizontalSpread));

        var toAim = aim - muzzle;
        var verticalDrop = -toAim.Y;
        var horizontalDistance = new Vector2(toAim.X, toAim.Z).Length();
        LastAttackAngleDegrees = Mathf.RadToDeg(Mathf.Atan2(horizontalDistance, Mathf.Max(0.001f, verticalDrop)));
        LastAttackPathClear = false;
        if (verticalDrop < MinimumAttackVerticalDrop
            || LastAttackAngleDegrees > MaximumAttackAngleDegrees
            || !HasOpenSkyAbove(aim, target)
            || !HasClearAttackPath(muzzle, aim, target))
        {
            return false;
        }

        LastAttackPathClear = true;
        return true;
    }

    private bool HasOpenSkyAbove(Vector3 point, Node3D target)
    {
        if (!IsInsideTree())
        {
            return false;
        }

        var query = PhysicsRayQueryParameters3D.Create(
            point + Vector3.Up * 0.8f,
            point + Vector3.Up * OpenSkyProbeHeight);
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        query.Exclude = AttackQueryExclusions(target);
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    private bool HasClearAttackPath(Vector3 from, Vector3 to, Node3D target)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1 | 4;
        query.CollideWithAreas = false;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return true;
        }
        return hit["collider"].AsGodotObject() == target;
    }

    private Godot.Collections.Array<Rid> AttackQueryExclusions(Node3D target)
    {
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (target is CollisionObject3D collisionTarget)
        {
            exclude.Add(collisionTarget.GetRid());
        }
        return exclude;
    }

    private void UpdateAttackFlight(Node3D target, float dt)
    {
        var parent = GetParent() as Node3D;
        var targetPosition = parent?.ToLocal(target.GlobalPosition) ?? target.GlobalPosition;
        if (!IsAttackOrbitActive)
        {
            var relative = Position - targetPosition;
            _attackOrbitPhase = new Vector2(relative.X, relative.Z).LengthSquared() > 0.01f
                ? Mathf.Atan2(relative.Z, relative.X)
                : 0.0f;
            IsAttackOrbitActive = true;
            _rejoiningPatrol = false;
        }

        _attackOrbitPhase = Mathf.PosMod(
            _attackOrbitPhase + AttackOrbitAngularSpeed * dt,
            Mathf.Tau);
        var desiredPosition = new Vector3(
            targetPosition.X + Mathf.Cos(_attackOrbitPhase) * AttackOrbitRadius,
            Mathf.Max(PatrolAltitude, targetPosition.Y + AttackAltitude),
            targetPosition.Z + Mathf.Sin(_attackOrbitPhase) * AttackOrbitRadius);
        ApplyFlightStep(
            Position.MoveToward(desiredPosition, AttackCruiseSpeed * dt),
            countPatrolDistance: false,
            dt);
        AttackHorizontalDistance = new Vector2(
            GlobalPosition.X - target.GlobalPosition.X,
            GlobalPosition.Z - target.GlobalPosition.Z).Length();
    }
}
