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
        => ApplyIncomingDamageFeedback(
            appliedDamage,
            region,
            armorHit,
            attacker,
            hitPosition,
            DamageSource(attacker));

    private void ApplyIncomingDamageFeedback(
        float appliedDamage,
        HitRegion region,
        bool armorHit,
        Node? attacker,
        Vector3 hitPosition,
        (string Key, string English) source)
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
        Hud?.ShowIncomingDamage(
            appliedDamage,
            angle,
            region,
            armorHit,
            source.Key,
            source.English);
    }

    public void ApplyExtractionNetworkDamageFeedback(
        float appliedDamage,
        HitRegion region,
        Vector3 sourcePosition,
        ExtractionDamageSourceKind source)
    {
        if (!float.IsFinite(appliedDamage) || appliedDamage <= 0.0f)
        {
            return;
        }
        Main?.InterruptLootForIncomingDamage();
        CancelPlate();
        CancelMedicalUse();
        ApplyIncomingDamageFeedback(
            appliedDamage,
            region,
            armorHit: false,
            attacker: null,
            hitPosition: sourcePosition,
            source: DamageSource(source));
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

    private static (string Key, string English) DamageSource(ExtractionDamageSourceKind source)
        => source switch
        {
            ExtractionDamageSourceKind.EnemyOperator => ("damage_source_enemy", "ENEMY OPERATOR"),
            ExtractionDamageSourceKind.AircraftStrike => ("damage_source_aircraft", "AIRCRAFT STRIKE"),
            ExtractionDamageSourceKind.Explosion => ("damage_source_explosion", "EXPLOSIVE BLAST"),
            ExtractionDamageSourceKind.Vehicle => ("damage_source_vehicle", "VEHICLE IMPACT"),
            _ => ("damage_source_environment", "ENVIRONMENTAL IMPACT")
        };

    private void UpdateDamageKick(float delta)
    {
        _damageKickPitch = Mathf.Lerp(
            _damageKickPitch,
            0.0f,
            SmoothFactor(9.5f, delta));
        _damageKickRoll = Mathf.Lerp(
            _damageKickRoll,
            0.0f,
            SmoothFactor(11.0f, delta));
        _damageKickOffset = _damageKickOffset.Lerp(
            Vector3.Zero,
            SmoothFactor(12.0f, delta));
    }

    internal float IncomingDamageAngleForDiagnostics(Node? attacker, Vector3 hitPosition = default)
        => IncomingDamageAngle(attacker, hitPosition);
}
