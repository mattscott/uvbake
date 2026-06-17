# Architecture & Roadmap

This synthesizes the research into a concrete **managed-C#/.NET 8, dependency-light, MIT-clean**
architecture for UVBake and an MVP → vNext roadmap. It is a *proposal* for the `Agents/Plans` roadmap
to refine, not a final spec.

## 1. Constraints

- **.NET 8, pure managed.** Geometry on `System.Numerics`; the sparse linear solves on a managed solver.
- **No native dependencies.** Segmentation, parameterization, packing, validation, and mesh I/O are
  in-repo or pure-managed — no native xatlas/UVAtlas/CGAL/libigl.
- **MIT/permissive-clean, clean-room.** Reimplement from papers; reference unwrappers only generate
  goldens. **Never read/copy CGAL** (GPL) or copy libigl files (MPL file-copyleft) —
  [`08`](08-prior-art-tools-and-libraries.md).
- **Deterministic & golden-tested.** Fixed reduction order + tie-breaks; xatlas as the oracle.
- **Library + CLI.**

## 2. Pipeline (data flow)

```text
 in.obj ─► load + half-edge ─► detect hard edges (dihedral ∪ tags)
        ─► SEGMENT charts (grow to MaxStretch; seams on hard edges)        [Core/Segmentation]
        ─► CUT each chart to a disk
        ─► PARAMETERIZE each chart  (LSCM/BFF init ─► SLIM polish, no-flip) [Core/Parameterization]
        ─► NORMALIZE texel density  (per-chart √(3D/UV area) scale)         [Core/Packing]
        ─► PACK (rasterized, gutter-reserved, no overlap)                   [Core/Packing]
        ─► VALIDATE (overlap=0, coverage, metrics)                          [Core/Validation]
        ─► out.obj (+ per-wedge UVs)
```

Each stage maps to a doc: segmentation/seams → [`03`](03-chart-segmentation-and-seams.md);
parameterization → [`04`](04-parameterization-and-distortion.md); normalize/pack →
[`05`](05-packing-and-texel-density.md); perf/determinism → [`06`](06-acceleration-and-performance.md);
validation → [`07`](07-quality-artifacts-and-validation.md).

## 3. Module layout (mirrors the established skeleton)

```text
src/
  UVBake.Core/                  dependency-light
    Mesh/                       half-edge / indexed mesh, attributes, deterministic triangulation
    Math/                       vec/mat, 2×2 SVD, sparse matrix + solver seam
    Segmentation/               hard-edge detection, chart growth, cut-to-disk, seam routing
    Parameterization/           Tutte/MVC, LSCM, ARAP, SLIM; injectivity/flip checks
    Packing/                    texel-density normalization, rasterized packer, gutter, UDIM
    Validation/                 signed-area + grid/BVH overlap, coverage, stretch metrics
    UVBaker.cs                  top-level orchestrator + UVBakeOptions
  UVBake.IO/                    OBJ first (per-wedge UVs); glTF later
  UVBake.Cli/                   `uvbake in.obj -o out.obj --res 2048 ...`
tests/
  UVBake.Tests/                 unit + golden-layout tests
  corpus/                       curated meshes + committed goldens
tools/
  reference-oracle/             xatlas / Blender headless scripts that emit golden layouts + metrics
```

## 4. Core data structures

- **Half-edge mesh** (SoA) with per-edge dihedral angle and hard-edge flags; **per-wedge** UV storage
  (a vertex can carry multiple UVs across seams) — this is the natural OBJ `v`/`vt` model.
- **Chart** = a face set + its boundary loop + its per-wedge UVs + its 3D/UV area (for normalization).
- **Sparse solver seam** — an `ISparseSolver` interface (factor-once / solve-many for ARAP/SLIM); see §5.
- **Bitmap occupancy** for the rasterized packer ([`05`](05-packing-and-texel-density.md) §1.2).

## 5. Dependency decisions (from [`08`](08-prior-art-tools-and-libraries.md))

| Need | Decision | Why |
|---|---|---|
| Parameterization | **clean-room** Tutte/LSCM/ARAP/SLIM | no managed unwrapper exists; libigl is MPL (spec only), CGAL is GPL (avoid) |
| Sparse linear solve | **Math.NET (MIT) + clean-room sparse Cholesky**, or **CSparse.NET (LGPL)** behind an `ISparseSolver` seam | MIT-clean default; CSparse is the strongest fit if LGPL via dynamic-link is OK |
| Packing | **in-repo rasterized (Tetris) packer** | matches the baking-atlas standard; full control of gutter |
| UV unwrap reference | xatlas (MIT) — **oracle only, not linked** | scope-matched golden source |
| Mesh I/O | **in-repo OBJ** (per-wedge); glTF later | trivial, lossless for seams, diffable |
| Test diff | layout PNG + metrics (+ optional SSIM) | golden-image + scorecard |

