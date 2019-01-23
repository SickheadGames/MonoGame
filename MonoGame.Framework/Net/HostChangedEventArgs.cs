using System;

namespace Microsoft.Xna.Framework.Net
{
    public class HostChangedEventArgs : EventArgs
    {
        private readonly NetworkGamer _newHost;
        private readonly NetworkGamer _oldHost;

        public HostChangedEventArgs (NetworkGamer newHost, NetworkGamer oldHost)
        {
            _newHost = newHost;
            _oldHost = oldHost;
        }

        public NetworkGamer NewHost { 
            get {
                return _newHost;
            }
        }

        public NetworkGamer OldHost { 
            get {
                return _oldHost;
            }
        }
    }
}