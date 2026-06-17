# The Atlas Pipeline — segment → cut → parameterize → normalize → pack

This is the end-to-end recipe the field converged on (Lévy et al. 2002's automatic atlas, refined by
Sorkine et al. 2002's bounded-distortion cutting), and the shape of UVBake's core. Each later doc
expands one stage.

## 1. The pipeline at a glance

```text
 mesh (no UVs)
   → Stage 0  condition + detect hard edges
   → Stage 1  SEGMENT into charts (grow largest low-distortion charts; seams on hard edges)   [doc 03]
   → Stage 2  CUT each chart to a topological disk (handle cuts where genus>0)                [doc 03]
   → Stage 3  PARAMETERIZE each chart to 2D, injective + bounded distortion                   [doc 04]
   → Stage 4  NORMALIZE texel density (per-chart scale to one global texels-per-unit)         [doc 05]
   → Stage 5  PACK charts into the atlas, no overlap, with a gutter                           [doc 05]
   → mesh + bake-ready UV atlas (per-wedge UVs)
```

Reference embodiment: **xatlas** (`ComputeCharts` = Stages 1–3, `PackCharts` = Stages 4–5).[^xatlas]

## 2. Stage inputs/outputs and where injectivity is enforced

| Stage | In → Out | Notes |
|---|---|---|
| **0 Condition** | mesh → half-edge mesh + per-edge dihedral + feature flags | detect **hard edges** (dihedral threshold ∪ provided tags) — these become forced seams |
| **1 Segment** | mesh + flags → face→chart labels | grow **largest** charts that stay near-developable and within the stretch budget; seams fall on hard edges ([`03`](03-chart-segmentation-and-seams.md)) |
| **2 Cut to disk** | chart → disk-topology chart | extra cuts where the distortion bound would break or to open handles (genus → 2g cuts) |
| **3 Parameterize** | disk chart → per-wedge UVs | **intra-chart injectivity enforced here** (no flips, no self-overlap) — LSCM/BFF init → SLIM polish ([`04`](04-parameterization-and-distortion.md)) |
| **4 Normalize** | UVs + per-chart 3D area → rescaled UVs | scale each chart by `√(3D area / UV area)` to one global density ([`05`](05-packing-and-texel-density.md) §3) |
| **5 Pack** | normalized charts → atlas placement | **inter-chart injectivity enforced here** (no chart overlaps); reserve the gutter by dilating silhouettes before placing ([`05`](05-packing-and-texel-density.md)) |
| **6 Validate** | atlas → pass/fail + metrics | global overlap=0 + coverage gate; stretch/seam/packing metrics ([`07`](07-quality-artifacts-and-validation.md)) |

**Injectivity is enforced in two places** — intra-chart at Stage 3 (barrier parameterization),
inter-chart at Stage 5 (packing) — and **validated globally** at Stage 6. (Alternatively, **SCAF**
fuses Stages 3–5: it parameterizes all charts *and* packs them in one domain with a global bijectivity
guarantee — [`04`](04-parameterization-and-distortion.md) §3.3.)

## 3. A cross-cutting decision: triangulation of quad input

The input is typically a **quad-dominant / pure-quad** mesh. All the Stage-1/3/5 machinery is
triangle-based, so quads are split into two triangles. **The split rule must be deterministic and
reproducible**, because a downstream tangent-space baker triangulates the *same* mesh to build its
tangent frames — if UVBake and the baker pick different diagonals, the baked normals are subtly wrong.

**Rule (matches MikkTSpace usage):** split each quad by its **shortest diagonal** (vertex positions);
on a tie, break by the shorter diagonal in **UV space**. Order-independent and mirror-safe.[^mikk] The
*initial* triangulation feeding parameterization (before UVs exist) tie-breaks deterministically on
vertex index/position; the *emitted* triangulation can then apply the UV tiebreak. UVBake pins and
documents this so the bake is bit-reproducible.

Parameterizing quads directly (bilinear) avoids the diagonal choice but most bakers triangulate anyway,
re-introducing the ambiguity at render time — so **triangulate consistently** is the recommended path.

## 4. Distinction: this is *unwrapping*, not quad-layout generation

UVBake **unwraps an existing mesh**. It does **not** decide where quad edges/singularities go — that
quad-layout problem (QuadCover, Mixed-Integer Quadrangulation, integer-grid maps) is the **upstream
remesher's** job. They share cross-field/parameterization math but are different deliverables: the
remesher answers *"where do the quads go,"* UVBake answers *"given this mesh, where does each face live
in texture space."*[^quadsurvey]

## 5. Output

Stage emits the mesh **with per-wedge UVs** — seams are coincident positions carrying different UVs.
**OBJ** (`v`/`vt`/`vn` independent indices) expresses this natively: a seam is a duplicated `vt` over a
shared `v`. **glTF** is per-vertex (`TEXCOORD_0`), so seams force vertex duplication on export. Emit
**OBJ first**, glTF next ([`09`](09-architecture-and-roadmap.md)). Format details:
[`01`](01-uv-mapping-fundamentals.md) §7.

---

[^xatlas]: xatlas (MIT) — the reference segment→parameterize→pack implementation. https://github.com/jpcy/xatlas
[^mikk]: MikkTSpace — shortest-diagonal-with-UV-tiebreak quad split. https://github.com/mmikk/MikkTSpace
[^quadsurvey]: Bommes et al., *Quad-Mesh Generation and Processing: A Survey*, CGF 2013. https://inria.hal.science/hal-01059755/en
