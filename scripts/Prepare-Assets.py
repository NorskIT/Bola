"""Deterministic prototype: cropped source grip/weights, rebuilt ropes, UVs, two LODs.
No source robot meshes are exported. This is a geometry prototype, not polished art.
"""
import bpy, bmesh, json, hashlib, math
from pathlib import Path
from mathutils import Vector, Matrix
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'unity/Assets/Generated'
OUT.mkdir(parents=True, exist_ok=True)
assert bpy.app.version == (4,5,3)
for name, expected in json.loads((ROOT/'assets/source/hashes.json').read_text()).items():
    assert hashlib.sha256((ROOT/'assets/source'/name).read_bytes()).hexdigest().upper() == expected, name
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets/source/bola.glb'))
source = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
source.data.transform(source.matrix_world); source.matrix_world = Matrix.Identity(4)
regions = json.loads((ROOT/'assets/selection-regions.json').read_text())
leather = bpy.data.materials.new('Leather'); leather.diffuse_color = (.17,.10,.055,1)
flint = bpy.data.materials.new('Flint'); flint.diffuse_color = (.32,.33,.30,1)
def crop(name, region, length, budget, material):
    bm = bmesh.new(); bm.from_mesh(source.data)
    for axis in range(3):
        for side, sign in [('min',1),('max',-1)]:
            normal = Vector(); normal[axis] = sign
            plane = Vector(); plane[axis] = region[side][axis]
            bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces), dist=.00001,
                                  plane_co=plane, plane_no=normal, clear_inner=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    assert len(bm.faces)>50, name
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    data = bpy.data.meshes.new(name); bm.to_mesh(data); bm.free()
    obj = bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    coords = [v.co.copy() for v in data.vertices]
    lo = Vector([min(v[i] for v in coords) for i in range(3)])
    hi = Vector([max(v[i] for v in coords) for i in range(3)])
    scale = length/max(hi-lo)
    data.transform(Matrix.Scale(scale,4) @ Matrix.Translation(-(lo+hi)/2))
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    data.calc_loop_triangles()
    dec=obj.modifiers.new('Budget','DECIMATE'); dec.ratio=min(1,budget/len(data.loop_triangles))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    tri=obj.modifiers.new('Triangles','TRIANGULATE'); bpy.ops.object.modifier_apply(modifier=tri.name)
    obj.data.materials.clear(); obj.data.materials.append(material)
    for p in obj.data.polygons: p.material_index=0
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.03); bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    return obj
bpy.ops.object.select_all(action='DESELECT')
parts=[]
grip=crop('Grip',regions['grip'],.14,650,leather)
grip.data.transform(Matrix.Rotation(-math.pi/2,4,'Y'))
parts.append(grip)
anchors=[]
for i, region in enumerate(regions['weights']):
    weight=crop('Weight'+str(i),region,.11,850,flint)
    weight.location=Vector(((i-1)*.095, [0,.05,-.045][i], -[.78,.72,.83][i]))
    parts.append(weight)
    end=weight.location+Vector((0,0,.04)); start=Vector((0,0,-.07))
    anchors.append({'name':weight.name,'start':list(start),'end':list(end),'segments':8})
    curve=bpy.data.curves.new('Rope'+str(i),'CURVE'); curve.dimensions='3D'
    curve.bevel_depth=.004; curve.bevel_resolution=1; curve.resolution_u=1
    spline=curve.splines.new('POLY'); spline.points.add(8)
    for j,p in enumerate(spline.points):
        q=start.lerp(end,j/8); q.y+=math.sin(j/8*math.pi)*.025; p.co=(*q,1)
    obj=bpy.data.objects.new('Rope'+str(i),curve); bpy.context.collection.objects.link(obj)
    obj.data.materials.append(leather); bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.convert(target='MESH'); obj=bpy.context.object; obj.select_set(False); parts.append(obj)
bpy.data.objects.remove(source,do_unlink=True)
def export(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/name), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
def triangles(objects):
    count=0
    for obj in objects: obj.data.calc_loop_triangles(); count+=len(obj.data.loop_triangles)
    return count
close_count=triangles(parts); assert close_count<=5000, close_count
export(parts,'BolaClose.fbx')
report={'closeTriangles':close_count,'gripLength':.14,'weightMaxDimension':.11,'ropes':anchors}
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'assets/prepared/prototype.blend'))
for obj in parts:
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('DistantBudget','DECIMATE'); mod.ratio=.28
    bpy.ops.object.modifier_apply(modifier=mod.name)
far_count=triangles(parts); assert far_count<=1500,far_count
export(parts,'BolaDistant.fbx'); report['distantTriangles']=far_count
(ROOT/'artifacts/geometry-report.json').write_text(json.dumps(report,indent=2))
print('BOLA_GEOMETRY_OK',close_count,far_count)
