using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public static class SoundLab
{
    private readonly record struct WeaponShotRecipe(
        float Duration,
        float CrackLevel,
        float CrackDecay,
        float PressureLevel,
        float PressureFrequency,
        float PressureDecay,
        float TailLevel,
        float TailDecay,
        float MechanicalLevel,
        float MechanicalFrequency,
        int Seed);

    private static readonly Dictionary<(WeaponPlatform Platform, bool Suppressed, bool Distant), AudioStreamWav>
        WeaponShotCache = new();
    private static readonly Dictionary<(MeleeWeaponStyle Style, int AttackIndex), AudioStreamWav>
        MeleeSwingCache = new();

    private static AudioStreamWav MakeStream(float[] samples, int rate = 22050)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)(Mathf.Clamp(samples[i], -1.0f, 1.0f) * short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xff);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = bytes
        };
    }

    public static AudioStreamWav Gunshot()
        => WeaponShot(WeaponPlatform.M4A1);

    public static AudioStreamWav DesertEagleShot()
        => WeaponShot(WeaponPlatform.DesertEagle);

    public static AudioStreamWav Gsh18Shot()
        => WeaponShot(WeaponPlatform.GSh18);

    public static AudioStreamWav EnemyShot()
        => WeaponShot(WeaponPlatform.M4A1, suppressed: false, distant: true);

    public static AudioStreamWav WeaponShot(WeaponBuild build, bool distant = false)
    {
        var suppressed = IsSuppressed(build);
        return WeaponShot(build.Platform, suppressed, distant);
    }

    public static AudioStreamWav WeaponShot(
        WeaponPlatform platform,
        bool suppressed = false,
        bool distant = false)
    {
        suppressed |= platform == WeaponPlatform.VSS;
        var key = (platform, suppressed, distant);
        if (!WeaponShotCache.TryGetValue(key, out var stream))
        {
            stream = BuildWeaponShot(WeaponShotRecipeFor(platform, suppressed), distant);
            WeaponShotCache[key] = stream;
        }
        return stream;
    }

    public static bool IsSuppressed(WeaponBuild build)
        => build.Platform == WeaponPlatform.VSS
        || build.Attachments.TryGetValue(AttachmentSlot.Muzzle, out var muzzleId)
            && muzzleId == "muzzle_suppressor";

    public static float WeaponShotVolumeDb(WeaponBuild build, bool distant = false)
    {
        var volume = build.Platform switch
        {
            WeaponPlatform.AXMC or WeaponPlatform.AWM => -1.0f,
            WeaponPlatform.M24 or WeaponPlatform.DesertEagle => -2.0f,
            WeaponPlatform.AK74 or WeaponPlatform.ScarL or WeaponPlatform.M1911 => -3.5f,
            WeaponPlatform.M3A1 => -4.5f,
            WeaponPlatform.MP5A5 or WeaponPlatform.P226 or WeaponPlatform.GSh18 => -5.0f,
            WeaponPlatform.VSS => -7.0f,
            _ => -4.0f
        };
        if (IsSuppressed(build))
        {
            volume -= 3.0f;
        }
        if (distant)
        {
            volume -= 2.0f;
        }
        return volume;
    }

    public static float PlayerWeaponShotVolumeDb(WeaponBuild build)
        => Mathf.Min(1.5f, WeaponShotVolumeDb(build) + 4.0f);

    public static int WeaponShotSignature(WeaponBuild build, bool distant = false)
    {
        var data = WeaponShot(build, distant).Data;
        var hash = 17;
        var stride = Mathf.Max(1, data.Length / 32);
        for (var index = 0; index < data.Length; index += stride)
        {
            hash = unchecked(hash * 31 + data[index]);
        }
        return hash;
    }

    private static WeaponShotRecipe WeaponShotRecipeFor(WeaponPlatform platform, bool suppressed)
    {
        var recipe = platform switch
        {
            WeaponPlatform.AK74 => new WeaponShotRecipe(0.38f, 1.08f, 112.0f, 1.08f, 82.0f, 13.0f, 0.42f, 7.5f, 0.18f, 1760.0f, 74211),
            WeaponPlatform.ScarL => new WeaponShotRecipe(0.42f, 1.16f, 108.0f, 1.18f, 74.0f, 11.0f, 0.48f, 7.0f, 0.16f, 1820.0f, 74237),
            WeaponPlatform.M24 => new WeaponShotRecipe(0.56f, 1.25f, 118.0f, 1.5f, 58.0f, 7.2f, 0.66f, 5.2f, 0.22f, 1460.0f, 74261),
            WeaponPlatform.AXMC => new WeaponShotRecipe(0.7f, 1.38f, 122.0f, 1.82f, 43.0f, 5.8f, 0.78f, 4.5f, 0.28f, 1320.0f, 74279),
            WeaponPlatform.AWM => new WeaponShotRecipe(0.68f, 1.42f, 124.0f, 1.76f, 47.0f, 5.9f, 0.74f, 4.7f, 0.26f, 1390.0f, 74297),
            WeaponPlatform.MP5A5 => new WeaponShotRecipe(0.29f, 0.88f, 126.0f, 0.62f, 112.0f, 17.0f, 0.24f, 9.5f, 0.12f, 2240.0f, 74317),
            WeaponPlatform.M3A1 => new WeaponShotRecipe(0.37f, 0.98f, 118.0f, 1.08f, 72.0f, 12.0f, 0.36f, 7.0f, 0.18f, 1980.0f, 74339),
            WeaponPlatform.P226 => new WeaponShotRecipe(0.31f, 0.84f, 132.0f, 0.66f, 106.0f, 15.0f, 0.23f, 9.0f, 0.1f, 2360.0f, 74359),
            WeaponPlatform.M1911 => new WeaponShotRecipe(0.37f, 1.05f, 119.0f, 0.9f, 91.0f, 11.5f, 0.32f, 7.0f, 0.14f, 2140.0f, 74377),
            WeaponPlatform.VSS => new WeaponShotRecipe(0.4f, 0.2f, 150.0f, 0.38f, 74.0f, 16.0f, 0.2f, 9.0f, 0.24f, 2620.0f, 74399),
            WeaponPlatform.DesertEagle => new WeaponShotRecipe(0.49f, 1.42f, 106.0f, 1.52f, 57.0f, 7.4f, 0.66f, 5.5f, 0.24f, 1740.0f, 74417),
            WeaponPlatform.GSh18 => new WeaponShotRecipe(0.32f, 0.88f, 132.0f, 0.7f, 110.0f, 15.5f, 0.24f, 9.0f, 0.12f, 2480.0f, 74437),
            _ => new WeaponShotRecipe(0.34f, 1.0f, 116.0f, 0.92f, 94.0f, 14.0f, 0.34f, 8.0f, 0.14f, 2060.0f, 74197)
        };
        if (!suppressed)
        {
            return recipe;
        }
        return new WeaponShotRecipe(
            recipe.Duration,
            recipe.CrackLevel * 0.22f,
            recipe.CrackDecay * 1.2f,
            recipe.PressureLevel * 0.38f,
            recipe.PressureFrequency * 0.92f,
            recipe.PressureDecay * 1.18f,
            recipe.TailLevel * 0.56f,
            recipe.TailDecay * 1.15f,
            recipe.MechanicalLevel * 1.18f,
            recipe.MechanicalFrequency,
            recipe.Seed + 9000);
    }

    private static AudioStreamWav BuildWeaponShot(WeaponShotRecipe recipe, bool distant)
    {
        const int rate = 44100;
        var samples = new float[(int)(rate * recipe.Duration)];
        var rng = new RandomNumberGenerator { Seed = (ulong)(recipe.Seed + (distant ? 1000003 : 0)) };
        var lowNoise = 0.0f;
        var midNoise = 0.0f;
        var peak = 0.0f;
        var crackLevel = distant ? recipe.CrackLevel * 0.34f : recipe.CrackLevel;
        var pressureLevel = distant ? recipe.PressureLevel * 0.66f : recipe.PressureLevel;
        var tailLevel = distant ? recipe.TailLevel * 1.12f : recipe.TailLevel;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var white = rng.RandfRange(-1.0f, 1.0f);
            lowNoise = Mathf.Lerp(lowNoise, white, 0.018f);
            midNoise = Mathf.Lerp(midNoise, white, 0.12f);
            var crack = white * crackLevel * Mathf.Exp(-t * recipe.CrackDecay);
            var pressureEnvelope = Mathf.Exp(-t * recipe.PressureDecay);
            var pressure = (lowNoise * 0.76f
                + Mathf.Sin(Mathf.Tau * (recipe.PressureFrequency - t * 24.0f) * t) * 0.42f
                + Mathf.Sin(Mathf.Tau * (recipe.PressureFrequency * 0.47f + t * 18.0f) * t) * 0.16f)
                * pressureLevel
                * pressureEnvelope;
            var tail = midNoise * tailLevel * Mathf.Exp(-t * recipe.TailDecay);
            var mechanical = Mathf.Sin(Mathf.Tau * recipe.MechanicalFrequency * t)
                * recipe.MechanicalLevel
                * Mathf.Exp(-Mathf.Abs(t - 0.032f) * 76.0f);
            var sub = Mathf.Sin(Mathf.Tau * (recipe.PressureFrequency * 0.32f - t * 14.0f) * t)
                * pressureLevel
                * 0.22f
                * Mathf.Exp(-t * (recipe.PressureDecay * 0.72f));
            var sample = Mathf.Tanh((crack + pressure + tail + mechanical + sub) * 1.28f) * 0.94f;
            samples[i] = sample;
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }
        if (peak > 0.001f)
        {
            var normalization = 0.94f / peak;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= normalization;
            }
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav MeleeSwing(MeleeWeaponStyle style, int attackIndex)
    {
        var key = (style, Mathf.Abs(attackIndex) % 3);
        if (MeleeSwingCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        const int rate = 44100;
        var heavy = style == MeleeWeaponStyle.ZhanmaDao;
        var duration = heavy ? 0.54f : style == MeleeWeaponStyle.TianxuanDao ? 0.42f : 0.34f;
        var samples = new float[(int)(rate * duration)];
        var rng = new RandomNumberGenerator
        {
            Seed = (ulong)(77431 + (int)style * 997 + key.Item2 * 71)
        };
        var air = 0.0f;
        var peak = 0.0f;
        for (var index = 0; index < samples.Length; index++)
        {
            var t = (float)index / rate;
            var phase = t / duration;
            var envelope = Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Clamp(phase, 0.0f, 1.0f)), 1.65f);
            var white = rng.RandfRange(-1.0f, 1.0f);
            air = Mathf.Lerp(air, white, heavy ? 0.055f : 0.095f);
            var sweepFrequency = Mathf.Lerp(
                heavy ? 160.0f : 260.0f,
                heavy ? 620.0f : 1120.0f,
                phase);
            var bladeTone = Mathf.Sin(Mathf.Tau * sweepFrequency * t)
                + Mathf.Sin(Mathf.Tau * sweepFrequency * 1.93f * t) * 0.26f;
            var shimmer = style == MeleeWeaponStyle.TianxuanDao
                ? Mathf.Sin(Mathf.Tau * (1480.0f + phase * 920.0f) * t) * 0.16f
                : 0.0f;
            var sample = (air * (heavy ? 1.18f : 0.92f)
                + white * 0.24f
                + bladeTone * (heavy ? 0.2f : 0.28f)
                + shimmer) * envelope;
            samples[index] = Mathf.Tanh(sample * 1.45f) * 0.82f;
            peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
        }
        if (peak > 0.001f)
        {
            var normalization = 0.88f / peak;
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] *= normalization;
            }
        }

        var stream = MakeStream(samples, rate);
        MeleeSwingCache[key] = stream;
        return stream;
    }

    public static AudioStreamWav ReloadClick()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.12f)];
        var rng = new RandomNumberGenerator { Seed = 9921 };
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var pulse = Mathf.Exp(-Mathf.Abs(t - 0.015f) * 150.0f)
                + 0.65f * Mathf.Exp(-Mathf.Abs(t - 0.075f) * 180.0f);
            samples[i] = (rng.RandfRange(-1.0f, 1.0f) * 0.25f
                + Mathf.Sin(Mathf.Tau * 1750.0f * t) * 0.3f) * pulse;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav Explosion()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.7f)];
        var rng = new RandomNumberGenerator { Seed = 122733 };
        var low = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var envelope = Mathf.Exp(-t * 5.8f);
            low = Mathf.Lerp(low, rng.RandfRange(-1.0f, 1.0f), 0.045f);
            var thump = Mathf.Sin(Mathf.Tau * (58.0f - t * 25.0f) * t) * Mathf.Exp(-t * 7.5f);
            samples[i] = (low * 1.35f + thump) * envelope * 0.75f;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav WorldBossPulse()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.82f)];
        var rng = new RandomNumberGenerator { Seed = 338700 };
        var lowNoise = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var envelope = Mathf.Exp(-t * 4.6f);
            var sweep = Mathf.Sin(Mathf.Tau * (76.0f + t * 138.0f) * t) * envelope;
            var ping = Mathf.Sin(Mathf.Tau * 940.0f * t) * Mathf.Exp(-t * 12.0f);
            lowNoise = Mathf.Lerp(lowNoise, rng.RandfRange(-1.0f, 1.0f), 0.035f);
            samples[i] = (sweep * 0.72f + ping * 0.22f + lowNoise * 0.2f) * envelope;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav Footstep()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.13f)];
        var rng = new RandomNumberGenerator { Seed = 48117 };
        var low = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            low = Mathf.Lerp(low, rng.RandfRange(-1.0f, 1.0f), 0.12f);
            var impact = Mathf.Exp(-t * 38.0f);
            var scrape = Mathf.Exp(-Mathf.Abs(t - 0.045f) * 65.0f);
            samples[i] = (low * 0.75f * impact + rng.RandfRange(-0.25f, 0.25f) * scrape) * 0.42f;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav PlayerHit()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.34f)];
        var rng = new RandomNumberGenerator { Seed = 948217 };
        var lowNoise = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var impact = Mathf.Exp(-t * 16.0f);
            var bodyThump = Mathf.Sin(Mathf.Tau * (72.0f - t * 46.0f) * t) * impact;
            var armorCrack = Mathf.Sin(Mathf.Tau * 1680.0f * t) * Mathf.Exp(-t * 72.0f);
            lowNoise = Mathf.Lerp(lowNoise, rng.RandfRange(-1.0f, 1.0f), 0.085f);
            samples[i] = (bodyThump * 0.8f + armorCrack * 0.24f + lowNoise * 0.28f) * impact;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav CasingDrop()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.08f)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var envelope = Mathf.Exp(-t * 55.0f);
            samples[i] = (Mathf.Sin(Mathf.Tau * 3100.0f * t) * 0.24f
                + Mathf.Sin(Mathf.Tau * 1850.0f * t) * 0.16f) * envelope;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav GlassBreak()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.62f)];
        var rng = new RandomNumberGenerator { Seed = 681421 };
        var shardTimes = new[] { 0.035f, 0.072f, 0.118f, 0.178f, 0.255f, 0.345f, 0.455f };
        var peak = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var noise = rng.RandfRange(-1.0f, 1.0f);
            var initialCrack = noise * Mathf.Exp(-t * 92.0f) * 1.35f;
            var body = Mathf.Sin(Mathf.Tau * 760.0f * t) * Mathf.Exp(-t * 19.0f) * 0.42f;
            var ring = (Mathf.Sin(Mathf.Tau * 2380.0f * t)
                + Mathf.Sin(Mathf.Tau * 3610.0f * t) * 0.58f)
                * Mathf.Exp(-t * 10.5f)
                * 0.3f;
            var shards = 0.0f;
            for (var shard = 0; shard < shardTimes.Length; shard++)
            {
                var localTime = t - shardTimes[shard];
                if (localTime < 0.0f)
                {
                    continue;
                }
                var decay = Mathf.Exp(-localTime * (34.0f + shard * 2.2f));
                var frequency = 1180.0f + shard * 315.0f;
                shards += (Mathf.Sin(Mathf.Tau * frequency * localTime) * 0.72f + noise * 0.55f)
                    * decay
                    * (1.0f - shard * 0.065f);
            }
            samples[i] = initialCrack + body + ring + shards * 0.54f;
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        }
        if (peak > 0.001f)
        {
            var normalization = 0.94f / peak;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= normalization;
            }
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav ExtractionRotorLoop()
    {
        const int rate = 22050;
        var samples = new float[rate];
        var rng = new RandomNumberGenerator { Seed = 442109 };
        var wash = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var bladePulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Mathf.Tau * 18.0f * t), 5.0f);
            wash = Mathf.Lerp(wash, rng.RandfRange(-1.0f, 1.0f), 0.018f);
            samples[i] = Mathf.Sin(Mathf.Tau * 54.0f * t) * 0.22f
                + Mathf.Sin(Mathf.Tau * 108.0f * t) * 0.1f
                + wash * (0.12f + bladePulse * 0.16f);
        }
        var stream = MakeStream(samples, rate);
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = 0;
        stream.LoopEnd = samples.Length;
        return stream;
    }
}
