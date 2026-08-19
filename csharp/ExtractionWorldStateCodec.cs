using System;
using System.IO;
using Godot;

namespace OperationSteelTide;

public static class ExtractionWorldStateCodec
{
    private const int FormatVersion = 1;
    private const int MaximumEntities = 512;

    public static byte[] Encode(ExtractionWorldNetworkState state)
    {
        using var stream = new MemoryStream(256 + state.Enemies.Length * 52 + state.Squad.Length * 56);
        using var writer = new BinaryWriter(stream);
        writer.Write(FormatVersion);
        writer.Write(state.Sequence);
        writer.Write(state.Enemies.Length);
        foreach (var enemy in state.Enemies)
        {
            writer.Write(enemy.NetworkId);
            writer.Write(enemy.TeamId);
            WriteVector(writer, enemy.Position);
            WriteVector(writer, enemy.Rotation);
            writer.Write(enemy.Health);
            writer.Write(enemy.WeaponPlatform);
            writer.Write(enemy.Flags);
        }
        writer.Write(state.Squad.Length);
        foreach (var member in state.Squad)
        {
            writer.Write(member.Slot);
            writer.Write(member.PeerId);
            writer.Write((int)member.Role);
            WriteVector(writer, member.Position);
            WriteVector(writer, member.Rotation);
            writer.Write(member.Health);
            writer.Write(member.Flags);
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static bool TryDecode(byte[] payload, out ExtractionWorldNetworkState state)
    {
        state = default;
        if (payload is null || payload.Length < 16)
        {
            return false;
        }
        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != FormatVersion)
            {
                return false;
            }
            var sequence = reader.ReadInt32();
            var enemyCount = reader.ReadInt32();
            if (enemyCount is < 0 or > MaximumEntities)
            {
                return false;
            }
            var enemies = new ExtractionEnemyNetworkState[enemyCount];
            for (var index = 0; index < enemyCount; index++)
            {
                enemies[index] = new ExtractionEnemyNetworkState(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    ReadVector(reader),
                    ReadVector(reader),
                    reader.ReadSingle(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }
            var squadCount = reader.ReadInt32();
            if (squadCount is < 0 or > SquadNetwork.ExtractionSquadCapacity)
            {
                return false;
            }
            var squad = new ExtractionSquadNetworkState[squadCount];
            for (var index = 0; index < squadCount; index++)
            {
                var slot = reader.ReadInt32();
                var peerId = reader.ReadInt64();
                var role = reader.ReadInt32();
                if (!Enum.IsDefined(typeof(OperatorRole), role))
                {
                    return false;
                }
                squad[index] = new ExtractionSquadNetworkState(
                    slot,
                    peerId,
                    (OperatorRole)role,
                    ReadVector(reader),
                    ReadVector(reader),
                    reader.ReadSingle(),
                    reader.ReadInt32());
            }
            if (stream.Position != stream.Length)
            {
                return false;
            }
            state = new ExtractionWorldNetworkState(sequence, enemies, squad);
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static Vector3 ReadVector(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}
