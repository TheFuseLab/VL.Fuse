namespace VL.ShaderFXtension
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;

    /// <summary>
    /// A dictionary that remembers the order that keys were first inserted. If a new entry overwrites an existing entry, the original insertion position is left unchanged. Deleting an entry and reinserting it will move it to the end.
    /// </summary>
    /// <typeparam name="TKey">The type of keys</typeparam>
    /// <typeparam name="TValue">The type of values</typeparam>
    public interface IOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// The value of the element at the given index.
        /// </summary>
        TValue this[int index] { get; set; }

        /// <summary>
        /// Find the position of an element by key. Returns -1 if the dictionary does not contain an element with the given key.
        /// </summary>
        int IndexOf(TKey key);

        /// <summary>
        /// Insert an element at the given index.
        /// </summary>
        void Insert(int index, TKey key, TValue value);

        /// <summary>
        /// Remove the element at the given index.
        /// </summary>
        void RemoveAt(int index);
    }

    /// <summary>
    /// A dictionary that remembers the order that keys were first inserted. If a new entry overwrites an existing entry, the original insertion position is left unchanged. Deleting an entry and reinserting it will move it to the end.
    /// </summary>
    /// <typeparam name="TKey">The type of keys. Musn't be <see cref="int"/></typeparam>
    /// <typeparam name="TValue">The type of values.</typeparam>
    public sealed class OrderedDictionary<TKey, TValue> : IOrderedDictionary<TKey, TValue>
    {
        /// <summary>
        /// An unordered dictionary of key pairs.
        /// </summary>
        private readonly Dictionary<TKey, TValue> _fDictionary;

        /// <summary>
        /// The keys of the dictionary in the exposed order.
        /// </summary>
        private readonly List<TKey> _fKeys;

        /// <summary>
        /// A dictionary that remembers the order that keys were first inserted. If a new entry overwrites an existing entry, the original insertion position is left unchanged. Deleting an entry and reinserting it will move it to the end.
        /// </summary>
        public OrderedDictionary()
        {
            if (typeof(TKey) == typeof(int))
                throw new NotSupportedException(
                    "Integer-like type is not appropriate for keys in an ordered dictionary - accessing values by key or index would be confusing.");

            _fDictionary = new Dictionary<TKey, TValue>();
            _fKeys = new List<TKey>();
        }

        /// <summary>
        /// The number of elements in the dictionary.
        /// </summary>
        public int Count => _fDictionary.Count;

        /// <summary>
        /// This dictionary is not read only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// The keys of the dictionary, in order.
        /// </summary>
        public ICollection<TKey> Keys => _fKeys.AsReadOnly();

        /// <summary>
        /// The values in the dictionary, in order.
        /// </summary>
        public ICollection<TValue> Values
        {
            get { return _fKeys.Select(key => _fDictionary[key]).ToArray(); }
        }

        /// <summary>
        /// The value at the given index.
        /// </summary>
        public TValue this[int index]
        {
            get
            {
                var key = _fKeys[index];
                return _fDictionary[key];
            }
            set
            {
                var key = _fKeys[index];
                _fDictionary[key] = value;
            }
        }

        /// <summary>
        /// The value under the given key. New entries are added at the end of the order. Updating an existing entry does not change its position.     
        /// </summary>
        public TValue this[TKey key]
        {
            get => _fDictionary[key];
            set
            {
                if (!_fDictionary.ContainsKey(key))
                {
                    // New entries are added at the end of the order.
                    _fKeys.Add(key);
                }

                _fDictionary[key] = value;
            }
        }

        /// <summary>
        ///  Find the position of an element by key. Returns -1 if the dictionary does not contain an element with the given key.
        /// </summary>
        public int IndexOf(TKey key)
        {
            return _fKeys.IndexOf(key);
        }

        /// <summary>
        /// Remove the element at the given index.
        /// </summary>
        public void RemoveAt(int index)
        {
            var key = _fKeys[index];
            _fDictionary.Remove(key);
            _fKeys.RemoveAt(index);
        }

        /// <summary>
        /// Test whether there is an element with the given key.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return _fDictionary.ContainsKey(key);
        }

        /// <summary>
        /// Try to get a value from the dictionary, by key. Returns false if there is no element with the given key.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _fDictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Insert an element at the given index.
        /// </summary>
        public void Insert(int index, TKey key, TValue value)
        {
            // Dictionary operation first, so exception thrown if key already exists.
            _fDictionary.Add(key, value);
            _fKeys.Insert(index, key);
        }

        /// <summary>
        /// Add an element to the dictionary.
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            // Dictionary operation first, so exception thrown if key already exists.
            _fDictionary.Add(key, value);
            _fKeys.Add(key);
        }

        /// <summary>
        /// Add an element to the dictionary.
        /// </summary>
        public void Add(KeyValuePair<TKey, TValue> pair)
        {
            Add(pair.Key, pair.Value);
        }

        /// <summary>
        /// Test whether the dictionary contains an element equal to that given.
        /// </summary>
        public bool Contains(KeyValuePair<TKey, TValue> pair)
        {
            return _fDictionary.Contains(pair);
        }

        /// <summary>
        /// Remove a key-value pair from the dictionary. Return true if pair was successfully removed. Otherwise, if the pair was not found, return false.
        /// </summary>
        public bool Remove(KeyValuePair<TKey, TValue> pair)
        {
            if (!Contains(pair)) return false;
            Remove(pair.Key);
            return true;

        }

        /// <summary>
        /// Remove the element with the given key key. If there is no element with the key, no exception is thrown.
        /// </summary>
        public bool Remove(TKey key)
        {
            var wasInDictionary = _fDictionary.Remove(key);
            var wasInKeys = _fKeys.Remove(key);
            Contract.Assume(wasInDictionary == wasInKeys);
            return wasInDictionary;
        }

        /// <summary>
        /// Delete all elements from the dictionary.
        /// </summary>
        public void Clear()
        {
            _fDictionary.Clear();
            _fKeys.Clear();
        }

        /// <summary>
        /// Copy the elements of the dictionary to an array, starting at at the given index.
        /// </summary>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Must be greater than or equal to zero.");

            if (index + _fDictionary.Count > array.Length)
                throw new ArgumentException("array", "Array is too small");

            foreach (var pair in this)
            {
                array[index] = pair;
                index++;
            }
        }

        private IEnumerable<KeyValuePair<TKey, TValue>> Enumerate()
        {
            return from key in _fKeys let value = _fDictionary[key] select new KeyValuePair<TKey, TValue>(key, value);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return Enumerate().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Enumerate().GetEnumerator();
        }

        /// <summary>
        /// Conditions that should be true at the end of every public method.
        /// </summary>
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_fDictionary.Count == _fKeys.Count,
                "Unordered dictionary and ordered key list should be the same length.");
        }
    }
}