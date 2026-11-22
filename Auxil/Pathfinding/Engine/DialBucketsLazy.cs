using System.Runtime.CompilerServices;

namespace Auxil.Pathfinding.Engine;

/// <summary>
/// Dial's algorithm with lazy duplicates and plain per-bucket arrays. The cheapest possible
/// bucket queue: push appends to the bucket array (no linking, no decrease-key), pop takes the
/// newest entry and callers discard stale entries via their closed-set stamp. Keys must be
/// non-decreasing (true for Dijkstra and A* with a consistent heuristic).
/// </summary>
internal struct DialBucketsLazy
{
    private int[]?[] _data;
    private int[] _size;
    private int _bucketCount;
    private int _cur;
    private int _max;
    private int _count;

    public DialBucketsLazy(int initialBuckets)
    {
        if (initialBuckets < 4)
        {
            initialBuckets = 4;
        }
        _data = new int[]?[initialBuckets];
        _size = new int[initialBuckets];
        _bucketCount = initialBuckets;
        _cur = 0;
        _max = -1;
        _count = 0;
    }

    public int Count => _count;

    public void Init(Grid grid)
    {
        int bc = DialHeapIndexed.DialBucketCount(grid);
        if (_data == null || _data.Length < bc)
        {
            _data = new int[]?[bc];
            _size = new int[bc];
        }
        else
        {
            Array.Clear(_size);
        }
        _bucketCount = bc;
        _cur = 0;
        _max = -1;
        _count = 0;
    }

    public void Clear()
    {
        for (int b = _cur; b <= _max; b++)
        {
            _size[b] = 0;
        }
        _cur = 0;
        _max = -1;
        _count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(int key, int id)
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
        int[]? data = _data[b];
        if (data == null)
        {
            data = new int[8];
            _data[b] = data;
        }
        int size = _size[b];
        if (size == data.Length)
        {
            Array.Resize(ref data, size * 2);
            _data[b] = data;
        }
        data[size] = id;
        _size[b] = size + 1;
        if (b > _max)
        {
            _max = b;
        }
        _count++;
    }

    private void GrowBuckets(int key)
    {
        int newCount = Math.Max(key + 1, _bucketCount * 2);
        Array.Resize(ref _data, newCount);
        Array.Resize(ref _size, newCount);
        _bucketCount = newCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Pop(out int key)
    {
        while (_size[_cur] == 0)
        {
            _cur++;
        }
        int size = --_size[_cur];
        int[] bucket = _data[_cur]
            ?? throw new InvalidOperationException("Popped from an empty bucket.");
        int id = bucket[size];
        _count--;
        key = _cur;
        return id;
    }
}
