using System.Runtime.CompilerServices;

namespace Auxil.Pathfinding.Engine;

/// <summary>
/// Dial's algorithm (bucket queue keyed by integer f), indexed. Near-O(1) per operation when the
/// key range is bounded. On grids f is at most 2 * (W + H + layers) * 10 plus a constant. Keys
/// can legitimately exceed that estimate on maze-like maps whose optimal costs are winding and
/// large, so the bucket array grows (amortized during warmup, zero-cost afterwards). Monotone
/// keys make clamping an insert to the current bucket a no-op in practice.
/// </summary>
internal struct DialHeapIndexed
{
    /// <summary>Bucket to first node in its doubly-linked list.</summary>
    private int[] _head;

    /// <summary>Node to next node in its bucket.</summary>
    private int[] _next;

    /// <summary>Node to previous node in its bucket.</summary>
    private int[] _prev;

    /// <summary>Node to f value (equal to its bucket index).</summary>
    private int[] _key;

    /// <summary>Lowest non-empty bucket candidate.</summary>
    private int _cur;

    /// <summary>Highest bucket ever used in this query.</summary>
    private int _max;
    private int _count;
    private int _bucketCount;

    public DialHeapIndexed(int capacity, int bucketCount)
    {
        if (capacity < 4)
        {
            capacity = 4;
        }
        _head = new int[bucketCount];
        Array.Fill(_head, -1);
        _next = new int[capacity];
        _prev = new int[capacity];
        Array.Fill(_prev, -1);
        _key = new int[capacity];
        _bucketCount = bucketCount;
        _cur = 0;
        _max = -1;
        _count = 0;
    }

    public int Count => _count;

    public void Init(Grid grid)
    {
        int cap = grid.Size;
        if (_next == null || _next.Length < cap)
        {
            _next = new int[cap];
            _prev = new int[cap];
            _key = new int[cap];
        }
        int bc = DialBucketCount(grid);
        if (_head == null || _head.Length < bc)
        {
            _head = new int[bc];
            Array.Fill(_head, -1);
        }
        _bucketCount = bc;
        _cur = 0;
        _max = -1;
        _count = 0;
    }

    public static int DialBucketCount(Grid grid) =>
        20 * (grid.Width + grid.Height + grid.Layers) + 64;

    public void Clear()
    {
        for (int b = _cur; b <= _max; b++)
        {
            _head[b] = -1;
        }
        _cur = 0;
        _max = -1;
        _count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int id, int key)
    {
        int b = key;
        if (b < _cur)
        {
            b = _cur;
        }
        if (b >= _bucketCount)
        {
            GrowBuckets(b);
        }
        _key[id] = b;
        int h = _head[b];
        _next[id] = h;
        _prev[id] = -1;
        if (h != -1)
        {
            _prev[h] = id;
        }
        _head[b] = id;
        if (b > _max)
        {
            _max = b;
        }
        _count++;
    }

    private void GrowBuckets(int key)
    {
        int newCount = Math.Max(key + 1, _bucketCount * 2);
        int oldCount = _bucketCount;
        Array.Resize(ref _head, newCount);
        Array.Fill(_head, -1, oldCount, newCount - oldCount);
        _bucketCount = newCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Decrease(int id, int key)
    {
        int b = key;
        if (b < _cur)
        {
            b = _cur;
        }
        if (b >= _bucketCount)
        {
            GrowBuckets(b);
        }
        int old = _key[id];
        if (old == b)
        {
            _key[id] = b;
            return;
        }

        int p = _prev[id],
            n = _next[id];
        if (p != -1)
        {
            _next[p] = n;
        }
        else
        {
            _head[old] = n;
        }
        if (n != -1)
        {
            _prev[n] = p;
        }

        _key[id] = b;
        int h = _head[b];
        _next[id] = h;
        _prev[id] = -1;
        if (h != -1)
        {
            _prev[h] = id;
        }
        _head[b] = id;
        if (b > _max)
        {
            _max = b;
        }
    }

    public int Pop(out int key)
    {
        while (_cur <= _max && _head[_cur] == -1)
        {
            _cur++;
        }
        int id = _head[_cur];
        int n = _next[id];
        _head[_cur] = n;
        if (n != -1)
        {
            _prev[n] = -1;
        }
        _count--;
        key = _cur;
        return id;
    }
}
