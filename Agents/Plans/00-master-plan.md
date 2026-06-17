# 00 — Master plan

## Vision

A permissively-licensed, dependency-light, cross-platform **managed-C# (.NET 8) library + CLI** that
automatically generates a **unique, non-overlapping UV atlas** for an arbitrary mesh — the largest
low-distortion islands the distortion budget allows, normalized to a consistent texel density, with
seams routed onto hard edges and packed with a baker-ready gutter. A standalone tool in a clean-room,
golden-tested, zero-native-dependency mold.

## Why this is achievable now

The research ([../Documents/](../Documents/)) confirms UV-atlas generation is a **well-understood,
decades-old pipeline**, not open research: *segment → cut to disk → parameterize → normalize → pack*
(Lévy et al. 2002; bounded-distortion cutting, Sorkine et al. 2002). Every stage has a clear algorithm
and a permissive open reference (**xatlas**). The one genuinely hard property — **injectivity** (no
flipped or overlapping triangles, required for baking) — has a settled solution: a **flip-barrier**
parameterization (symmetric Dirichlet / **SLIM**) seeded from a flip-free start, plus a post-hoc
validator. The only nontrivial dependency is a sparse linear solver, for which managed options exist.

## Scope (v1.0)

**In:**
- Triangle/quad mesh input (quad split by a deterministic shortest-diagonal rule).
- **Hard-edge detection** (dihedral angle ∪ provided crease tags) → forced seams.
- **Chart segmentation** growing the largest near-developable charts within a stretch budget.
- **Injective, bounded-distortion parameterization** per chart (LSCM/BFF init → SLIM polish, no flips).
- **Texel-density normalization** (per-chart scaling to one global density).
- **Atlas packing** (rasterized/Tetris) with a gutter sized for a baker's dilation.
- **OBJ output** with per-wedge UVs (seams round-trip); CLI + clean public API; deterministic output.

**Out (v1.0 non-goals):**
- **Retopology / quad-layout generation** (an upstream concern).
- **Texture/normal baking** (a downstream concern).
- GPU acceleration; UV-editing UI.
- glTF/FBX/PLY output, UDIM, OptCuts/Autocuts joint optimization, SCAF — **post-1.0** (planned).

## Success criteria (v1.0 "done")

1. On a corpus mesh, produces a **unique, non-overlapping** atlas (**overlap = 0**, no flips, full
   coverage — hard gates) in a reasonable time on a desktop CPU.
2. **Largest low-distortion islands:** chart stretch within the budget; island count and seam length
   competitive with a reference unwrapper (xatlas) on the same inputs.
3. **Normalized texel density** (per-chart density variance ≈ 0) and a gutter ≥ 2× the target dilation.
4. **Deterministic** output for fixed params (fixed reduction order + tie-breaks).
5. **Bake-consistent triangulation** (pinned shortest-diagonal + UV tiebreak).
6. Single managed artifact, no native binaries, MIT/permissive-clean dependency graph.

## Milestones

| Milestone | Phases | Outcome |
|---|---|---|
| **M1 — Skeleton** | P0 | Builds, green CI, license/NOTICE, oracle stub |
| **M2 — Mesh + I/O** | P1 | OBJ (per-wedge UVs) load/save; half-edge mesh; hard-edge detection; corpus |
| **M3 — Charts** | P2 | Disk-topology charts, seams on hard edges, within stretch budget |
| **M4 — Injective flatten** | P3 | **Kill-point cleared** — no-flip parameterization + managed solver validated |
| **M5 — First atlas** | P4 | **Demoable** unique bake-ready atlas, validated vs xatlas golden |
| **M6 — Production quality** | P5 | OptCuts/Autocuts, UDIM, glTF, mirrored UVs, SCAF |
| **M7 — Ship** | P6–P7 | Perf + determinism hardened; CLI + API + NuGet |

## Top risks

1. **Injectivity — the #1 correctness risk.** Conformal/ARAP energies are finite at a flip, so they do
   *not* prevent foldovers; overlapping/mirrored UVs corrupt a bake. Mitigate: **SLIM (symmetric
   Dirichlet) flip-barrier** from a Tutte/MVC flip-free seed, **+ a signed-area + grid/BVH overlap
   validator** as a hard gate ([../Documents/04-parameterization-and-distortion.md](../Documents/04-parameterization-and-distortion.md), [../Documents/07-quality-artifacts-and-validation.md](../Documents/07-quality-artifacts-and-validation.md)).
2. **Managed sparse solver.** LSCM/ARAP/SLIM are sparse solves; no pure-managed unwrapper exists.
   Mitigate: an `ISparseSolver` seam — default MIT (Math.NET + clean-room Cholesky), CSparse.NET (LGPL)
   as a dynamic-linked option ([../Documents/06-acceleration-and-performance.md](../Documents/06-acceleration-and-performance.md)).
3. **Triangulation determinism.** Disagreeing with a downstream baker on a quad's diagonal bakes wrong
   normals. Mitigate: pin shortest-diagonal + UV tiebreak ([../Documents/02-the-atlas-pipeline.md](../Documents/02-the-atlas-pipeline.md) §3).
4. **Gutter ↔ packing coupling.** Reserve the gutter by dilating silhouettes *before* packing, not as a
   post-step ([../Documents/05-packing-and-texel-density.md](../Documents/05-packing-and-texel-density.md) §2).

## Working method

- Each phase has explicit **deliverables + acceptance tests** ([02-phase-roadmap.md](02-phase-roadmap.md)).
- `UVBake.Core` is **clean-room** — no code lifted from xatlas/libigl/CGAL; the papers are the spec.
  **CGAL (GPL) is never read/copied; libigl (MPL) is spec-only.**
- Validate against a **reference unwrapper** (xatlas; Blender cross-check) — hard-gate overlap=0 +
  coverage, tolerance-gate the metric scorecard, image-diff the UV layout.
- Keep a **test corpus** under `tests/corpus/` (sphere, cube, cylinder, a beveled hard-surface part,
  one dense organic mesh) with committed goldens under `tests/golden/`.
- CPU + fixed accumulation order + deterministic tie-breaks for reproducibility.
