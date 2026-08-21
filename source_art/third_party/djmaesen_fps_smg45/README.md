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
```

See `assets/models/djmaesen_smg45/LICENSE.md` for attribution and local file
mapping.
