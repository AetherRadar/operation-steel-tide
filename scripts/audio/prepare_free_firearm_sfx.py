"""Prepare tracked field recordings used by the runtime weapon audio.

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

# The runtime has thirteen weapon platforms, while the CC0 library contains
# twenty-four recorded firearms.  Each entry names the closest real firearm
# recording and the short windows that isolate its first near/mid report.
# ``suppressed`` is a derived, low-passed take of the same licensed recording;
# it is used for the VSS and for muzzle-suppressor builds at runtime.
ALL_PROFILES = {
    "m4a1": ("D_32P.wav", "D_24P.wav", "AR-15", (0.58, 1.30), (0.48, 1.28), 0.58, 0.68, 0.64),
    "ak74": ("C_28P.wav", "C_31P.wav", "AK-47", (0.54, 1.22), (0.29, 1.58), 0.52, 0.64, 0.72),
    "scarl": ("D_32P.wav", "D_24P.wav", "AR-15", (0.58, 1.30), (0.48, 1.28), 0.46, 0.56, 0.60),
    "m24": ("B_24P.wav", "B_16P.wav", "1917", (1.18, 2.05), (0.53, 1.40), 0.58, 0.68, 0.70),
    "mp5a5": ("G_31P.wav", "G_20P.wav", "Carl Gustav M45", (0.25, 0.95), (0.25, 1.05), 0.52, 0.62, 0.62),
    "m3a1": ("P_30P.wav", "P_16P.wav", "PPSh", (0.30, 1.05), (0.30, 1.20), 0.54, 0.64, 0.64),
    "axmc": ("T_27P.wav", "T_17P.wav", "Savage 10 .300 Blackout", (0.78, 1.65), (0.78, 1.65), 0.66, 0.76, 0.76),
    "p226": ("X_39P.wav", "X_31P.wav", "Walther PPQ", (1.12, 1.82), (1.02, 1.78), 0.52, 0.62, 0.62),
    "m1911": ("A_42P.wav", "A_34P.wav", "1911", (0.80, 1.62), (1.42, 2.24), 0.56, 0.66, 0.68),
    "awm": ("W_29P.wav", "W_24P.wav", "Tikka", (0.52, 1.42), (0.68, 1.48), 0.64, 0.74, 0.74),
    "vss": ("C_28P.wav", "C_31P.wav", "AK-47", (0.54, 1.22), (0.29, 1.58), 0.66, 0.74, 0.68),
    "deserteagle": ("M_21P.wav", "M_26P.wav", "Mosin Nagant", (0.92, 1.82), (1.02, 1.92), 0.66, 0.76, 0.78),
    "gsh18": ("F_47P.wav", "F_41P.wav", "Bersa", (0.25, 1.00), (0.34, 1.18), 0.50, 0.60, 0.60),
}

SOURCE_FOLDERS = {
    "AR-15": "AR-15",
    "AK-47": "AK-47",
    "1917": "1917",
    "Carl Gustav M45": "Carl%20Gustav%20M45",
    "PPSh": "PPSh",
    "Savage 10 .300 Blackout": "Savage%2010%20.300%20Blackout",
    "Walther PPQ": "Walther%20PPQ",
    "1911": "1911",
    "Tikka": "Tikka",
    "Mosin Nagant": "Mosin%20Nagant",
    "Bersa": "Bersa",
}


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
    suppressed: bool = False,
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
    if suppressed:
        # A deterministic one-pole low-pass plus attenuation keeps the real
        # muzzle transient and mechanical texture while producing a restrained
        # integral-suppressor profile.  This is a derivative of the CC0 take,
        # not a synthetic replacement.
        filtered = bytearray(len(raw))
        state = [0.0] * channels
        for frame_start in range(0, len(raw), 2 * channels):
            for channel in range(channels):
                offset = frame_start + channel * 2
                sample = int.from_bytes(raw[offset:offset + 2], "little", signed=True)
                state[channel] += (sample - state[channel]) * 0.16
                quiet = state[channel] * 0.78 + sample * 0.12
                filtered[offset:offset + 2] = int(max(-32768, min(32767, quiet))).to_bytes(
                    2, "little", signed=True
                )
        raw = bytes(filtered)
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
    parser.add_argument(
        "--all-output-dir",
        type=Path,
        default=Path("assets/audio/weapons"),
        help="root directory for the per-platform outputs emitted by --all",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="also prepare the recorded takes for every runtime weapon platform",
    )
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

    if not args.all:
        return

    source_base = (
        "https://raw.githubusercontent.com/petroulacl/fps-asset-kit/main/"
        "sfx/firearm_sfx/Prepared%20SFX%20Library/"
    )
    for platform_id, profile in ALL_PROFILES.items():
        near_name, distant_name, folder, near_window, distant_window, near_peak, world_peak, distant_peak = profile
        folder_url = SOURCE_FOLDERS[folder]
        near_source = args.source_dir / near_name
        distant_source = args.source_dir / distant_name
        for source, filename in ((near_source, near_name), (distant_source, distant_name)):
            if not source.exists():
                download(source_base + folder_url + "/" + filename, source)

        output_dir = args.all_output_dir / platform_id
        profile_outputs = {
            f"{platform_id}_player_near.wav": convert(
                near_source,
                output_dir / f"{platform_id}_player_near.wav",
                near_window[0],
                near_window[1],
                1,
                near_peak,
                suppressed=False,
            ),
            f"{platform_id}_world.wav": convert(
                near_source,
                output_dir / f"{platform_id}_world.wav",
                near_window[0],
                near_window[1],
                1,
                world_peak,
                suppressed=False,
            ),
            f"{platform_id}_enemy_distant.wav": convert(
                distant_source,
                output_dir / f"{platform_id}_enemy_distant.wav",
                distant_window[0],
                distant_window[1],
                1,
                distant_peak,
                suppressed=False,
            ),
        }
        for name, digest in profile_outputs.items():
            print(f"{name} sha256={digest}")


if __name__ == "__main__":
    main()
