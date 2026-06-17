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
[`../../Agents/Documents/08-prior-art-tools-and-libraries.md`](../../Agents/Documents/08-prior-art-tools-and-libraries.md).

## Secondary cross-check — Blender (headless)

`blender --background --python <script>.py` (Smart UV Project / angle-based unwrap + Pack Islands)
gives a second, independent reference. Use it to cross-validate that UVBake's layouts are competitive
with a widely-used unwrapper, not as the primary CI gate.

## Comparison metrics

Pure pixel-equality of a UV layout is brittle, so the gates are **metric-based** plus a layout image
diff: hard-gate on **overlap = 0** and **full coverage** (every face parameterized); compare
**max/avg stretch**, **chart count**, **total seam length**, and **packing efficiency (% texels used)**
against the oracle within tolerance; emit a rendered UV-layout PNG as a CI artifact on failure.

## Status

Scaffold only — scripts are added in **P0/P1** (see
[`../../Agents/Plans/02-phase-roadmap.md`](../../Agents/Plans/02-phase-roadmap.md)). The working tree
(`_work/`, logs) is git-ignored; the committed goldens under `tests/golden/` are not.
