using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kontur.GameStats.Server.Inerfaces;

namespace Kontur.GameStats.Server.Utils
{
    public class StatsCache<T> where T : ICacheable
    {
        public StatsCache(int cacheTime)
        {
            this.cacheTime = cacheTime;
        }

        private readonly object locker = new object();

        public bool TryGetItem(string key, ref T result)
        {
            Monitor.Enter(locker);
            try
            {
                var removeItemsKey =
                    items.TakeWhile(i => (DateTime.Now - i.Value.LastUpdateDateTime).TotalSeconds >= cacheTime)
                         .Select(i => i.Key).ToList();
                removeItemsKey.ForEach(i => items.Remove(i));
                CacheItem cacheItem;
                if (!items.TryGetValue(key, out cacheItem))
                    return false;

                result = cacheItem.Entity;
                return true;
            }
            finally
            {
                Monitor.Exit(locker);
            }
        }

        public void Insert(T entity)
        {
            Monitor.Enter(locker);
            try
            {
                var cacheItem = new CacheItem(entity, DateTime.Now);
                if (items.ContainsKey(entity.Key))
                    items.Add(entity.Key, cacheItem);
                else
                    items[entity.Key] = cacheItem;
            }
            finally
            {
                Monitor.Exit(locker);
            }
        }

        private readonly SortedDictionary<string, CacheItem> items = new SortedDictionary<string, CacheItem>();

        private readonly int cacheTime;

        private class CacheItem : IComparable
        {
            public CacheItem(T entity, DateTime lastUpdateDateTime)
            {
                Entity = entity;
                LastUpdateDateTime = lastUpdateDateTime;
            }

            public T Entity { get; }

            public DateTime LastUpdateDateTime { get; }

            public int CompareTo(object obj)
            {
                return LastUpdateDateTime.CompareTo((obj as CacheItem)?.LastUpdateDateTime);
            }
        }
    }
}
