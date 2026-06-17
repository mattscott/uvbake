namespace UVBake.Core.Packing;

/// <summary>
/// A skyline (bottom-left) rectangle bin: tracks the top profile of placed boxes and places each new box
/// at the lowest, then leftmost, position that fits (Jylänki's Skyline-BL, Documents/05 §1.1). Fast and
/// deterministic, it is the P4 packer; silhouette/Tetris packing for tighter fills is a P5 refinement.
/// </summary>
internal sealed class Skyline
{
    private readonly int _width;
    private readonly int _height;
    private readonly List<Node> _nodes;

    public Skyline(int width, int height)
    {
        _width = width;
        _height = height;
        _nodes = [new Node(0, 0, width)];
    }

    /// <summary>Places a <paramref name="boxWidth"/>×<paramref name="boxHeight"/> box; returns its lower-left corner, or null if it does not fit.</summary>
    public (int X, int Y)? Place(int boxWidth, int boxHeight)
    {
        int bestX = -1, bestY = int.MaxValue, bestNode = -1;
        for (int i = 0; i < _nodes.Count; i++)
        {
            int x = _nodes[i].X;
            if (x + boxWidth > _width)
            {
                continue;
            }

            int y = SupportHeight(i, boxWidth);
            if (y < 0 || y + boxHeight > _height)
            {
                continue;
            }

            if (y < bestY || (y == bestY && x < bestX))
            {
                bestY = y;
                bestX = x;
                bestNode = i;
            }
        }

        if (bestNode < 0)
        {
            return null;
        }

        AddNode(bestX, bestY + boxHeight, boxWidth);
        return (bestX, bestY);
    }

    /// <summary>The skyline height a box of <paramref name="boxWidth"/> would rest at if its left edge is node <paramref name="start"/>; -1 if it runs off the edge.</summary>
    private int SupportHeight(int start, int boxWidth)
    {
        int x = _nodes[start].X;
        int remaining = boxWidth;
        int y = 0;
        for (int i = start; i < _nodes.Count && remaining > 0; i++)
        {
            y = Math.Max(y, _nodes[i].Y);
            remaining -= _nodes[i].Width;
        }

        return remaining > 0 ? -1 : y;
    }

    private void AddNode(int x, int y, int width)
    {
        // Insert the new top segment, then clip/remove the segments it covers and merge equal-height runs.
        var inserted = new Node(x, y, width);

        int index = 0;
        while (index < _nodes.Count && _nodes[index].X < x)
        {
            index++;
        }

        _nodes.Insert(index, inserted);

        // Clip following segments that the new node overlaps.
        for (int i = index + 1; i < _nodes.Count;)
        {
            Node node = _nodes[i];
            if (node.X < inserted.X + inserted.Width)
            {
                int shrink = (inserted.X + inserted.Width) - node.X;
                if (shrink >= node.Width)
                {
                    _nodes.RemoveAt(i);
                    continue;
                }

                _nodes[i] = new Node(node.X + shrink, node.Y, node.Width - shrink);
            }

            break;
        }

        MergeAdjacent();
    }

    private void MergeAdjacent()
    {
        for (int i = 0; i < _nodes.Count - 1;)
        {
            if (_nodes[i].Y == _nodes[i + 1].Y)
            {
                _nodes[i] = new Node(_nodes[i].X, _nodes[i].Y, _nodes[i].Width + _nodes[i + 1].Width);
                _nodes.RemoveAt(i + 1);
            }
            else
            {
                i++;
            }
        }
    }

    private readonly record struct Node(int X, int Y, int Width);
}
