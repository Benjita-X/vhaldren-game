
import bpy,bmesh,math,random,json,pathlib,hashlib
from mathutils import Vector
R = pathlib.Path(__file__).resolve().parents[1]
src = R / "ST_01_v0007.blend"
dst = R / "ST_01_v0008.blend"
out_render = R / "Renders" / "v0008_perspectiva.png"
out_report = R / "Reports" / "v0008_auditoria.json"
sha=hashlib.sha256(src.read_bytes()).hexdigest()
o=bpy.data.objects["ST_01_Cimiento"]
o.name="ST_01_v0008"
# Guardar referencia a la malla anterior en otra posición, fuera de render.
ref=o.copy();ref.data=o.data.copy();bpy.context.scene.collection.objects.link(ref)
ref.name="ST_01_v0007_REFERENCIA";ref.location.x=-4;ref.hide_render=True
for name in ("ST_01_HIGH","ST_01_Cimiento_LOD1"):
 old=bpy.data.objects.get(name)
 if old:old.hide_render=True
def active(x):
 bpy.ops.object.select_all(action="DESELECT")
 x.hide_set(False);x.select_set(True)
 bpy.context.view_layer.objects.active=x
def cutter(name,x,y,z,a,b,c,angle=0):
 points=[(-1,-1,-.2),(.9,-1,-.14),(-.9,.95,-.42),(.95,.91,-.38),
         (-.9,-.98,.92),(.78,-.8,1),(-.8,.85,1.01),(.9,.8,.9),
         (-.1,-.15,-1.05),(.2,.21,1.15)]
 verts=[]
 rng=random.Random(510+sum(ord(v) for v in name))
 for px,py,pz in points:
  px*=a*(1+rng.uniform(-.07,.07));py*=b*(1+rng.uniform(-.07,.07));pz*=c*(1+rng.uniform(-.05,.05))
  verts.append((x+px*math.cos(angle)-py*math.sin(angle),y+px*math.sin(angle)+py*math.cos(angle),z+pz))
 bm=bmesh.new();vs=[bm.verts.new(pt) for pt in verts]
 res=bmesh.ops.convex_hull(bm,input=vs,use_existing_faces=False)
 if res.get("geom_interior"):bmesh.ops.delete(bm,geom=res["geom_interior"],context="VERTS")
 me=bpy.data.meshes.new(name);bm.to_mesh(me);bm.free();me.update()
 obj=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(obj);return obj
# Los cortes convexos forman PLANOS de fractura en vez de cavidades redondas.
for props in [
 ("Fractura_frontal_izquierda",-.62,-.52,.48,.25,.23,.23,-.12),
 ("Fractura_frontal_derecha",.58,-.52,.47,.26,.25,.22,.19),
 ("Fractura_lateral_superior",.92,.31,.49,.19,.24,.18,.41)]:
 cut=cutter(*props)
 mod=o.modifiers.new("Cincel_angular","BOOLEAN")
 mod.operation="DIFFERENCE";mod.solver="EXACT";mod.object=cut
 active(o);bpy.ops.object.modifier_apply(modifier=mod.name)
 bpy.data.objects.remove(cut,do_unlink=True)
# Reparación topológica consolidando las intersecciones superpuestas de la v0007.
active(o)
o.data.remesh_voxel_size=.009
o.data.remesh_voxel_adaptivity=0.0
bpy.ops.object.voxel_remesh()
sm=o.modifiers.new("Suavizar_solo_escalones_voxel","SMOOTH")
sm.factor=.4;sm.iterations=2
active(o);bpy.ops.object.modifier_apply(modifier=sm.name)
# Proteger módulo 2.0x1.009x0.5 y base plana.
verts=o.data.vertices
mn=[min(v.co[i] for v in verts) for i in range(3)]
mx=[max(v.co[i] for v in verts) for i in range(3)]
for v in verts:
 v.co.x=2*(v.co.x-mn[0])/(mx[0]-mn[0])-1
 v.co.y=1.009*(v.co.y-mn[1])/(mx[1]-mn[1])-.5045
 v.co.z=.5*(v.co.z-mn[2])/(mx[2]-mn[2])
 if v.co.z<.01:v.co.z=0
