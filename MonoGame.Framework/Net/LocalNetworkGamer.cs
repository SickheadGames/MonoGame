#region License
// /*
// Microsoft Public License (Ms-PL)
// MonoGame - Copyright © 2009 The MonoGame Team
// 
// All rights reserved.
// 
// This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
// accept the license, do not use the software.
// 
// 1. Definitions
// The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
// U.S. copyright law.
// 
// A "contribution" is the original software, or any additions or changes to the software.
// A "contributor" is any person that distributes its contribution under this license.
// "Licensed patents" are a contributor's patent claims that read directly on its contribution.
// 
// 2. Grant of Rights
// (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
// (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
// 
// 3. Conditions and Limitations
// (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
// (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
// your patent license from such contributor to the software ends automatically.
// (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
// notices that are present in the software.
// (D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
// a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
// code form, you may only do so under a license that complies with this license.
// (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
// or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
// permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
// purpose and non-infringement.
// */
#endregion License

#region Using clause
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework.GamerServices;

#endregion Using clause

namespace Microsoft.Xna.Framework.Net
{
	public sealed partial class LocalNetworkGamer : NetworkGamer
	{
	    private readonly SignedInGamer _signedInGamer;

		internal readonly Queue<CommandReceiveData> _receivedData;

        public bool IsDataAvailable
        {
            get
            {
                lock (_receivedData)
                {
                    return _receivedData.Count > 0;
                }
            }
        }

        public SignedInGamer SignedInGamer
        {
            get
            {
                return _signedInGamer;
            }
        }

        internal LocalNetworkGamer(NetworkSession session, SignedInGamer signedInGamer, NetworkSessionParticipantId internalId, GamerStates state)
            : base(session, internalId, state)
        {
            _signedInGamer = signedInGamer;
            _receivedData = new Queue<CommandReceiveData>();
        }

        /*
		public void EnableSendVoice (
			NetworkGamer remoteGamer, 
			bool enable)
		{
			throw new NotImplementedException ();
		}
        */

		public int ReceiveData (
			byte[] data, 
			int offset,
			out NetworkGamer sender)
		{
			if (data == null)
				throw new ArgumentNullException("data");            
			
			if (_receivedData.Count <= 0) {
				sender = null;
				return 0;
			}
			
			lock (_receivedData) 
            {								
                var cmd = _receivedData.Peek();

                if ((offset + data.Length) > cmd._length)
					throw new ArgumentException("The specified array is too small to receive the incoming network packet.");
								
				_receivedData.Dequeue();

                Array.Copy(cmd._data, cmd._offset, data, offset, cmd._length);

                sender = _session.AllGamers.GetByStationId(cmd._sender);
                if (sender == null)
                    throw new Exception(string.Format("LocalNetworkGamer.ReceiveData(); found no gamer with id of specified sender '{0}'", cmd._sender));              

                return cmd._length;
			}
			
		}

		public int ReceiveData (
			byte[] data,
			out NetworkGamer sender)
		{
			return ReceiveData(data, 0, out sender);
		}

	    public int ReceiveData(
	        PacketReader reader,
	        out NetworkGamer sender)
	    {
	        lock (_receivedData)
	        {
	            if (_receivedData.Count <= 0)
	            {
	                sender = null;
	                return 0;
	            }

	            // JCF: Isn't this going to create huge allocation churn every frame?
	            reader.Reset(0);

	            var cmd = _receivedData.Dequeue();

                reader.Reset(cmd._data.Length);
	            
                //if (data.Length < cmd._length)
	                //data.Reset(cmd._data.Length);

                Array.Copy(cmd._data, cmd._offset, reader.Data, 0, cmd._length);

                sender = _session.AllGamers.GetByStationId(cmd._sender);
                if (sender == null)
                    throw new Exception(string.Format("LocalNetworkGamer.ReceiveData(); found no gamer with id of specified sender '{0}'", cmd._sender));              

	            return cmd._length;
	        }
	    }

	    public void SendData (
			byte[] data,
			int offset,
			int count,
			SendDataOptions options)
		{
		    Session.SendData(data, offset, count, options, null, this);
		}

		public void SendData (
			byte[] data,
			int offset,
			int count,
			SendDataOptions options,
			NetworkGamer recipient)
		{
            Session.SendData(data, offset, count, options, recipient, this);
		}

		public void SendData (
			byte[] data,
			SendDataOptions options)
		{
            SendData(data, 0, data.Length, SendDataOptions.None);
		}

		public void SendData (
			byte[] data,
			SendDataOptions options,
			NetworkGamer recipient)
		{
            SendData(data, 0, data.Length, options, recipient);
		}

		public void SendData (
            PacketWriter writer,
            SendDataOptions options)
		{
		    SendData(writer, options, null);
		}

		public void SendData (
            PacketWriter writer,
			SendDataOptions options,
			NetworkGamer recipient)
		{
            writer.Flush();
            SendData(writer.Data, 0, writer.Position, options, recipient);
            writer.Reset();
		}		
	}
}
