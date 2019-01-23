using System;

namespace Microsoft.Xna.Framework.GamerServices
{
    public class SignedOutEventArgs : EventArgs
    {
        private readonly SignedInGamer _gamer;

        public SignedInGamer Gamer
        {
            get { return _gamer; }
        }

        public SignedOutEventArgs (SignedInGamer gamer )
        {
            _gamer = gamer;
        }
    }
}