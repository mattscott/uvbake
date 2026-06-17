# 02 — Phase roadmap

Each phase lists **tasks**, **deliverables**, and **acceptance tests**. A phase is "done" only when its
acceptance tests pass on the shared corpus (`tests/corpus/`). Effort is rough relative sizing. Doc
references point at [../Documents/](../Documents/).

---

## P0 — Scaffold  *(effort: S)*  ✅ done

**Tasks**
- `UVBake.sln` + three `src/` projects + `tests/` project (.NET 8), classic `.sln` (not `.slnx`).
- Shared `Directory.Build.props`/`Packages.props`/`global.json`/`.editorconfig` (analyzers on, style
  enforced, warnings fatal, deterministic).
- CI: build + test on push (Windows + Linux). MIT `LICENSE` + `NOTICE` (parameterization papers,
  xatlas/Blender oracle). Reference-oracle stub under `tools/reference-oracle/`.

**Deliverables**: empty-but-building solution; green CI; oracle README in place.
**Acceptance**: `dotnet build` + `dotnet test` pass with placeholder tests (✅ 2/2, 0 warnings).

---

## P1 — Mesh & I/O + hard-edge detection  *(effort: M)*

**Tasks**
- `Mesh` / `UvMesh` POCOs + **half-edge mesh** with per-edge dihedral angle and **per-wedge UV** storage.
- `UVBake.IO`: **OBJ reader/writer** with per-wedge UVs (seam = duplicated `vt`, shared `v`).
- **Deterministic shortest-diagonal** triangulation (UV tiebreak on emit) — §02.
- **Hard-edge detection** (dihedral threshold ∪ provided crease tags) — §03.
- Assemble the **corpus** (sphere, cube, cylinder, beveled hard-surface part, one dense organic mesh).

**Deliverables**: OBJ (with UVs) round-trips; hard-edge sets computed; corpus committed.
**Acceptance**: round-trip preserves positions + per-wedge UVs; triangulation is reproducible;
hard-edge detection matches expected creases on the cube/bevel.

---

## P2 — Segmentation  *(effort: L)*

**Tasks**
- **Chart growth** to a stretch budget (`MaxStretch`), seeded so **seams fall on hard edges**; prefer
  large, near-developable charts; merge adjacent charts while the union stays within budget — §03.
- **Cut-to-disk** (handle cuts for genus > 0).
- **Seam routing** for the non-hard-edge cuts (curvature anchors + MST + visibility weight, Seamster).

**Deliverables**: a face→chart labelling; each chart disk-topology; seam set ⊇ hard edges.
**Acceptance**: every chart is disk-topology; all hard edges are seams; per-chart stretch ≤ budget;
chart count competitive with xatlas on the corpus.

---

## P3 — Parameterization  *(effort: L — kill-point)*

**Tasks**
- `ISparseSolver` + factorization seam; settle the managed solver (Math.NET + clean-room Cholesky, or
  CSparse.NET) — §06.
- **LSCM** init (sparse least-squares, free boundary, pin 2) — §04; **Tutte/MVC** flip-free fallback.
- **SLIM** polish minimizing **symmetric Dirichlet** with a flip-preventing line search → **guaranteed
  locally injective** charts.
- Per-triangle **flip check** (`det J > 0`).

**Deliverables**: each chart flattened, low-distortion, **no flips**.
**Acceptance**: **zero flips** on the corpus; area-weighted L² / max L∞ stretch within tolerance of
xatlas; solver factor-reuse works; runs the corpus without NaNs. **Validate the managed solver +
no-flip guarantee in isolation before P4.**

> **Kill-point:** if a fast, robust, no-flip managed parameterization can't be achieved, rethink the
> solver/approach before building pack + IO on top.

---

## P4 — Normalize + pack → first atlas  *(effort: L)* → **M5 demoable**

**Tasks**
- **Texel-density normalization**: scale each chart by `√(3D area / UV area)` to one global target — §05.
- **Rasterized (Tetris) packer** with the **gutter reserved by dilating silhouettes before placing**;
  90°/axis rotation; power-of-two atlas — §05.
- **Validation**: global **overlap = 0** (grid/BVH triangle test) + **full coverage** hard gate — §07.
- Wire `UVBaker.Generate` end-to-end; CLI `uvbake in.obj -o out.obj`.

**Deliverables**: **end-to-end** mesh → unique bake-ready UV atlas (per-wedge UVs in OBJ).
**Acceptance**: overlap = 0, no flips, full coverage on the corpus; packing efficiency + density
variance + seam length within tolerance of an **xatlas golden**; UV-layout image diff passes.

---

## P5 — Robustness & quality  *(effort: M)* → **M6**

**Tasks**
- **OptCuts/Autocuts** joint cut+parameterization mode (lower distortion at a seam budget) — §03.
- **UDIM** multi-tile output; **glTF** export (vertex-split at seams) — §05, §02.
- **Mirrored/stacked UV** handling (UDIM offset); optional **SCAF** fused parameterize+pack — §04.
- Density **tolerance band** for packing efficiency.

**Deliverables**: production-grade atlases on hard-surface + dense organic assets; multi-format output.
**Acceptance**: OptCuts lowers distortion vs P4 at equal seam budget; UDIM/glTF round-trip; SCAF (if
enabled) stays bijective; mirrored shells handled.

---

## P6 — Performance & determinism  *(effort: M)*

**Tasks**
- **Parallel-across-charts** parameterization; factor-once/solve-many in SLIM/ARAP.
- **Determinism hardening:** fixed reduction order / Kahan; deterministic tie-breaks; fixed iteration
  counts; pinned seeds — §06.
- Benchmark harness + perf-regression gate; robustness pass over dirty inputs (non-manifold, holes).

**Deliverables**: meets the time budget; deterministic, crash-free on the dirty corpus.
**Acceptance**: bit-exact output for fixed params across Windows + Linux; perf budget met; robustness
corpus passes.

---

## P7 — Packaging, CLI, API polish  *(effort: S–M)* → **M7 ship**

**Tasks**
- `UVBake.Cli` full option surface + `--help`; progress reporting.
- Public API docs (XML + README); `IProgress<>`/cancellation.
- NuGet for `UVBake.Core` + `UVBake.IO`; semantic versioning.
- Sample gallery (UV-layout + distortion-heatmap renders from the corpus).

**Deliverables**: installable package + working CLI + docs.
**Acceptance**: `dotnet tool`/NuGet install works clean on Windows + Linux; a new user can unwrap an
OBJ from the CLI following the README alone.

---

## Cross-cutting (every phase)

- Update the **test corpus** and golden layouts/metrics as stages stabilize.
- Validate against **xatlas** (+ Blender cross-check): hard-gate overlap=0 + coverage; tolerance-gate
  stretch / packing % / chart count / seam length / density variance; image-diff the UV layout.
- Keep `UVBake.Core` dependency-light and **clean-room** (papers are the spec; **CGAL GPL never
  read/copied**, libigl MPL spec-only); permissive deps only; attribute influences in `NOTICE`.
- CPU + fixed accumulation order + deterministic tie-breaks; no native binaries.
