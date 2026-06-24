using System.Collections;
using System.Collections.Generic;

namespace Fuse;

public sealed class OrderedUniqueCollection<T> : IEnumerable<T>
{
    private readonly List<T> _items = [];
    private readonly HashSet<T> _set;

    public OrderedUniqueCollection()
        : this(null)
    {
    }

    public OrderedUniqueCollection(IEqualityComparer<T> comparer)
    {
        _set = new HashSet<T>(comparer);
    }

    public int Count => _items.Count;

    public bool Add(T item)
    {
        if (!_set.Add(item))
            return false;

        _items.Add(item);
        return true;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
