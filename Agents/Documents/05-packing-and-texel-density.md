# Packing & Texel Density — laying islands into the atlas

After each chart is flattened ([`04`](04-parameterization-and-distortion.md)) it has an arbitrary 2D
outline. Packing places all charts into the atlas **without overlap**, scaled to a **consistent texel
density**, with a **gutter** sized for a downstream baker's dilation skirt.

## 1. Two packing families

### 1.1 Rectangle bin-packing (chart = its bounding box)
The canonical practical reference is Jylänki's *"A Thousand Ways to Pack the Bin"* (public domain
paper + code).[^jylanki] Four families:

| Family | Idea | Trade |
|---|---|---|
| **Shelf** | rows of equal height = first item placed | fastest; loosest (wasted row height) |
| **Guillotine** | each placement splits free space with one edge-to-edge cut; recursive free-rect list | best speed/density balance; good default |
| **MaxRects** | maintain the set of *maximal* free rectangles; split all overlapped on insert | **tightest** packs (BSSF/ContactPoint), higher CPU |
| **Skyline** | track the top profile; place on top | low memory, fast, good density (glyph atlases) |

Rule of thumb: **MaxRects-BSSF/ContactPoint** = tightest, **Skyline-BL / Guillotine** = best
speed/quality compromise, **Shelf** = fastest/loosest. Insert charts in **decreasing area** order for
a material improvement in all of them.

### 1.2 Rasterized / bitmap "Tetris" packing (chart = its true silhouette) — recommended
Bounding boxes waste the concave space around irregular charts. The state of the art for **baking
atlases** rasterizes both the chart silhouette and the atlas occupancy into bitmaps and searches for a
non-overlapping placement of the *actual shape* — "Tetris packing," as **xatlas** (and its `thekla_atlas`
ancestor from *The Witness*) does.[^xatlas][^tabi] Steps:

1. Rotate each chart to its convex-hull axis so its box is tight (`rotateChartsToAxis`).
2. **Rasterize** the chart's triangles into a per-chart occupancy bitmap, **dilated by `padding`** (and
   1 texel for bilinear) — so the gutter is reserved *by construction*.
3. Maintain an atlas occupancy bitmap. For each chart (largest first), scan candidate `(x,y)` +
   orientation; reject any overlap of set bits; keep the best-scoring valid spot (favouring tight/
   bottom-left fit); blit.
4. If a chart doesn't fit, grow the atlas (or spill to a new page/UDIM tile) and retry.

`bruteForce` scans all positions (best occupancy); `random` samples (faster, slightly looser).
**UVBake should adopt rasterized packing with "pad the silhouette, then pack"** so the gutter is
correct by construction rather than a fragile post-step.

### 1.3 Rotation, power-of-two, block alignment, quality knob
xatlas's `PackOptions` enumerate the relevant knobs: `padding`, `texelsPerUnit`, `resolution`,
`bilinear`, `blockAlign` (4×4 for BC compression boundaries, also speeds the search), `bruteForce`,
`rotateChartsToAxis`, `rotateCharts`.[^xatlas] Practical choices: **90°/axis rotation** (free arbitrary
rotation packs marginally tighter but breaks block alignment and complicates baking); **power-of-two
square** atlas (1024/2048/4096) for GPU mips — fix `resolution` or round `texelsPerUnit`-driven size up
to the next POT; expose a `bruteForce` quality flag (default on for offline asset prep).

## 2. Gutter / margin — coupled to the baker's dilation

A baker writes colour only inside each island, then **dilates** it outward into the gutter so bilinear
filtering and mips don't sample empty/neighbouring texels across a seam. If two islands are packed
closer than twice the dilation radius, their skirts **collide and bleed**.[^polypad][^blenderbake][^adobepad]
So spacing must be reserved **at pack time** — the packer cannot pick island spacing independently of
the intended dilation width.

**Rule: gutter between islands ≈ 2× the per-island padding** (each island dilates by its own padding;
the two skirts meet in the middle). Padding scales with resolution and mip depth:

| Atlas res | Padding (per edge) |
|---|---|
| 1024² | ~4–8 px |
| 2048² | ~8–16 px |
| 4096² | ~16–32 px |

