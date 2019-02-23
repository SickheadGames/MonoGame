using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MonoGame.Switch
{
    #region Network Types

    internal struct SessionInformation
    {
        public uint sessionId;
        public IntPtr session;
        public string ownerName;
        public int MaxMembers;
        public int NumMembers;
        public int GameMode;
        public string data;

        public bool IsValid()
        {
            return session.ToInt64() != 0;
        }
    };

    public enum NetworkMode : int
    {
        Local,
        Online,
        Leaderboards,
    }

    public enum NetEventKind : int
    {
        Joined,
        Kicked,
        Left,
        RoomDestroyed,
        RoomOwnerChanged
    }

    public struct UdpPacket
    {
        public byte[] buffer;
        public StationId fromStationId;
        public StationId toStationId;
        public int size;
    }

    #endregion

    public static class Network
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool IsAvailable(NetworkMode mode);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int _TrySearch(
            int gameMode,
            Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer,
            Microsoft.Xna.Framework.Net.NetworkSessionProperties properties,
            List<SessionInformation> sessions);

        internal static int TrySearch(
            int gameMode,
            Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer,
            Microsoft.Xna.Framework.Net.NetworkSessionProperties properties,
            out List<SessionInformation> sessions)
        {
            sessions = new List<SessionInformation>();
            int result = _TrySearch(gameMode, gamer, properties, sessions);
            return result;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int TryStart(UserId userId, NetworkMode mode);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Shutdown();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int TryHost(
            int gameMode,
            Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer,
            int maxGamers,
            Microsoft.Xna.Framework.Net.NetworkSessionProperties properties);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int TryJoin(Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer, SessionInformation session);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int LeaveSession();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int SessionLocked(bool locked);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void Update(float elapsedSeconds);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern bool ConnectedToInternet();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void DumpState();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern StationId GetHostStationId();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern StationId GetLocalStationId();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern string GetPlayerName(StationId stationId);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern bool CheckReceivedData(out UdpPacket packet);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static unsafe extern void SendData(StationId fromStationId, StationId toStationId, byte* data, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int GetNumStations();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern StationId GetStationId(int index);

        internal static bool TryGetEvent(out StationId stationId, out NetEventKind kind, out int resultCode)
        {
            SessionEventInfo e;
            if (_sessionEvents.TryDequeue(out e))
            {
                stationId = e.stationId;
                kind = e.kind;
                resultCode = e.resultCode;
                return true;
            }

            stationId = StationId.Invalid;
            kind = default(NetEventKind);
            resultCode = 0;

            return false;
        }

        private class SessionEventInfo
        {
            public StationId stationId;
            public NetEventKind kind;
            public int resultCode;
        };

        private static ThreadSafeQueue<SessionEventInfo> _sessionEvents = new ThreadSafeQueue<SessionEventInfo>();

        internal static void PushEvent(StationId stationId, NetEventKind kind, int resultCode)
        {
            var e = new SessionEventInfo();
            e.stationId = stationId;
            e.kind = kind;
            e.resultCode = resultCode;

            _sessionEvents.Enqueue(e);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void UpdateSessionProperties(Microsoft.Xna.Framework.Net.NetworkSessionProperties properties);

        private static Microsoft.Xna.Framework.Net.NetworkSessionProperties _requestLobbyDataProperties;
        private static List<MonoGame.Switch.SessionInformation> _requestLobbyDataResults;
        private static string _appData;
    }

    #region UserService Types

    public struct UserId
    {
        internal UInt64 a;
        internal UInt64 b;

        public override string ToString()
        {
            return a.ToString() + "+" + b.ToString();
        }

        public static bool operator ==(UserId a, UserId b)
        {
            return a.a == b.a && a.b == b.b;
        }

        public static bool operator !=(UserId a, UserId b)
        {
            return !(a.a == b.a && a.b == b.b);
        }
    }

    public struct StationId
    {
        public static StationId Invalid = new StationId(0);

        public UInt64 id;

        public StationId(UInt64 val)
        {
            id = val;
        }

        public override string ToString()
        {
            return id.ToString();
        }

        public static bool operator ==(StationId a, StationId b)
        {
            return a.id == b.id;
        }

        public static bool operator !=(StationId a, StationId b)
        {
            return a.id != b.id;
        }
    }

    public struct UserHandle
    {
        internal IntPtr handle;
        public UserId id;

        public override string ToString()
        {
            return id.ToString();
        }
    }

    public struct OnlineId
    {
        public static OnlineId Invalid = new OnlineId(0);

        internal UInt64 id;

        public OnlineId(UInt64 val)
        {
            id = val;
        }

        public static bool operator ==(OnlineId a, OnlineId b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(OnlineId a, OnlineId b)
        {
            return !Equals(a, b);
        }

        public static bool Equals(OnlineId a, OnlineId b)
        {
            return a.id == b.id;
        }
    }

    #endregion

    public static class UserService
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UserId GetInitialLocalUser();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern string GetLocalUserNickname(UserHandle id);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern OnlineId GetLocalUserOnlineId(UserHandle id);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UserHandle OpenLocalUser(UserId id);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void CloseLocalUser(UserHandle id);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UserHandle GetOpenLocalUserHandle(UserId id);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern Microsoft.Xna.Framework.PlayerIndex GetLocalUserPlayerIndex(UserHandle id);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Update(float elapsedSeconds);
    }

    public static class Ranking
    {
        #region Types

        public class GetRankResults
        {
            public int FirstInRange;
            public int LastInRange;
            public int TotalPlayers;
            public List<Item> Items;

            public GetRankResults()
            {
                FirstInRange = 0;
                LastInRange = 0;
                TotalPlayers = 0;
                Items = new List<Item>();
            }
        }

        public struct Item
        {
            public uint Category;
            public uint Group0;
            public uint Group1;
            public UInt64 Score;
            public uint Ranking;
            public byte[] Data;
            public string UserName;
            public UInt64 PrincipalId;
        }

        public enum RequestMode
        {
            Everyone,
            Friends,
            Nearby,
        }

        #endregion

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void WaitSafeCallTimeout();

        
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Update();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int _TryStartup(MonoGame.Switch.UserId userId);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void OnNetworkShuttingDown();

        public static int TryStartup(MonoGame.Switch.UserId userId)
        {
            int resultCode = Network.TryStart(userId, NetworkMode.Leaderboards);
            if (resultCode != 0)
            {
                return resultCode;
            }

            return _TryStartup(userId);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int _TryUpload(ref Item item);

        // after this call, item.Rank will be assigned to the approximate rank
        // item.PrincipalId is not used, its always the currently signed in user
        public static int TryUpload(ref Item item)
        {
            // combines the gamertag into the byte array
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(1); // version number
            writer.Write(item.UserName);
            if (item.Data == null)
                writer.Write(0);
            else
            {
                writer.Write(item.Data.Length);
                writer.Write(item.Data);
            }
            item.Data = stream.ToArray();

            return _TryUpload(ref item);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int _TryDownload(RequestMode mode, int category, int startPos, int sizeOfPage, GetRankResults results);

        public static int TryDownload(RequestMode mode, int category, int startPos, int sizeOfPage, GetRankResults results)
        {
            int resultCode = _TryDownload(mode, category, startPos, sizeOfPage, results);
            if (resultCode != 0)
                return resultCode;

            // extract gamertag from byte[]


            for (var i = 0; i < results.Items.Count; i++)
            {
                var item = results.Items[i];

                if (item.Data != null)
                {
                    var stream = new MemoryStream(item.Data);
                    var io = new BinaryReader(stream);
                    int ver = io.ReadInt32(); // version number
                    if (ver != 1)
                    {
                        Console.WriteLine("Leaderboard Data is version '{0}' but we expected it to be '{1}'", ver, 1);
                        continue;
                    }

                    var userName = io.ReadString();
                    item.UserName = userName;

                    var len = io.ReadInt32();
                    if (len == 0)
                    {
                        item.Data = null;
                    }
                    else
                    {
                        var data = io.ReadBytes(len);
                        item.Data = data;
                    }
                }
                else
                {
                    item.UserName = "Player";
                    item.Data = null;
                }

                results.Items[i] = item;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void DumpState();
    }

}
