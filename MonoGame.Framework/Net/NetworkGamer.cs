// #region License
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
// #endregion License
// 

#region Using clause
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework.GamerServices;
#endregion Using clause

namespace Microsoft.Xna.Framework.Net
{
    public struct NetworkSessionParticipantId
    {
        public static readonly NetworkSessionParticipantId Invalid = new NetworkSessionParticipantId(0);

        internal readonly UInt64 _value;

        public NetworkSessionParticipantId(UInt64 id)
        {
            _value = id;
        }

        public static bool operator <(NetworkSessionParticipantId a, NetworkSessionParticipantId b)
        {
            return a._value < b._value;
        }

        public static bool operator >(NetworkSessionParticipantId a, NetworkSessionParticipantId b)
        {
            return a._value > b._value;
        }

        public static bool operator ==(NetworkSessionParticipantId a, NetworkSessionParticipantId b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(NetworkSessionParticipantId a, NetworkSessionParticipantId b)
        {
            return !Equals(a, b);
        }

        public static bool Equals(NetworkSessionParticipantId a, NetworkSessionParticipantId b)
        {
            return a._value == b._value;
        }
    }

    public static class NetworkSessionParticipantIdExtensions
    {
        public static void Write(this BinaryWriter io, NetworkSessionParticipantId pid)
        {
            io.Write(pid._value);
        }

        public static NetworkSessionParticipantId ReadNetId(this BinaryReader io)
        {
            var id = io.ReadUInt64();
            return new NetworkSessionParticipantId(id);
        }
    }

    public class NetworkGamer : Gamer
	{
        protected readonly NetworkSessionParticipantId _internalId;
	    protected readonly NetworkSession _session; 
		internal GamerStates _gamerState;
        private GamerStates _prevGamerState;
		
		internal NetworkGamer(NetworkSession session, NetworkSessionParticipantId interalId, GamerStates state)
		{
            _internalId = interalId;

			_session = session;

			_gamerState = state;

            // A new networkgamer should transmit its state... or does it already in the join message?
			_prevGamerState = state;
		}
		
		public bool HasLeftSession 
		{ 
			get
			{
				return false;
			}
		}
		
		public bool HasVoice 
		{ 
			get
			{
				return (_gamerState & GamerStates.HasVoice) != 0;
			}
		}
		
		public bool IsGuest 
		{ 
			get
			{
				return (_gamerState & GamerStates.Guest) != 0;
			}
		}
		
		public bool IsHost 
		{ 
			get
			{
				return (_gamerState & GamerStates.Host) != 0;
			}
		}
		
		public bool IsLocal 
		{ 
			get
			{
				return (_gamerState & GamerStates.Local) != 0;
			}
		}
		
		public bool IsMutedByLocalUser 
		{ 
			get
			{
				return true;
			}
		}
		
		public bool IsPrivateSlot 
		{ 
			get
			{
				return false;
			}
		}
		
		public bool IsReady 
		{ 
			get
			{
				return (_gamerState & GamerStates.Ready) != 0;
			}
		    set
		    {
		        if (value)
		            _gamerState |= GamerStates.Ready;
		        else
		            _gamerState &= ~GamerStates.Ready;
		    }
		}
		
		public bool IsTalking 
		{ 
			get
			{
				return false;
			}
		}

        public NetworkSessionParticipantId Id
        {
            get
            {
                return _internalId;
            }
        }
		
		private NetworkMachine _machine;
		public NetworkMachine Machine 
		{ 
			get
			{
				return _machine;
			}
			set
			{
				if (_machine != value )
					_machine = value;
			}
		}
		
		public TimeSpan RoundtripTime 
		{ 
			get
			{
				return TimeSpan.MinValue;
			}
		}
		
		public NetworkSession Session 
		{ 
			get
			{
				return _session;
			}
		}

	    public GamerStates Status
	    {
	        get { return _gamerState; }
	    }

        /// <summary>
        /// Sets the passed state but does not raise the dirty flag.        
        /// </summary>        
	    internal void Set(GamerStates newState)
        {
            _gamerState = newState;

            // What if prevState was already different from the current state?
            if (_gamerState == _prevGamerState)
            {
                _gamerState = newState;
                _prevGamerState = newState;
            }
            else
            {
                _gamerState = newState;
            }            
        }

        /// <summary>
        /// Returns true if GamerStates has been modified since the last call to Update.
        /// </summary>        
        internal bool Update(out GamerStates newState, out GamerStates prevState)
	    {
            newState = _gamerState;
            prevState = _prevGamerState;

            if (newState != prevState)
            {
                _prevGamerState = _gamerState;
                return true;
            }

            return false;
	    }
	}
}
