
using System;

namespace Microsoft.Xna.Framework.GamerServices
{
    /// <summary>
    /// Represents the arguments passed to an InviteAccepted event.
    /// </summary>
    public class InviteAcceptedEventArgs : EventArgs
    {
        private readonly bool _isCurrentSession;
        private readonly SignedInGamer _gamer;
        private readonly string _sessionId;

        public bool IsCurrentSession
        {
            get { return _isCurrentSession; }
        }

        public SignedInGamer Gamer
        {
            get { return _gamer; }
        }

        public string SessionId
        {
            get { return _sessionId; }
        }
        
        public InviteAcceptedEventArgs(SignedInGamer gamer, bool isCurrentSession)
        {
            _gamer = gamer;
            _isCurrentSession = isCurrentSession;
        }

        public InviteAcceptedEventArgs(SignedInGamer gamer, bool isCurrentSession, string sessionId)
            : this(gamer, isCurrentSession)
        {
            _sessionId = sessionId;
        }
    }
}
