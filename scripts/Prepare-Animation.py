"""Prototype upper-body clips derived from the supplied 1..70 action.
Release marker remains a runtime validation gate, not an assumed FBX event.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Quaternion, Vector
ROOT=Path(__file__).resolve().parents[1]
assert bpy.app.version==(4,5,3)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'assets/source/throw_objekt.fbx'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
scene=bpy.context.scene; scene.render.fps=60
original=arm.animation_data.action
assert tuple(original.frame_range)==(1,70)
samples={}
for frame in range(1,71):
    scene.frame_set(frame)
    samples[frame]={b.name:b.matrix_basis.decompose() for b in arm.pose.bones}
arm.animation_data.action=None
for action in list(bpy.data.actions): bpy.data.actions.remove(action)
def blend(a,b,t):
    return {name:(a[name][0].lerp(b[name][0],t),a[name][1].slerp(b[name][1],t),a[name][2].lerp(b[name][2],t)) for name in a}
def at(frame):
    low=max(1,min(70,int(frame))); high=min(70,low+1)
    return blend(samples[low],samples[high],frame-low)
def loop(t):
    pose=at(27)
    # Small shoulder/elbow circulation; the rope solver supplies the broad overhead orbit.
    for name,amplitude,offset in [('mixamorig:RightArm',.13,0),('mixamorig:RightForeArm',.10,math.pi/2)]:
        loc,rot,scale=pose[name]
        pose[name]=(loc,rot@Quaternion(Vector((0,1,0)),amplitude*math.sin(t*2*math.pi+offset)),scale)
    return pose
definitions=[
    ('ChargeStart',15,lambda t:blend(at(1),loop(0),t*t*(3-2*t))),
    ('ChargeLoop',36,loop),
    ('ChargeCancel',12,lambda t:blend(loop(0),at(1),t*t*(3-2*t))),
    ('ThrowRelease',26,lambda t:at(27+13*t)),
    ('ThrowRecover',15,lambda t:blend(at(40),at(1),t*t*(3-2*t)))
]
for name,frames,pose_at in definitions:
    action=bpy.data.actions.new(name); arm.animation_data.action=action; action.use_fake_user=True
    for frame in range(frames+1):
        pose=pose_at(frame/frames)
        # Preserve the source hip turn at the upper-body boundary before locking the pelvis.
        hip_reference=samples[1]['mixamorig:Hips'][1]
        hip_current=pose['mixamorig:Hips'][1]
        loc,rot,scale=pose['mixamorig:Spine']
        pose['mixamorig:Spine']=(loc,hip_reference.inverted()@hip_current@rot,scale)
        for bone in arm.pose.bones:
            bone.rotation_mode='QUATERNION'
            loc,rot,scale=pose[bone.name]
            # Keep hip translation/rotation fixed and lower-body pose at reference sample.
            if bone.name=='mixamorig:Hips' or any(part in bone.name for part in ['UpLeg','Leg','Foot','Toe']):
                loc,rot,scale=samples[1][bone.name]
            bone.location=loc; bone.rotation_quaternion=rot; bone.scale=scale
            for prop in ['location','rotation_quaternion','scale']: bone.keyframe_insert(prop,frame=frame+1,group=bone.name)
    action['prototype_duration']=frames/60
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(ROOT/'unity/Assets/Generated/BolaActions.fbx'),use_selection=True,
    object_types={'ARMATURE'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,
    bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
(ROOT/'artifacts/animation-recipe.json').write_text(json.dumps({
    'fps':60,'clips':{name:frames/60 for name,frames,_ in definitions},
    'releaseSourceFrames':[27,40],'releaseDelay':.12,
    'releaseMarkerValidated':False,'referenceMeshesExported':False},indent=2))
print('BOLA_ANIMATION_PROTOTYPE_OK')
