# UVBake — Research & Design Docs

Background research for **UVBake**: a standalone, managed-C#/.NET 8, dependency-light, MIT-clean
library+CLI that automatically generates a **unique, non-overlapping UV atlas** with the **largest
low-distortion islands**, normalized texel density, and seams on hard edges — built for downstream
texture/normal baking.

Start with **[`00-overview.md`](00-overview.md)**. Suggested reading order:

| # | Doc | What it answers |
|---|---|---|
| 00 | [Overview & index](00-overview.md) | What UVBake is, where it sits in the pipeline, scope, glossary. |
| 01 | [UV mapping fundamentals](01-uv-mapping-fundamentals.md) | UVs, charts/islands/seams, the injective requirement, distortion types, texel density. |
| 02 | [The atlas pipeline](02-the-atlas-pipeline.md) | segment → cut → parameterize → normalize → pack; quad triangulation determinism. |
| 03 | [Chart segmentation & seams](03-chart-segmentation-and-seams.md) | Why cut, hard-edge seams, chart growth, Seamster, OptCuts/Autocuts. |
| 04 | [Parameterization & distortion](04-parameterization-and-distortion.md) | Tutte/LSCM/ABF++/ARAP/SLIM/BFF, injectivity, stretch metrics — the math core. |
| 05 | [Packing & texel density](05-packing-and-texel-density.md) | Rasterized packing, gutters, texel-density normalization, UDIM. |
| 06 | [Acceleration & performance](06-acceleration-and-performance.md) | Sparse solves, managed solver options, overlap checks, determinism. |
| 07 | [Quality, artifacts & validation](07-quality-artifacts-and-validation.md) | Injectivity gates, artifact table, the quality scorecard, golden testing. |
| 08 | [Prior art, tools & libraries](08-prior-art-tools-and-libraries.md) | xatlas/UVAtlas/Blender/Ministry of Flat/RizomUV; libraries + licenses; oracle. |
| 09 | [Architecture & roadmap](09-architecture-and-roadmap.md) | Proposed module layout, dependency decisions, CLI, MVP → vNext. |
|    | [references.md](references.md) | Consolidated, grouped bibliography with URLs. |

## TL;DR — the load-bearing conclusions

1. **This is a well-understood pipeline:** *segment → cut to disk → parameterize → normalize density →
   pack* (Lévy et al. 2002, refined by Sorkine et al. 2002). The open reference is **xatlas**.
2. **Injectivity is the hard contract.** Conformal/ARAP energies are *finite* at a flip, so they don't
   prevent foldovers — bake-usable UVs need a **flip-barrier** parameterization (symmetric Dirichlet /
   **SLIM**) plus a post-hoc signed-area + overlap validator (docs 04, 07).
3. **Recommended flattening:** **LSCM (or BFF) initialize → SLIM polish → SCAF (or a rasterized packer)**
   — fast, near-isometric (uniform texel density), provably flip/overlap-free (doc 04).
4. **"Largest normalized islands"** = grow charts to a stretch budget with seams forced on hard edges,
   merge while distortion allows, then scale every chart by `√(3D area / UV area)` to one density (docs 03, 05).
5. **Quad input:** triangulate by a **deterministic shortest-diagonal (+UV tiebreak)** rule so a
   downstream tangent-space baker stays bit-consistent (doc 02).
6. **Managed/MIT-clean is feasible**, but **no pure-C# unwrapper exists** — it's a clean-room port; the
   one real dependency choice is the sparse solver (Math.NET MIT + clean-room Cholesky, or CSparse.NET
   LGPL behind a seam). libigl is MPL (spec only); **CGAL is GPL — avoid** (doc 08).
7. **Validate against a reference oracle:** **xatlas** (MIT, scope-matched) goldens + a **Blender**
   cross-check; hard-gate overlap=0/coverage, tolerance-gate the metric scorecard (docs 07, 08).

## How this was produced

Synthesized from four parallel research passes over primary sources (the seminal SIGGRAPH/Vis papers,
the three parameterization surveys, the injectivity/SLIM/SCAF literature, the Jylänki packing survey,
tool docs, and library license files). Every nontrivial claim is cited inline; full list in
[`references.md`](references.md).
