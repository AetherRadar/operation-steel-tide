"""Retarget an upright CC0 Quaternius cycle onto a HY-3D operator in Blender.

The HY-3D mesh and skin remain authored.  Only the lower-body locomotion is
replaced, so the upper body stays neutral for runtime two-hand weapon IK.
"""
import argparse, os, sys
import bpy
from mathutils import Matrix, Vector, Quaternion

MAP={"Root":"root","Hips":"Hips","Spine":"Spine","Spine1":"Spine1","Spine2":"Spine2","Neck":"Neck","Head":"Head","LeftShoulder":"LeftShoulder","LeftArm":"LeftArm","LeftForeArm":"LeftForeArm","LeftHand":"LeftHand","RightShoulder":"RightShoulder","RightArm":"RightArm","RightForeArm":"RightForeArm","RightHand":"RightHand","LeftUpLeg":"LeftUpLeg","LeftLeg":"LeftLeg","LeftFoot":"LeftFoot","RightUpLeg":"RightUpLeg","RightLeg":"RightLeg","RightFoot":"RightFoot"}
REPLACE={"walk":"walk","run":"walk","sprint":"walk","ready_walk":"walk","ready_run":"walk","ready_sprint":"walk","aim_walk":"walk","aim_run":"walk","aim_sprint":"walk"}
# Keep the pelvis/torso bind frame fixed.  Moving Hips also moves both
# shoulders and makes the support wrist miss the rifle while the character is
# walking; the leg chains alone provide the visible step cycle.
LOWER_BODY={"LeftUpLeg","LeftLeg","LeftFoot","RightUpLeg","RightLeg","RightFoot"}

def args():
    av=sys.argv[sys.argv.index('--')+1:]
    p=argparse.ArgumentParser(); p.add_argument('--input',required=True); p.add_argument('--source',required=True); p.add_argument('--output',required=True); return p.parse_args(av)

def armature(objects): return next(o for o in objects if o.type=='ARMATURE')
def source_bone(arm, canonical):
    return arm.pose.bones.get(canonical) or arm.pose.bones.get('mixamorig:'+canonical)

def rest_basis(arm):
    arm.animation_data_create(); arm.animation_data.action=None
    bpy.context.view_layer.update()
    return {canonical: source_bone(arm,canonical).matrix_basis.copy() for canonical in MAP if source_bone(arm,canonical)}

def make_action(name, source, target, src_action, frame_rotation):
    source.animation_data.action=src_action
    start,end=[int(round(v)) for v in src_action.frame_range]
    frames=list(range(start,end+1))
    samples={canonical:[] for canonical in MAP}
    target.animation_data.action=None
    valid_start=min(end,start+3)
    valid_span=max(1,end-valid_start+1)
    ordered=sorted(MAP.items(),key=lambda item:len(target.data.bones[item[1]].parent_recursive))
    for frame in frames:
        sample=valid_start+((frame-start)%valid_span)
        bpy.context.scene.frame_set(sample); bpy.context.view_layer.update()
        for canonical,tname in ordered:
            sb=source_bone(source,canonical); tb=target.pose.bones.get(tname); td=target.data.bones.get(tname)
            if sb is None or tb is None or td is None: continue
            desired=td.matrix_local.copy()
            if canonical in LOWER_BODY:
                source_rest=sb.bone.matrix_local.to_3x3().normalized()
                source_pose=sb.matrix.to_3x3().normalized()
                delta=source_pose @ source_rest.inverted()
                mapped=frame_rotation @ delta @ frame_rotation.inverted()
                desired=mapped.to_4x4() @ td.matrix_local
            desired.translation=td.matrix_local.translation
            tb.matrix=desired
            tb.location=Vector((0.0,0.0,0.0))
            tb.scale=Vector((1.0,1.0,1.0))
            loc,rot,scale=tb.location.copy(),tb.rotation_quaternion.copy(),tb.scale.copy()
            samples[canonical].append((rot.copy(),loc.copy(),scale.copy()))
    action=bpy.data.actions.new(name)
    slot=action.slots.new(target.id_type,target.name)
    layer=action.layers.new('Layer'); strip=layer.strips.new(type='KEYFRAME'); bag=strip.channelbag(slot,ensure=True)
    for canonical,tname in MAP.items():
        vals=samples[canonical]
        if len(vals)!=len(frames): continue
        # Use quaternion channels and locations; HY3D bind scales stay 1.
        for prop,n in [('rotation_quaternion',4),('location',3)]:
            for idx in range(n):
                fc=bag.fcurves.new(data_path=f'pose.bones["{tname}"].{prop}',index=idx)
                fc.keyframe_points.add(len(frames))
                prev=None
                for kp,fr,val in zip(fc.keyframe_points,frames,vals):
                    v=val[0] if prop=='rotation_quaternion' else val[1]
                    if prop=='rotation_quaternion' and idx==0: pass
                    kp.co=(fr,float(v[idx])); kp.interpolation='BEZIER'
                fc.update()
    return action

def main():
    c=args(); bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(c.input)); target_objects=list(bpy.context.scene.objects); target=armature(target_objects); target_actions=[a for a in bpy.data.actions]; target_action_names=[a.name for a in target_actions]
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(c.source)); source_objects=[o for o in bpy.context.scene.objects if o not in target_objects]; source=armature(source_objects); source_actions=[a for a in bpy.data.actions if a not in target_actions]
    # pick canonical source actions, stripping Blender's duplicate suffix.
    srcmap={}
    for a in source_actions:
        base=a.name.rsplit('.',1)[0] if a.name.rsplit('.',1)[-1].isdigit() else a.name
        srcmap.setdefault(base,a)
    rest_basis(source); rest_basis(target)
    build_module=__import__('runpy').run_path(os.path.join(os.path.dirname(__file__),'build_hy3d_operator.py'))
    frame_rotation=build_module['_frame_transform'](source,target).to_3x3().normalized()
    print('source actions',sorted(srcmap)[:8], 'target actions',len(target_actions))
    for desired,base in REPLACE.items():
        old_names=[name for name in target_action_names if name==desired or (name.startswith(desired+'.') and name[len(desired)+1:].isdigit())]
        for old_name in old_names:
            a=bpy.data.actions.get(old_name)
            if a is None: continue
            for o in target_objects:
                if o.animation_data and o.animation_data.action==a: o.animation_data.action=None
            bpy.data.actions.remove(a)
        action=make_action(desired,source,target,srcmap[base],frame_rotation)
        print('baked',desired,action.frame_range,len(action.layers))
    # drop source actions and hide source objects. Target retains its non-locomotion clips + new actions.
    for a in source_actions:
        if a.name in bpy.data.actions: bpy.data.actions.remove(a, do_unlink=True)
    for o in source_objects:
        bpy.data.objects.remove(o,do_unlink=True)
    target.animation_data.action=bpy.data.actions.get('run'); bpy.context.scene.frame_set(11)
    # export only target hierarchy and non-helper target meshes; source already removed.
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(c.output),export_format='GLB',export_animations=True,export_skins=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
    print('wrote',c.output,os.path.getsize(c.output))
main()
