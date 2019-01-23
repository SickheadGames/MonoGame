using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MonoGame.Switch
{
    internal struct SessionInformation
    {
        public uint sessionId;
        public IntPtr session;
        public string ownerName;
        public int MaxMembers;
        public int NumMembers;
        public Dictionary<string, string> data;

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern int GetIntAttribute(string name, out bool exists);

        public bool IsValid()
        {
            return session.ToInt64() != 0;
        }
    };

    public enum NetworkMode : int
    {
        Local,
        Online
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

    public static class Network
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int _TrySearch(Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer, List<SessionInformation> sessions);

        internal static int TrySearch(Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer, out List<SessionInformation> sessions)
        {
            sessions = new List<SessionInformation>();
            //var handle = GCHandle.Alloc(sessions, GCHandleType.Pinned);
            int result = _TrySearch(gamer, sessions);
            //handle.Free();
            return result;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int TryStart(UserId userId, NetworkMode mode);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void Shutdown();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int TryHost(Microsoft.Xna.Framework.GamerServices.SignedInGamer gamer);

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
    }

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

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool Equals(OnlineId a, OnlineId b);
    }

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

    /*
        public class SwitchSDKHelper : SDKHelper
        {
            private readonly SwitchSDKNetHelper _netHelper = new SwitchSDKNetHelper();

            public void EarlyInitialize()
            {
            }

            public void Initialize()
            {
                // Note we don't do anything here because
                // we delay network connections until the
                // user tries to actually play co-op.

                ConnectionProgress = 0;
            }

            public void GetAchievement(string achieve)
            {
            }

            public void ResetAchievements()
            {
            }

            public void Update()
            {
                _netHelper.Update();
            }

            public void Shutdown()
            {
            }

            public void DebugInfo()
            {
            }

            public string FilterDirtyWords(string words)
            {
                return SwitchSDKNetHelper.FilterDirtyWords(words);
            }

            public virtual string Name { get; } = "Switch";

            public bool ConnectionFinished
            {
                get
                {
                    ConnectionProgress = 3;
                    return _netHelper.IsConnectionFinished();
                }
            }

            public int ConnectionProgress { get; private set; }

            public SDKNetHelper Networking
            {
                get
                {
                    if (_netHelper.IsStarted())
                        return _netHelper;

                    return null;
                }
            }
        }
    */

#if false
    class SwitchSDKNetHelper
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern ulong SwitchGetNsaId();
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern ulong SwitchGetUserIdForStation(ulong stationId);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void SwitchRequestFriendLobbyData();
        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void SwitchNetUpdate();
        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void SwitchNetShutdown();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal extern bool IsConnectionFinished();
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal extern bool IsStarted();

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern string FilterDirtyWords(string text);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern bool CheckFreeCommunicationPermission();

        public static bool LocalMode = false;

        private static string _userId;

        private List<LobbyUpdateListener> lobbyUpdateListeners = new List<LobbyUpdateListener>();

        //private SwitchNetServer _server;
        //private SwitchNetClient _client;

        private readonly Dictionary<uint, SessionInfo> _browseSessions = new Dictionary<uint, SessionInfo>();

        private static void ErrorExitToTitle()
        {
            Console.WriteLine("ErrorExitToTitle(); Not implemented");
        }

        public SwitchSDKNetHelper()
        {
        }

        public string GetLobbyData(uint sessionId, string key)
        {
            SessionInfo info;
            if (_browseSessions.TryGetValue(sessionId, out info))
            {
                string value;
                if (info.data.TryGetValue(key, out value))
                    return value;
            }
            return string.Empty;
        }

        public object GetLobbyFromInviteCode(string inviteCode)
        {
            throw new NotImplementedException();
        }

        public string GetLobbyOwnerName(object lobby)
        {
            var sessionId = (uint)lobby;
            SessionInfo info;
            if (_browseSessions.TryGetValue(sessionId, out info))
                return info.ownerName;
            return string.Empty;
        }

        //public Client GetRequestedClient()
        //{
        //    throw new NotImplementedException();
        //}

        //public string GetUserID()
        //{
        //    return GetSwitchUserID();
        //}

        //internal static string GetSwitchUserID()
        //{
        //    if (LocalMode)
        //        return "";

        //    if (_userId == null)
        //    {
        //        var id = SwitchGetNsaId();
        //        _userId = id.ToString("X");
        //    }

        //    return _userId;
        //}

        //internal static string GetUserIdFromStation(ulong stationId)
        //{
        //    if (LocalMode)
        //        return "";

        //    var id = SwitchGetUserIdForStation(stationId);
        //    var userId = id.ToString("X");
        //    return userId;
        //}

        //public void RequestFriendLobbyData()
        //{
        //    _browseSessions.Clear();
        //    SwitchRequestFriendLobbyData();
        //}

        //public void ShowInviteDialog(object lobby)
        //{
        //    throw new NotImplementedException();
        //}

        //public bool SupportsInviteCodes()
        //{
        //    return false;
        //}

        /*
        internal void Update()
        {
            // Shutdown the network if we're not actively playing online
            // or within one of the stages of the coop menus.
            var inMultiplayer = false;
            inMultiplayer |= Game1.IsClient;
            inMultiplayer |= Game1.IsServer;
            inMultiplayer |= TitleMenu.subMenu is CoopMenu;
            inMultiplayer |= TitleMenu.subMenu is FarmhandMenu;
            inMultiplayer |= TitleMenu.subMenu is CharacterCustomization;
            inMultiplayer |= Game1.gameMode == Game1.loadingMode;
            if (!inMultiplayer)
            {
                _client = null;
                _server = null;
                SwitchNetShutdown();
            }

            SwitchNetUpdate();
        }
        */

        private void OnLobbyDataUpdated(uint sessionId, IntPtr session, string ownerName, string data)
        {
            // Create/update the slot data.
            var info = new SessionInfo();
            info.session = session;
            info.ownerName = ownerName;
            info.data = new Dictionary<string, string>();

            var kvp = data.Split('\n');
            for (var i = 0; (i + 1) < kvp.Length; i += 2)
            {
                var key = kvp[i + 0];
                var val = kvp[i + 1];
                info.data[key] = val;
            }

            _browseSessions[sessionId] = info;

            foreach (var listener in lobbyUpdateListeners)
            {
                listener.OnLobbyUpdate(sessionId);
            }
        }

        public void AddLobbyUpdateListener(LobbyUpdateListener listener)
        {
            lobbyUpdateListeners.Add(listener);
        }

        public void RemoveLobbyUpdateListener(LobbyUpdateListener listener)
        {
            lobbyUpdateListeners.Remove(listener);
        }
    }
