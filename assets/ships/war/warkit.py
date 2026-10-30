"""
War kit: the war economy's props (docs/superpowers/specs/2026-10-18-war-and-strife-design.md, section 5), built on
ship kit v2 so they share the navy's plate, paint and drives. Each prop is one script here writing
assets/ships/war/<Name>.glb, in metres, long axis along +X, Z up.

Every prop carries an "Owner" material: a painted band the game recolours per instance with the owner's empire
colour (Works.WarModel swaps it for a shader that reads INSTANCE_CUSTOM). Factories, slips and barracks are drawn
up to 160 at a time per system through one MultiMesh each, so props stay lean: a few thousand triangles, no
greeble fields, detail from silhouette and the tiling textures.
    SF_ROOT = r"C:/Users/diamo/Desktop/starforge"      # optional: build from another checkout
    exec(open(SF_ROOT + "/assets/ships/war/slip.py").read())
"""
SF_ROOT = globals().get("SF_ROOT", r"C:/Users/diamo/Desktop/starforge")
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit2.py").read())

M["Owner"] = mat("Owner", (0.62, 0.22, 0.16), 0.1, 0.55)        # previews only: the game repaints it per owner
M["Furnace"] = mat("Furnace", (1.0, 0.45, 0.12), emit=(1.0, 0.4, 0.1), strength=0.9)
if "Owner" not in SM: SM.append("Owner")
if "Furnace" not in SM: SM.append("Furnace")
OWN, FURN = SM.index("Owner"), SM.index("Furnace")


def owner_band(bm, at, axis, r, width, seg=16):
    """A painted owner-colour collar round a part of radius r."""
    band(bm, at, axis, r, width, max(r * 0.06, 0.3), mi=OWN, seg=seg)


def export_war(col, root, name):
    """Export this prop to <SF_ROOT>/assets/ships/war/<name>.glb (no .blend save: stations.blend is shared).
    Returns its triangle count and size."""
    folder = os.path.join(SF_ROOT, "assets", "ships", "war"); os.makedirs(folder, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(False)
    root.select_set(True)
    for o in col.objects: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(folder, name + ".glb"), export_format="GLB", use_selection=True, export_apply=True, export_lights=False)
    meshes = [o for o in col.objects if o.type == "MESH"]
    lo = Vector((1e9,) * 3); hi = -lo
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return {"tris": sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes), "dims_m": tuple(round(v, 1) for v in (hi - lo))}
