using System;

namespace Microsoft.Xna.Framework.Net
{
    public class NetworkSessionEndedEventArgs : EventArgs
    {
        NetworkSessionEndReason endReason;

        public NetworkSessionEndedEventArgs (NetworkSessionEndReason aEndReason)
        {
            endReason = aEndReason;
        }

        public NetworkSessionEndReason EndReason { 
            get {
                return endReason;
            }
        }

    }
}