#endif
    public interface LobbyUpdateListener
    {
        void OnLobbyUpdate(uint lobby);
    }

    /*
        public interface SDKNetHelper
        {
            string GetUserID();

            Client CreateClient(LobbyId lobby);

            // The platform can ask us to behave as a client. If it does this it provides
            // the address / lobby through its API. In Steam, this can happen two ways:
            //  * Through the +connect_lobby command-line argument.
            //  * Through the GameLobbyJoinRequested_t callback.
            // When this happens, the game disconnects from any existing games and connects
            // through the client returned by this function.
            // If we're not being asked to behave as a client, this function returns null.
            Client GetRequestedClient();

            Server CreateServer(IGameServer gameServer);

            // Listen for lobbies found by RequestFriendLobbyData.
            void AddLobbyUpdateListener(LobbyUpdateListener listener);
            void RemoveLobbyUpdateListener(LobbyUpdateListener listener);

            // Returns data asynchronously through LobbyUpdateListeners.
            // GetLobbyData may be used during LobbyUpdateListener.OnLobbyUpdate
            void RequestFriendLobbyData();

            // Returns data from a lobby obtained by RequestFriendLobbyData.
            string GetLobbyData(LobbyId lobby, string key);
            string GetLobbyOwnerName(LobbyId lobby);

            bool SupportsInviteCodes();
            object GetLobbyFromInviteCode(string inviteCode);
            void ShowInviteDialog(LobbyId lobby);
        }
    */

    //public interface SDKHelper
    //{
    //    // EarlyInitialize vs Initialize:
    //    // * EarlyInitialize happens before any content is loaded. SDKs can use this to install
    //    //   crash handlers before content is loaded. Rail does this for instance.
    //    // * Initialize happens after audio is loaded. Audio files are large, and XACT requires
    //    //   contiguous memory to load them. If the SDK increases memory fragmentation, like
    //    //   Steam apparently does, and causes random crashes when loading audio, it should be
    //    //   initialized here.
    //    void EarlyInitialize();
    //    void Initialize();

    //    void Update();
    //    void Shutdown();
    //    void DebugInfo();

    //    void GetAchievement(string achieve);
    //    void ResetAchievements();
    //    string FilterDirtyWords(string words);

    //    string Name { get; }

    //    // Networking is null until a connection to the online service is established
    //    SDKNetHelper Networking { get; }

    //    // Indicates if the game has finished trying to establish a connection.
    //    // If it succeeded, Networking will be non-null.
    //    bool ConnectionFinished { get; }
    //    // An arbitrary integer >= 0 that indicates progress towards establishing a connection
    //    // with the online service.
    //    int ConnectionProgress { get; }
    //}
}
