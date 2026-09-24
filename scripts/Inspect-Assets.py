"""Read source copies and write prototype evidence; never modify source assets."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[1]
assert bpy.app.version == (4, 5, 3), bpy.app.version_string
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets/source/bola.glb'))
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
report = {'blender': bpy.app.version_string, 'model': {'vertices': len(points), 'bounds': [[min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]]}}
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.world.color = (.15, .15, .15)
center = sum(points, Vector()) / len(points)
def aim(obj): obj.rotation_euler = (center - obj.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=center + Vector((0,-4,0)))
camera = bpy.context.object
aim(camera)
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 2.3
scene.camera = camera
for pos in [(2,-3,3),(-2,-2,-1)]:
    bpy.ops.object.light_add(type='AREA', location=pos)
    light=bpy.context.object
    light.data.energy=400
    light.data.size=3
    aim(light)
scene.render.filepath = str(ROOT/'artifacts/source-model.png')
bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'assets/source/throw_objekt.fbx'))
arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
report['animation'] = {'bones':[b.name for b in arm.data.bones], 'actions':[{'name':a.name, 'frames':list(a.frame_range)} for a in bpy.data.actions]}
(ROOT/'artifacts/source-inspection.json').write_text(json.dumps(report, indent=2))
