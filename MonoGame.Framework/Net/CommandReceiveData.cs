using System;

namespace Microsoft.Xna.Framework.Net
{
	internal class CommandReceiveData : ICommand
	{
        internal readonly MonoGame.Switch.StationId _sender;
        internal readonly MonoGame.Switch.StationId _receiver;        
		internal readonly byte[] _data;
	    internal readonly int _offset;
	    internal readonly int _length;        
		
		public CommandReceiveData(MonoGame.Switch.StationId senderInternalId, MonoGame.Switch.StationId receiverInternalId, byte[] data, int offset, int length)
		{
            _sender = senderInternalId;
            _receiver = receiverInternalId;
		    _data = data;
		    _offset = offset;
		    _length = length;
		}
		
		public CommandEventType Command {
			get { return CommandEventType.ReceiveData; }
		}

	    public void Dispose()
	    {
	    }
	}
}

