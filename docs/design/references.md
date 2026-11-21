# References

Consolidated bibliography for the UVBake research docs, grouped by topic; each entry is cited from the
relevant document.

## Seminal atlas-generation lineage
- Maillot, Yahia, Verroust, *Interactive Texture Mapping*, SIGGRAPH 1993. https://doi.org/10.1145/166117.166120 · https://inria.hal.science/inria-00423776
- Sander, Snyder, Gortler, Hoppe, *Texture Mapping Progressive Meshes* (stretch metric), SIGGRAPH 2001. https://hhoppe.com/tmpm.pdf · https://doi.org/10.1145/383259.383307
- Lévy, Petitjean, Ray, Maillot, *Least Squares Conformal Maps for Automatic Texture Atlas Generation*, SIGGRAPH 2002. https://doi.org/10.1145/566654.566590
- Sorkine, Cohen-Or, Goldenthal, Lischinski, *Bounded-distortion Piecewise Mesh Parameterization*, IEEE Vis 2002. https://people.eecs.berkeley.edu/~jrs/meshpapers/SCOGL.pdf
- Sander, Wood, Gortler, Snyder, Hoppe, *Multi-Chart Geometry Images*, SGP 2003. https://hhoppe.com/proj/mcgim/
- Sander et al., *Signal-Specialized Parametrization*, EGWR 2002. https://hhoppe.com/ssp.pdf

## Surveys
- Floater & Hormann, *Surface Parameterization: a Tutorial and Survey*, 2005. https://graphics.stanford.edu/courses/cs468-05-fall/Papers/param-survey.pdf
- Sheffer, Praun, Rose, *Mesh Parameterization Methods and their Applications*, FnT CGV 2006. https://www.cs.ubc.ca/~sheffa/papers/param_survey06.pdf
- Hormann, Lévy, Sheffer, *Mesh Parameterization: Theory and Practice*, SIGGRAPH 2007 course. https://www.inf.usi.ch/hormann/papers/Hormann.2008.MPT.pdf · https://dl.acm.org/doi/10.1145/1281500.1281510

## Segmentation & seams
- Garland, Willmott, Heckbert, *Hierarchical Face Clustering on Polygonal Surfaces*, I3D 2001. https://www.cs.cmu.edu/~garland/Papers/cluster.pdf
- Cohen-Steiner, Alliez, Desbrun, *Variational Shape Approximation*, SIGGRAPH 2004. https://dl.acm.org/doi/10.1145/1015706.1015817
- Julius, Kraevoy, Sheffer, *D-Charts: Quasi-Developable Mesh Segmentation*, EG 2005. https://www.cs.ubc.ca/~sheffa/papers/EG05Dcharts.pdf
- Gu, Gortler, Hoppe, *Geometry Images*, SIGGRAPH 2002. https://hhoppe.com/proj/gim/
- Sheffer & Hart, *Seamster: Inconspicuous Low-Distortion Texture Seam Layout*, IEEE Vis 2002. https://dl.acm.org/doi/10.5555/602099.602154
- Poranne, Tarini, et al., *Autocuts*, SIGGRAPH Asia 2017. https://igl.ethz.ch/projects/autocuts/
- Li, Kaufman, et al., *OptCuts: Joint Optimization of Surface Cuts and Parameterization*, SIGGRAPH Asia 2018. https://www.cs.ucla.edu/~minchenl/research/optcuts/

## Parameterization (flattening)
- Tutte, *How to Draw a Graph*, 1963. https://www.cs.jhu.edu/~misha/Fall09/Tutte63.pdf
- Floater, *Parametrization and smooth approximation*, CAGD 1997. https://www.cs.jhu.edu/~misha/Fall09/Floater97.pdf · https://doi.org/10.1016/S0167-8396(96)00031-3
- Floater, *Mean Value Coordinates*, CAGD 2003. https://doi.org/10.1016/S0167-8396(03)00002-5 · Hormann & Floater, ACM TOG 2006. https://www.mn.uio.no/math/english/people/aca/michaelf/papers/barycentric.pdf
- Desbrun, Meyer, Alliez, *Intrinsic Parameterizations of Surface Meshes* (DCP/DNCP), EG 2002. https://dl.acm.org/doi/10.1111/1467-8659.00580
- Sheffer, Lévy, Mogilnitsky, Bogomyakov, *ABF++*, ACM TOG 2005. https://www.cs.ubc.ca/~sheffa/papers/abf_plus_plus.pdf
- Liu, Zhang, Xu, Gotsman, Gortler, *A Local/Global Approach to Mesh Parameterization* (ARAP), SGP 2008. https://www.eecs.harvard.edu/~sjg/papers/arap.pdf
- Sawhney & Crane, *Boundary First Flattening*, ACM TOG 2018. https://arxiv.org/pdf/1704.06873 · https://geometrycollective.github.io/boundary-first-flattening/