o.data.update()
# Malla fuente de más detalle para futuro bake
hero=o.copy();hero.data=o.data.copy();bpy.context.scene.collection.objects.link(hero)
hero.name="ST_01_v0008_ALTA_pintura_normal";hero.location.x=4;hero.hide_render=True
dec=o.modifiers.new("LOD0_aprox_3300","DECIMATE")
dec.ratio=min(1.0,3300/max(1,len(o.data.polygons)*2))
active(o);bpy.ops.object.modifier_apply(modifier=dec.name)
tri=o.modifiers.new("Triangulado_para_juego","TRIANGULATE")
active(o);bpy.ops.object.modifier_apply(modifier=tri.name)
active(o);bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(island_margin=.015)
bpy.ops.object.mode_set(mode="OBJECT")
# Material original tenía mapas baked de otra topología; no reutilizar sin rebake.
m=bpy.data.materials.new("M_ST_01_Granito_v0008")
m.use_nodes=True;m.diffuse_color=(.40,.41,.39,1)
n=m.node_tree.nodes;n.clear();lk=m.node_tree.links
out=n.new("ShaderNodeOutputMaterial");out.location=(750,100)
bs=n.new("ShaderNodeBsdfPrincipled");bs.location=(470,100)
bs.inputs["Roughness"].default_value=.86
lk.new(bs.outputs["BSDF"],out.inputs["Surface"])
tx=n.new("ShaderNodeTexCoord");tx.location=(-850,120)
ns=n.new("ShaderNodeTexNoise");ns.location=(-580,190)
ns.inputs["Scale"].default_value=4.1
ns.inputs["Detail"].default_value=3
lk.new(tx.outputs["Generated"],ns.inputs["Vector"])
r=n.new("ShaderNodeValToRGB");r.location=(-300,230)
r.color_ramp.elements[0].position=.22
r.color_ramp.elements[0].color=(.24,.27,.27,1)
r.color_ramp.elements[1].position=.80
r.color_ramp.elements[1].color=(.53,.52,.49,1)
lk.new(ns.outputs["Fac"],r.inputs["Fac"])
nsm=n.new("ShaderNodeTexNoise");nsm.location=(-560,-180)
nsm.inputs["Scale"].default_value=78
lk.new(tx.outputs["Generated"],nsm.inputs["Vector"])
bump=n.new("ShaderNodeBump");bump.location=(210,-140)
bump.inputs["Strength"].default_value=.10
bump.inputs["Distance"].default_value=.018
lk.new(nsm.outputs["Fac"],bump.inputs["Height"])
lk.new(bump.outputs["Normal"],bs.inputs["Normal"])
mix=n.new("ShaderNodeMixRGB");mix.blend_type="MIX";mix.location=(80,150)
mix.inputs[2].default_value=(.19,.30,.22,1)
lk.new(r.outputs["Color"],mix.inputs[1])
# solo musgo bajo y concentrado en zonas variables
xyz=n.new("ShaderNodeSeparateXYZ");xyz.location=(-600,-470)
lk.new(tx.outputs["Generated"],xyz.inputs["Vector"])
low=n.new("ShaderNodeMapRange");low.location=(-330,-410)
low.inputs["From Min"].default_value=.03
low.inputs["From Max"].default_value=.6
low.inputs["To Min"].default_value=.25
low.inputs["To Max"].default_value=0
lk.new(xyz.outputs["Z"],low.inputs["Value"])
fac=n.new("ShaderNodeMath");fac.operation="MULTIPLY";fac.location=(-70,-260)
lk.new(low.outputs["Result"],fac.inputs[0]);lk.new(ns.outputs["Fac"],fac.inputs[1])
lk.new(fac.outputs[0],mix.inputs[0])
lk.new(mix.outputs["Color"],bs.inputs["Base Color"])
o.data.materials.clear();o.data.materials.append(m)
for p in o.data.polygons:p.use_smooth=False
# un LOD1 bajo el mismo contrato
lod=o.copy();lod.data=o.data.copy();bpy.context.scene.collection.objects.link(lod)
lod.name="ST_01_Cimiento_LOD1_v0008";lod.location.x=4;lod.hide_render=True
de=lod.modifiers.new("LOD1_1400","DECIMATE")
de.ratio=min(1,1400/max(1,len(lod.data.polygons)))
active(lod);bpy.ops.object.modifier_apply(modifier=de.name)
def audit(obj):
 bm=bmesh.new();bm.from_mesh(obj.data)
 a={"vertices":len(bm.verts),"tris":sum(len(f.verts)-2 for f in bm.faces),
    "non_manifold":sum(not e.is_manifold for e in bm.edges),
    "boundary":sum(e.is_boundary for e in bm.edges),
    "degenerate":sum(f.calc_area()<1e-9 for f in bm.faces),
    "uv":len(obj.data.uv_layers),"materials":len(obj.data.materials),
    "dims":[round(float(v),4) for v in obj.dimensions]}
 bm.free();return a
result={"LOD0":audit(o),"LOD1":audit(lod),"reference_file":src.name,
        "source_unchanged":hashlib.sha256(src.read_bytes()).hexdigest()==sha}
print("AUDIT",json.dumps(result))
if result["LOD0"]["non_manifold"] or result["LOD1"]["non_manifold"]:
 raise RuntimeError("Quality gate failed: topologia aun no manifold")
# escena de presentación; el mapa original queda en otra sesión de Blender.
camera=bpy.data.objects["Cam"];camera.location=(2.4,-3.35,1.65)
camera.rotation_euler=(Vector((0,0,.25))-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type="ORTHO";camera.data.ortho_scale=3
bpy.context.scene.camera=camera
bpy.context.scene.render.engine="BLENDER_EEVEE"
bpy.context.scene.eevee.taa_render_samples=48
bpy.context.scene.render.resolution_x=1250
bpy.context.scene.render.resolution_y=780
bpy.context.scene.render.resolution_percentage=100
for obj in bpy.context.scene.objects:
 if obj.name.startswith("ST_01"):obj.hide_render=obj is not o
bpy.ops.wm.save_as_mainfile(filepath=str(dst),check_existing=False)
out_report.write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding="utf-8")
bpy.context.scene.render.filepath=str(out_render)
bpy.ops.render.render(write_still=True)
print("FINAL_SAVED",dst.name,"bytes",dst.stat().st_size,"render_bytes",out_render.stat().st_size)
