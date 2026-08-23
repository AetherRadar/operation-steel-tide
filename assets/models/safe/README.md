# Vault Safe Model - CC0 / CC-BY

This vault house interior uses a procedural safe by default (FreightTerminalWorld.VaultHouse.cs:338 BuildProceduralSafe) so the build remains self-contained and passes validation without external binaries.

## Optional CC0 / CC-BY replacement (higher fidelity)

To replace the procedural safe with an internet CC0 model:

1. **Sketchfab Low Poly Safe (CC-BY 4.0, 5.1k tris)** - DVOSTINA
   - URL: https://sketchfab.com/3d-models/low-poly-safe-da84ff04e48c4107b73bac8659919096
   - License: CC BY 4.0 (credit DVOSTINA)
   - Download: GLB (glTF Binary) -> save as
     `
     assets/models/safe/low_poly_safe.glb
     `

2. **Hard Cash: Bank & Vault Props - Free Base (CC0 1.0)** - TheSideQuestShop
   - URL: https://thesidequestshop.itch.io/hard-cash-bank-vault-props
   - License: CC0 1.0 (free base), no attribution required
   - Download: hard-cash-bank-vault-props-free.zip -> extract
     `
     assets/models/vault/vault_door.glb   (from zip)
     `

The code TryLoadExternalSafe() in FreightTerminalWorld.VaultHouse.cs:292 will auto-detect any of:
- res://assets/models/safe/low_poly_safe.glb
- res://assets/models/vault/vault_door.glb
- res://assets/models/safe/safe.glb

and swap in the authored mesh with correct collision (1.15x1.25x0.85) and PBR setup via ConfigureAuthoredMapModel().

## Attribution
- Procedural safe: original, no external license, approximates Hard Cash / Low Poly Safe silhouette (body + door + bolts + dial + wheel + keypad).
- External GLBs retain their original licenses above.

## Validation
After replacing, re-run:
`
godot --headless -- --validate-freight-terminal-doors
godot --headless -- --validate-map-density
`
Both must report valid=True (now 11 doors, 6 buildings / 24 rooms).
