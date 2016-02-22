using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Vault.Core.Tools
{
    public class LazyStorage<TKey, TResult> 
    {
        public LazyStorage(Func<TKey, TResult> getter)
        {
            Contract.Requires(getter != null);
            _getter = getter;
        }

        public int Count => _storage.Count;

        public TResult this[TKey index] => Get(index);

        private TResult Get(TKey key)
        {
            if (!_storage.ContainsKey(key))
            {
                    var result = _getter(key);
                    if (result == null)
                        return default(TResult);

                    _storage.Add(key, result);
            }

            return _storage[key];
        }

        private readonly Func<TKey, TResult> _getter;
        private readonly Dictionary<TKey, TResult> _storage = new Dictionary<TKey, TResult>();
    }
}
