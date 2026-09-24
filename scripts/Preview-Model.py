"""Render the generated prototype geometry for review, without editing source assets."""
import bpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'assets/prepared/prototype.blend'))
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE_NEXT'
scene.render.resolution_x=700; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.world.color=(.2,.2,.2)
center=Vector((0,0,-.4))
def aim(obj): obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1,-3,0)); camera=bpy.context.object; aim(camera)
camera.data.type='ORTHO'; camera.data.ortho_scale=1.15; scene.camera=camera
for position in [(1,-2,2),(-2,-1,0)]:
    bpy.ops.object.light_add(type='AREA',location=position); light=bpy.context.object
    light.data.energy=200; light.data.size=2; aim(light)
scene.render.filepath=str(ROOT/'artifacts/prepared-model.png'); bpy.ops.render.render(write_still=True)
