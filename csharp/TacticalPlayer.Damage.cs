using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private float _damageKickPitch;
    private float _damageKickRoll;
    private Vector3 _damageKickOffset;

    public float DamageKickMagnitude => Mathf.Abs(_damageKickPitch)
        + Mathf.Abs(_damageKickRoll)
        + _damageKickOffset.Length();

    private void ApplyIncomingDamageFeedback(
        float appliedDamage,
        HitRegion region,
        bool armorHit,
        Node? attacker,
        Vector3 hitPosition)
    {
        var angle = IncomingDamageAngle(attacker, hitPosition);
        var intensity = Mathf.Clamp(appliedDamage / 38.0f, 0.32f, 1.0f);
        var side = Mathf.Sin(angle);
        if (Mathf.Abs(side) < 0.12f)
        {
            side = _rng.RandfRange(-0.4f, 0.4f);
        }
        _damageKickPitch -= Mathf.Lerp(0.025f, 0.085f, intensity);
        _damageKickRoll += -side * Mathf.Lerp(0.035f, 0.11f, intensity);
        _damageKickOffset += new Vector3(
            -side * Mathf.Lerp(0.018f, 0.055f, intensity),
            -Mathf.Lerp(0.012f, 0.042f, intensity),
            Mathf.Cos(angle) * Mathf.Lerp(0.008f, 0.028f, intensity));
        var (sourceKey, sourceEnglish) = DamageSource(attacker);
        Hud?.ShowIncomingDamage(appliedDamage, angle, region, armorHit, sourceKey, sourceEnglish);
    }

    private float IncomingDamageAngle(Node? attacker, Vector3 hitPosition)
    {
        Vector3 sourcePosition;
        if (attacker is Node3D sourceNode && IsInstanceValid(sourceNode) && sourceNode != this)
        {
            sourcePosition = sourceNode.GlobalPosition;
        }
        else if (!hitPosition.IsZeroApprox())
        {
            sourcePosition = hitPosition;
        }
        else
        {
            return 0.0f;
        }
        var direction = sourcePosition - GlobalPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() < 0.001f)
        {
            return 0.0f;
        }
        var local = GlobalBasis.Inverse() * direction.Normalized();
        return Mathf.Atan2(local.X, -local.Z);
    }

    private static (string Key, string English) DamageSource(Node? attacker) => attacker switch
    {
        EnemyOperator => ("damage_source_enemy", "ENEMY OPERATOR"),
        DestructibleAircraft or AircraftShell => ("damage_source_aircraft", "AIRCRAFT STRIKE"),
        ExplosiveBarrel or FragGrenade => ("damage_source_explosion", "EXPLOSIVE BLAST"),
        DriveableVehicle => ("damage_source_vehicle", "VEHICLE IMPACT"),
        _ => ("damage_source_environment", "ENVIRONMENTAL IMPACT")
    };

    private void UpdateDamageKick(float delta)
    {
        _damageKickPitch = Mathf.Lerp(_damageKickPitch, 0.0f, delta * 9.5f);
        _damageKickRoll = Mathf.Lerp(_damageKickRoll, 0.0f, delta * 11.0f);
        _damageKickOffset = _damageKickOffset.Lerp(Vector3.Zero, delta * 12.0f);
    }

    internal float IncomingDamageAngleForDiagnostics(Node? attacker, Vector3 hitPosition = default)
        => IncomingDamageAngle(attacker, hitPosition);
}
