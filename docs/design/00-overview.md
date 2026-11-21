# UVBake — Project Overview & Research Index

> **Status: research / pre-scaffold.** This directory holds the background research the tool will be
> built on. The implementation roadmap lives in [`../Plans`](../Plans).

## 1. What UVBake is

**UVBake** automatically generates a **UV atlas** for an input mesh: it cuts the surface into
**charts**, flattens each chart into the 2D texture plane with **bounded distortion**, scales every
chart to a **consistent texel density**, and **packs** them into a single atlas. The result is a
**unique, non-overlapping (injective)** UV layout — *every polygon face has its own texels* — with the
**largest low-distortion islands** the distortion budget allows and **seams routed along hard edges**.

This is the standard automatic "texture atlas generation" problem (segment → parameterize → pack),
tuned for one purpose: producing UVs that a downstream **texture/normal baker** can bake into cleanly.

## 2. Where UVBake sits

UVBake fills the gap between a remesher that produces clean topology **without** UVs and a baker that
**requires** them:

```
  mesh (no UVs)  ──►  UVBake (this tool)  ──►  same mesh + bake-ready UV atlas  ──►  texture/normal baker
  (from an             segment → flatten →        unique, non-overlapping,           (bakes detail into
   upstream remesher)  normalize → pack           large low-distortion islands        the UV-space map)
```

