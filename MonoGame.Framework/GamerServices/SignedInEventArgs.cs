using System;

namespace Microsoft.Xna.Framework.GamerServices
{
    public class SignedInEventArgs : EventArgs
    {
        private readonly SignedInGamer _gamer;

        public SignedInGamer Gamer
        {
            get { return _gamer; }
        }

        public SignedInEventArgs ( SignedInGamer gamer )
        {
            _gamer = gamer;
        }
    }
}