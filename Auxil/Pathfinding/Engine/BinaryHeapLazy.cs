using System.Runtime.CompilerServices;

namespace Auxil.Pathfinding.Engine;

/// <summary>
/// Binary min-heap with lazy duplicates. Growable: a modest preallocation is enough, and warmup
/// grows it to the steady-state size.
/// </summary>
internal struct BinaryHeapLazy
{
    private int[] _key;
    private int[] _id;
    private int _count;

    public BinaryHeapLazy(int capacity)
    {
        if (capacity < 4)
        {
            capacity = 4;
        }
        _key = new int[capacity];
        _id = new int[capacity];
        _count = 0;
    }

    public int Count => _count;

    public void Init(Grid grid)
    {
        int cap = Math.Min(grid.Size, 1 << 18);
        if (_key == null || _key.Length < cap)
        {
            _key = new int[cap];
            _id = new int[cap];
        }
        _count = 0;
    }

    public void Clear() => _count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(int key, int id)
    {
        if (_count == _key.Length)
        {
            Grow();
        }
        int[] keys = _key,
            ids = _id;
        int i = _count++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (keys[p] <= key)
            {
                break;
            }
            keys[i] = keys[p];
            ids[i] = ids[p];
            i = p;
        }
        keys[i] = key;
        ids[i] = id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Pop()
    {
        int[] keys = _key,
            ids = _id;
        int id = ids[0];
        int n = --_count;
        if (n > 0)
        {
            int k = keys[n],
                v = ids[n];
            int i = 0;
            int half = n >> 1;
            while (i < half)
            {
                int c = 2 * i + 1;
                if (c + 1 < n && keys[c + 1] < keys[c])
                {
                    c++;
                }
                if (keys[c] >= k)
                {
                    break;
                }
                keys[i] = keys[c];
                ids[i] = ids[c];
                i = c;
            }
            keys[i] = k;
            ids[i] = v;
        }
        return id;
    }

    private void Grow()
    {
        Array.Resize(ref _key, _key.Length * 2);
        Array.Resize(ref _id, _id.Length * 2);
    }
}
