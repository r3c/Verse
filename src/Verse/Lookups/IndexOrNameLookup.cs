using System.Globalization;
using Verse.LookupNodes;

namespace Verse.Lookups;

internal class IndexOrNameLookup<TValue> : ILookup<int, TValue>
{
    public ILookupNode<int, TValue> Root => fastRoot;

    private readonly FastLookupNode<int, TValue> fastRoot;
    private readonly HashLookupNode<int, TValue> indexRoot;
    private readonly HashLookupNode<int, TValue> nameRoot;

    public IndexOrNameLookup()
    {
        var index = new HashLookupNode<int, TValue>(k => k);
        var name = new HashLookupNode<int, TValue>(k => k);

        fastRoot = new FastLookupNode<int, TValue>(index, name);
        indexRoot = index;
        nameRoot = name;
    }

    public bool ConnectTo(string sequence, TValue value)
    {
        if (int.TryParse(sequence, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            var indexNode = indexRoot.ConnectTo(index);

            if (indexNode.HasValue)
                return false;

            indexNode.HasValue = true;
            indexNode.Value = value;
        }

        var current = nameRoot;

        foreach (var key in sequence)
            current = current.ConnectTo(key);

        if (current.HasValue)
            return false;

        current.HasValue = true;
        current.Value = value;

        return true;
    }
}