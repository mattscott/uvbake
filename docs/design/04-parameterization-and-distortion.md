# Parameterization & Distortion — flattening each chart

Once the mesh is cut into disk-topology **charts** ([`03-chart-segmentation-and-seams.md`](03-chart-segmentation-and-seams.md)),
each chart is **flattened** from 3D into the 2D texture plane. This document covers the flattening
methods, the **injectivity** invariant baking demands, and the **distortion metrics** that score
quality. This is the mathematical heart of UVBake.

## 1. The problem & the hard invariant

A parameterization of a chart `S ⊂ ℝ³` is a map `φ: S → Ω ⊂ ℝ²` giving each surface point a `(u,v)`.
On a triangle mesh it is piecewise-linear (affine per triangle); a baker samples the **inverse** `φ⁻¹`
— for each texel it finds the surface point and records colour/normal there.

**Injectivity terms** (load-bearing):
- **Injective** — no two surface points map to the same `(u,v)` (image triangles don't overlap).
- **Bijective** — injective *and* covering the target domain exactly once.
- **Locally injective / foldover-free** — every triangle's Jacobian has `det J > 0` (no mirrored/
  inverted triangle). A *local* property: it forbids folds but not two distant triangles overlapping.

For a PL map, **global bijectivity = local injectivity (no flips) + no boundary self-intersection** —
the decomposition SLIM and SCAF exploit (§3).[^slim][^scaf]

**Why baking requires no overlaps/flips — UVBake's hard correctness invariant:**
- A **flipped** triangle (`det J < 0`) mirrors the surface into UV: the rasterizer's winding and
  derivatives break, mip-derivatives get the wrong sign, and tangent-space normals invert.
- An **overlap** makes one texel store two unrelated surface signals — last-rasterized wins, the other
  region samples a wrong neighbour → bleed/ghosting. It cannot be averaged away.

So the invariant is: **every chart flattens locally-injective (no flips), no chart self-overlaps, and
the packed atlas has no inter-chart overlap.** Distortion is a *quality* objective layered on top.

## 2. Linear methods (fast — one sparse solve)

### 2.1 Tutte / barycentric (the only free bijectivity guarantee)
Fix the boundary to a **convex** polygon; place each interior vertex at a convex combination of its
neighbours: `u_i = Σ_j λ_ij u_j`, `λ_ij > 0`, `Σ_j λ_ij = 1` — a sparse solve (decoupled `u`, `v`).
**Tutte's theorem:** positive weights + convex boundary ⇒ a **provably bijective** straight-line
embedding (no flips/overlaps).[^tutte] Weight choices:
- **Uniform** `1/deg` — bijective but geometry-blind ⇒ high distortion.
- **Cotangent/harmonic** `½(cot α + cot β)` — discrete-harmonic, near-conformal, but obtuse triangles
  give *negative* weights ⇒ guarantee voids, flips possible.
- **Mean-value coordinates** (Floater 2003) — `w_ij = (tan(δ⁻/2)+tan(δ⁺/2))/‖v_j−v_i‖`, **always
  positive** ⇒ always bijective with a convex boundary, nearly conformal.[^mvc]

Use Tutte/MVC as a **guaranteed flip-free seed**; its weakness is boundary distortion from the forced
convex shape.

### 2.2 LSCM — Least-Squares Conformal Maps (Lévy et al. 2002)
A conformal map satisfies Cauchy–Riemann. Writing `w = u + iv` per triangle, LSCM minimizes the
**conformal energy** = area-weighted Cauchy–Riemann residual, which is **quadratic** in the vertex
UVs ⇒ a single sparse least-squares solve. **Free boundary** (its big advantage over Tutte); the
4-DOF similarity null space is removed by **pinning two vertices**.[^lscm] Identity:
`E_conformal = E_Dirichlet − Area`. *Pros:* free boundary, fast, robust, great initializer. *Cons:*
conformal ⇒ **no area control** (texel density drifts); not strictly flip-free (rare).

