# Reference oracle (test-only)

UVBake validates its output with a golden-output discipline: a trusted, free, scriptable, deterministic
reference unwrapper produces **golden UV layouts and metrics**, and CI diffs UVBake's output against
them. None of these tools are shipped or linked — they are invoked as external processes to emit
committed goldens.

## Primary oracle — xatlas

[**xatlas**](https://github.com/jpcy/xatlas) (MIT) is the open de-facto standard for generating
unique, bake-ready texture atlases (segment → parameterize → pack). It is deterministic, scriptable,
and cross-platform, which makes it the natural primary oracle: run it on each corpus mesh and commit
the resulting UV layout + summary metrics (chart count, total seam length, max/avg stretch, packing
efficiency, overlap = 0) as goldens. See
[`../../docs/design/08-prior-art-tools-and-libraries.md`](../../docs/design/08-prior-art-tools-and-libraries.md).

## Secondary cross-check — Blender (headless)

`blender --background --python <script>.py` (Smart UV Project / angle-based unwrap + Pack Islands)
gives a second, independent reference. Use it to cross-validate that UVBake's layouts are competitive
with a widely-used unwrapper, not as the primary CI gate.

## Comparison metrics

Pure pixel-equality of a UV layout is brittle, so the gates are **metric-based** plus a layout image
diff: hard-gate on **overlap = 0** and **full coverage** (every face parameterized); compare
**max/avg stretch**, **chart count**, **total seam length**, and **packing efficiency (% texels used)**
against the oracle within tolerance; emit a rendered UV-layout PNG as a CI artifact on failure.

## Harness scripts

| Script | Purpose |
|---|---|
| `download-corpus.ps1` | Fetch the tier-B real meshes (spot, armadillo, bunny, gazebo) into `tests/corpus/downloaded/` (git-ignored). |
| `build-xatlas.ps1` | Clone + CMake-build xatlas into `_work/` (git-ignored). Needs git/cmake/C++ toolchain; the heavy native step, not run in CI. |
| `run-oracle.ps1` | Run the built xatlas over the corpus, emitting golden UV layouts + metrics into `tests/golden/`. |

The tier-A procedural corpus (`cube`, `sphere`, `cylinder`, `beveled-box`, `torus`) is generated in-code
(`tests/UVBake.Tests/Corpus/Primitives.cs`) and committed under `tests/corpus/`; regenerate it with
`UVBAKE_WRITE_CORPUS=1 dotnet test --filter CorpusGeneration`.

## Status

Harness in place (P1). The native xatlas build + golden generation is **not** run yet — it lights up the
golden gate in **P4** (see [`../../docs/plans/02-phase-roadmap.md`](../../docs/plans/02-phase-roadmap.md)).
The working tree (`_work/`, logs) and downloaded raws are git-ignored; the committed goldens under
`tests/golden/` are not.
