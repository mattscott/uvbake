"""Parse a (UVBake-written) binary FBX and (1) report its UV/normal layers, (2) render the packed UV
atlas (island edges in UV space) to a PNG. For quick visual inspection of an unwrap."""
import struct
import sys
import zlib

import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.collections import LineCollection

path, out = sys.argv[1], sys.argv[2]
data = open(path, "rb").read()
assert data[:20] == b"Kaydara FBX Binary  "
version = struct.unpack_from("<I", data, 23)[0]
off = 8 if version >= 7500 else 4


def read_off(p):
    return (struct.unpack_from("<q" if off == 8 else "<I", data, p)[0], p + off)


def read_props(p, n):
    out_props = []
    for _ in range(n):
        t = chr(data[p]); p += 1
        if t == "Y": out_props.append(struct.unpack_from("<h", data, p)[0]); p += 2
        elif t == "C": out_props.append(data[p]); p += 1
        elif t == "I": out_props.append(struct.unpack_from("<i", data, p)[0]); p += 4
        elif t == "F": out_props.append(struct.unpack_from("<f", data, p)[0]); p += 4
        elif t == "D": out_props.append(struct.unpack_from("<d", data, p)[0]); p += 8
        elif t == "L": out_props.append(struct.unpack_from("<q", data, p)[0]); p += 8
        elif t in "fdlib":
            length, enc, comp = struct.unpack_from("<III", data, p); p += 12
            payload = data[p:p + comp]; p += comp
            if enc == 1: payload = zlib.decompress(payload)
            out_props.append(np.frombuffer(payload, np.dtype("<" + {"f":"f","d":"d","l":"q","i":"i","b":"b"}[t])))
        elif t in "SR":
            ln = struct.unpack_from("<I", data, p)[0]; p += 4
            out_props.append(data[p:p + ln].decode("latin1") if t == "S" else data[p:p + ln]); p += ln
    return out_props, p


def read_node(p):
    end, p = read_off(p); nprops, p = read_off(p); _, p = read_off(p)
    nl = data[p]; p += 1
    if end == 0: return None, p
    name = data[p:p + nl].decode("latin1"); p += nl
    props, p = read_props(p, nprops)
    kids = []
    while p < end:
        k, p = read_node(p)
        if k is None: break
        kids.append(k)
    return {"name": name, "props": props, "children": kids}, end


pos = 27; top = []
while pos + 3 * off + 1 <= len(data):
    n, pos = read_node(pos)
    if n is None: break
    top.append(n)


def child(node, name):
    return next((c for c in node["children"] if c["name"] == name), None)


geo = None
for objs in (n for n in top if n["name"] == "Objects"):
    geo = child(objs, "Geometry")
    if geo:
        break

pvi = np.asarray(child(geo, "PolygonVertexIndex")["props"][0], np.int64)
nlayer = child(geo, "LayerElementNormal")
ulayer = child(geo, "LayerElementUV")
n_normals = len(child(nlayer, "Normals")["props"][0]) // 3 if nlayer else 0
uv = np.asarray(child(ulayer, "UV")["props"][0], np.float64).reshape(-1, 2) if ulayer else None
uvindex = np.asarray(child(ulayer, "UVIndex")["props"][0], np.int64) if ulayer and child(ulayer, "UVIndex") else None
print(f"verts={len(child(geo,'Vertices')['props'][0])//3}  polygon-vertices={len(pvi)}  "
      f"normals={n_normals}  uvs={0 if uv is None else len(uv)}  "
      f"normalLayer={'yes' if nlayer else 'NO'}  uvLayer={'yes' if ulayer else 'NO'}")

# Resolve a UV per polygon-vertex (IndexToDirect via UVIndex, else Direct).
uv_pv = uv[uvindex] if uvindex is not None else uv

# Build face edge segments in UV space.
segs = []
face = []
for pv, idx in enumerate(pvi):
    face.append(pv)
    if idx < 0:
        for i in range(len(face)):
            a, b = uv_pv[face[i]], uv_pv[face[(i + 1) % len(face)]]
            segs.append([(a[0], a[1]), (b[0], b[1])])
        face = []

lo = uv_pv.min(0); hi = uv_pv.max(0)
fig, ax = plt.subplots(figsize=(9, 9), dpi=150)
ax.add_collection(LineCollection(segs, colors=(0.10, 0.30, 0.65), linewidths=0.35))
ax.add_patch(plt.Rectangle((0, 0), 1, 1, fill=False, ec=(0.8, 0.2, 0.2), lw=1.0))  # the 0–1 tile
m = 0.03
ax.set_xlim(min(0, lo[0]) - m, max(1, hi[0]) + m)
ax.set_ylim(min(0, lo[1]) - m, max(1, hi[1]) + m)
ax.set_aspect("equal")
ax.set_title(f"UV atlas — {len(segs)} edges; UV bounds [{lo[0]:.3f},{lo[1]:.3f}]–[{hi[0]:.3f},{hi[1]:.3f}]",
             fontsize=10)
fig.tight_layout()
fig.savefig(out, dpi=150, bbox_inches="tight")
print("wrote", out)