## Injectivity / bijectivity
- Schüller, Kavan, Panozzo, Sorkine-Hornung, *Locally Injective Mappings*, SGP 2013. https://doi.org/10.1111/cgf.12179 · https://igl.ethz.ch/projects/LIM/
- Smith & Schaefer, *Bijective Parameterization with Free Boundaries*, SIGGRAPH 2015. https://people.engr.tamu.edu/schaefer/research/bijective.pdf · https://doi.org/10.1145/2766947
- Rabinovich, Poranne, Panozzo, Sorkine-Hornung, *Scalable Locally Injective Mappings* (SLIM), ACM TOG 2017. https://cims.nyu.edu/gcl/papers/SLIM2017.pdf · https://doi.org/10.1145/2983621
- Jiang, Schaefer, Panozzo, *Scaffold/SCAF*, SIGGRAPH Asia 2017. https://github.com/jiangzhongshi/Scaffold-Map
- Liu, Ye, Ni, Fu, *Progressive Parameterizations*, SIGGRAPH 2018. https://ruiqini.github.io/Publication/18_PP/SIGGRAPH18_ProgressivePara.pdf

## Packing & texel density
- Jylänki, *A Thousand Ways to Pack the Bin*, 2010. http://clb.demon.fi/files/RectangleBinPack.pdf · code (Public Domain) https://github.com/juj/RectangleBinPack
- *TABI: Tight and Balanced Interactive Atlas Packing*, CGF. https://onlinelibrary.wiley.com/doi/10.1111/cgf.70332
- Foundry/Modo — Texel density. https://learn.foundry.com/modo/content/help/pages/uving/texel_density.html
- BeyondExtent — Texel density deep dive. https://www.beyondextent.com/deep-dives/deepdive-texeldensity
- Blender — UDIMs. https://docs.blender.org/manual/en/latest/modeling/meshes/uv/workflows/udims.html
- Polycount wiki — Edge padding; Texture Baking. http://wiki.polycount.com/wiki/Edge_padding · http://wiki.polycount.com/wiki/Texture_Baking
- Adobe Substance — Texture dilation/padding. https://substance3d.adobe.com/documentation/spdoc/texture-dilation-or-padding-134643719.html

## Quad-mesh layout (upstream — NOT UVBake's job, for disambiguation)
- Kälberer, Nieser, Polthier, *QuadCover*, CGF 2007. https://doi.org/10.1111/j.1467-8659.2007.01060.x
- Bommes, Zimmer, Kobbelt, *Mixed-Integer Quadrangulation*, SIGGRAPH 2009. https://www.graphics.rwth-aachen.de/media/papers/bommes_zimmer_2009_siggraph_011.pdf
- Bommes et al., *Integer-Grid Maps for Reliable Quad Meshing*, SIGGRAPH 2013. https://doi.org/10.1145/2461912.2462014
- Bommes et al., *Quad-Mesh Generation and Processing: A Survey*, CGF 2013. https://inria.hal.science/hal-01059755/en

## Tools
- xatlas (MIT). https://github.com/jpcy/xatlas
- Microsoft UVAtlas (MIT; isochart). https://github.com/microsoft/UVAtlas
- Blender Manual — UV editing / unwrap. https://docs.blender.org/manual/en/latest/modeling/meshes/editing/uv.html
- Ministry of Flat. https://www.quelsolaar.com/ministry_of_flat/
- RizomUV. https://www.rizomuv.com/ · Unwrella (3d-io). https://www.unwrella.com/ · Headus UVLayout. https://www.uvlayout.com/
- SideFX Labs Auto UV. https://www.sidefx.com/docs/houdini/nodes/sop/labs--autouv.html

## Libraries (license-checked)
- libigl (MPL-2.0). https://github.com/libigl/libigl · https://libigl.github.io/tutorial/
- Boundary First Flattening (MIT). https://github.com/GeometryCollective/boundary-first-flattening
- geometry-central (MIT). https://github.com/nmwsharp/geometry-central
- CGAL Surface Mesh Parameterization (GPL-3.0+/commercial — avoid). https://doc.cgal.org/latest/Surface_mesh_parameterization/
- OpenNL — geogram (BSD-3). https://github.com/BrunoLevy/geogram · Eigen (MPL-2.0). https://gitlab.com/libeigen/eigen
- CSparse.NET (LGPL-2.1+, managed). https://github.com/wo80/CSparse.NET · Math.NET Numerics (MIT). https://numerics.mathdotnet.com

## Triangulation / output formats / validation
- MikkTSpace — shortest-diagonal quad split. https://github.com/mmikk/MikkTSpace
- OBJ format (independent v/vt/vn indices). https://dirsig.cis.rit.edu/docs/new/obj.html · https://en.wikipedia.org/wiki/Wavefront_.obj_file
- glTF 2.0 (per-vertex TEXCOORD_0). https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html
- PLY. https://paulbourke.net/dataformats/ply/
- PBRT-v4 — BVH (overlap broad/narrow phase). https://pbr-book.org/4ed/Primitives_and_Intersection_Acceleration/Bounding_Volume_Hierarchies
- Floating-point non-associativity & reproducibility. https://arxiv.org/html/2408.05148v1
