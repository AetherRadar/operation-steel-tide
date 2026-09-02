"""Prepare the tracked AK-47 field recordings used by the AK74 runtime audio.

The upstream files are intentionally not downloaded at runtime.  This script
is a reproducible offline/online preparation step: it downloads the CC0 source,
cuts one take, converts it to Godot-friendly PCM, and writes the three runtime
roles (first-person, positional world, and distant enemy).
"""

from __future__ import annotations

import argparse
import audioop
import hashlib
from pathlib import Path
import urllib.request
import wave


SOURCE_BASE = (
    "https://raw.githubusercontent.com/petroulacl/fps-asset-kit/main/"
    "sfx/firearm_sfx/Prepared%20SFX%20Library/"
)
SOURCES = {
    "near": SOURCE_BASE + "AK-47/C_28P.wav",
    "distant": SOURCE_BASE + "AK-47/C_31P.wav",
}
DEFAULT_SOURCE_DIR = Path(".cache/free_firearm_sound_library")
DEFAULT_OUTPUT_DIR = Path("assets/audio/weapons/ak74")


def download(url: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    request = urllib.request.Request(url, headers={"User-Agent": "OperationSteelTide"})
    with urllib.request.urlopen(request) as response, destination.open("wb") as output:
        output.write(response.read())


def convert(
    source: Path,
    destination: Path,
    start_seconds: float,
    end_seconds: float,
    channels: int,
    target_peak: float,
) -> str:
    with wave.open(str(source), "rb") as reader:
        source_channels = reader.getnchannels()
        source_width = reader.getsampwidth()
        source_rate = reader.getframerate()
        start_frame = int(start_seconds * source_rate)
        end_frame = int(end_seconds * source_rate)
        reader.setpos(start_frame)
        raw = reader.readframes(max(0, end_frame - start_frame))

    if channels == 1 and source_channels == 2:
        raw = audioop.tomono(raw, source_width, 0.5, 0.5)
    elif channels != source_channels:
        raise ValueError(f"unsupported channel conversion: {source_channels} -> {channels}")

    raw, _ = audioop.ratecv(raw, source_width, channels, source_rate, 44100, None)
    raw = audioop.lin2lin(raw, source_width, 2)
    peak = audioop.max(raw, 2)
    if peak:
        raw = audioop.mul(raw, 2, target_peak * 32767.0 / peak)

    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), "wb") as writer:
        writer.setnchannels(channels)
        writer.setsampwidth(2)
        writer.setframerate(44100)
        writer.writeframes(raw)
    return hashlib.sha256(destination.read_bytes()).hexdigest().upper()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, default=DEFAULT_SOURCE_DIR)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    args = parser.parse_args()

    near_source = args.source_dir / "C_28P.wav"
    distant_source = args.source_dir / "C_31P.wav"
    if not near_source.exists():
        download(SOURCES["near"], near_source)
    if not distant_source.exists():
        download(SOURCES["distant"], distant_source)

    outputs = {
        "ak74_player_near.wav": convert(
            near_source, args.output_dir / "ak74_player_near.wav", 0.54, 1.22, 1, 0.52
        ),
        "ak74_world.wav": convert(
            near_source, args.output_dir / "ak74_world.wav", 0.54, 1.22, 1, 0.64
        ),
        "ak74_enemy_distant.wav": convert(
            distant_source,
            args.output_dir / "ak74_enemy_distant.wav",
            0.29,
            1.58,
            1,
            0.72,
        ),
    }
    for name, digest in outputs.items():
        print(f"{name} sha256={digest}")


if __name__ == "__main__":
    main()
