using System;
using System.Runtime.Serialization;

namespace Microsoft.Xna.Framework.GamerServices
{
    [DataContract]
    public struct LeaderboardIdentity
    {
        [DataMember]
        public int GameMode { get; set; }

        [DataMember]
        public int Key { get; set; }

        public static LeaderboardIdentity Create(int aKey)
        {
            return new LeaderboardIdentity() { Key = aKey};
        }

        public static LeaderboardIdentity Create(int aKey, int aGameMode)
        {
            return new LeaderboardIdentity() { Key = aKey, GameMode = aGameMode};
        }
    }
}

