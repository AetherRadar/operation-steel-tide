# Compact Demolition Device Source

`demolition_device.blend` is the authoritative editable source for the 5v5
demolition objective device. Rebuild it from the repository root with Blender
4.5 LTS:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" --background --factory-startup --python scripts/blender/build_demolition_device.py
```

The build creates a compact 0.344 by 0.201 by 0.164 metre rugged electronics
unit with 48 authored mesh pieces, 9,216 triangles, and nine scalar PBR
materials. High-visibility orange rails, amber indicators, and a cyan telemetry
screen use emissive materials; the dark shell, impact bumpers, keypad, handle,
vents, fasteners, and twin antennae provide readable detail at player-camera
distance.

The GLB contract contains exactly one `SteelTideDemolitionDevice` root plus
`DeviceCase`, `DeviceScreen`, `DeviceStatusLight`, and `DeviceCarrySocket`.
The script saves the packed `.blend`, exports an embedded GLB, round-trips it,
checks node uniqueness and bounds, renders a studio preview, and rejects mesh,
triangle, or material drift. No third-party geometry, textures, fonts, logos,
or marketplace content are used. The source and outputs are covered by the
repository's root MIT license.
