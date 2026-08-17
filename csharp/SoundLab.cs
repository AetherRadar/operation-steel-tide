using Godot;

namespace OperationSteelTide;

public static class SoundLab
{
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
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.28f)];
        var rng = new RandomNumberGenerator { Seed = 77231 };
        var low = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var envelope = Mathf.Exp(-t * 23.0f);
            low = Mathf.Lerp(low, rng.RandfRange(-1.0f, 1.0f), 0.18f);
            var crack = rng.RandfRange(-1.0f, 1.0f) * Mathf.Exp(-t * 70.0f);
            var boom = Mathf.Sin(Mathf.Tau * (92.0f - t * 90.0f) * t) * envelope;
            samples[i] = (crack * 0.78f + low * 0.42f + boom * 0.7f) * envelope;
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav DesertEagleShot()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.46f)];
        var rng = new RandomNumberGenerator { Seed = 501917 };
        var pressure = 0.0f;
        var peak = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var blast = Mathf.Exp(-t * 18.0f);
            var muzzleCrack = rng.RandfRange(-1.0f, 1.0f) * Mathf.Exp(-t * 92.0f);
            pressure = Mathf.Lerp(pressure, rng.RandfRange(-1.0f, 1.0f), 0.035f);
            var chestThump = Mathf.Sin(Mathf.Tau * (64.0f - t * 24.0f) * t) * Mathf.Exp(-t * 7.0f);
            var metallicSnap = Mathf.Sin(Mathf.Tau * 1850.0f * t) * Mathf.Exp(-t * 48.0f);
            samples[i] = muzzleCrack * 1.05f
                + pressure * blast * 0.72f
                + chestThump * 0.9f
                + metallicSnap * 0.18f;
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        }
        if (peak > 0.001f)
        {
            var normalization = 0.96f / peak;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= normalization;
            }
        }
        return MakeStream(samples, rate);
    }

    public static AudioStreamWav Gsh18Shot()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.36f)];
        var rng = new RandomNumberGenerator { Seed = 918018 };
        var pressure = 0.0f;
        var peak = 0.0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / rate;
            var crack = rng.RandfRange(-1.0f, 1.0f) * Mathf.Exp(-t * 105.0f);
            pressure = Mathf.Lerp(pressure, rng.RandfRange(-1.0f, 1.0f), 0.075f);
            var compactBlast = Mathf.Sin(Mathf.Tau * (118.0f - t * 52.0f) * t) * Mathf.Exp(-t * 13.0f);
            var slideSnap = Mathf.Sin(Mathf.Tau * 2380.0f * t) * Mathf.Exp(-Mathf.Abs(t - 0.026f) * 76.0f);
            var tail = pressure * Mathf.Exp(-t * 18.0f);
            samples[i] = crack * 0.92f + compactBlast * 0.64f + slideSnap * 0.17f + tail * 0.32f;
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

    public static AudioStreamWav EnemyShot()
    {
        var stream = Gunshot();
        stream.MixRate = 18500;
        return stream;
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