Set `padding` to the intended dilation width and dilate the silhouette **before** the overlap search
(§1.2). **UDIM** spill when one 0–1 tile can't hold everything at target density: tile index
`1001 + u + 10·v`, files `name.1001.ext` …; each tile is an independent 0–1 packing problem, largest
charts first, per-tile gutter preserved.[^udim]

## 3. Texel-density normalization (the "normalized islands" requirement)

Texel density = texture pixels per unit of real surface. **Normalized** = every chart shares the same
texels-per-unit, so detail is uniform (no crisp island next to a blurry one). Base formula:

```text
TexelDensity = TextureResolution / WorldSize        (px per unit length)
# e.g. 1024² over a 1×1 m quad = 1024 px/m ≈ 10.24 px/cm
```

Per-chart scale to hit a target density `D` (texels/unit), from 3D vs UV area:

```text
texelsPerUnit_current = AtlasRes · sqrt( UV_area_chart / Surface_area_3D_chart )
scale_to_apply        = D_target / texelsPerUnit_current
UV_area_fraction      = Surface_area · (D / AtlasRes)²
```

This is exactly what a single global `texelsPerUnit` encodes — set it once, scale every chart to it,
then pack ⇒ uniform density automatically.[^xatlas][^modotd][^beyondextent] **Pipeline order:** flatten
→ measure each chart's 3D area + UV area → scale to the global target → **then** pack.

**Tension — strict density vs packing efficiency.** Locking every chart to one exact density forbids
nudging an awkward chart to fill a gap ⇒ more wasted space / a bigger atlas. Standard compromise: a
small density **tolerance band** (≈ ±10%) lets the packer trade invisible density variance for real
occupancy. Expose a policy flag (`StrictTexelDensity` vs `PackEfficiencyFirst`), default
**strict-with-small-tolerance** — the "balanced for baking" objective.

## 4. Recommendations for UVBake

- **Rasterized (Tetris) packing**, silhouette dilated by `padding` before the search; `bruteForce`
  default-on for offline quality; 90°/axis rotation; power-of-two square; optional 4×4 block align.
- **Gutter = 2× the downstream dilation width**; default padding per the resolution table; reserve it
  by dilating the silhouette, not as a post-step.
- **Normalize texel density**: one global `texelsPerUnit`, per-chart uniform scale before packing,
  ±10% tolerance band in efficiency mode; UDIM spill when over-budget.
- Report **packing efficiency (% texels used)** as the headline pack-quality metric
  ([`07`](07-quality-artifacts-and-validation.md)).
- Note: **SCAF** ([`04`](04-parameterization-and-distortion.md) §3.3) folds packing into the
  parameterization with a bijectivity guarantee — an alternative to a standalone packer; the rasterized
  packer is the simpler, more controllable default and pairs with per-chart SLIM.

---

[^jylanki]: J. Jylänki, *A Thousand Ways to Pack the Bin* (2010). Paper http://clb.demon.fi/files/RectangleBinPack.pdf · code (Public Domain) https://github.com/juj/RectangleBinPack
[^xatlas]: xatlas (MIT) — `PackOptions`, Tetris/bitmap packing, `texelsPerUnit`. https://github.com/jpcy/xatlas
[^tabi]: *TABI: Tight and Balanced Interactive Atlas Packing* (CGF) — describes xatlas's rasterized Tetris packing. https://onlinelibrary.wiley.com/doi/10.1111/cgf.70332
[^polypad]: Polycount wiki — Edge padding. http://wiki.polycount.com/wiki/Edge_padding
[^blenderbake]: Blender Manual — Render Baking (bake margin / dilation). https://docs.blender.org/manual/en/latest/render/cycles/baking.html
[^adobepad]: Adobe Substance 3D — Texture dilation / padding. https://substance3d.adobe.com/documentation/spdoc/texture-dilation-or-padding-134643719.html
[^udim]: Blender Manual — UDIMs. https://docs.blender.org/manual/en/latest/modeling/meshes/uv/workflows/udims.html
[^modotd]: Foundry/Modo — Texel density. https://learn.foundry.com/modo/12.0/content/help/pages/uving/texel_density.html
[^beyondextent]: BeyondExtent — Texel density deep dive. https://www.beyondextent.com/deep-dives/deepdive-texeldensity
