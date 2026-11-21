# UV Mapping Fundamentals — the atlas as a data artifact

This document defines what a UV atlas *is* and the properties UVBake must guarantee. The algorithms
that produce them are in [`02`](02-the-atlas-pipeline.md)–[`05`](05-packing-and-texel-density.md).

## 1. What a UV map is

A **UV map** (parameterization) assigns every surface point a 2D texture coordinate `(u,v)` in the
`[0,1]²` tile. On a triangle mesh it's defined by a `(u,v)` per vertex-corner and interpolated affinely
within each triangle. A texture then "wraps" the surface by sampling at each point's `(u,v)`; a baker
goes the other way, walking texels and finding the surface point each one represents.

## 2. Charts, islands, seams

A curved or closed surface can't be flattened in one piece (§4), so it is cut into pieces:
- **Chart** — a connected, **disk-topology** patch flattened as a unit.
- **Seam** — an edge where the surface is cut (so it can flatten, or where two charts meet). At a seam
  the UVs **split**: one mesh vertex carries *two* `(u,v)` values, one per side.
- **Island** — a chart's footprint in the packed 2D atlas.

The number/placement of seams is a quality lever: more cuts ⇒ lower distortion but more islands, more
wasted gutter, and more visible boundaries (see [`03`](03-chart-segmentation-and-seams.md)).

## 3. The unique / injective requirement (non-negotiable for baking)

UVBake's hard invariant: the layout is **injective** — *no two surface points (faces) map to the same
`(u,v)`*. Equivalently, no two UV triangles overlap and none is flipped. This is what *"a unique layout
for every polygon face"* means.

Why it's non-negotiable: a bake is texel → surface. If two faces share a texel, that texel must store
two unrelated signals — one overwrites the other and the loser samples a wrong neighbour (bleed,
ghosting, inverted normals). As Smith & Schaefer put it, a non-bijective map means *"a single point in
the texture could map to multiple, disconnected regions of the surface … rendering the parameterization
useless for this application."*[^ss] Position/normal/AO bakes specifically break on overlap.[^poly]

Three sub-conditions, enforced at different stages ([`02`](02-the-atlas-pipeline.md)):
- **No flips** (`det J > 0` per triangle) — at parameterization.
- **No intra-chart self-overlap** — at parameterization (barrier methods).
- **No inter-chart overlap** — at packing, then validated globally.

Intentional mirrored/overlapping UVs (instancing) are pushed to a **separate UDIM tile** so the active
tile stays injective.[^poly]

## 4. Distortion — why a curved surface can't flatten cleanly

Flattening stretches the surface. Per triangle, the parameterization's 2×2 Jacobian has **singular
values** `σ₁ ≥ σ₂` — the max/min local stretch. The three ideals:

| Property | Condition | Preserves |
|---|---|---|
| **Conformal** | `σ₁ = σ₂` | angles (shape) — area may drift |
| **Authalic** | `σ₁·σ₂ = 1` | area — angles may shear |
| **Isometric** | `σ₁ = σ₂ = 1` | both — distances exactly |

**Gauss's Theorema Egregium:** an isometric flattening exists **only for developable surfaces** (zero
Gaussian curvature). Any curved chart must give up angles or area — the conformal↔authalic trade UVBake
balances. The standard scalar measures (Sander et al. stretch `L²=√((σ₁²+σ₂²)/2)`, `L∞=σ₁`) are in
[`04`](04-parameterization-and-distortion.md) §4 and used as quality metrics in
[`07`](07-quality-artifacts-and-validation.md).

## 5. Texel density

**Texel density** = texture pixels per unit of real surface area (px/m or px/cm). **Normalized** =
consistent across all islands, so no region is sharper/blurrier than another. It's set by **per-chart
scaling before packing** (scale by `√(3D area / UV area)` to a global target) —
[`05`](05-packing-and-texel-density.md) §3. "Largest normalized islands" therefore means: islands as
large as the distortion budget allows, *all at one density*.

## 6. The bake contract UVBake must satisfy

The downstream baker imposes concrete requirements; UVBake produces and self-checks them
([`07`](07-quality-artifacts-and-validation.md)):

- **Injective / non-overlapping** in the active tile (§3).
- **Seams on every hard edge** (tangent-space requirement — [`03`](03-chart-segmentation-and-seams.md) §2).
- **Inter-island gutter ≥ 2× the baker's dilation padding** ([`05`](05-packing-and-texel-density.md) §2).
- **Consistent texel density** (§5).
- A **deterministic triangulation** the baker can reproduce (shortest-diagonal, UV tiebreak) —
  [`02`](02-the-atlas-pipeline.md) §3.

## 7. Output: per-wedge UVs

A seam needs **two UVs at one position**. **OBJ** expresses this natively — `v`, `vt`, `vn` are
independent index lists and a face corner is `f v/vt/vn`, so the two sides of a seam share `v` but use
different `vt`. **glTF** stores UV as a *per-vertex* attribute (`TEXCOORD_0`) under one shared index
buffer, so a seam **requires duplicating the vertex**. UVBake emits **OBJ first**; details in
[`02`](02-the-atlas-pipeline.md) §6 / [`09`](09-architecture-and-roadmap.md).

---

[^ss]: Smith & Schaefer, *Bijective Parameterization with Free Boundaries*, 2015. https://people.engr.tamu.edu/schaefer/research/bijective.pdf
[^poly]: Polycount wiki — Texture Baking (overlap breaks position/normal bakes; UDIM offset for mirrors). http://wiki.polycount.com/wiki/Texture_Baking
