# UVBake

[![ci](https://github.com/mattscott/uvbake/actions/workflows/ci.yml/badge.svg)](https://github.com/mattscott/uvbake/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A standalone, permissively-licensed, dependency-light, cross-platform **managed-C# (.NET 8)** library +
CLI that automatically generates a **UV atlas** for a mesh: a **unique, non-overlapping**
parameterization with the **largest possible low-distortion islands**, normalized texel density, and
seams on hard edges — built so the result can be **textured/baked** cleanly.

It is a **clean-room** implementation: mesh **segmentation into charts** → **injective parameterization**
(LSCM conformal init → SLIM symmetric-Dirichlet polish) → **atlas packing**, with **no native
dependencies** (an in-repo conjugate-gradient sparse solver), MIT-clean.

> **Status: end-to-end and shipping (P1–P7 complete).** The full pipeline runs — mesh in, unique
> bake-ready UV atlas out — validated (overlap = 0, no flips, full coverage) on the procedural corpus.
> See [`docs/plans`](docs/plans) for the roadmap, [`docs/design`](docs/design) for the
> research, and [`docs/design/10-technical-debt.md`](docs/design/10-technical-debt.md) for
> deferred/reduced items.

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
  UVBake.IO/     mesh readers/writers (OBJ and binary FBX in; OBJ, FBX and glTF out; per-wedge UVs at seams)
  UVBake.Cli/    `uvbake in.obj -o out.obj --res 2048 ...`
tests/
  UVBake.Tests/  unit + golden-layout tests
tools/
  reference-oracle/  scripts that generate golden UV layouts/metrics (xatlas / Blender headless)
```

## Build & test

```sh
dotnet build
dotnet test          # 67 tests
```

Requires the .NET 8 runtime (the SDK may be newer; `global.json` rolls forward).

## CLI

```sh
# unwrap an OBJ into a bake-ready atlas
uvbake model.obj -o model.uv.obj

# common options
uvbake model.obj -o atlas.obj \
  --res 2048           # atlas resolution (square)
  --gutter 8           # inter-island gutter (texels)
  --max-stretch 0.25   # distortion budget for chart growth
  --hard-edge 45       # dihedral hard-edge threshold (degrees)
  --seams Both         # DihedralAngle | ProvidedTags | Both
  --packer Silhouette  # BoundingBox (default) | Silhouette
  --density 512        # fixed texels-per-unit; spills across UDIM tiles if needed

# glTF output (UVs per-vertex, seams vertex-split) — chosen by extension
uvbake model.obj -o atlas.gltf

# also write an SVG of the packed layout (islands colored by chart)
uvbake model.obj -o atlas.obj --layout atlas.svg
```

Run `uvbake --help` for the full option list. The CLI packs as a .NET tool, command `uvbake`. It is not on NuGet yet, so
install it from a local build: `dotnet pack -c Release -o artifacts`, then
`dotnet tool install --global --add-source ./artifacts UVBake.Cli`.

## Library

```csharp
using UVBake.Core;
using UVBake.IO;

Mesh mesh = MeshIo.Read("model.obj");
UvMesh result = UVBaker.Generate(mesh, new UVBakeOptions { Resolution = 2048, GutterPixels = 8 });

// Hard gates: overlap = 0, no flips, full coverage.
if (result.Validation.IsValid)
{
    MeshIo.Write(result.Mesh, "model.uv.obj");   // per-wedge UVs (seams round-trip)
}
```

`UVBaker.Generate` also has an overload taking `IProgress<string>` (per-phase progress) and a
`CancellationToken`. `UVBake.Core` and `UVBake.IO` pack as NuGet packages (`dotnet pack`; not yet on nuget.org).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Please report security issues privately ([SECURITY.md](SECURITY.md)).

## License

MIT — see [LICENSE](LICENSE). Algorithmic attributions are in [NOTICE](NOTICE).
