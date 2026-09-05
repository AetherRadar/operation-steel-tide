"""Build the original Operation Steel Tide opening combat loop.

The generator uses only deterministic standard-library oscillators and noise.
It intentionally creates a restrained tactical bed rather than a cinematic
trailer cue, leaving headroom for the game's recorded firearm reports.
"""

from __future__ import annotations

import math
import random
import wave
from array import array
from pathlib import Path


RATE = 44_100
BPM = 128.0
BEAT = 60.0 / BPM
BARS = 16
DURATION = BARS * 4.0 * BEAT
OUTPUT = Path(__file__).resolve().parents[2] / "assets" / "audio" / "music" / "steel_tide_opening_combat.wav"


def midi_hz(note: int) -> float:
    return 440.0 * (2.0 ** ((note - 69) / 12.0))


def add_tone(
    left: array,
    right: array,
    start: float,
    duration: float,
    frequency: float,
    gain: float,
    pan: float = 0.0,
    shape: str = "saw",
    attack: float = 0.012,
    release: float = 0.16,
) -> None:
    begin = max(0, int(start * RATE))
    end = min(len(left), int((start + duration) * RATE))
    if end <= begin:
        return
    left_gain = gain * math.sqrt((1.0 - pan) * 0.5)
    right_gain = gain * math.sqrt((1.0 + pan) * 0.5)
    for index in range(begin, end):
        elapsed = (index / RATE) - start
        if elapsed < attack:
            envelope = elapsed / attack
        elif elapsed > duration - release:
            envelope = max(0.0, (duration - elapsed) / release)
        else:
            envelope = 1.0
        phase = (elapsed * frequency) % 1.0
        if shape == "saw":
            sample = 2.0 * phase - 1.0
            sample = 0.72 * sample + 0.18 * math.sin(elapsed * frequency * math.tau)
        elif shape == "square":
            sample = 1.0 if phase < 0.5 else -1.0
        else:
            sample = math.sin(elapsed * frequency * math.tau)
        left[index] += sample * envelope * left_gain
        right[index] += sample * envelope * right_gain


def add_kick(left: array, right: array, start: float, gain: float) -> None:
    begin = max(0, int(start * RATE))
    end = min(len(left), begin + int(0.24 * RATE))
    for index in range(begin, end):
        elapsed = (index - begin) / RATE
        envelope = math.exp(-elapsed * 22.0)
        frequency = 142.0 * math.exp(-elapsed * 18.0) + 42.0
        sample = math.sin(elapsed * frequency * math.tau) * envelope * gain
        left[index] += sample
        right[index] += sample


def add_snare(left: array, right: array, start: float, gain: float, rng: random.Random) -> None:
    begin = max(0, int(start * RATE))
    end = min(len(left), begin + int(0.18 * RATE))
    for index in range(begin, end):
        elapsed = (index - begin) / RATE
        envelope = math.exp(-elapsed * 25.0)
        noise = rng.uniform(-1.0, 1.0)
        body = math.sin(elapsed * 190.0 * math.tau) * math.exp(-elapsed * 28.0)
        sample = (noise * 0.72 + body * 0.28) * envelope * gain
        left[index] += sample * 0.96
        right[index] += sample


def add_hat(left: array, right: array, start: float, gain: float, rng: random.Random) -> None:
    begin = max(0, int(start * RATE))
    end = min(len(left), begin + int(0.055 * RATE))
    previous = 0.0
    for index in range(begin, end):
        elapsed = (index - begin) / RATE
        envelope = math.exp(-elapsed * 72.0)
        raw = rng.uniform(-1.0, 1.0)
        high_pass = raw - previous * 0.82
        previous = raw
        sample = high_pass * envelope * gain
        left[index] += sample * 0.76
        right[index] += sample * 0.92


def add_impact(left: array, right: array, start: float, gain: float, rng: random.Random) -> None:
    begin = max(0, int(start * RATE))
    end = min(len(left), begin + int(0.7 * RATE))
    for index in range(begin, end):
        elapsed = (index - begin) / RATE
        envelope = math.exp(-elapsed * 6.0)
        rumble = math.sin(elapsed * (54.0 - 9.0 * elapsed) * math.tau)
        metal = math.sin(elapsed * 830.0 * math.tau) * math.exp(-elapsed * 18.0)
        grit = rng.uniform(-1.0, 1.0) * math.exp(-elapsed * 34.0)
        sample = (rumble * 0.58 + metal * 0.19 + grit * 0.23) * envelope * gain
        left[index] += sample * 0.92
        right[index] += sample


