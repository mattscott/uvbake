# Acceleration, Performance & Determinism

Unlike a baker (which is ray-bound), an unwrapper is bound by **sparse linear algebra** (the
parameterization solves) and **rasterized search** (packing). This document covers the compute core,
the managed solver options, the overlap/coverage checks, and — a hard requirement — **determinism**.

## 1. Where the time goes

| Stage | Compute | Cost |
|---|---|---|
| Segmentation | region growing + dihedral/curvature evaluation | near-linear in faces |
| **Parameterization** | **sparse linear solves** | the heavy stage |
| Packing | rasterized silhouette search | grows with atlas resolution × chart count |
| Validation | overlap/coverage rasterization or UV triangle intersection | near-linear with a grid/BVH |

The parameterization stage dominates:
- **LSCM / BFF** — *one* sparse least-squares / Poisson solve per chart
  ([`04`](04-parameterization-and-distortion.md) §2).
- **ARAP / SLIM / SCAF** — *repeated* sparse SPD solves; the system matrix is the **same** each
  iteration, so **factor once and back-substitute** every iteration (the single biggest speedup).
- **Charts are independent** ⇒ the whole parameterization stage is **embarrassingly parallel across
  charts** (one thread per chart; no shared mutable state).

## 2. Managed sparse linear solvers (no native deps)

LSCM = a sparse least-squares solve (normal equations → sparse Cholesky, or sparse QR). ARAP/SLIM =
repeated sparse SPD solves with factor reuse. Pure-managed options:[^csparse][^mathnet][^alglib]

| Library | License | Sparse capability | Fit |
|---|---|---|---|
| **CSparse.NET** | **LGPL-2.1+** ⚠️ | sparse **direct** Cholesky/LDLᵀ, QR, LU + AMD ordering | strongest technical fit (exactly the LSCM/ARAP solves) — but copyleft; dynamic-link or accept |
| **Math.NET Numerics** | **MIT** | dense direct; **sparse iterative** (CG/BiCGSTAB) only | cleanest license; CG works on SPD LSCM normal equations, less robust on ill-conditioned meshes |
| **ALGLIB (.NET)** | GPL/commercial ⚠️ | sparse Cholesky/LU + iterative | license-encumbered; avoid |

**Recommendation:** for a strictly MIT-clean core, **Math.NET (MIT) for general LA + a clean-room
simplicial/supernodal sparse Cholesky** for the SPD solves; if the LGPL is acceptable via dynamic
linking, **CSparse.NET** is the most direct fit. Either way the solver is a swappable seam.

> There is **no pure-managed C# UV unwrapper** to build on (see
> [`08`](08-prior-art-tools-and-libraries.md)) — UVBake is a clean-room port, so the solver layer is a
> deliberate, isolated choice.

## 3. Overlap & coverage checks (the injectivity gate)

The hard invariant — no inter-chart overlap, full coverage — is verified the same way packing works:
- **Rasterize** all islands (with gutter) into the atlas bitmap; assert **no texel is claimed twice**
  (overlap = 0) and every face contributes ≥ 1 texel (coverage). O(atlas texels).
- Or **UV-space triangle–triangle intersection** accelerated by a **uniform grid / BVH** over UV
  triangles — exact, independent of atlas resolution.

Per-triangle **flip detection** (`det J < 0`) is O(1) and runs during/after parameterization.

## 4. Managed-SIMD toolbox (.NET 8)

- `System.Numerics.Vector2/3/4` for geometry (edge vectors, areas, Jacobians, the 2×2 SVD in ARAP's
  local step).
- `System.Runtime.Intrinsics` for the rasterized packing bitmap ops (bitwise overlap tests over rows of
  packed bits) and any hot reductions.

## 5. Determinism (a hard requirement)

Deterministic output for fixed inputs is non-negotiable (it's what makes golden tests meaningful).
**Fixing an RNG seed is necessary but not sufficient** — the dominant nondeterminism is **floating-point
non-associativity** under parallel/reordered reductions (`(a+b)+c ≠ a+(b+c)`).[^fpna] Measures:

1. **Fix every RNG seed and algorithm** (the random packing search; any randomized solver directions).
   The seed is an explicit, logged input.
2. **Pin reduction order.** Single-thread reductions, or use a fixed-order parallel reduction; use
   **Kahan/compensated summation** for area/energy sums so the result is order-independent. Never use
   unordered atomic-add accumulation.
3. **Deterministic tie-breaking everywhere** — sort charts for packing by a total order (area, then a
   stable id); break greedy ties by index.
4. **Fixed iteration counts / fixed convergence thresholds** for ABF/ARAP/SLIM (not wall-clock or
   adaptive stops) so the same input runs the same number of steps.
5. **Pin the math** — avoid FMA-contraction variance; the sparse solver's AMD ordering is deterministic
   given the same matrix structure (good).
6. **Version-stamp goldens** with tool version, atlas resolution, and all option values, so an
   intentional algorithm change regenerates goldens explicitly.

Because charts are parameterized independently and reductions are the only cross-element step, this is
very achievable: per-chart work is deterministic, and the only care needed is in the linear-algebra
reductions and the packing tie-breaks.

## 6. Parallelism summary

- **Across charts:** parameterize all charts concurrently (independent). 
- **Within a solve:** rely on the (deterministic) sparse factorization; avoid nondeterministic parallel
  reduces.
- **Packing:** the rasterized search is largely serial (each placement updates shared occupancy) but
  cheap relative to the solves; keep its tie-breaking deterministic.

---

[^csparse]: CSparse.NET (LGPL-2.1+, managed sparse-direct). https://github.com/wo80/CSparse.NET
[^mathnet]: Math.NET Numerics (MIT). https://numerics.mathdotnet.com · https://github.com/mathnet/mathnet-numerics
[^alglib]: ALGLIB .NET (GPL/commercial). https://www.alglib.net
[^fpna]: On floating-point non-associativity and reproducibility. https://arxiv.org/html/2408.05148v1
