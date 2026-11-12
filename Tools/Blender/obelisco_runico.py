# Vhaldren — modelo original generado por script en Blender (sin interfaz).
# Uso: blender --background --python obelisco_runico.py -- <ruta_salida.fbx>
# Crea un obelisco rúnico: base escalonada, columna de cuatro caras afilada,
# punta piramidal y dos anillos de runas flotantes (objeto aparte para brillo).
import sys
import math
import bpy

out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "obelisco_runico.fbx"

bpy.ops.wm.read_factory_settings(use_empty=True)


def bevel(obj, width=0.04, segments=2):
    mod = obj.modifiers.new("Bisel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def cube(name, size, location):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    bevel(obj)
    return obj


parts = []
# Base escalonada (tres peldaños).
parts.append(cube("Peldano1", (2.6, 2.6, 0.30), (0, 0, 0.15)))
parts.append(cube("Peldano2", (2.0, 2.0, 0.28), (0, 0, 0.44)))
parts.append(cube("Peldano3", (1.5, 1.5, 0.30), (0, 0, 0.73)))

# Columna de cuatro caras que se estrecha hacia arriba.
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.78, radius2=0.46, depth=3.4, location=(0, 0, 2.58),
                                rotation=(0, 0, math.radians(45)))
shaft = bpy.context.active_object
shaft.name = "Columna"
bevel(shaft, 0.03)
parts.append(shaft)

# Punta piramidal.
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.52, radius2=0.0, depth=0.85, location=(0, 0, 4.7),
                                rotation=(0, 0, math.radians(45)))
tip = bpy.context.active_object
tip.name = "Punta"
parts.append(tip)

# Grietas: tres muescas restadas a la columna para que no sea un bloque perfecto.
for i, (z, angle) in enumerate([(1.7, 20), (2.6, 140), (3.4, 260)]):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.55 * math.cos(math.radians(angle)),
                                                      0.55 * math.sin(math.radians(angle)), z))
    notch = bpy.context.active_object
    notch.scale = (0.35, 0.12, 0.18)
    notch.rotation_euler = (0.3, 0.2, math.radians(angle))
    mod = shaft.modifiers.new("Grieta%d" % i, "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = notch
    bpy.context.view_layer.objects.active = shaft
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(notch, do_unlink=True)

# Unir piedra en un solo objeto.
bpy.ops.object.select_all(action="DESELECT")
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = shaft
bpy.ops.object.join()
stone = bpy.context.active_object
stone.name = "Obelisco"
bpy.ops.object.shade_flat()

# Anillos de runas (objeto aparte: en Unity llevan material emisivo).
rings = []
for z, radius in [(2.0, 1.05), (3.3, 0.85)]:
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.045, major_segments=40,
                                     minor_segments=6, location=(0, 0, z))
    rings.append(bpy.context.active_object)
bpy.ops.object.select_all(action="DESELECT")
for r in rings:
    r.select_set(True)
bpy.context.view_layer.objects.active = rings[0]
bpy.ops.object.join()
bpy.context.active_object.name = "Runas"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
print("[Vhaldren] Exportado", out)