def add_riser(left: array, right: array, start: float, duration: float, gain: float, rng: random.Random) -> None:
    begin = max(0, int(start * RATE))
    end = min(len(left), int((start + duration) * RATE))
    for index in range(begin, end):
        elapsed = (index - begin) / RATE
        progress = elapsed / duration
        envelope = progress * progress
        frequency = 180.0 + progress * 880.0
        tone = math.sin(elapsed * frequency * math.tau)
        noise = rng.uniform(-1.0, 1.0) * 0.12
        sample = (tone * 0.88 + noise) * envelope * gain
        left[index] += sample * (0.72 + progress * 0.2)
        right[index] += sample * (0.92 - progress * 0.1)


def build() -> None:
    frame_count = int(DURATION * RATE)
    left = array("f", [0.0]) * frame_count
    right = array("f", [0.0]) * frame_count
    rng = random.Random(0x53544545)

    # D minor / Bb / F / C: familiar tactical tension without sounding heroic.
    chords = ((50, 53, 57, 60), (46, 50, 53, 57), (41, 45, 48, 53), (48, 52, 55, 59))
    arp_offsets = (0, 1, 2, 1, 0, 2, 3, 2)
    for bar in range(BARS):
        bar_start = bar * 4.0 * BEAT
        chord = chords[bar % len(chords)]
        root = midi_hz(chord[0] - 12)
        add_tone(left, right, bar_start, 3.5 * BEAT, root, 0.16, -0.08, "sine", 0.04, 0.28)
        add_tone(left, right, bar_start, 3.5 * BEAT, root * 2.0, 0.055, 0.08, "sine", 0.04, 0.3)
        for step in range(8):
            note = chord[arp_offsets[step]] + 12
            add_tone(
                left,
                right,
                bar_start + step * 0.5 * BEAT,
                0.34 * BEAT,
                midi_hz(note),
                0.075,
                -0.35 if step % 2 == 0 else 0.35,
                "saw",
                0.006,
                0.08,
            )
        for beat in range(4):
            beat_start = bar_start + beat * BEAT
            add_kick(left, right, beat_start, 0.42 if beat in (0, 2) else 0.27)
            add_hat(left, right, beat_start, 0.06, rng)
            add_hat(left, right, beat_start + 0.5 * BEAT, 0.045, rng)
            if beat in (1, 3):
                add_snare(left, right, beat_start, 0.20, rng)
        if bar in (0, 4, 8, 12):
            add_impact(left, right, bar_start, 0.24 if bar else 0.32, rng)
        if bar in (7, 15):
            add_riser(left, right, bar_start + 2.0 * BEAT, 2.0 * BEAT, 0.09, rng)

    # Short radio-click / sub-drop opening signature.
    add_impact(left, right, 0.02, 0.35, rng)
    add_tone(left, right, 0.08, 0.7, midi_hz(74), 0.18, -0.15, "square", 0.004, 0.22)

    # Keep the loop quiet at the seam, then normalize with gentle saturation.
    crossfade = int(0.035 * RATE)
    for index in range(crossfade):
        progress = index / crossfade
        tail = frame_count - crossfade + index
        left[tail] = left[tail] * (1.0 - progress) + left[index] * progress
        right[tail] = right[tail] * (1.0 - progress) + right[index] * progress
    peak = max(max(abs(value) for value in left), max(abs(value) for value in right), 1e-6)
    scale = 0.86 / peak
    pcm = array("h")
    for l_value, r_value in zip(left, right):
        l_value = math.tanh(l_value * scale * 1.2) / math.tanh(1.2)
        r_value = math.tanh(r_value * scale * 1.2) / math.tanh(1.2)
        pcm.append(int(max(-1.0, min(1.0, l_value)) * 32767))
        pcm.append(int(max(-1.0, min(1.0, r_value)) * 32767))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as stream:
        stream.setnchannels(2)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    print(f"Wrote {OUTPUT} ({DURATION:.2f}s, {RATE}Hz stereo)")


if __name__ == "__main__":
    build()
