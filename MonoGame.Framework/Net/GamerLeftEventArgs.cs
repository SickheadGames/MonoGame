using System;

namespace Microsoft.Xna.Framework.Net
{
    public class GamerLeftEventArgs : EventArgs
    {
        private NetworkGamer gamer;

        public GamerLeftEventArgs (NetworkGamer aGamer)
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