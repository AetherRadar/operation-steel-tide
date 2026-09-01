# DJMaesen FPS Animated SMG Source

`fps_animated_smg.glb` is the tracked CC BY 4.0 source used to generate the
first-person arms, two-handed reload animation, and SMG-45 field model. Its
embedded glTF metadata records DJMaesen as the creator and links the original
Sketchfab model. The build extends the two upper sleeves behind the gameplay
camera so the source cuff cuts cannot enter the first-person view.

Regenerate the editable Blender source and runtime GLBs from the repository
root with Blender 4.5 LTS or newer:

```bash
blender --background --factory-startup --python scripts/blender/build_djmaesen_smg45.py
blender --background --factory-startup --python-exit-code 1 --python scripts/blender/build_animated_reload_arms.py
```

The second command rebuilds `animated_reload_arms.blend` and the contract-
revision-8 runtime GLB with 24 tactical/empty clips. Its 2026-08-31 pose-to-
pose pass groups the twelve platforms into straight-rifle, rock-and-lock, MP5,
precision/internal, service-pistol, and Desert Eagle choreography. Long guns
use short camera-safe magazine paths instead of the retired waist-pouch arc;
cropped pistol forearms use a deterministic analytical shoulder/elbow solve so
the right hand stays fixed and empty reloads retain a readable slide beat.
Long guns render a dedicated 12,686-triangle crop with 28 source-unit cuffs;
the complete 13,700-triangle arms remain a hidden audit layer and pistols keep
their 9,334-triangle compact crop (9,306 after Godot import removes collinear
cut-ring faces). The 2026-09-01 sidearm pass adds a five-digit magazine grasp,
moves the real installed/staged magazine with that authored glove contact,
removes runtime wrist/shoulder pose correction, and gives service pistols and
Desert Eagle separate professional slide beats. Extraction and insertion must
retain 45-60 mm glove-anchor clearance and at least 0.65 radians of curl on
every digit chain. The builder samples the long-gun crop in a
right-grip camera proxy and proves the full audit layer fails that envelope,
then rejects missing beat holds, out-of-envelope controls, discontinuous bones
or palms, endpoint mismatch, and GLB clip-duration drift before export.

See `assets/models/djmaesen_smg45/LICENSE.md` for attribution and local file
mapping.