UVBake answers *"how do I give this mesh a clean, unique UV layout?"* — it does **not** retopologize
(that is the upstream remesher's job) and it does **not** bake textures (that is the downstream
baker's job).

## 3. Design philosophy

| Principle | Consequence for UVBake |
|---|---|
| **Managed C# / .NET 8** | Pure managed core; sparse linear solves (conformal/ARAP) on a managed solver; `System.Numerics` for geometry. |
| **Dependency-light, no native deps** | Segmentation, parameterization, packing, and mesh I/O are in-repo or pure-managed — no native xatlas/UVAtlas/CGAL. |
| **MIT/permissive-clean, clean-room** | Algorithms reimplemented from papers; reference unwrappers used only to *generate golden outputs*, never linked or copied. (CGAL's GPL parameterization is avoided.) |
| **Library + CLI** | `UVBake.Core` (engine) + `UVBake.Cli` (`uvbake in.obj -o out.obj --res 2048 …`). |
| **Deterministic, golden-tested** | A reference unwrapper (candidate: **xatlas**, with **Blender** headless as a cross-check) produces golden layouts/metrics we validate against (overlap = 0, coverage, stretch, seam length, packing %). |

See [`09-architecture-and-roadmap.md`](09-architecture-and-roadmap.md) for the proposed module layout
and MVP plan.

## 4. The core idea in one paragraph

A UV map assigns every surface point a 2D `(u,v)` coordinate. A closed or curved surface cannot be
flattened without cutting and without distortion, so UVBake (a) **segments** the mesh into
disk-topology **charts**, cutting along **hard edges** and wherever curvature would otherwise force too
much stretch; (b) **parameterizes** each chart into 2D with a method that guarantees **no flipped or
overlapping triangles** while keeping **distortion bounded** (angles and areas close to the surface);
(c) **normalizes** every chart to the same **texel density** so detail is uniform; and (d) **packs**
the charts into the atlas with a **gutter** between islands. The whole game is the three-way trade
between **seam count** (fewer, larger islands), **distortion** (low stretch), and **packing
efficiency** (few wasted texels) — resolved here in favor of *large, low-distortion, unique islands*.

## 5. The three things that decide whether a UV atlas is good

These recur throughout the docs:

1. **Chart segmentation & seam placement.** Where you cut sets the floor on distortion and the seam
   count. Cuts must include every hard edge; beyond that, fewer/larger charts are better until
   distortion exceeds budget. See [`03-chart-segmentation-and-seams.md`](03-chart-segmentation-and-seams.md).
2. **Parameterization & injectivity.** Each chart must flatten with **no overlaps/flips** (baking
   requires a unique layout) at **bounded distortion**. See
   [`04-parameterization-and-distortion.md`](04-parameterization-and-distortion.md).
3. **Packing & texel density.** Islands are scaled to a uniform density and packed tightly with a
   gutter sized for a baker's dilation skirt. See
   [`05-packing-and-texel-density.md`](05-packing-and-texel-density.md).

## 6. Scope

**In scope for the MVP:** triangle/quad mesh input; hard-edge detection (dihedral angle + provided
tags); chart segmentation; bounded-distortion, injective per-chart parameterization; texel-density
normalization; atlas packing with a gutter; OBJ output with per-wedge UVs (seams round-trip).

**Likely fast-follow:** UDIM multi-tile output; glTF output; chart rotation/packing-efficiency modes;
adaptive (curvature-driven) texel density; an "aggressive merge" island-size mode.

**Explicitly out of scope:** retopology / quad-layout generation (an upstream concern); the actual
texture/normal **baking** (a downstream concern); UV editing UI.

## 7. The research document set

| Doc | Topic |
|---|---|
| [`01-uv-mapping-fundamentals.md`](01-uv-mapping-fundamentals.md) | UVs, charts/islands/seams, distortion types, texel density, the unique/injective requirement. |
| [`02-the-atlas-pipeline.md`](02-the-atlas-pipeline.md) | The end-to-end segment → parameterize → normalize → pack pipeline. |
| [`03-chart-segmentation-and-seams.md`](03-chart-segmentation-and-seams.md) | Chart growth, seam routing on hard edges, distortion-bounded charts, OptCuts/Autocuts. |
| [`04-parameterization-and-distortion.md`](04-parameterization-and-distortion.md) | Tutte/LSCM/ABF++/ARAP/SLIM/BFF, injectivity, distortion metrics. |
| [`05-packing-and-texel-density.md`](05-packing-and-texel-density.md) | Chart packing, gutters/margin, texel-density normalization, UDIM. |
| [`06-acceleration-and-performance.md`](06-acceleration-and-performance.md) | Sparse linear solves, managed SIMD, overlap/coverage checks, determinism. |
| [`07-quality-artifacts-and-validation.md`](07-quality-artifacts-and-validation.md) | Overlaps/flips, distortion heatmaps, seam minimization, QC, the bake-contract checks. |
| [`08-prior-art-tools-and-libraries.md`](08-prior-art-tools-and-libraries.md) | xatlas, UVAtlas, Ministry of Flat, RizomUV, Blender; libraries + licenses; oracle choice. |
| [`09-architecture-and-roadmap.md`](09-architecture-and-roadmap.md) | Proposed C#/.NET 8 module layout, dependency decisions, CLI, MVP → vNext. |
| [`references.md`](references.md) | Consolidated bibliography (papers, docs, repos) with URLs. |

## 8. Glossary

- **UV / parameterization:** an assignment of 2D `(u,v)` texture coordinates to a mesh's surface.
- **Chart:** a connected, disk-topology piece of the mesh that is flattened as a unit.
- **Island:** a chart's footprint in the packed 2D atlas.
- **Seam:** an edge where the surface is cut so it can flatten / where two islands meet; UVs split there.
- **Injective / unique layout:** no two surface points (faces) map to the same UV location — required
  so a baker has dedicated texels per face.
- **Distortion:** how much the flattening stretches the surface — **conformal** (preserves angles),
  **authalic** (preserves area), **isometric** (both, impossible in general for curved surfaces).
- **Stretch metric:** a per-triangle measure of distortion from the parameterization's Jacobian
  singular values (Sander et al. 2001).
- **Texel density:** texture pixels per unit of surface area; **normalized** = consistent across islands.
- **Gutter / margin:** empty space between islands so a baker's dilation/filtering doesn't bleed across.
- **Packing efficiency:** fraction of atlas texels covered by islands (vs wasted).
- **Oracle / golden:** a trusted reference unwrapper whose outputs/metrics we diff against in tests.
