# 01 — Architecture

## Solution layout

```
UVBake.sln
├─ src/
│  ├─ UVBake.Core/                  # the library (no IO/CLI deps in the algorithm core)
│  │  ├─ Math/                      # Vector2/3, 2×2 SVD, sparse matrix + ISparseSolver
│  │  ├─ Mesh/                      # half-edge mesh, attributes, deterministic triangulation
│  │  ├─ Segmentation/              # hard-edge detection, chart growth, cut-to-disk, seam routing
│  │  ├─ Parameterization/          # Tutte/MVC, LSCM, ARAP, SLIM; flip/injectivity checks
│  │  ├─ Packing/                   # texel-density normalization, rasterized packer, gutter, UDIM
│  │  ├─ Validation/                # signed-area + grid/BVH overlap, coverage, stretch metrics
│  │  └─ UVBaker.cs                 # top-level orchestrator + UVBakeOptions
│  ├─ UVBake.IO/                    # OBJ (per-wedge UVs) reader/writer; glTF later
│  └─ UVBake.Cli/                   # `uvbake in.obj -o out.obj --res 2048 ...`
└─ tests/
   ├─ UVBake.Tests/                 # unit + golden-layout tests
   └─ corpus/                       # shared meshes + committed goldens
```

`UVBake.Core` keeps a **dependency-light** runtime graph: segmentation, parameterization, packing, and
validation are in-repo; the only external option is a managed sparse solver behind `ISparseSolver`. No
native dependencies ([../design/08-prior-art-tools-and-libraries.md](../design/08-prior-art-tools-and-libraries.md)).

## Core data structures

### Half-edge mesh (`Mesh/`)
Index-based SoA connectivity with **per-wedge UV** storage (a vertex carries multiple UVs across seams —
the OBJ `v`/`vt` model), per-edge **dihedral angle**, and **hard-edge** flags. Responsibilities:
adjacency circulators; per-face area/normal; deterministic **shortest-diagonal** triangulation (UV
tiebreak on the emitted mesh); non-manifold-tolerant build.

### Chart (`Segmentation/`)
```
Chart : int[] faces; HalfEdgeLoop boundary; Wedge→UV map;
        double surfaceArea3D, uvArea;   // for texel-density normalization
        // disk-topology invariant after cut-to-disk
```

### Sparse solve seam (`Math/`)
```csharp
interface ISparseSolver {
    IFactorization Factor(SparseMatrix spd);   // factor once
}
interface IFactorization { double[] Solve(double[] rhs); }   // solve many (ARAP/SLIM reuse)
```
Default: MIT (Math.NET + a clean-room sparse Cholesky); CSparse.NET (LGPL) as a dynamic-linked option.

### Bake buffers / occupancy (`Packing/`, `Validation/`)
A bitmap atlas occupancy for the rasterized packer; a uniform-grid/BVH over UV triangles for the
overlap validator.

## Pipeline orchestration (`UVBaker.cs`)

```csharp
public static class UVBaker
{
    public static UvMesh Generate(Mesh input, UVBakeOptions options);
}
```

Internal flow (maps 1:1 to phases):
```
Mesh
  → DetectHardEdges (dihedral ∪ tags)          (P1)
  → Segment        (grow charts to MaxStretch, seams on hard edges)   (P2)
  → CutToDisk      (handle cuts)                                      (P2)
  → Parameterize   (LSCM/BFF init → SLIM polish; per chart, parallel) (P3)   [injectivity here]
  → NormalizeTexelDensity (per-chart √(3D/UV area) scale)             (P4)
  → Pack           (rasterized, gutter-reserved)                     (P4)   [inter-chart injectivity]
  → Validate       (overlap=0, coverage, metrics)                    (P4)
  → UvMesh (per-wedge UVs)
```

## Key interfaces (stable seams for testing & swaps)

```csharp
interface ISegmenter      { IReadOnlyList<Chart> Segment(Mesh m, SeamSet seams, double maxStretch); }
interface IParameterizer  { void Flatten(Chart c); }       // LSCM, ARAP, SLIM — must leave it injective
interface IPacker         { void Pack(IReadOnlyList<Chart> charts, UVBakeOptions o); }
interface IOverlapCheck   { bool IsInjective(UvMesh m); }  // signed-area + grid/BVH triangle overlap
```

These let us validate each stage against the oracle in isolation (segment vs xatlas charts;
parameterize vs an injectivity oracle; pack vs xatlas occupancy) and swap a flattener or packer
(e.g. drop in SCAF, which fuses parameterize+pack) without touching the others.

## Determinism & parallelism

- **Charts are independent** ⇒ parameterize all charts **in parallel** (one thread per chart, no shared
  mutable state).
- **Bit-exact reproducibility caveat:** the only cross-element steps are linear-algebra **reductions**
  (area/energy sums) and the **packing** placement. Float addition is non-associative, so reductions
  use a **fixed order / Kahan summation**, and packing/segmentation use **deterministic tie-breaks**
  (sort charts by area then stable id; break greedy ties by index). Fixed RNG seed + fixed iteration
  counts for SLIM. No `Math.Random`/wall-clock in the hot path.
- CPU only for v1.0.

## Public surface (v1.0)

- `UVBaker.Generate(...)` (one-shot) and an `IProgress<>`/cancellation variant.
- `Mesh` / `UvMesh` POCOs + `UVBake.IO` OBJ loader/writer.
- `UVBakeOptions` (already in the scaffold) — resolution, texel density, gutter, max-stretch, hard-edge
  angle, seam source, format, threads.
- CLI mirrors the options.
