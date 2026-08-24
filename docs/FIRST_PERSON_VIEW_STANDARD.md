# First-Person View Standard

This document defines the runtime contract for visible first-person arms and weapons.

## Coordinate Contract

- All grip anchors are `WeaponRoot`-local metres.
- `+X` is screen-right, `+Y` is up, and `-Z` points from the camera toward the weapon.
- A weapon asset may have its own authored import rotation, but runtime presentation must preserve its authored aspect ratio.
- Runtime code may translate or rotate a presentation wrapper. It must not overwrite an imported asset's root basis to fit a new weapon.

## Arm Rig Contract

- Visible first-person arms must come from an authored, redistributable asset.
- The procedural arm mesh remains diagnostic scaffolding and is always hidden in normal play.
- The authored arm asset exposes unique `RightArm` and `LeftArm` mount nodes, each with a named palm marker.
- Arm roots keep a fixed authored scale. Weapon-specific alignment changes only the two mount positions.
- `FirstPersonArmPoseCatalog` is the single source of truth for pose families:
  - `Sidearm`: P226, M1911, GSh-18, Desert Eagle
  - `Compact`: MP5A5, M3A1
  - `Rifle`: M4A1, AK-74N, SCAR-L, VSS
  - `LongRifle`: M24, AXMC, AWM

## Weapon Contract

- Each visible weapon platform uses its own authored weapon asset or an explicitly documented authored adaptation.
- GSh-18 runtime geometry is exported as a centered static GLB with baked metre scale and deterministic axes before Godot loads it.
- Presentation scaling is length-driven and uniform; width and height are never independently stretched to hide a bad import.

## Validation Gate

Before delivery, run:

```text
--validate-combat-models
--validate-hand-diagnostics
--validate-weapon-ui
--validate-equipment
```

`HAND_POSE_CHECK valid=true` additionally requires authored selection, hidden procedural arms, grip residuals at or below `0.002 m`, finite uniform root scale, on-screen palm markers, and idempotent weapon switching.
