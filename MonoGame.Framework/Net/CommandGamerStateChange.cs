using System;

namespace Microsoft.Xna.Framework.Net
{
    internal class CommandSendGamerState : ICommand
    {
        private readonly LocalNetworkGamer _gamer;
		private readonly GamerStates _newState;
        private readonly GamerStates _prevState;

        public CommandSendGamerState(LocalNetworkGamer gamer, GamerStates newState, GamerStates prevState)
		{
			_gamer = gamer;
            _newState = newState;
            _prevState = prevState;
		}
		
		public LocalNetworkGamer Gamer 
		{
			get { return _gamer; }
		}
		public GamerStates NewState
		{
			get { return _newState; }
		}
		
		public GamerStates PrevState
		{
			get { return _prevState; }
		}
		
		public CommandEventType Command {
			get { return CommandEventType.SendGamerState; }
		}

	    public void Dispose()
	    {
	        
	    }
    }

    internal class CommandReceiveGamerState : ICommand
    {
        private readonly NetworkGamer _gamer;
        private readonly GamerStates _newState;
        private readonly GamerStates _prevState;

        public CommandReceiveGamerState(NetworkGamer gamer, GamerStates newState, GamerStates prevState)
        {
            _gamer = gamer;
            _newState = newState;
            _prevState = prevState;
        }

        public NetworkGamer Gamer
        {
            get { return _gamer; }
        }
        public GamerStates NewState
        {
            get { return _newState; }
        }

        public GamerStates PrevState
        {
            get { return _prevState; }
        }

        public CommandEventType Command
        {
            get { return CommandEventType.ReceiveGamerState; }
        }

        public void Dispose()
        {

        }
    }
}

