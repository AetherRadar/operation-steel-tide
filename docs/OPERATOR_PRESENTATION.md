# Operator skin and animation authoring

The runtime operators use the editable scenes in `source_art/hy3d_operators/`.
The enemy uses `source_art/combat_models/enemy_operator.blend`. Mesh weights,
hair binding, weapon grip poses, socket placement, floor contact, and animation
retargeting belong to those Blender scenes. Godot selects and plays their clips.

## Rebuild

With Blender 4.5 installed, run from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.5/blender.exe' -b --python-exit-code 2 --python scripts/blender/repair_operator_presentation.py
```

Use `-- --roles lynx` to rebuild a single operator. The pipeline applies the
garment/hair skin repair, retargets the CC0 Quaternius Universal Animation
Library, authors handgun poses and sockets, bakes mesh floor contact, and saves
both `.blend` and `.glb`. The final enemy export derives from the completed Viper
scene. Do not export the enemy before rebuilding Viper.

The approved Tencent meshes and the CC0 animation source are documented in
`assets/models/LICENSE.md` and `docs/CONTENT_PROVENANCE.md`. This pass does not
acquire a new third-party mesh or change the source licenses.

The corrected scenes retain their editable meshes, UVs, weights, sockets, and
actions. Viper's fused glove/pouch contact is separated in
`repair_viper_contact.py`. Magpie's incomplete right arm is rebuilt from its
own intact left arm in `repair_magpie_contacts.py`. The shared skin pass keeps
each shoulder's influence family consistent; the export preserves the authored
weights rather than pruning them into sharp changes across a seam.

Magpie's right forearm and glove retain the mirrored source UVs and authored
left-arm influence family. The selection follows the rest skeleton and complete
hand components, excluding the adjacent thigh pouch. The shoulder paint pass
limits adjacent weight jumps before export. It does not repaint
the intact sleeve as a forearm, and rerunning it leaves the source weights intact.
The presentation diagnostic samples actual forearm surface coverage on both
sides so a valid skeleton/socket cannot hide a missing limb.

The final glTF export retains all eight authored influences per vertex. Trimming
them to four creates discontinuities at torso/arm transitions even when the
Blender skin is continuous. No imported edge is exempted from the tear check.
Blender validates triangulated edges, including export-created diagonals.

`finalize_operator_contacts.py` uses a two-bone solve to bake complete low-foot
stance intervals into the run/sprint actions and their weapon variants. Both
feet use one measured backwards speed, with eased transitions into swing and a
closed loop. Godot only adjusts clip playback to actual actor travel.
`stabilize_operator_rifle.py` bakes the reference rifle/wrist frame over moving
torsos and constrains both arm reach and right-elbow bend. It leaves the
already-authored leg channels intact. Apply it with `--finish-only
--stabilize-rifle`; no runtime arm or muzzle correction is required.
`--finish-only --finalize-contacts` applies this pass to an existing corrected
source. `--finish-only --settle-prone` adds a 5 mm authoring margin below the
0.70 m prone silhouette limit; it does not relax the imported geometry gate.

## Regression checks

```powershell
dotnet build OperationSteelTide.csproj
& '<Godot console executable>' --headless --path . -- --validate-operator-presentation
& '<Godot console executable>' --headless --path . -- --validate-enemy-death-lifecycle
& '<Godot console executable>' --headless --path . -- --validate-operator-deformation
& '<Godot console executable>' --headless --path . -- --validate-operator-animations
& '<Godot console executable>' --headless --path . -- --validate-operator-carry
& '<Godot console executable>' --headless --path . -- --validate-operator-roster
& '<Godot console executable>' --headless --path . -- --validate-squad
& '<Godot console executable>' --headless --path . scenes/diagnostics/operator_locomotion.tscn -- --validate-operator-locomotion --operator-role=all
```

`operator-presentation` runs an isolated diagnostic scene instead of constructing
the full map. It evaluates the imported skin using the actual inverse bind
matrices and sampled bone transforms. It checks short triangle edges for visible
tearing, settled body height and floor contact, and authored handgun pose/socket
selection and grip contact for all six visuals and all four sidearms. These
checks catch defects which a bone-length-only test cannot detect.

`enemy-death-lifecycle` kills an actual enemy and verifies that its physics
continues beyond the former 1.9-second cutoff, then stops only after the
authored death clip finishes. Moving fire and reload also retain their gait;
incapacitation takes priority over upper-body actions.

The six committed `.glb.import` files preserve exact authored action names
and constant tracks. Godot's name-suffix processing must remain disabled:
`jump_loop` and `slide_loop` are gameplay identifiers, not importer directives.
Each exported `.locomotion.tres` stores stance-foot travel in final world metres
per second. The final Blender parent authors the 1.86 m presentation, forward
direction, and foot pivot. Runtime playback uses actual actor displacement;
the gameplay model is instantiated without corrective scaling or rotation.

The source pass also absorbs the old root-bone pivot into the pelvis and
aligns the initial horizontal centre across action families. This preserves
bone orientations and within-clip movement while removing the former lateral
jump between standing and walking. Geometry validation runs after the final
Blender parent has been applied, before exporting.

Capture representative poses in Godot with:

```powershell
& '<Godot console executable>' --path . --resolution 1280x960 -- --capture-operator-presentation
```

Images are saved under `logs/operator-presentation/`. Add
`--operator-visual=Lynx` for a focused iteration; the diagnostic reports the
number of visuals actually tested. Final delivery requires the unfiltered run.
The gray plane and controlled lighting exist only in this explicit diagnostic.

Visual review must inspect the hands themselves as well as the grip markers,
garment seams during full strides, Lynx's hair/backpack clearance, and the whole
settled body. Markers can meet while the visible glove remains beside the grip.
