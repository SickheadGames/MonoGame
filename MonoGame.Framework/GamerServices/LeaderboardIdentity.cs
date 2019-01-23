using System;
using System.Runtime.Serialization;

namespace Microsoft.Xna.Framework.GamerServices
{    
    public struct LeaderboardIdentity
    {
        private readonly int _key;
        
        public int Key
        {
            get { return _key; }          
        }

        private LeaderboardIdentity(int key)
        {
            _key = key;
        }

        public static LeaderboardIdentity Create(int key)
        {
            return new LeaderboardIdentity(key);
        }
    }
}

