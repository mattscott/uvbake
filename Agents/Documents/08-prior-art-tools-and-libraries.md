# Prior Art — Tools, Reference Oracle & Reusable Libraries

This surveys the existing automatic-UV tools (to learn from and to pick a **golden-output oracle**),
then catalogues **license-checked libraries** for a managed, MIT-clean implementation. The headline
finding: **no pure-managed C# UV unwrapper exists**, so UVBake is a clean-room port — which makes
license hygiene of the *reference* sources important.

## 1. Existing automatic-UV tools

| Tool | Automatic? / island strategy | License | URL |
|---|---|---|---|
| **xatlas** | Fully automatic library: `ComputeCharts` (segment into quasi-developable D-charts + LSCM-family flatten) → `PackCharts` (Tetris/bitmap). Grows charts to a distortion budget. The open de-facto **baking-atlas** standard. | **MIT** | https://github.com/jpcy/xatlas |
| **Microsoft UVAtlas** | The canonical **isochart**: stretch-driven spectral segmentation → parameterization → repack; supports importance maps. **Archived April 2026** (legacy). | **MIT** | https://github.com/microsoft/UVAtlas |
| **Blender** | Scriptable DCC. Unwrap: **ABF** (default), **Conformal/LSCM**, **Minimum-Stretch/SLIM** (4.3+). **Smart UV Project** auto-cuts by angle; **Pack Islands**. Headless via `bpy`. | **GPL-2.0+** | https://docs.blender.org/manual/en/latest/modeling/meshes/editing/uv.html |
| **SideFX Labs Auto UV** (Houdini) | Free/open HDA: curvature/orientation seam cuts, island build + pack; modes Shortest-Path / Cluster / AutoSeam. (Houdini host is commercial.) | free toolset | https://www.sidefx.com/docs/houdini/nodes/sop/labs--autouv.html |
| **Ministry of Flat** | The most aggressively automatic; famous for **large, clean, low-distortion islands** ("no artist input"); 20+ topology-specialized algorithms; even texel density + auto pack. | commercial (free build exists, restricted) | https://www.quelsolaar.com/ministry_of_flat/ |
| **RizomUV** | Professional auto-UV gold standard (one-click autoseams/unwrap/pack) + precise manual control. | commercial | https://www.rizomuv.com/ |
| **Unwrella** (3d-io GmbH — *not* Autodesk) | One-click fully-automatic unwrap+pack, low distortion, uniform texel distribution. | commercial | https://www.unwrella.com/ |
| **Headus UVLayout** | Interactive dynamics-based relaxation, very low distortion; semi-automatic. | commercial | https://www.uvlayout.com/ |
| **Roadkill** | Free standalone, LSCM+ABF (from Blender); semi-automatic. Legacy. | GPL | https://www.pullin-shapes.co.uk/ |

## 2. Reference oracle: **xatlas** primary, **Blender headless** cross-check

- **xatlas — primary.** Single-purpose library with an explicit staged API; with fixed options + pinned
  threads it is deterministic input→atlas. **MIT** = zero friction to vendor into CI. Crucially its
  scope **matches UVBake exactly** (segment → parameterize → pack), so its outputs — chart count, island
  count, packed UVs, occupancy — are directly comparable metric-for-metric. The natural golden baseline.
- **Blender headless — secondary.** Genuinely free and fully scriptable with a complete multi-algorithm
  pipeline (ABF/LSCM/SLIM/Smart UV Project/Pack Islands) — great for cross-checking distortion quality
  and human-comparable layouts. But **GPL** (keep out-of-tree/opt-in), version-drifty (SLIM is
  iterative), and slow/stateful per test. Pin an exact version.

Practical split: vendor **xatlas (MIT)** as the in-CI deterministic baseline; keep **pinned Blender**
out-of-tree as the distortion-quality/visual oracle.

## 3. Reusable libraries (license-verified)

**All capable parameterization/atlas libraries are native C/C++ — none is managed.** UVBake's
zero-native stance rules out P/Invoke wrappers ⇒ **clean-room port**, preferring MIT/BSD/MPL2 sources
and treating GPL strictly as paper reference.

