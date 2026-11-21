# Quality, Artifacts & Validation

UVBake has a **hard correctness contract** (injective, full coverage) plus **quality objectives**
(low distortion, large islands, tight packing, normalized density). This document covers the validation
gates, the artifact taxonomy, and the golden-test scorecard.

## 1. The hard gates (pass/fail, not tolerances)

These are **invariants**, checked before any atlas is emitted ([`01`](01-uv-mapping-fundamentals.md) §3):

- **No flips.** Per UV triangle, the signed area `½·det[u₂−u₁, u₃−u₁]` must share one sign across all
  faces of a chart. Any sign disagreement or zero = a fold/degeneracy. O(F).
- **No overlap (injectivity).** No two UV triangles intersect (within the active tile). Broad-phase
  cull by **AABBs in a uniform grid / BVH**, then exact **2D triangle–triangle** tests on survivors —
  near-O(F) for evenly sized triangles vs O(F²) brute force.[^pbrt] Intentional UDIM-tile offsets count
  as non-overlaps.
- **Full coverage.** Every input face is parameterized (no dropped/unassigned faces).

A failure here is a bug, not a quality knob — the bake would be corrupt.

## 2. Quality scorecard (tolerance metrics)

Recorded per mesh and diffed against the golden within tolerances:

| Metric | Definition | Good |
|---|---|---|
| **Max / area-weighted L² stretch** | Sander singular-value stretch (`L²=√((σ₁²+σ₂²)/2)`, `L∞=σ₁`), area-weighted[^stretch] | near 1.0 |
| **Max quasi-conformal `K`** | `σ₁/σ₂` (angle distortion) | near 1.0 |
| **Texel-density variance** | std-dev of per-chart texels-per-unit | ≈0 (normalized) |
| **Packing efficiency** | occupied texels ÷ atlas texels | high |
| **Chart / island count** | integer | low (large islands) — most sensitive regression signal |
| **Total seam length** | Σ cut-edge 3D length | low (short, hidden seams) |
| **Min inter-island gutter** | texels between nearest islands | ≥ required padding |

These four — stretch, density variance, packing %, seam length — are the headline quality numbers and
the levers the "balanced for baking" objective trades.

## 3. Artifact taxonomy (symptom → cause → fix)

| Symptom (downstream) | Cause | Fix |
|---|---|---|
| **Garbled/ghosted bake** | UV **overlap** (injectivity violated) | barrier parameterization (SLIM) + packing overlap test; push intentional mirrors to UDIM |
| **Inverted normals / wrong derivatives** | **flipped** triangle (`det J < 0`) | flip-barrier energy (symmetric Dirichlet); signed-area gate |
| **Blurry patch next to a crisp one** | inconsistent **texel density** | per-chart `√(3D/UV area)` normalization ([`05`](05-packing-and-texel-density.md) §3) |
| **Seam bleed under mip/filtering** | gutter < 2× dilation | reserve gutter by dilating silhouettes before packing |
| **Lighting seam at a hard edge** | hard edge **not** a UV seam | force seams on hard edges ([`03`](03-chart-segmentation-and-seams.md) §2) |
| **X-shaped shading errors after bake** | UVBake/baker disagree on quad **diagonal** | pin the deterministic shortest-diagonal+UV-tiebreak ([`02`](02-the-atlas-pipeline.md) §3) |
| **Visible stretching / low detail** | distortion budget too loose | lower `MaxStretch` (more, smaller charts) |
| **Wasted texture / huge atlas** | packing too loose or strict density | tighter packer (brute-force); density tolerance band |

## 4. QC visualizations

- **Distortion heatmap** — per-triangle stretch coloured onto the UVs / the mesh; spots the
  high-stretch regions a segment cut would fix.
- **UV-layout image** — islands coloured by chart id, rasterized deterministically to PNG; the primary
  human-legible diff and a CI artifact on failure.
- **Re-bake check (authoritative)** — bake a test signal (e.g. position/normal) through the atlas and
  confirm it round-trips with no bleed/overlap; this exercises the actual downstream use.

## 5. Golden testing (vs the reference oracle)

Mirror the determinism + golden discipline ([`06`](06-acceleration-and-performance.md) §5,
[`08`](08-prior-art-tools-and-libraries.md) §2/§4): generate goldens with **xatlas** (MIT,
scope-matched) plus a **Blender** cross-check; on a curated corpus (sphere, cube, cylinder, a beveled
hard-surface part, one dense organic mesh):

- **Hard-gate** on overlap=0, no-flips, full coverage.
- **Tolerance-gate** on the §2 scorecard (stretch, packing %, chart count, seam length, density
  variance, gutter) vs the golden.
- **Image-diff** the UV-layout PNG (per-pixel / SSIM) and diff the canonically-sorted UV buffer (~1e-5)
  to catch layout drift.
- **Version-stamp** goldens with tool version + resolution + all options so intentional algorithm
  changes regenerate them explicitly.

Because UVBake uses the same algorithm family as xatlas, a correct implementation should land close on
the metrics; large deltas localize the bug (segmentation, flattening, normalization, or packing).

---

[^pbrt]: PBRT-v4 — BVH broad/narrow-phase. https://pbr-book.org/4ed/Primitives_and_Intersection_Acceleration/Bounding_Volume_Hierarchies
[^stretch]: Sander, Snyder, Gortler, Hoppe, *Texture Mapping Progressive Meshes*, SIGGRAPH 2001 (stretch metric). https://hhoppe.com/tmpm.pdf
