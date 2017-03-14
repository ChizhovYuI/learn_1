using System;
using System.Collections.Generic;
using System.Linq;

namespace Kontur.GameStats.Server.Utils
{
    public class ReportCache<T>
    {
        public object Locker = new object();

        public ReportCache(int cacheTime)
        {
            this.cacheTime = cacheTime;
        }

        public bool TryGetItems(int count, ref List<T> result)
        {
            if (!((DateTime.Now - lastUpdateDateTime).TotalSeconds < cacheTime))
                return false;

            result = list.Take(count).ToList();
            return true;
        }

        public void Update(List<T> newResult)
        {
            list = newResult;
            lastUpdateDateTime = DateTime.Now;
        }

        private List<T> list;

        private DateTime lastUpdateDateTime;

        private readonly int cacheTime;
    }
}
