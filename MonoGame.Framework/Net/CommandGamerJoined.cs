using System;

namespace Microsoft.Xna.Framework.Net
{
	internal class CommandGamerJoined : ICommand
	{
		GamerStates _states;
        MonoGame.Switch.StationId _stationId = MonoGame.Switch.StationId.Invalid;
        byte _internalId;
        string _gamerTag = string.Empty;
		string _displayName = string.Empty;

        public CommandGamerJoined (MonoGame.Switch.StationId stationId, byte internalId, string displayName, string gamerTag, bool isHost, bool isLocal)
		{
            _stationId = stationId;
            _internalId = internalId;
            _displayName = displayName;
            _gamerTag = gamerTag;

            if (isHost)
                _states = _states | GamerStates.Host;
			if (isLocal)
                _states = _states | GamerStates.Local;
		}
		
		public string DisplayName {
			get {
				return _displayName;
			}
			set {
                _displayName = value;
			}
		}	
		
		public string GamerTag {
			get {
				return _gamerTag;
			}
			set {
                _gamerTag = value;
			}
		}

        public MonoGame.Switch.StationId StationId
        {
            get
            {
                return _stationId;
            }
            set
            {
                _stationId = value;
            }
        }

        public byte InternalId
        {
            get
            {
                return _internalId;
            }
            set
            {
                _internalId = value;
            }
        }

        public GamerStates State
		{
			get { return _states; }
			set { _states = value; }
		}
		
		public CommandEventType Command {
			get { return CommandEventType.GamerJoined; }
		}

	    public void Dispose()
	    {	        
	    }
	}
}

