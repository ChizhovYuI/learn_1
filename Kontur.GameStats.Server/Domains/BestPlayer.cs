
using System.Collections.Specialized;
using System.Runtime.Remoting.Channels;

namespace Kontur.GameStats.Server.Domains
{
    public class BestPlayer
    {
        public BestPlayer(string name, decimal killToDeathRatio)
        {
            Name = name;
            KillToDeathRatio = killToDeathRatio;
        }

        public string Name { get; }

        public decimal KillToDeathRatio { get; }

        protected bool Equals(BestPlayer other)
        {
            return string.Equals(Name, other.Name) && KillToDeathRatio == other.KillToDeathRatio;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;

            return obj.GetType() == GetType() && Equals((BestPlayer)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name?.GetHashCode() ?? 0) * 397) ^ KillToDeathRatio.GetHashCode();
            }
        }

        public static class Properties
        {
            public const string Name = "name";

            public const string KillToDeathRatio = "killToDeathRatio";
        }
    }
}
