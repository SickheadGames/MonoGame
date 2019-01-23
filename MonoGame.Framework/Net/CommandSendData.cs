using System;

namespace Microsoft.Xna.Framework.Net
{
	internal class CommandSendData : ICommand
	{		
		internal readonly byte[] _data;
        internal readonly SendDataOptions _options;
        internal readonly int _offset;
        internal readonly int _length;
        internal readonly NetworkGamer _recipient;
        internal readonly LocalNetworkGamer _sender;

        public CommandEventType Command
        {
            get { return CommandEventType.SendData; }
        }

	    public CommandSendData(byte[] data, int offset, int length, SendDataOptions options, NetworkGamer recipient, LocalNetworkGamer sender)
	    {
	        _data = NetworkSession.GetBuffer(length);
            Array.Copy(data, offset, _data, 0, length);
			
		    _offset = 0;
		    _length = length;
            _options = options;
            _recipient = recipient;
            _sender = sender;				
		}

	    public void Dispose()
	    {
	    }
	}
}

