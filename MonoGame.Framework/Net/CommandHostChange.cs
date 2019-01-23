using System;

namespace Microsoft.Xna.Framework.Net
{
	internal class CommandHostChange : ICommand
	{
	    private readonly MonoGame.Switch.StationId _newHostOnlineId;
	    private readonly MonoGame.Switch.StationId _oldHostOnlineId;

        public CommandHostChange(MonoGame.Switch.StationId newHostOnlineId, MonoGame.Switch.StationId oldHostOnlineId)
		{
            _newHostOnlineId = newHostOnlineId;
            _oldHostOnlineId = oldHostOnlineId;
		}

        public MonoGame.Switch.StationId NewHost
		{
            get { return _newHostOnlineId; }
		}

        public MonoGame.Switch.StationId OldHost
		{
            get { return _oldHostOnlineId; }
		}
		
		public CommandEventType Command {
			get { return CommandEventType.HostChange; }
		}

	    public void Dispose()
	    {
	        
	    }
	}
}

