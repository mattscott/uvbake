# UVBake — Implementation Plans

The build plan for a managed-C# automatic UV-atlas generator, based on the research in
[../Documents/](../Documents/). Direction in one line: **segment the mesh into the largest
low-distortion charts, flatten each injectively (no flips/overlaps), normalize texel density, and pack
into a unique bake-ready atlas** — the classic segment → parameterize → pack pipeline, tuned for
baking.

| Doc | Contents |
|-----|----------|
| [00-master-plan.md](00-master-plan.md) | Vision, scope, non-goals, milestones, success criteria, risks |
| [01-architecture.md](01-architecture.md) | Project/module layout, core data structures, public API, determinism |
| [02-phase-roadmap.md](02-phase-roadmap.md) | Phase-by-phase tasks, deliverables, acceptance tests |

**Phase ladder at a glance**

```
P0 Scaffold ─► P1 Mesh+IO ─► P2 Segmentation ─► P3 Parameterization ─►
P4 Normalize+Pack (atlas) ─► P5 Robustness+quality ─► P6 Perf+determinism ─► P7 Packaging/CLI/API
```

The first *demoable* milestone (a unique bake-ready atlas out) is end of **P4**.
**P3 (injective parameterization + the managed sparse solver)** is the de-risking **kill-point** —
validated in isolation (no-flip guarantee + solver correctness/perf) before P4 builds on it.

**Pipeline context.** UVBake is the **UV-unwrap** stage: it takes a mesh that has clean topology but no
UVs (e.g. from an upstream quad-remesher) and produces the same mesh with a unique, non-overlapping UV
atlas, for a downstream texture/normal baker. It does **not** retopologize and does **not** bake — see
[../Documents/00-overview.md](../Documents/00-overview.md) §2.
