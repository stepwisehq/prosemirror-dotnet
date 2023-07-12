//
// Generic OrderedDictionary implementation updated for C# 11.
//
// Original code from @mattmc3: https://stackoverflow.com/a/9844528/33244
//
// http://unlicense.org
//

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;


namespace StepWise.Prose.Collections;

public interface IOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IOrderedDictionary
{
    new TValue this[int index] { get; set; }
    new TValue this[TKey key] { get; set; }
    new int Count { get; }
    new ICollection<TKey> Keys { get; }
    new ICollection<TValue> Values { get; }
    new void Add(TKey key, TValue value);
    new void Clear();
    void Insert(int index, TKey key, TValue value);
    int IndexOf(TKey key);
    bool ContainsValue(TValue value);
    bool ContainsValue(TValue value, IEqualityComparer<TValue> comparer);
    new bool ContainsKey(TKey key);
    new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();
    new bool Remove(TKey key);
    new void RemoveAt(int index);
    new bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
    TValue GetValue(TKey key);
    void SetValue(TKey key, TValue value);
    KeyValuePair<TKey, TValue> GetItem(int index);
    void SetItem(int index, TValue value);
}

/// <summary>
/// A dictionary object that allows rapid hash lookups using keys, but also
/// maintains the key insertion order so that values can be retrieved by
/// key index.
/// </summary>
public class OrderedDictionary<TKey, TValue> : IOrderedDictionary<TKey, TValue> where TKey : notnull
{
    #region Fields/Properties
    private readonly KeyedCollection2<TKey, KeyValuePair<TKey, TValue>> _keyedCollection;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key associated with the value to get or set.</param>
    public TValue this[TKey key]
    {
        get => GetValue(key);
        set => SetValue(key, value);
    }

    /// <summary>
    /// Gets or sets the value at the specified index.
    /// </summary>
    /// <param name="index">The index of the value to get or set.</param>
    public TValue this[int index]
    {
        get => GetItem(index).Value;
        set => SetItem(index, value);
    }

    public int Count => _keyedCollection.Count;
    public ICollection<TKey> Keys => _keyedCollection.Select(x => x.Key).ToList();
    public ICollection<TValue> Values => _keyedCollection.Select(x => x.Value).ToList();
    public IEqualityComparer<TKey>? Comparer { get; private set; }
    #endregion

    #region Constructors
    public OrderedDictionary()
    {
        _keyedCollection = new(x => x.Key);
    }

    public OrderedDictionary(IEqualityComparer<TKey> comparer)
    {
        _keyedCollection = new(x => x.Key, comparer);
        Comparer = comparer;
    }

    public OrderedDictionary(IOrderedDictionary<TKey, TValue> dictionary)
        : this()
    {
        foreach (var pair in dictionary)
        {
            _keyedCollection.Add(pair);
        }
    }

