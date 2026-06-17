using System.Xml.Linq;
using UVBake.Core;
using UVBake.IO;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>P7 QC artifact: the UV-layout SVG is well-formed and renders every island.</summary>
public class UvLayoutSvgTests
{
    [Fact]
    public void Svg_IsWellFormedWithOnePolygonPerTriangle()
    {
        UvMesh atlas = UVBaker.Generate(Primitives.Cube(), new UVBakeOptions());

        XDocument doc = XDocument.Parse(UvLayoutSvg.ToSvg(atlas.Mesh));
        XNamespace svg = "http://www.w3.org/2000/svg";

        Assert.Equal(svg + "svg", doc.Root!.Name);
        Assert.NotNull(doc.Root.Attribute("viewBox"));

        int polygons = doc.Descendants(svg + "polygon").Count();
        Assert.Equal(atlas.Mesh.Faces.Count, polygons);

        // The cube has 6 charts → 6 distinct island fill colors.
        int distinctFills = doc.Descendants(svg + "polygon")
            .Select(p => p.Attribute("fill")!.Value)
            .Distinct()
            .Count();
        Assert.Equal(6, distinctFills);
    }
}
