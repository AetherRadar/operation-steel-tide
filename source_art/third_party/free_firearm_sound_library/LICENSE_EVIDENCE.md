# Free Firearm Sound Library — AK-47 evidence

- Library: **The Free Firearm Sound Library**
- Recorded by: **Ben Jaszczak, Brian Nelson, Kevin Heras, and Matthew Nanney**
- Official source page: <https://opengameart.org/content/the-free-firearm-sound-library>
- License shown by the source page: **CC0 1.0 Universal / no rights reserved**
- License deed: <https://creativecommons.org/publicdomain/zero/1.0/>
- Acquisition date: **2026-09-02**
- Attribution required: No. Creator and source are retained here as courtesy and audit evidence.

The OpenGameArt page identifies this as an open firearm-sound library and
explicitly lists the CC0 license. The public [mirror's catalog](https://github.com/petroulacl/fps-asset-kit)
describes the same collection as high-quality field recordings from real
firearms. Its prepared master sheet identifies the AK-47 recordings as 7.62×39
mm, with separate near-distance and mid-distance takes:

The raw bytes were retrieved from the public GitHub mirror listed below because
the original download links on the OpenGameArt page are archived MediaFire
links. The OpenGameArt license statement and the mirror's file-level metadata
are both retained; the mirror is not treated as a new rights grant.

- Metadata: <https://raw.githubusercontent.com/petroulacl/fps-asset-kit/main/sfx/firearm_sfx/Prepared%20SFX%20Library/Prepared%20Master%20Sheet.csv>
- Repository-local metadata copy: `Prepared_Master_Sheet.csv` (SHA-256 `98E3CE7FF594722B364D238C6C65F9C69638A0A0D9DD92D0EF55C8B137C0E9AA`)
- Near source (`C_28P.wav`): <https://raw.githubusercontent.com/petroulacl/fps-asset-kit/main/sfx/firearm_sfx/Prepared%20SFX%20Library/AK-47/C_28P.wav>
- Mid source (`C_31P.wav`): <https://raw.githubusercontent.com/petroulacl/fps-asset-kit/main/sfx/firearm_sfx/Prepared%20SFX%20Library/AK-47/C_31P.wav>
- Source SHA-256 (`C_28P.wav`): `E0934C1D79192D2216DB62FDF6AB57BF9D5D585267AF367A1CFB21F0972A537D`
- Source SHA-256 (`C_31P.wav`): `5C3261C3BDD7657FD07C141F1F683BEF06C2E03DA475F43F3943F755360A4994`

## Repository mapping

The checked-in files are loss-controlled, trimmed derivatives, not a claim that
the source recording has one universal AK-47 sound. The preparation script is
`scripts/audio/prepare_free_firearm_sfx.py`.

| Runtime file | Source take and processing | SHA-256 |
| --- | --- | --- |
| `assets/audio/weapons/ak74/ak74_player_near.wav` | `C_28P.wav`, 0.54–1.22 s, 96 kHz 24-bit stereo → 44.1 kHz 16-bit mono, peak 0.52 | `73338BB3907D27002EA23BEE9088FCB08D193A7E49E24825E52940A8782BCEBA` |
| `assets/audio/weapons/ak74/ak74_world.wav` | `C_28P.wav`, same trim, downmixed to mono, peak 0.64 | `62B005CF23615F1BA6169AFAA29C75799147F77C379BBAF076AEFDB82D8BB179` |
| `assets/audio/weapons/ak74/ak74_enemy_distant.wav` | `C_31P.wav`, 0.29–1.58 s, 96 kHz 24-bit stereo → 44.1 kHz 16-bit mono, peak 0.72 | `19719BADA2CA4C05AEAF9A5AC8FC6C2E11309560D1BD5844CB08C8C9F1D3324C` |

The runtime maps these recordings to the AK74 platform only when the weapon is
unsuppressed: first-person playback uses the near take, teammate/world playback
uses the mono near take, and enemy playback uses the mid-distance take.
Suppressed AK74 remains a separately processed procedural variant because the
library does not provide a licensed suppressed AK-47 take.