| Library | UV-relevant capability | License | Native? | Note for a clean-room MIT port |
|---|---|---|---|---|
| **xatlas**[^xatlas] | D-chart segment + LSCM flatten + Tetris pack | **MIT** | C++11, no deps | the closest blueprint; port-friendly |
| **MS UVAtlas**[^uvatlas] | isochart (spectral segment + param + repack) | **MIT** | C++ | archived 2026; isochart reference |
| **libigl**[^libigl] | LSCM, ARAP, SLIM, **SCAF**, harmonic/Tutte | **MPL-2.0** | C++ | file-level copyleft — **spec, don't copy** |
| **Boundary First Flattening**[^bff] | linear conformal w/ cones; seamless | **MIT** *(repo LICENSE is plain MIT — verify at integration; deps on SuiteSparse)* | C++ | usable reference |
| **geometry-central**[^gc] | halfedge mesh + geometry processing | **MIT** | C++ | great architectural reference for a managed halfedge mesh |
| **CGAL** Surface Mesh Param[^cgal] | LSCM, ARAP, conformal/authalic, Tutte, Orbifold | **GPL-3.0+ / commercial** ⚠️ | C++ | **do NOT read/copy** (taint) — paper reference only |
| **OpenNL**[^onl] | sparse least-squares + iterative solvers (original LSCM backend) | orig **LGPL**; **geogram** copy **BSD-3** | C | use the **BSD geogram** copy |
| **Eigen**[^eigen] | sparse direct + iterative solvers | **MPL-2.0** (`EIGEN_MPL2_ONLY`) | C++ header | solver backend reference |

**No pure-managed C#/.NET UV unwrapper exists** (no managed LSCM/ARAP/SCAF/SLIM/BFF/isochart on NuGet
or GitHub; only native bindings, and geometry libs like g3Sharp ship no parameterization). **A
clean-room managed port is mandatory.**

### Managed sparse solvers (for the LSCM/ARAP solves)
See [`06`](06-acceleration-and-performance.md) §2: **CSparse.NET** (LGPL-2.1+, sparse-direct, strongest
fit — dynamic-link or accept copyleft), **Math.NET Numerics** (MIT, sparse *iterative* only), ALGLIB
(GPL/commercial — avoid). MIT-clean default: Math.NET + a clean-room sparse Cholesky.

## 4. Golden testing (metrics + determinism)

Generate goldens with xatlas (+ Blender cross-check); diff per-mesh:
- **Hard gates (pass/fail):** **overlap = 0** (injectivity) and **full coverage** (every face
  parameterized) — see [`07`](07-quality-artifacts-and-validation.md).
- **Tolerance metrics:** chart/island **count**, **total seam length**, **max & area-weighted-L²
  stretch** (Sander), **packing efficiency (% texels used)**, **texel-density variance**, **min
  inter-island gutter**.
- **Layout image diff:** rasterize islands (coloured by chart id) to a deterministic PNG; per-pixel or
  SSIM diff to catch layout drift; also diff the packed UV buffer (canonically sorted) at ~1e-5.
- **Determinism** is a prerequisite — see [`06`](06-acceleration-and-performance.md) §5 (FP
  non-associativity, fixed reduction order, deterministic tie-breaks, fixed iteration counts).

## 5. License corrections worth flagging

- **BFF is MIT** (commercially usable), not an academic/non-commercial licence.
- **CGAL's parameterization package is GPL-3.0+** (not LGPL) — the one library *not* to copy in a
  clean-room MIT port.
- **Unwrella is by 3d-io GmbH**, a third-party plugin vendor — not Autodesk.
- **xatlas folded `ParameterizeCharts` into `ComputeCharts`**; public stages are `ComputeCharts` →
  `PackCharts` (or `Generate()`).
- **MS UVAtlas was archived (April 2026)** — still MIT and a valid isochart reference, unmaintained.

---

[^xatlas]: xatlas (MIT). https://github.com/jpcy/xatlas
[^uvatlas]: Microsoft UVAtlas (MIT; isochart). https://github.com/microsoft/UVAtlas · isochart report https://www.microsoft.com/en-us/research/wp-content/uploads/2017/01/isochart_report.pdf
[^libigl]: libigl (MPL-2.0). https://github.com/libigl/libigl · https://libigl.github.io/tutorial/
[^bff]: Boundary First Flattening (MIT). https://github.com/GeometryCollective/boundary-first-flattening
[^gc]: geometry-central (MIT). https://github.com/nmwsharp/geometry-central · https://geometry-central.net
[^cgal]: CGAL Surface Mesh Parameterization (GPL-3.0+/commercial). https://doc.cgal.org/latest/Surface_mesh_parameterization/ · https://www.cgal.org/license.html
[^onl]: OpenNL — geogram (BSD-3) https://github.com/BrunoLevy/geogram · original (LGPL) http://alice.loria.fr/index.php/software/4-library/23-opennl.html
[^eigen]: Eigen (MPL-2.0). https://gitlab.com/libeigen/eigen
