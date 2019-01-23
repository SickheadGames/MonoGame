using System;

namespace Microsoft.Xna.Framework.Net
{
    public class GamerJoinedEventArgs : EventArgs
    {
        private NetworkGamer gamer;

        public GamerJoinedEventArgs (NetworkGamer aGamer)
        {
            gamer = aGamer;
        }

        public NetworkGamer Gamer { 
            get {
                return gamer;
            }
        }
    }
}