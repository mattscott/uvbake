# Technical Debt & Carried-Forward Items

A living register of everything implemented in a reduced/proxy form, deferred, or known-limited — with
the reason and where a fuller version belongs. Updated as the build progresses; nothing here is a silent
gap. Status key: **deferred** (not built, planned), **reduced** (built in a simpler form than the ideal),
**limitation** (intentional scope boundary), **perf** (correct but not optimized).

## Parameterization (P3)

- **SLIM is gradient descent, not the full reweighted-proxy Newton step** — *reduced.* The implemented
  polish minimizes symmetric Dirichlet with an analytic gradient + flip-preventing line search (correct
  and flip-free), but uses plain gradient descent rather than SLIM's reweighted ARAP-proxy global solve.
  Consequence: more iterations to converge. The flip barrier + injectivity guarantee are unaffected.
- **Factor-once/solve-many is only where it is real** — *limitation.* Tutte factors its Laplacian once
  and solves both u and v against it; LSCM is a single solve. SLIM (gradient descent) has no constant
  global matrix, so factor-reuse does not apply to it. An ARAP local/global mode (constant cotangent
  Laplacian, factor once) is a natural future addition that would benefit from reuse.

## Segmentation (P2)

- **Seam routing (Seamster) not implemented** — *deferred.* Seams fall on hard edges and chart
  boundaries (correct), but the non-hard cuts are not routed to be short/hidden via curvature anchors +
  MST + visibility weight (Documents/03 §4).
- **Chart growth uses a developability proxy, not exact stretch** — *reduced.* Growth merges by a
  normal-cone angle derived from `MaxStretch`; the true Sander L²/L∞ stretch is only measurable after
  parameterization (P3) and is reported there, not enforced as the growth budget.
- **Explicit handle-cutting not implemented** — *limitation.* Charts stay disks by construction (merges
  preserve χ=1), so genus>0 inputs are covered by several disk charts. Cutting a handle to allow one
  larger chart that spans it is not done.
- **"Competitive chart count vs xatlas" unverified** — *deferred,* pending the oracle (below).

## Packing (P4/P5)

- **Silhouette ("Tetris") packer is comparable to, not better than, the bbox packer on the corpus** —
  *limitation.* It is correct (overlap-free, gutter-by-construction) and ships opt-in, but on the
  near-convex procedural charts it does not beat the bbox skyline. A real efficiency win needs
  overhang-heavy real assets, richer orientation search (180°/reflection, convex-hull axis align), and
  measurement against the oracle.
- **±10% density tolerance band / `PackEfficiencyFirst` policy** — *deferred.* A meaningful band needs
  per-chart scale optimization the skyline packers don't support; a no-op flag would mislead. Strict
  uniform density (what bakers usually want) is the implemented default.
- **Packing search re-rasterizes per binary-search iteration** — *perf.* Fine for the corpus; for large
  meshes the silhouette rasterization should be computed once and scaled.

## I/O (P1/P5)

- **glTF is export-only, JSON+embedded-base64 (no `.glb`)** — *limitation.* No glTF reader; the binary
  `.glb` container is not emitted (the self-contained `.gltf` is valid and simpler).
- **n-gon triangulation is a deterministic fan, not shortest-diagonal-optimal** — *reduced.* Quads use
  the shortest-diagonal rule with an index tiebreak; n>4 faces fan from corner 0 (the corpus is tris +
  quads, so this is untested in anger).
- **Emit-time UV tiebreak for triangulation not applied** — *deferred.* The initial (pre-UV)
  triangulation tiebreaks on vertex index; the documented UV-space tiebreak on emit (Documents/02 §3) is
  not yet wired.
- **OBJ smoothing-group creases: two group-0 faces treated as smooth** — *limitation.* A minor edge case
  in deriving crease tags from `s` groups.

## Mesh connectivity (P1)

- **Connectivity is by position index (assumes welded input)** — *limitation.* Two coincident-but-distinct
  positions are not auto-welded; non-manifold edges are tolerated (counted, left as boundary) and
  degenerate triangles don't crash, but a position-welding pass for dirty input is not implemented.

## Validation (P4)

- **Intra-chart self-overlap relies on the parameterizer** — *limitation.* The global validator checks
  inter-chart overlap (SAT) + per-chart no-flip; intra-chart bijectivity is guaranteed by Tutte/SLIM
  rather than independently tested by a global self-intersection sweep.

## Determinism & performance (P6)

- **Cross-platform bit-exactness is expected but not golden-tested** — *limitation.* In-platform
  determinism (parallel==serial, repeat-run identical) is tested. Strict Windows==Linux bit-exactness is
  expected under IEEE-754 but transcendental functions (Acos/Sqrt/Cos/Sin) may differ by 1 ULP; no
  committed cross-platform golden, to avoid a brittle CI failure on the other OS.
- **Kahan/compensated summation not applied** — *limitation.* Fixed sequential reduction order suffices
  for reproducibility; Kahan would improve accuracy, not determinism.
- **Validator/packer are not optimized for large meshes** — *perf.* Correct and fast on the corpus; the
  overlap grid and packing searches are not tuned for very high triangle counts.

## Reference oracle (P0/P4)

- **Native xatlas build + golden generation not run; golden tolerance gates not wired** — *deferred.*
  The harness scripts exist (`tools/reference-oracle/`), but the xatlas build, golden UV/metric
  generation, and the tolerance-gate comparisons (stretch / packing % / chart count / seam length vs
  xatlas) are not executed or wired into CI. `download-corpus.ps1` URLs are unverified.

## Advanced methods (P5, optional)

- **OptCuts/Autocuts joint cut+parameterization** — *deferred.* Lower distortion at a seam budget;
  significant research effort, only worthwhile with rigorous validation + the oracle.
- **SCAF fused parameterize+pack (bijective whole-atlas)** — *deferred.* An alternative to the standalone
  packer with a global bijectivity guarantee; advanced, same gating as OptCuts.
- **Mirrored/stacked UV instancing → UDIM offset** — *deferred.* UDIM spill exists for density overflow;
  intentional mirrored-shell handling is not modeled.