    public OrderedDictionary(IOrderedDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        : this(comparer)
    {
        foreach (var pair in dictionary)
        {
            _keyedCollection.Add(pair);
        }
    }
    #endregion

    #region Methods
    private void AssertIndexIsInRange(int index)
    {
        if (index < 0 || index >= _keyedCollection.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void Add(TKey key, TValue value) => _keyedCollection.Add(new KeyValuePair<TKey, TValue>(key, value));
    public void Clear() => _keyedCollection.Clear();
    public void Insert(int index, TKey key, TValue value) => _keyedCollection.Insert(index, new KeyValuePair<TKey, TValue>(key, value));
    public int IndexOf(TKey key) => _keyedCollection.Contains(key) ? _keyedCollection.IndexOf(_keyedCollection[key]) : -1;
    public bool ContainsValue(TValue value) => Values.Contains(value);
    public bool ContainsValue(TValue value, IEqualityComparer<TValue> comparer) => Values.Contains(value, comparer);
    public bool ContainsKey(TKey key) => _keyedCollection.Contains(key);

    public KeyValuePair<TKey, TValue> GetItem(int index)
    {
        AssertIndexIsInRange(index);
        return _keyedCollection[index];
    }

    /// <summary>
    /// Sets the value at the index specified.
    /// </summary>
    /// <param name="index">The index of the value desired</param>
    /// <param name="value">The value to set</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the index specified does not refer to a KeyValuePair in this object
    /// </exception>
    public void SetItem(int index, TValue value)
    {
        AssertIndexIsInRange(index);
        _keyedCollection[index] = new(_keyedCollection[index].Key, value);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _keyedCollection.GetEnumerator();
    public bool Remove(TKey key) => _keyedCollection.Remove(key);

    public void RemoveAt(int index)
    {
        AssertIndexIsInRange(index);
        _keyedCollection.RemoveAt(index);
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key associated with the value to get.</param>
    public TValue GetValue(TKey key) =>
        !_keyedCollection.Contains(key)
            ? throw new KeyNotFoundException($"The given key is not present in the dictionary ({key}).")
            : _keyedCollection[key].Value;

    /// <summary>
    /// Sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key associated with the value to set.</param>
    /// <param name="value">The the value to set.</param>
    public void SetValue(TKey key, TValue value)
    {
        var kvp = new KeyValuePair<TKey, TValue>(key, value);

        var idx = IndexOf(key);
        if (idx > -1)
        {
            _keyedCollection[idx] = kvp;
        }
        else
        {
            _keyedCollection.Add(kvp);
        }
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_keyedCollection.Contains(key))
        {
            value = _keyedCollection[key].Value;
            return true;
        }

        value = default;
        return false;
    }
    #endregion

    #region Sorting
    public void SortKeys() => _keyedCollection.SortByKeys();
    public void SortKeys(IComparer<TKey> comparer) => _keyedCollection.SortByKeys(comparer);
    public void SortKeys(Comparison<TKey> comparison) => _keyedCollection.SortByKeys(comparison);
    public void SortValues() => SortValues(Comparer<TValue>.Default);
    public void SortValues(IComparer<TValue> comparer) => _keyedCollection.Sort((x, y) => comparer.Compare(x.Value, y.Value));
    public void SortValues(Comparison<TValue> comparison) => _keyedCollection.Sort((x, y) => comparison(x.Value, y.Value));
    #endregion

    #region IDictionary<TKey, TValue>
    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => Add(key, value);
    bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => ContainsKey(key);
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
    bool IDictionary<TKey, TValue>.Remove(TKey key) => Remove(key);
    bool IDictionary<TKey, TValue>.TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => TryGetValue(key, out value);
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
    TValue IDictionary<TKey, TValue>.this[TKey key]
    {
        get => this[key];
        set => this[key] = value;
    }
    #endregion

    #region ICollection<KeyValuePair<TKey, TValue>>
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => _keyedCollection.Add(item);
    void ICollection<KeyValuePair<TKey, TValue>>.Clear() => _keyedCollection.Clear();
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => _keyedCollection.Contains(item);
    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _keyedCollection.CopyTo(array, arrayIndex);
    int ICollection<KeyValuePair<TKey, TValue>>.Count => _keyedCollection.Count;
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => _keyedCollection.Remove(item);
    #endregion

    #region IEnumerable<KeyValuePair<TKey, TValue>>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    #endregion

    #region IEnumerable
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion

    #region IOrderedDictionary
    IDictionaryEnumerator IOrderedDictionary.GetEnumerator() => new DictionaryEnumerator<TKey, TValue>(this);
    void IOrderedDictionary.Insert(int index, object key, object? value) => Insert(index, (TKey)key, (TValue)value!);
    void IOrderedDictionary.RemoveAt(int index) => RemoveAt(index);

    object? IOrderedDictionary.this[int index]
    {
        get => this[index];
        set => this[index] = (TValue)value!;
    }
    #endregion

    #region IDictionary
    void IDictionary.Add(object key, object? value) => Add((TKey)key, (TValue)value!);
    void IDictionary.Clear() => Clear();
    bool IDictionary.Contains(object key) => _keyedCollection.Contains((TKey)key);
    IDictionaryEnumerator IDictionary.GetEnumerator() => new DictionaryEnumerator<TKey, TValue>(this);
    bool IDictionary.IsFixedSize => false;
    bool IDictionary.IsReadOnly => false;
    ICollection IDictionary.Keys => (ICollection)Keys;
    void IDictionary.Remove(object key) => Remove((TKey)key);
    ICollection IDictionary.Values => (ICollection)Values;
    object? IDictionary.this[object key]
    {
        get => this[(TKey)key];
        set => this[(TKey)key] = (TValue)value!;
    }
    #endregion

    #region ICollection
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_keyedCollection).CopyTo(array, index);
    int ICollection.Count => ((ICollection)_keyedCollection).Count;
    bool ICollection.IsSynchronized => ((ICollection)_keyedCollection).IsSynchronized;
    object ICollection.SyncRoot => ((ICollection)_keyedCollection).SyncRoot;
    #endregion
}

public delegate int NullableComparison<in T>(T? x, T? y);

public class KeyedCollection2<TKey, TItem> : KeyedCollection<TKey, TItem> where TKey : notnull
{
    private readonly Func<TItem, TKey> _getKeyForItemDelegate;

    public KeyedCollection2(Func<TItem, TKey> getKeyForItemDelegate)
        : base() => _getKeyForItemDelegate = getKeyForItemDelegate ?? throw new ArgumentNullException(nameof(getKeyForItemDelegate));

    public KeyedCollection2(Func<TItem, TKey> getKeyForItemDelegate, IEqualityComparer<TKey>? comparer)
        : base(comparer) => _getKeyForItemDelegate = getKeyForItemDelegate ?? throw new ArgumentNullException(nameof(getKeyForItemDelegate));

    protected override TKey GetKeyForItem(TItem item) => _getKeyForItemDelegate(item);
    public void SortByKeys() => SortByKeys(Comparer<TKey>.Default);
    public void SortByKeys(IComparer<TKey> keyComparer) => Sort(new Comparer2<TItem>((x, y) => keyComparer.Compare(GetKeyForItem(x!), GetKeyForItem(y!))));
    public void SortByKeys(Comparison<TKey> keyComparison) => Sort(new Comparer2<TItem>((x, y) => keyComparison(GetKeyForItem(x!), GetKeyForItem(y!))));
    public void Sort() => Sort(Comparer<TItem>.Default);
    public void Sort(NullableComparison<TItem> comparison) => Sort(new Comparer2<TItem>((x, y) => comparison(x, y)));
    public void Sort(IComparer<TItem> comparer) => (Items as List<TItem>)?.Sort(comparer);
}

public class Comparer2<T> : Comparer<T>
{
    private readonly NullableComparison<T> _compareFunction;

    public Comparer2(NullableComparison<T> comparison) => _compareFunction = comparison ?? throw new ArgumentNullException(nameof(comparison));

    public override int Compare(T? x, T? y) => _compareFunction(x, y);
}

public sealed class DictionaryEnumerator<TKey, TValue> : IDictionaryEnumerator, IDisposable where TKey : notnull
{
    private readonly IEnumerator<KeyValuePair<TKey, TValue>> _impl;

    public DictionaryEnumerator(IDictionary<TKey, TValue> value) => _impl = value.GetEnumerator();

    public void Reset() => _impl.Reset();
    public bool MoveNext() => _impl.MoveNext();
    public DictionaryEntry Entry => new(_impl.Current.Key, _impl.Current.Value);
    public object Key => _impl.Current.Key;
    public object? Value => _impl.Current.Value;
    public object Current => Entry;

    public void Dispose() => _impl.Dispose();
}