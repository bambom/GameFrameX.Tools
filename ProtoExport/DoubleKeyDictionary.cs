// ========================================================
// 描述：DoubleKeyDictionary.cs
// 作者：Bambomtan 
// 创建时间：2024-12-27 11:18:56 星期五 
// Email：837659628@qq.com
// 版 本：1.0
// ========================================================

public class DoubleKeyDictionary<TKey1, TKey2, TValue>
{
    private readonly Dictionary<Tuple<TKey1, TKey2>, TValue> _dictionary;

    public DoubleKeyDictionary()
    {
        _dictionary = new Dictionary<Tuple<TKey1, TKey2>, TValue>();
    }

    public void Add(TKey1 key1, TKey2 key2, TValue value)
    {
        var tuple = new Tuple<TKey1, TKey2>(key1, key2);
        if (_dictionary.ContainsKey(tuple))
        {
            throw new ArgumentException("An item with the same keys already exists.");
        }
        _dictionary.Add(tuple, value);
    }

    public bool TryGetValue(TKey1 key1, TKey2 key2, out TValue value)
    {
        return _dictionary.TryGetValue(new Tuple<TKey1, TKey2>(key1, key2), out value);
    }

    public TValue this[TKey1 key1, TKey2 key2]
    {
        get
        {
            var tuple = new Tuple<TKey1, TKey2>(key1, key2);
            if (!_dictionary.ContainsKey(tuple))
            {
                throw new KeyNotFoundException("The given keys were not present in the dictionary.");
            }
            return _dictionary[tuple];
        }
        set
        {
            var tuple = new Tuple<TKey1, TKey2>(key1, key2);
            _dictionary[tuple] = value;
        }
    }

    public bool Remove(TKey1 key1, TKey2 key2)
    {
        return _dictionary.Remove(new Tuple<TKey1, TKey2>(key1, key2));
    }

    public void Clear()
    {
        _dictionary.Clear();
    }

    public bool ContainsKeys(TKey1 key1, TKey2 key2)
    {
        return _dictionary.ContainsKey(new Tuple<TKey1, TKey2>(key1, key2));
    }

    public int Count => _dictionary.Count;

    public IEnumerable<TValue> Values => _dictionary.Values;

    public IEnumerable<Tuple<TKey1, TKey2>> Keys => _dictionary.Keys;
}