# Chart Segmentation & Seams — where to cut

Before any chart can be flattened ([`04`](04-parameterization-and-distortion.md)), the mesh must be
**cut** into **disk-topology charts**. *Where* you cut sets the floor on both distortion and seam
count — it is the first of the three decisions that determine atlas quality.

## 1. Why cut at all

- **Topology.** Planar parameterization applies only to **disk-topology** surfaces; closed surfaces and
  any surface of genus > 0 must be **cut to a disk** first. A genus-`g` surface needs **2g** cuts to
  open its handles plus a path connecting them to a boundary; finding *minimal-length* such cuts is
  **NP-hard** (Erickson & Har-Peled 2004).[^survey]
- **Distortion (Gauss).** A globally **isometric** flattening exists only for **developable** surfaces
  (zero Gaussian curvature). Every curved chart must distort *something* (angles or area), and the more
  curvature a single chart spans, the worse the stretch.[^survey]

So segmentation is a balance, stated verbatim by the canonical survey: *"a delicate balance between the
conflicting goals of small distortion and short cuts."*[^survey] The objective: *"break the surface
into several charts such that the parametric distortion … is sufficiently low, while the number of
charts remains small and their boundaries are kept as short as possible."*[^survey] For the
**balanced-for-baking** target, that means **grow the largest charts the distortion budget allows, and
route their seams onto hard edges** (where they're least visible and where baking needs them anyway).

## 2. Hard edges must become seams

For a downstream tangent-space bake, **every hard edge must be a UV seam** (the tangent basis must be
free to "twist" across a sharp edge; not splitting there bakes lighting errors). UVBake therefore seeds
the cut graph with hard edges **before** distortion-driven growth:

- **From tags:** if the input carries crease/sharp-edge flags, force seams there (`SeamSource.ProvidedTags`).
- **From geometry:** mark an edge hard when its **dihedral angle** exceeds a threshold (default 45°)
  (`SeamSource.DihedralAngle`). The union is the default (`SeamSource.Both`).

Minimizing seam count/length isn't only a packing concern — chart boundaries *"introduce artefacts in
texture mapping applications,"*[^survey] so fewer, shorter, hidden seams directly improve bake quality.

## 3. Segmentation algorithms

Listed roughly by sophistication; all grow/merge charts under a distortion or developability criterion.

- **Hierarchical Face Clustering (Garland, Willmott, Heckbert 2001)** — greedy bottom-up merging of
  adjacent faces/clusters by a **planarity** (and compactness) cost, building a cluster hierarchy; cut
  the hierarchy at a target chart count or flatness.[^hfc]
- **Variational Shape Approximation (Cohen-Steiner, Alliez, Desbrun 2004)** — Lloyd-style iteration:
  represent each region by a **proxy** (a plane), reassign faces to the nearest proxy, refit proxies;
  converges to near-planar regions. The variational backbone many segmenters use.[^vsa]
- **D-Charts (Julius, Kraevoy, Sheffer 2005)** — grows **quasi-developable** charts (each approximately
  a cone/plane so it flattens with low stretch), explicitly targeting low parametric distortion.[^dchart]
- **Distortion-bounded growth (Sorkine et al. 2002; Gu, Gortler, Hoppe 2002)** — grow a chart adding
  faces until the **stretch** (worst singular value) *"reaches a threshold,"* then start a new chart /
  cut. Gives a **hard distortion bound** at the cost of longer boundaries; the precedent for "segment
  *and* cut so each chart's distortion is bounded *and* injective."[^sorkine][^survey]
- **What xatlas does** — grows roughly disk-shaped **quasi-developable charts** to a distortion budget,
  parameterizing as it goes (`ComputeCharts`), then packs. The open reference for this whole
  stage.[^xatlas]

**Chart-growth loop (the practical recipe):**
```text
mark hard edges as forced seams (tags ∪ dihedral > threshold)
seed one chart per still-unassigned face (or at planar maxima)
repeat (region growing):
    for each chart, consider adjacent faces in priority order
       (smallest added distortion / best planarity first)
    add a face iff:  it doesn't cross a forced seam, AND
                     adding it keeps chart stretch ≤ MaxStretch, AND
                     the chart stays disk-topology (no handle/pinch)
merge adjacent charts whose union still flattens within MaxStretch   # favor large islands
cut any chart of genus>0 to a disk (handle cuts + path to boundary)
```

## 4. Seam routing (hiding the cuts)

Within the freedom the hard-edge and distortion constraints leave, route seams to be **short and
hidden**. **Seamster (Sheffer & Hart 2002)** is the canonical method: *"first finds regions of high
Gaussian curvature … then uses a minimal spanning tree of the mesh edges to connect those … cuts the
mesh along the tree edges,"* using a **visibility metric as edge weight** *"to trace the cuts through
the less visible parts of the surface, hiding the potential cross-cut discontinuities."*[^seamster] So:
anchor seams at curvature extrema, connect with a min-spanning-tree, weight by visibility.

## 5. Joint cut + parameterization (state of the art)

Segment-then-flatten commits to cuts before knowing their distortion cost. Modern methods optimize
**cuts and the flattening together**:
- **Autocuts (Poranne, Tarini, et al. 2017)** — interactively optimizes a combined **distortion +
  seam** energy, introducing cuts where they most reduce distortion.[^autocuts]
- **OptCuts (Li, Kaufman, et al. 2018)** — **automatically** and jointly minimizes distortion subject
  to a seam-length budget (or vice-versa), starting from arbitrary cuts and converging to a low
  seam-length, bounded-distortion atlas — no manual seams.[^optcuts]

These beat segment-then-flatten when distortion and seam placement are tightly coupled; they are a
strong **vNext** target for UVBake once the linear pipeline is solid.

## 6. Recommendations for UVBake (balanced-for-baking)

1. **Force seams on hard edges first** (tags ∪ dihedral > 45°) — required for baking.
2. **Grow quasi-developable charts to a stretch budget** (`MaxStretch`, default a normalized ≈0.25),
   preferring large, planar charts (HFC/VSA/D-Charts-style growth).
3. **Merge** adjacent charts whenever the union still flattens within budget — favor large islands.
4. **Route the remaining (non-hard-edge) seams** short and hidden (Seamster: curvature anchors + MST +
   visibility weight).
5. **Cut any genus > 0 chart to a disk** (handle cuts) before flattening.
6. Expose `MaxStretch` as the main quality dial (lower ⇒ more, smaller charts, less distortion); keep
   **OptCuts/Autocuts** as a vNext "joint optimization" mode.

Distortion is *measured* with the stretch metric (area-weighted L²/L∞ of the Jacobian singular values)
during growth — see [`04`](04-parameterization-and-distortion.md) §4 and
[`07`](07-quality-artifacts-and-validation.md).

---

[^survey]: Sheffer, Praun, Rose, *Mesh Parameterization Methods and their Applications* (2006) — segmentation objective, distortion-vs-cuts balance, genus/2g, isometry impossibility, boundary artefacts. https://www.cs.ubc.ca/~sheffa/papers/param_survey06.pdf · Hormann, Lévy, Sheffer, *Mesh Parameterization: Theory and Practice*, SIGGRAPH 2007 course. https://www.inf.usi.ch/hormann/papers/Hormann.2008.MPT.pdf
[^hfc]: Garland, Willmott, Heckbert, *Hierarchical Face Clustering on Polygonal Surfaces*, I3D 2001. https://www.cs.cmu.edu/~garland/Papers/cluster.pdf
[^vsa]: Cohen-Steiner, Alliez, Desbrun, *Variational Shape Approximation*, SIGGRAPH 2004. https://dl.acm.org/doi/10.1145/1015706.1015817
[^dchart]: Julius, Kraevoy, Sheffer, *D-Charts: Quasi-Developable Mesh Segmentation*, Eurographics 2005. https://www.cs.ubc.ca/~sheffa/papers/EG05Dcharts.pdf
[^sorkine]: Sorkine, Cohen-Or, Goldenthal, Lischinski, *Bounded-distortion Piecewise Mesh Parameterization*, IEEE Vis 2002. https://people.eecs.berkeley.edu/~jrs/meshpapers/SCOGL.pdf · Gu, Gortler, Hoppe, *Geometry Images*, SIGGRAPH 2002. https://hhoppe.com/proj/gim/
[^xatlas]: xatlas (MIT) — chart computation + packing. https://github.com/jpcy/xatlas
[^seamster]: Sheffer & Hart, *Seamster: Inconspicuous Low-Distortion Texture Seam Layout*, IEEE Vis 2002. https://dl.acm.org/doi/10.5555/602099.602154
[^autocuts]: Poranne, Tarini, et al., *Autocuts: Simultaneous Distortion and Cut Optimization for UV Mapping*, SIGGRAPH Asia 2017. https://igl.ethz.ch/projects/autocuts/
[^optcuts]: Li, Kaufman, et al., *OptCuts: Joint Optimization of Surface Cuts and Parameterization*, SIGGRAPH Asia 2018. https://www.cs.ucla.edu/~minchenl/research/optcuts/