```text
LSCM:
  per triangle, build local 2D frame; express ∇w linearly in vertex (u,v)
  assemble sparse M (Cauchy–Riemann residual, rows scaled by sqrt(Area))
  pin 2 vertices (4 DOF) -> RHS
  solve (MᵀM) x = Mᵀb   (sparse Cholesky / LSQR)  ->  (u,v) per vertex
```

**DCP/DNCP** (Desbrun, Meyer, Alliez 2002) is mathematically equivalent to LSCM; the DNCP "natural
boundary" variant drops the pin bias.[^dcp]

### 2.3 ABF / ABF++ — Angle-Based Flattening (Sheffer et al.)
Solve for the flattened **angles** `α` (not positions): minimize `Σ (1/β²)(α−β)²` subject to
triangle (`Σα=π`), wheel (`Σα=2π` around interior vertices), and reconstruction (`Π sin α_left =
Π sin α_right`) constraints, then sew angles into positions. Gives **provably valid, low-stretch,
near-conformal, free-boundary** maps; **ABF++** adds a hierarchical solver to scale to millions of
triangles.[^abf] *Cons:* constrained Newton (not one linear solve); conformal ⇒ still needs a stretch
pass for area.

## 3. Nonlinear / global methods (low distortion + no-flip guarantees)

### 3.1 ARAP parameterization (local/global, Liu et al. 2008)
Make each triangle's 2D image as close to a **rigid** motion of its 3D shape as possible (isometric).
Alternate: **local step** — per triangle, best rotation `L_t = UVᵀ` from the SVD of the cotangent-
weighted Jacobian covariance (flip a sign if `det < 0` to stay a proper rotation); **global step** —
a single **cotangent-Laplacian Poisson solve** (same matrix each iteration → factor once).[^arap]
Per-triangle energy `(σ₁−1)²+(σ₂−1)²`. *Allowing uniform scale* in `L_t` ⇒ **ASAP** = conformal =
LSCM. *Pros:* near-isometric ⇒ **uniform texel density**, simple. *Cons:* **not flip-guaranteed** as
published.

```text
ARAP (local/global):
  init u (e.g. LSCM); precompute cot weights + Laplacian L; factor L once
  repeat:
    local:  for each tri t: S = Σ c_ij (u_i-u_j)(x_i-x_j)ᵀ; (U,Σ,V)=SVD(S);
            if det(UVᵀ)<0 flip last col of U; L_t = U Vᵀ
    global: b_i = Σ c_ij L_t (x_i-x_j);  solve L u = b  (cached factor)
```

### 3.2 SLIM — Scalable Locally Injective Mappings (Rabinovich et al. 2017)
Minimize a **flip-preventing** energy — canonically the **symmetric Dirichlet**
`‖J‖²_F + ‖J⁻¹‖²_F = σ₁²+σ₂²+1/σ₁²+1/σ₂²` — which → ∞ as any triangle degenerates (`det J→0⁺`), a
built-in flip barrier. Each iteration builds a reweighted ARAP-style quadratic proxy, solves it with
the ARAP local/global machinery to get a **direction**, then does a **flip-preventing line search**
(closed-form max step before any `det J = 0`). From a locally-injective init it **stays locally
injective for all iterations** and scales to >25M triangles.[^slim] This is the recommended polish.

### 3.3 SCAF — bijective whole-atlas packing (Jiang et al. 2017)
Triangulate the **empty space** around the charts (a "scaffold") and run SLIM on charts **+ scaffold**
together; a chart overlapping another would have to **invert scaffold triangles**, which the barrier
forbids — so **local injectivity ⇒ global bijectivity**, with no explicit collision test. SCAF
optimizes *all* charts in one domain at once, **folding packing into the optimization**.[^scaf] The
theoretical backstop is **Smith & Schaefer 2015** (symmetric Dirichlet + boundary barrier = guaranteed
bijective free-boundary map).[^ss]

### 3.4 Others worth knowing
- **Progressive Parameterization** (Liu et al. 2018) — robust flip-free init+drive for hard charts.[^pp]
- **BFF — Boundary First Flattening** (Sawhney & Crane 2017) — a *linear* method with explicit boundary
  control (scale factors or curvatures) and **minimal-area-distortion** conformal flattening from a
  single Cholesky factorization; an excellent conformal initializer.[^bff]