## 6. CLI sketch

```text
uvbake <in-mesh> -o <out-mesh> [options]

  --res 2048              atlas resolution (POT; packing/texel-density reference)
  --max-stretch 0.25      distortion budget for chart growth (lower = more, smaller charts)
  --gutter 8              inter-island padding in texels (>= 2x downstream bake dilation)
  --texels-per-unit <d>   fixed texel density (default: auto-scale to fill the atlas)
  --hard-edge 45          dihedral angle (deg) that forces a seam
  --seams both|angle|tags  hard-edge seam source
  --rotate / --no-rotate  90deg chart rotation when packing
  --pow2                  round atlas to power-of-two
  --format obj|gltf       output container (obj = native per-wedge UVs)
  --threads N --deterministic
```

## 7. Roadmap

Ladder: **P0 scaffold ✅ → P1 mesh+IO → P2 segmentation → P3 parameterization → P4 normalize+pack
(demoable) → P5 robustness → P6 perf/determinism → P7 ship.**

### P0 — Scaffold ✅
Solution, build config, CI, license/NOTICE, oracle stub. Builds clean; 2/2 tests.

### P1 — Mesh & I/O + hard-edge detection
Half-edge mesh; **OBJ reader/writer with per-wedge UVs** (seam = duplicated `vt`); deterministic
shortest-diagonal triangulation; **dihedral hard-edge detection** + tag ingest; curated corpus.

### P2 — Segmentation
Chart growth to a stretch budget with **seams forced on hard edges**; cut-to-disk (handle cuts);
short/hidden seam routing. Acceptance: charts disk-topology, seams ⊇ hard edges, chart stretch ≤ budget.

### P3 — Parameterization  *(kill-point)*
**LSCM** init (managed sparse least-squares) → **SLIM** polish (symmetric Dirichlet, flip-preventing
line search) → **guaranteed locally injective** charts; Tutte/MVC fallback. The `ISparseSolver` choice
is settled here. **Validate the managed solver + injectivity in isolation** before integrating.
*If a fast, robust, no-flip managed solve isn't achievable, rethink before building pack/IO around it.*

### P4 — Normalize + pack → first atlas  *(demoable)*
Texel-density normalization; **rasterized packer** with gutter reserved by silhouette dilation; global
**overlap=0 + coverage** gate. End-to-end `in.obj → out.obj` with a bake-ready atlas. **Validate vs an
xatlas golden** (overlap=0, stretch, packing %, chart count, seam length, layout image).

### P5 — Robustness & quality
**OptCuts/Autocuts** joint cut+param mode; **UDIM** multi-tile; **glTF** output (vertex-split at seams);
mirrored/stacked UV handling; optional **SCAF** fused param+pack; density tolerance band.

### P6 — Performance & determinism
Parallel-across-charts; factor-once/solve-many; determinism hardening (fixed reduction order, Kahan,
deterministic tie-breaks, fixed iteration counts); perf-regression gate.

### P7 — Packaging, CLI, API
Full CLI + `--help`; XML docs; NuGet for `UVBake.Core`/`UVBake.IO`; sample gallery (UV-layout renders).

### Always-on
Determinism, golden corpus, no native deps, MIT-clean.

## 8. The four things most likely to bite (watch list)

1. **Injectivity guarantee.** Conformal/ARAP energies are finite at a flip, so they *don't* prevent
   foldovers — you must use a **flip-barrier** energy (symmetric Dirichlet / SLIM) and a post-hoc
   signed-area + overlap validator. The non-negotiable correctness item
   ([`04`](04-parameterization-and-distortion.md) §4, [`07`](07-quality-artifacts-and-validation.md) §1).
2. **Triangulation determinism.** Pin the shortest-diagonal+UV-tiebreak split so a downstream
   tangent-space baker stays bit-consistent ([`02`](02-the-atlas-pipeline.md) §3).
3. **Sparse-solver licensing.** Keep it behind `ISparseSolver`; default MIT-clean (Math.NET +
   clean-room Cholesky), with CSparse.NET (LGPL) as a dynamic-linked option
   ([`06`](06-acceleration-and-performance.md) §2).
4. **Gutter ↔ packing coupling.** Reserve the gutter by **dilating silhouettes before packing**, not as
   a post-step, or islands pack too close and bleed ([`05`](05-packing-and-texel-density.md) §2).
