using System;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework.Net;

namespace Microsoft.Xna.Framework.GamerServices
{
    public sealed class LeaderboardEntry
    {        
        public long Rating { get; set; }

        public MemoryStream GameInfo { get; set; }

        public int Ranking { get; internal set; }

        public Gamer Gamer { get; internal set; }

        public override string ToString()
        {
            return string.Format("{{ Gamer={0}, Ranking={1}, Rating={2}, GameInfo={3} }}",
                Gamer.NullOrGamertag(),
                Ranking,
                Rating,
                GameInfo == null ? "[null]" : "[notnull]");
        }
    }
}

