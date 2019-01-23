using System;

namespace Microsoft.Xna.Framework.Net
{
	internal class CommandGamerLeft : ICommand
	{
	    private readonly MonoGame.Switch.StationId _stationId;

        public CommandGamerLeft(MonoGame.Switch.StationId stationId)
		{
            _stationId = stationId;			
		}		
		
		public MonoGame.Switch.StationId StationId
        {
            get { return _stationId; }
		}
		
		public CommandEventType Command {
			get { return CommandEventType.GamerLeft; }
		}

	    public void Dispose()
	    {
	        
	    }
	}
}

