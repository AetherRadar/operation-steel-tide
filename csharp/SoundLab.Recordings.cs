using System;
using Godot;

namespace OperationSteelTide;

public static partial class SoundLab
{
    // The source metadata and CC0 evidence live beside the derivatives in
    // source_art/third_party/free_firearm_sound_library/.
    private const string Ak74PlayerNearRecordingPath =
        "res://assets/audio/weapons/ak74/ak74_player_near.wav";
    private const string Ak74WorldRecordingPath =
        "res://assets/audio/weapons/ak74/ak74_world.wav";
    private const string Ak74EnemyDistantRecordingPath =
        "res://assets/audio/weapons/ak74/ak74_enemy_distant.wav";

    internal static bool RecordedWeaponShotReadyForDiagnostics(
        WeaponPlatform platform,
        bool distant,
        bool nearField)
        => TryLoadRecordedWeaponShot(
            platform,
            suppressed: false,
            distant: distant,
            nearField: nearField,
            out _);

    private static bool TryLoadRecordedWeaponShot(
        WeaponPlatform platform,
        bool suppressed,
        bool distant,
        bool nearField,
        out AudioStreamWav stream)
    {
        stream = null!;
        if (platform != WeaponPlatform.AK74 || suppressed)
        {
            return false;
        }

        var path = distant
            ? Ak74EnemyDistantRecordingPath
            : nearField
                ? Ak74PlayerNearRecordingPath
                : Ak74WorldRecordingPath;
        var loaded = LoadRecordedPcmWav(path);
        if (loaded is null || loaded.Data.Length <= 1000)
        {
            return false;
        }

        stream = loaded;
        return true;
    }

    private static AudioStreamWav? LoadRecordedPcmWav(string path)
    {
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            return null;
        }

        try
        {
            var fileLength = file.GetLength();
            if (fileLength > int.MaxValue)
            {
                return null;
            }
            var bytes = file.GetBuffer((long)fileLength);
            if (bytes.Length < 12
                || ReadFourCc(bytes, 0) != "RIFF"
                || ReadFourCc(bytes, 8) != "WAVE")
            {
                return null;
            }

            ushort format = 0;
            ushort channels = 0;
            ushort bitsPerSample = 0;
            var mixRate = 0;
            var dataOffset = -1;
            var dataLength = 0;
            for (var cursor = 12; cursor + 8 <= bytes.Length;)
            {
                var chunkId = ReadFourCc(bytes, cursor);
                var chunkLength = ReadUInt32(bytes, cursor + 4);
                var chunkStart = cursor + 8L;
                var available = bytes.Length - chunkStart;
                if (available < 0)
                {
                    break;
                }

                if (chunkId == "fmt " && chunkLength >= 16 && available >= 16)
                {
                    format = ReadUInt16(bytes, (int)chunkStart);
                    channels = ReadUInt16(bytes, (int)chunkStart + 2);
                    mixRate = ReadInt32(bytes, (int)chunkStart + 4);
                    bitsPerSample = ReadUInt16(bytes, (int)chunkStart + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = (int)chunkStart;
                    dataLength = (int)Math.Min((long)chunkLength, available);
                }

                var next = chunkStart + chunkLength + (chunkLength & 1u);
                if (next > int.MaxValue || next <= cursor)
                {
                    break;
                }
                cursor = (int)next;
            }

            if (format != 1
                || channels is < 1 or > 2
                || mixRate <= 0
                || bitsPerSample != 16
                || dataOffset < 0
                || dataLength < 2)
            {
                return null;
            }

            // Read the checked-in derivative as raw PCM instead of asking
            // ResourceLoader for an imported stream. This keeps Godot's
            // optional ADPCM import setting from changing the licensed take.
            var pcm = new byte[dataLength];
            Array.Copy(bytes, dataOffset, pcm, 0, dataLength);
            return new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = mixRate,
                Stereo = channels == 2,
                Data = pcm
            };
        }
        finally
        {
            file.Close();
        }
    }

    private static string ReadFourCc(byte[] bytes, int offset)
        => offset < 0 || offset + 4 > bytes.Length
            ? string.Empty
            : string.Create(
                4,
                (bytes, offset),
                static (destination, state) =>
                {
                    for (var index = 0; index < 4; index++)
                    {
                        destination[index] = (char)state.bytes[state.offset + index];
                    }
                });

    private static ushort ReadUInt16(byte[] bytes, int offset)
        => (ushort)(bytes[offset] | bytes[offset + 1] << 8);

    private static uint ReadUInt32(byte[] bytes, int offset)
        => (uint)(bytes[offset]
            | bytes[offset + 1] << 8
            | bytes[offset + 2] << 16
            | bytes[offset + 3] << 24);

    private static int ReadInt32(byte[] bytes, int offset)
        => unchecked((int)ReadUInt32(bytes, offset));
}
