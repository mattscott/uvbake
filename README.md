# UVBake

A standalone, permissively-licensed, dependency-light, cross-platform **managed-C# (.NET 8)** library +
CLI that automatically generates a **UV atlas** for a mesh: a **unique, non-overlapping**
parameterization with the **largest possible low-distortion islands**, normalized texel density, and
seams on hard edges — built so the result can be **textured/baked** cleanly.

It is a **clean-room** implementation: mesh **segmentation into charts** → **conformal/ARAP
parameterization** → **atlas packing**, with **no native dependencies**, MIT-clean.

> **Status: P0 — scaffold.** The solution builds and tests run; the unwrapper is not yet implemented.
> See [`Agents/Plans`](Agents/Plans) for the roadmap and [`Agents/Documents`](Agents/Documents) for
> the research it is based on.

## Pipeline

UVBake fills the gap between an **upstream remesher** (which produces clean topology but strips/omits
UVs) and a **downstream texture/normal baker** (which needs a unique UV layout to bake into):

```
mesh (no UVs)  ─►  UVBake (this tool)  ─►  same mesh + bake-ready UV atlas  ─►  texture/normal baker
```

The output is a **unique, non-overlapping** atlas (every face has its own texels), islands grown as
large as the distortion budget allows, scaled to a consistent texel density, with seams routed along
hard edges and packed with a gutter sized for a baker's dilation skirt.

## Layout

```
src/
  UVBake.Core/   the library (segmentation, parameterization, packing) — dependency-light
  UVBake.IO/     mesh readers/writers (OBJ first; per-wedge UVs at seams)
  UVBake.Cli/    `uvbake in.obj -o out.obj --res 2048 ...`
tests/
  UVBake.Tests/  unit + golden-layout tests
tools/
  reference-oracle/  scripts that generate golden UV layouts/metrics (xatlas / Blender headless)
```

## Build & test

```sh
dotnet build
dotnet test
```

Requires the .NET 8 runtime (the SDK may be newer; `global.json` rolls forward).

## License

MIT — see [LICENSE](LICENSE). Algorithmic attributions are in [NOTICE](NOTICE).