## 4. Distortion metrics

Per triangle the map has a 2×2 Jacobian (in the triangle's local frame) with **singular values**
`σ₁ ≥ σ₂ ≥ 0` — the max/min local stretch (a UV unit circle maps to a surface ellipse with semi-axes
`σ₁, σ₂`). All metrics are functions of `(σ₁, σ₂)`:

| Property | Condition | Meaning |
|---|---|---|
| **Conformal** | `σ₁ = σ₂` | angles preserved; scale (area) free to drift |
| **Authalic** | `σ₁·σ₂ = 1` | area preserved; angles shear |
| **Isometric** | `σ₁ = σ₂ = 1` | perfect; only developable charts admit it exactly |

A non-developable chart **cannot** be isometric — hence the conformal↔authalic tension.

- **Texture stretch (Sander et al. 2001):** `L²(T)=√((σ₁²+σ₂²)/2)` (RMS), `L∞(T)=σ₁` (worst case);
  aggregate area-weighted: `L²(mesh)=√(Σ A·L²² / Σ A)`, `L∞=max σ₁`. Minimizing stretch **equalizes
  sampling rate** (uniform texel density) — exactly what a baker wants.[^stretch]
- **Symmetric Dirichlet:** `σ₁²+σ₂²+1/σ₁²+1/σ₂²` — min 4 at isometry, → ∞ at degeneracy (flip barrier).
- **ARAP:** `(σ₁−1)²+(σ₂−1)²`. **Dirichlet/harmonic:** `½(σ₁²+σ₂²)`.
- **Quasi-conformal dilatation** `K = σ₁/σ₂ ≥ 1` (1 = conformal) — isolates *angle* distortion.

For UVBake the two operational numbers are **max angle distortion** `K=σ₁/σ₂` and **texel-density
spread** (`√(σ₁σ₂)` across triangles). A balanced bake bounds both.

## 5. Recommended pipeline for the balanced-bake objective

Priority order: **(1) no flips/overlap (hard) → (2) full coverage (hard) → (3) minimize area-weighted
L² and L∞ stretch → (4) tight packing.** The consensus pipeline that optimizes exactly these:

1. **Initialize (linear):** **LSCM** (free-boundary conformal; pragmatic default — one solve, no
   params), or **BFF** for explicit minimal-area-distortion control. Fall back to **Tutte+MVC** for a
   guaranteed flip-free seed on pathological charts.
2. **Polish (nonlinear, no-flip):** **SLIM** minimizing **symmetric Dirichlet** — drives toward
   isometry ⇒ **normalized (uniform) texel density** while the flip barrier + line search **guarantee
   local injectivity**. (ARAP is the no-barrier special case; use only if you add explicit flip
   prevention.)
3. **Pack bijectively (whole-atlas):** **SCAF** lays out all charts at once, minimizing distortion and
   **guaranteeing no inter-chart overlap**. (Lighter alternative: per-chart SLIM + a rectangle packer
   with a hard overlap test — see [`05-packing-and-texel-density.md`](05-packing-and-texel-density.md).)

**Normalize texel density:** scale each chart by its `√(surface area / UV area)` before/within packing
so one texture resolution gives uniform detail everywhere ("normalized islands").

**One line:** *LSCM (or BFF) → SLIM (symmetric Dirichlet) → SCAF* — fast, near-isometric, provably
flip/overlap-free. Report **L²/L∞ stretch**, **max `K=σ₁/σ₂`**, and **packing %** as the scorecard.

## 6. Reference implementations & licensing

**libigl** has reference implementations of all of the above (`igl::lscm`, `igl::arap`, `igl::slim`,
scaffold/SCAF, `igl::harmonic`/Tutte) — but it is **MPL-2.0, not MIT**: file-level copyleft, so treat
it as a **spec to clean-room reimplement**, not code to copy.[^libigl] Other permissive references:
**OpenABF** (ABF/ABF++) and **Boundary First Flattening** (open source). **CGAL**'s parameterization
package is **GPL** — reference its bijectivity tables only, never link it.[^cgal] The authoritative
spec is the **papers + surveys** below.

**Surveys to anchor the implementation:** Floater & Hormann, *Surface Parameterization: a Tutorial and
Survey* (2005);[^survey-fh] Sheffer, Praun, Rose, *Mesh Parameterization Methods and their
Applications* (2006);[^survey-spr] Hormann, Lévy, Sheffer, *Mesh Parameterization: Theory and
Practice*, SIGGRAPH 2007 course.[^survey-course]

---

[^tutte]: Tutte embedding theorem (Gortler et al., proof). https://www.eecs.harvard.edu/~sjg/papers/tutte.pdf
[^mvc]: Floater, *Mean Value Coordinates*, CAGD 2003. https://www.sciencedirect.com/science/article/abs/pii/S0167839603000025
[^lscm]: Lévy, Petitjean, Ray, Maillot, *Least Squares Conformal Maps*, SIGGRAPH 2002. https://inria.hal.science/inria-00100754v1/file/lscm.pdf · https://dl.acm.org/doi/10.1145/566654.566590
[^dcp]: Desbrun, Meyer, Alliez, *Intrinsic Parameterizations of Surface Meshes*, EG 2002. https://dl.acm.org/doi/10.1111/1467-8659.00580
[^abf]: Sheffer, Lévy, Mogilnitsky, Bogomyakov, *ABF++*, ACM TOG 2005. https://www.cs.ubc.ca/~sheffa/papers/abf_plus_plus.pdf · https://dl.acm.org/doi/10.1145/1061347.1061354
[^arap]: Liu, Zhang, Xu, Gotsman, Gortler, *A Local/Global Approach to Mesh Parameterization*, SGP 2008. https://www.eecs.harvard.edu/~sjg/papers/arap.pdf
[^slim]: Rabinovich, Poranne, Panozzo, Sorkine-Hornung, *Scalable Locally Injective Mappings*, ACM TOG 2017. https://cims.nyu.edu/gcl/papers/SLIM2017.pdf · https://igl.ethz.ch/projects/slim/
[^scaf]: Jiang, Schaefer, Panozzo, *Simplicial Complex Augmentation Framework (SCAF)*, SIGGRAPH Asia 2017. https://github.com/jiangzhongshi/Scaffold-Map
[^ss]: Smith & Schaefer, *Bijective Parameterization with Free Boundaries*, ACM TOG 2015. https://dl.acm.org/doi/10.1145/2766947
[^pp]: Liu, Ye, Ni, Fu, *Progressive Parameterizations*, SIGGRAPH 2018. https://ruiqini.github.io/Publication/18_PP/SIGGRAPH18_ProgressivePara.pdf
[^bff]: Sawhney & Crane, *Boundary First Flattening*, ACM TOG 2018. https://arxiv.org/pdf/1704.06873 · https://geometrycollective.github.io/boundary-first-flattening/
[^stretch]: Sander, Snyder, Gortler, Hoppe, *Texture Mapping Progressive Meshes*, SIGGRAPH 2001. https://hhoppe.com/proj/tmpm/
[^libigl]: libigl (MPL-2.0). https://github.com/libigl/libigl · tutorial https://libigl.github.io/tutorial/
[^cgal]: CGAL Surface Mesh Parameterization (GPL; bijectivity reference). https://doc.cgal.org/latest/Surface_mesh_parameterization/index.html
[^survey-fh]: Floater & Hormann, *Surface Parameterization: a Tutorial and Survey*, 2005. https://graphics.stanford.edu/courses/cs468-05-fall/Papers/param-survey.pdf
[^survey-spr]: Sheffer, Praun, Rose, *Mesh Parameterization Methods and their Applications*, FnT CGV 2006.
[^survey-course]: Hormann, Lévy, Sheffer, *Mesh Parameterization: Theory and Practice*, SIGGRAPH 2007 course. https://dl.acm.org/doi/10.1145/1281500.1281510
