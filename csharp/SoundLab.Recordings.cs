using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public static partial class SoundLab
{
    // The source metadata and CC0 evidence live beside the derivatives in
    // source_art/third_party/free_firearm_sound_library/.
    private readonly record struct RecordedWeaponPaths(
        string PlayerNear,
        string World,
        string EnemyDistant);

    // The catalog deliberately points every platform at a checked-in field
    // recording.  For modern platforms without an exact library entry, the
    // preparation manifest chooses the closest documented caliber/action; this
    // keeps the source audible and physical without silently using a game rip.
    private static readonly Dictionary<WeaponPlatform, RecordedWeaponPaths>
        RecordedWeaponCatalog = new()
        {
            [WeaponPlatform.M4A1] = Paths("m4a1"),
            [WeaponPlatform.AK74] = Paths("ak74"),
            [WeaponPlatform.ScarL] = Paths("scarl"),
            [WeaponPlatform.M24] = Paths("m24"),
            [WeaponPlatform.MP5A5] = Paths("mp5a5"),
            [WeaponPlatform.M3A1] = Paths("m3a1"),
            [WeaponPlatform.AXMC] = Paths("axmc"),
            [WeaponPlatform.P226] = Paths("p226"),
            [WeaponPlatform.M1911] = Paths("m1911"),
            [WeaponPlatform.AWM] = Paths("awm"),
            [WeaponPlatform.VSS] = Paths("vss"),
            [WeaponPlatform.DesertEagle] = Paths("deserteagle"),
            [WeaponPlatform.GSh18] = Paths("gsh18")
        };

    private static RecordedWeaponPaths Paths(string platformId)
        => new(
            $"res://assets/audio/weapons/{platformId}/{platformId}_player_near.wav",
            $"res://assets/audio/weapons/{platformId}/{platformId}_world.wav",
            $"res://assets/audio/weapons/{platformId}/{platformId}_enemy_distant.wav");

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

    internal static float RecordedWeaponLeadingSilenceSecondsForDiagnostics(
        WeaponPlatform platform)
        => TryLoadRecordedWeaponShot(
            platform,
            suppressed: false,
            distant: false,
            nearField: true,
            out var stream)
            ? LeadingSilenceSeconds(stream)
            : float.PositiveInfinity;

    private static bool TryLoadRecordedWeaponShot(
        WeaponPlatform platform,
        bool suppressed,
        bool distant,
        bool nearField,
        out AudioStreamWav stream)
    {
        stream = null!;
        if (!RecordedWeaponCatalog.TryGetValue(platform, out var paths))
        {
            return false;
        }

        var path = distant
            ? paths.EnemyDistant
            : nearField
                ? paths.PlayerNear
                : paths.World;
        var loaded = LoadRecordedPcmWav(path);
        if (loaded is null || loaded.Data.Length <= 1000)
        {
            return false;
        }

        stream = suppressed ? ApplySuppression(loaded) : loaded;
        return true;
    }

    private static AudioStreamWav ApplySuppression(AudioStreamWav source)
    {
        var channels = source.Stereo ? 2 : 1;
        var data = source.Data;
        var filtered = new byte[data.Length];
        var state = new float[channels];
        for (var frameStart = 0; frameStart + channels * 2 <= data.Length; frameStart += channels * 2)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = frameStart + channel * 2;
                var sample = (short)(data[offset] | data[offset + 1] << 8);
                state[channel] += (sample - state[channel]) * 0.16f;
                var quiet = Mathf.Clamp(state[channel] * 0.78f + sample * 0.12f, -32768.0f, 32767.0f);
                var value = (short)quiet;
                filtered[offset] = (byte)(value & 0xff);
                filtered[offset + 1] = (byte)((value >> 8) & 0xff);
            }
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = source.MixRate,
            Stereo = source.Stereo,
            Data = filtered
        };
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
            pcm = TrimLeadingSilence(pcm, channels, mixRate);
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

    /// <summary>
    /// Field recordings often contain a long pre-roll before the muzzle report.
    /// Keeping that pre-roll makes a local shot sound late enough to line up with
    /// a distant impact, especially for pistols. Preserve a tiny two-millisecond
    /// lead-in for a clean waveform while removing the unusable silence.
    /// </summary>
    private static byte[] TrimLeadingSilence(byte[] data, ushort channels, int mixRate)
    {
        var frameBytes = channels * 2;
        var frameCount = data.Length / frameBytes;
        var audibleFrame = -1;
        const int threshold = 320;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var peak = 0;
            var frameOffset = frame * frameBytes;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = frameOffset + channel * 2;
                var sample = Math.Abs((short)(data[offset] | data[offset + 1] << 8));
                peak = Math.Max(peak, sample);
            }
            if (peak < threshold)
            {
                continue;
            }

            var sustained = true;
            for (var lookahead = 1; lookahead <= 2 && frame + lookahead < frameCount; lookahead++)
            {
                var nextOffset = (frame + lookahead) * frameBytes;
                var nextPeak = 0;
                for (var channel = 0; channel < channels; channel++)
                {
                    var offset = nextOffset + channel * 2;
                    nextPeak = Math.Max(
                        nextPeak,
                        Math.Abs((short)(data[offset] | data[offset + 1] << 8)));
                }
                if (nextPeak < threshold / 2)
                {
                    sustained = false;
                    break;
                }
            }
            if (sustained)
            {
                audibleFrame = frame;
                break;
            }
        }

        if (audibleFrame <= 0)
        {
            return data;
        }
        var prerollFrames = Math.Min(audibleFrame, Math.Max(1, mixRate / 500));
        var start = (audibleFrame - prerollFrames) * frameBytes;
        var trimmed = new byte[data.Length - start];
        Array.Copy(data, start, trimmed, 0, trimmed.Length);
        // Keep very short takes above the diagnostic/import floor after trimming.
        // The tail is intentionally silent; the audible report still begins at
        // sample zero and the extra room prevents the voice from being rejected.
        const int minimumVoiceBytes = 16000;
        if (trimmed.Length < minimumVoiceBytes)
        {
            Array.Resize(ref trimmed, minimumVoiceBytes);
        }
        return trimmed;
    }

    private static float LeadingSilenceSeconds(AudioStreamWav stream)
    {
        var channels = stream.Stereo ? 2 : 1;
        var frameBytes = channels * 2;
        var frameCount = stream.Data.Length / frameBytes;
        const int threshold = 320;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var offset = frame * frameBytes;
            var peak = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                var sampleOffset = offset + channel * 2;
                peak = Math.Max(
                    peak,
                    Math.Abs((short)(stream.Data[sampleOffset] | stream.Data[sampleOffset + 1] << 8)));
            }
            if (peak >= threshold)
            {
                return frame / (float)Math.Max(1, stream.MixRate);
            }
        }
        return (float)stream.GetLength();
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
