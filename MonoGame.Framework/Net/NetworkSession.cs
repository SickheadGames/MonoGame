
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.GamerServices;
#if SWITCH
#else
using Sce.PlayStation4.Network;
using Sce.PlayStation4.Network.ToolkitNp;
using Sce.PlayStation4.System;
#endif

namespace Microsoft.Xna.Framework.Net
{
    internal static partial class HelperExtensions
    {

        public static IAsyncResult AsApm<T>(this Task<T> task,
                                    AsyncCallback callback,
                                    object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<T>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }
    }

    public sealed partial class NetworkSession : IDisposable
    {
#region Types

        //private delegate NetworkSession JoinDelegate(AvailableNetworkSession availableSession);
        private delegate NetworkSession JoinInvitedDelegate(IEnumerable<SignedInGamer> gamers, string sessionId);

#endregion

#region Private & Internal Data

        private NetworkSessionState _sessionState;

        private readonly GamerCollection<NetworkGamer> _allGamers;
        private readonly GamerCollection<LocalNetworkGamer> _localGamers;
        private readonly GamerCollection<NetworkGamer> _remoteGamers;
        private readonly GamerCollection<NetworkGamer> _previousGamers;
        private readonly List<NetworkMachine> _machines;

        internal readonly Queue<CommandEvent> _commandQueue;
        private readonly NetworkSessionType _sessionType;
        private readonly NetworkSessionProperties _sessionProperties;
        private readonly int _gameMode;

        private bool _isHost;
        private NetworkGamer _hostingGamer;
        private int _maxGamers;
        private int _privateGamerSlots;
        
        private EventHandler<GamerJoinedEventArgs> _gamerJoined;

#if SWITCH
        internal MonoGame.Switch.SessionInformation _sessionInfo;
#else
        internal Session _matchingSession;
#endif

        private static NetworkSession _currentSession;

        internal static NetworkSession GetCurrentSession()
        {
            return _currentSession;
        }

#endregion

#region Properties

        public GamerCollection<NetworkGamer> AllGamers
        {
            get { return _allGamers; }
        }

        public bool AllowHostMigration
        {
            get { return true; }
            set
            {
                // JCF: Technically AllowHostMigration should be false by default, as it is in XNA.
                //      However the ability to change the np session's corresponding value for this
                //      after it has already been created has not been implemented. Since I need it
                //      true for SotS...                
            }
        }

        private bool _allowJoinInProgress = false;

        /// <summary>        
        /// Gets or sets whether join-in-progress is allowed. If the host enables this setting, new machines will be able to join at any time. 
        /// The default value is false, indicating that join-in-progress is disabled. AllowJoinInProgress can be read by any machine in the 
        /// session, but can only be changed by the host.
        /// https://msdn.microsoft.com/en-us/library/microsoft.xna.framework.net.networksession.allowjoininprogress.aspx
        /// 
        /// Note that a session in its 'lobby' state is joinable regardless of this setting. This setting only controls whether joining
        /// a session in non-lobby states is allowed.
        /// 
        /// JCFTODO: Sessions are only joinable while in lobby state, with current ps4 implementation! This property does nothing!
        /// </summary>
        public bool AllowJoinInProgress
        {
            get { return _allowJoinInProgress; }
            set { _allowJoinInProgress = value; }
        }

        public NetworkGamer Host
        {
            get { return _hostingGamer; }
        }

        private bool _isDisposed = false;

        public bool IsDisposed
        {
            get
            {
                return _isDisposed; // TODO (this.kernelHandle == 0);
            }
        }

        public bool IsEveryoneReady
        {
            get
            {
                if (_allGamers.Count == 0)
                    return false;
                foreach (NetworkGamer gamer in _allGamers)
                {
                    if (!gamer.IsReady)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public bool IsHost
        {
            get { return _isHost; }
        }

        public GamerCollection<LocalNetworkGamer> LocalGamers
        {
            get { return _localGamers; }
        }

        public int MaxGamers
        {
            get { return _maxGamers; }
            set { _maxGamers = value; }
        }

        public GamerCollection<NetworkGamer> PreviousGamers
        {
            get { return _previousGamers; }
        }

        public int PrivateGamerSlots
        {
            get { return _privateGamerSlots; }
            set { _privateGamerSlots = value; }
        }

        public GamerCollection<NetworkGamer> RemoteGamers
        {
            get { return _remoteGamers; }
        }

        public NetworkSessionProperties SessionProperties
        {
            get { return _sessionProperties; }
        }

        public NetworkSessionState SessionState
        {
            get { return _sessionState; }
        }

        public NetworkSessionType SessionType
        {
            get
            {
                return _sessionType;
            }
        }

        private bool _locked;

#if DEBUG
        public
#else
        private
#endif
 bool Locked
        {
            get { return _locked; }
            set
            {
                Console.WriteLine("NetworkSession.Locked = {0}", value);

                try
                {
                    if (_locked == value)
                    {
                        Console.WriteLine("NetworkSession.Locked is already that value.");
                        return;
                    }

                    if (!_isHost)
                    {
                        Console.WriteLine("Only the host can change NetworkSession.Locked");
                        return;
                    }

                    _locked = value;

                    int lockResult = MonoGame.Switch.Network.SessionLocked(value);
                    Console.WriteLine("MonoGame.Switch.Network.SessionLocked returned : " + lockResult);
                }
                catch (Exception e)
                {
                    Console.WriteLine("exception : " + e);
                    Console.WriteLine("NetworkSession.Locked = {0}", value);
                }
            }
        }

#endregion

#region Events

        private static EventHandler<InviteAcceptedEventArgs> _inviteAccepted;

        public static event EventHandler<InviteAcceptedEventArgs> InviteAccepted
        {
            add
            {
                Console.WriteLine("NetworkSession.InviteAccepted_add");
                Extensions.PrintCallstack();

                _inviteAccepted += value;

                if (_receivedInvitation != null)
                {
                    Console.WriteLine("Firing stored invitation");

                    value(null, CreateArgs(_receivedInvitation));

                    _receivedInvitation = null;
                }

            }
            remove
            {
                _inviteAccepted -= value;
                _receivedInvitation = null;
            }
        }

        public event EventHandler<GameEndedEventArgs> GameEnded;

        /// <summary>
        /// @see https://msdn.microsoft.com/en-us/library/microsoft.xna.framework.net.networksession.gamerjoined.aspx
        /// This event is raised for both local and remote players, including when the session is first created.
        /// 
        /// When a new event handler is attached to GamerJoined the handler receives join notifications for all players 
        /// currently in the session. This means there will be a join notification for each gamer that has joined the session 
        /// before the event handler is attached to the event
        /// </summary>
        public event EventHandler<GamerJoinedEventArgs> GamerJoined
        {
            add
            {
                _gamerJoined += value;

                foreach (var g in _allGamers)
                    value(this, new GamerJoinedEventArgs(g));

            }
            remove
            {
                _gamerJoined -= value;
            }
        }

        public event EventHandler<GamerLeftEventArgs> GamerLeft;
        public event EventHandler<GameStartedEventArgs> GameStarted;
        public event EventHandler<HostChangedEventArgs> HostChanged;
        public event EventHandler<NetworkSessionEndedEventArgs> SessionEnded;

        private static Tuple<string, string> _receivedInvitation;

        private static InviteAcceptedEventArgs CreateArgs(Tuple<string, string> obj)
        {
            var invitedGamer = Gamer.SignedInGamers.GetByGamerTag(obj.Item1);

            // Eg, a player who is signed in locally but who hasn't joined a local session
            //     ie they aren't the initial user...
            //
            //     Such a player shouldn't actually receive an invitation at all and
            //     should instead join via NetworkSession.AddLocalGamer... so that case 
            //     probably never has to be handled here...
            //
            //     Alternately... check if Sessions::GetPrimary() is not null and its sessionid matches.
            var isCurrentSession = false;

            var args = new InviteAcceptedEventArgs(invitedGamer, isCurrentSession, obj.Item2);

            return args;
        }

        internal static void HandleInvitationAccepted(Tuple<string, string> obj)
        {
            Console.WriteLine("NetworkSession.HandleInvitationAccepted(); a={0}, b={1}", obj.Item1, obj.Item2);

            var e = _inviteAccepted;
            if (e == null)
            {
                Console.WriteLine("storing invitation since no one is subscribed");
                _receivedInvitation = obj;
                return;
            }

            Console.WriteLine("triggering invitation since there is a subscriber");
            _inviteAccepted(null, CreateArgs(obj));
        }

#endregion

#region Constructor, Destructor, Dispose

        // use the static Create or BeginCreate methods
        private NetworkSession()
        {
            if (_currentSession != null)
                throw new Exception("Cannot allocate a NetworkSession when there is already a _currentSession.");

            _currentSession = this;

            _allGamers = new GamerCollection<NetworkGamer>();
            _localGamers = new GamerCollection<LocalNetworkGamer>();
            _remoteGamers = new GamerCollection<NetworkGamer>();
            _previousGamers = new GamerCollection<NetworkGamer>();
            _hostingGamer = null;
            _machines = new List<NetworkMachine>();
            _commandQueue = new Queue<CommandEvent>();
        }

        /// <summary>
        /// Accept invite to join a NetworkSession.
        /// </summary>
        private NetworkSession(IEnumerable<SignedInGamer> localGamers, string invitedSessionId)
            : this()
        {
            Console.WriteLine("NetworkSession.NetworkSession(localGamers, invitedSession)");

#if SWITCH
#else
            foreach (var g in localGamers)
            {
                Console.WriteLine("Gamer[{0}] : {1}:{2}", g.PlayerIndex, g.DisplayName, g.UserId);
            }

            try
            {
                Console.WriteLine("Joining invited session with local gamers...");

                foreach (var g in localGamers)
                {
                    Console.WriteLine("Gamer[{0}] : {1}:{2}", g.PlayerIndex, g.DisplayName, g.UserId);

                    Console.WriteLine("Joining invited session for gamer {0}.", g.Gamertag);

                    var req = new InviteJoinSessionRequest(g.UserId, invitedSessionId);
                    ToolkitResult joinResult;
                    _matchingSession = Matching.InvitedJoinSession(req, out joinResult);

                    if (joinResult != ToolkitResult.Ok)
                    {
                        if (joinResult == ToolkitResult.WebErrorResponse || joinResult == ToolkitResult.MatchingServerErrorNoSuchRoom)
                            throw new NetworkSessionJoinException(null, NetworkSessionJoinError.SessionNotFound);

                        throw new NetErrorException(g.UserId, (int)joinResult);
                    }
                }

                var sessionInfo = _matchingSession.Info;
                _isHost = false;
                _sessionProperties = NetworkSessionProperties.Get(sessionInfo);
                _maxGamers = sessionInfo.MaxMembers;

                _sessionType = _sessionProperties[NetworkSessionProperties.RankedSession] > 0
                    ? NetworkSessionType.Ranked
                    : NetworkSessionType.PlayerMatch;

                FlushCommands();

                // TODO: These should be defined / given a value as session attributes
                _privateGamerSlots = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("NetworkSession(); Exception : " + ex.Message);

                Sessions.Leave();

                _currentSession = null;

                _matchingSession = null;

                _isDisposed = true;

                throw;
            }
#endif
        }

        /// <summary>
        /// Join a NetworkSession.
        /// </summary>
        private NetworkSession(AvailableNetworkSession availableSession)
            : this()
        {
            Console.WriteLine("NetworkSession(availableSession)");

            if (availableSession == null)
                throw new NullReferenceException("NetworkSession() Error: passed argument 'availableSession' is null.");

            var sessionInfo = availableSession.Info;
            if (!sessionInfo.IsValid())
                throw new NullReferenceException("NetworkSession() Error: passed argument 'availableSession'.Info is null.");

            // When joining an existing session, localGamers is not provided.
            // Instead we populate it from availableSession.LocalGamersMask.
            var localGamers = new List<SignedInGamer>();

            for (var i = 0; i < 4; i++)
            {
                var mask = 1 << i;
                if ((mask & availableSession.LocalGamersMask) > 0)
                {
                    var gamer = Gamer.SignedInGamers[(PlayerIndex)i];
                    if (gamer == null)
                    {
                        var exmsg = string.Format("Could not find gamer with index of {0}. Gamer.SignedInGamers[{0}] is null.",
                                                  i,
                                                  (PlayerIndex)i);
                        throw new Exception(exmsg);
                    }

                    localGamers.Add(gamer);
                }
            }

            Console.WriteLine("Joining session with local gamers...");

            try
            {
                foreach (var g in localGamers)
                {
                    Console.WriteLine("Gamer[{0}] : {1}:{2}", g.PlayerIndex, g.DisplayName, g.UserId);                   
                    Console.WriteLine("Joining session for gamer {0}.", g.Gamertag);

                    int joinResult = MonoGame.Switch.Network.TryJoin(g, sessionInfo);

                    //var joinRequest = new JoinSessionRequest(g.UserId, g.Gamertag, sessionInfo);

                    //ToolkitResult joinResult;
                    //_matchingSession = Matching.JoinSession(joinRequest, out joinResult);

                    if (joinResult != 0)
                    {                        
                        throw new NetErrorException(g.UserId, (int)joinResult, 0);
                    }
                }

                //sessionInfo = _matchingSession.Info;
                _isHost = false;
                _sessionProperties = NetworkSessionProperties.FromApplicationDataString(sessionInfo.data);
                _maxGamers = sessionInfo.MaxMembers;
                
                _sessionType = sessionInfo.GameMode == 1 ? NetworkSessionType.Ranked : NetworkSessionType.PlayerMatch;

                FlushCommands();

                // TODO: These should be defined / given a value as session attributes
                _privateGamerSlots = 0;

            }
            catch (Exception ex)
            {
                Console.WriteLine("NetworkSession(); Exception : " + ex.Message);

                //Sessions.Leave();

                _currentSession = null;

                //_matchingSession = null;

                _isDisposed = true;

                throw;
            }
        }

        /// <summary>
        /// Host a NetworkSession.
        /// </summary>        
        private NetworkSession(NetworkSessionType sessionType,
                               IEnumerable<SignedInGamer> localGamers,
                               int maxGamers,
                               int privateGamerSlots,
                               NetworkSessionProperties sessionProperties,                               
                               PlayerIndex hostGamerIndex)
            : this()
        {
            if (sessionProperties == null)
            {
                throw new ArgumentNullException("sessionProperties");
            }

            _sessionType = sessionType;
            _maxGamers = maxGamers;
            _privateGamerSlots = privateGamerSlots;
            _sessionProperties = sessionProperties;
            _sessionProperties.MarkClean();
            _isHost = true;

            if (_sessionType == NetworkSessionType.Ranked)
                _gameMode = 1;
            else
                _gameMode = 0;

            /*
            foreach (var g in localGamers)
            {
                Console.WriteLine("Gamer[{0}] : {1}:{2}", g.PlayerIndex, g.DisplayName, g.UserId);
            }
            */

            try
            {
                var hostGamer = localGamers.GetByPlayerIndex(hostGamerIndex);
                var hostUserId = MonoGame.Switch.UserService.GetOpenLocalUserHandle(hostGamer.UserId);

                Console.WriteLine("Creating a session for player {0}:{1}.",
                    hostUserId.id.a <= 0 ? "[null]" : MonoGame.Switch.UserService.GetLocalUserNickname(hostUserId),
                    hostUserId);
#if SWITCH
                int hostResult = MonoGame.Switch.Network.TryHost(_gameMode, hostGamer, _maxGamers, _sessionProperties);

                if (hostResult != 0)
                {
                    Console.WriteLine("MonoGame.Switch.Network.TryHost failed, error: {0}", hostResult);
                    throw new NetErrorException(hostUserId.id, hostResult, 0);
                }
#endif

#if PLAYSTATION4
                var createRequest = new CreateSessionRequest(hostUserId)
                {
                    ImagePath = "/app0/Content/session_image.jpg",
                    MaxSlots = maxGamers,
                    Name = "My Game Session",
                    NatRestricted = true,
                    SignalingEnabled = true,     
                    HostMigration = false,
                };

                _sessionProperties.Set(createRequest);

                ToolkitResult resCode;
                _matchingSession = Matching.CreateSession(createRequest, out resCode);

                if (resCode != ToolkitResult.Ok)
                {
                    Console.WriteLine("Matching.CreateSession failed, error: {0:x6}", (int)resCode);
                    throw new NetErrorException(hostUserId, (int)resCode);
                }

                if (localGamers != null)
                {
                    Console.WriteLine("Joining session with local gamers...");

                    var joinSession = _matchingSession.Info;

                    foreach (var g in localGamers)
                    {
                        Console.WriteLine("Gamer[{0}] : {1}:{2}", g.PlayerIndex, g.DisplayName, g.UserId);

                        if (g.PlayerIndex == hostGamerIndex)
                            continue;

                        Console.WriteLine("Joining session for gamer {0}.", g.Gamertag);

                        var joinRequest = new JoinSessionRequest(g.UserId, g.Gamertag, joinSession);

                        ToolkitResult joinResult;
                        Matching.JoinSession(joinRequest, out joinResult);

                        if (joinResult != ToolkitResult.Ok)
                            throw new NetErrorException(g.UserId, (int)joinResult);
                    }
                }
#endif

                FlushCommands();
                
                /*
                while (true)
                {
                    _matchingSession.Update();

                    bool done = true;
                    
                    foreach (var g in localGamers)
                    {
                        var peerState = _matchingSession.GetPeerState(g.Gamertag);

                        if (peerState == PeerState.Inactive)
                        {
                            done = false;
                        }

                        if (peerState == PeerState.)
                        {
                            done = false;
                        }

                        if (peerState == PeerState.ConnectionError)
                        {
                            // JCFTODO: Store a real error code on the peer?                          
                            throw new NetErrorException(g.UserId, 0);
                        }
                    }

                    if (done)
                        break;
                }     
                */
            }
            catch (Exception ex)
            {
                Console.WriteLine("NetworkSession(); Exception : " + ex.Message);

                MonoGame.Switch.Network.LeaveSession();

                _currentSession = null;

                //_matchingSession = null;

                _isDisposed = true;
                
                throw;
            }
        }

        ~NetworkSession()
        {
            Dispose(false);
        }

        public void Dispose()
        {            
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Console.WriteLine("NetworkSession.Dispose()");
                    Extensions.PrintCallstack();

                    _isDisposed = true;

                    Console.WriteLine("Before Sessions.Leave()");

                    MonoGame.Switch.Network.LeaveSession();

                    Console.WriteLine("After Sessions.Leave()");

                    // JCF: unmanaged memory already freed for this (and all) matching sessions.
                    //if (_matchingSession != null)
                    //{
                    //    _matchingSession = null;

                    //    //Console.WriteLine("Before _matchingSession.Dispose()");
                    //    //_matchingSession.Dispose();
                    //    //Console.WriteLine("After _matchingSession.Dispose()");
                    //}

                    //_matchingSession = null;

                    if (_currentSession == this)
                        _currentSession = null;
                }
            }
        }

        private void FlushCommands()
        {
            Console.WriteLine("NetworkSession.FlushCommands");

            Update();

            while (_commandQueue.Count > 0)
            {
                Update();
            }
        }

#endregion

#region Static Methods - Create

        public static NetworkSession Create(
            NetworkSessionType sessionType,
            // Type of session being hosted.
            int maxLocalGamers,
            // Maximum number of local players on the same gaming machine in this network session.
            int maxGamers
            // Maximum number of players allowed in this network session.  For Zune-based games, this value must be between 2 and 8; 8 is the maximum number of players supported in the session.
            )
        {
            var privateGamerSlots = 0;

            var sessionProperties = new NetworkSessionProperties();

            var localGamers = new List<SignedInGamer>();

            // JCF: What if maxLocalGamers is less than the number of gamers currently signed in?
            //      I do not know the XNA/XBOX behavior. 
            //
            //      I presume that this situation would arise if a game mode was selected which
            //      only supports 2 players but 3 are logged in, but I do not know what XNA would
            //      do next.
            //
            //      As a simplification we include the initialGamer and then as many other gamers
            //      currently signed in as we can.                        

            var count = 0;
            foreach (var g in Gamer.SignedInGamers)
            {
                if (count >= maxLocalGamers)
                    break;

                if (localGamers.Contains(g))
                    continue;

                if (g.IsSignedInToLive)
                {
                    localGamers.Add(g);
                    count++;
                }
            }

            return Create(sessionType, localGamers, maxGamers, privateGamerSlots, sessionProperties);
        }

        public static NetworkSession Create(
            NetworkSessionType sessionType,
            // Type of session being hosted.
            IEnumerable<SignedInGamer> localGamers,
            // Maximum number of local players on the same gaming machine in this network session.
            int maxGamers,
            // Maximum number of players allowed in this network session.  For Zune-based games, this value must be between 2 and 8; 8 is the maximum number of players supported in the session.
            int privateGamerSlots,
            // Number of reserved private session slots created for the session. This value must be less than maximumGamers. 
            NetworkSessionProperties sessionProperties // Properties of the session being created.
            )
        {
            NetworkSession session = null;

            if (sessionProperties == null)
            {
                sessionProperties = new NetworkSessionProperties();
            }

            var hostGamerIndex = localGamers.FirstOrDefault().PlayerIndex;
            session = new NetworkSession(sessionType, localGamers, maxGamers, privateGamerSlots, sessionProperties, hostGamerIndex);

            return session;
        }

        /*
        public static NetworkSession Create(
            NetworkSessionType sessionType,
            int maxLocalGamers,
            int maxGamers,
            int privateGamerSlots,
            NetworkSessionProperties sessionProperties)
        {
            var hostGamer = Gamer.FindGamerByUserId(UserService.InitialUser);
            int hostGamerIndex = hostGamer.UserId;
            bool isHost = true;

            return CreateEx(sessionType, maxLocalGamers, maxGamers, privateGamerSlots, sessionProperties, hostGamerIndex, isHost);
        }
        */

#endregion

#region Static Methods - Find

        public static AvailableNetworkSessionCollection Find(
            NetworkSessionType sessionType,
            int maxLocalGamers,
            NetworkSessionProperties searchProperties)
        {
            if (maxLocalGamers < 1 || maxLocalGamers > 4)
                throw new ArgumentOutOfRangeException("maxLocalGamers must be between 1 and 4.");

            var localGamers = new List<SignedInGamer>();

            // JCF: The initial user must always be part of the localGamers collection
            //      for whom we are finding a match.
            //      This may not be a rule in XNA but it is a simplification for my peace of mind.
            var initialGamer = Gamer.SignedInGamers.GetByUserId(MonoGame.Switch.UserService.GetInitialLocalUser());
            localGamers.Add(initialGamer);

            // JCF: What if maxLocalGamers is less than the number of gamers currently signed in?
            //      I do not know the XNA/XBOX behavior. 
            //
            //      I presume that this situation would arise if a game mode was selected which
            //      only supports 2 players but 3 are logged in, but I do not know what XNA would
            //      do next.
            //
            //      As a simplification we include the initialGamer and then as many other gamers
            //      currently signed in as we can.                        

            var count = 1;
            foreach (var g in Gamer.SignedInGamers)
            {
                if (count >= maxLocalGamers)
                    break;

                if (localGamers.Contains(g))
                    continue;

                if (g.IsSignedInToLive)
                {
                    localGamers.Add(g);
                    count++;
                }
            }

            return Find(sessionType, localGamers, searchProperties);
        }

        public static AvailableNetworkSessionCollection Find(
            NetworkSessionType sessionType,
            IEnumerable<SignedInGamer> localGamers,
            NetworkSessionProperties searchProperties)
        {
#if SWITCH
            if (sessionType == NetworkSessionType.Local)
                throw new ArgumentException("Find cannot be used from NetworkSession objects of session type NetworkSessionType.Local.");

            // JCF: Does this match XNA?
            if (localGamers == null)
                localGamers = Gamer.SignedInGamers;

            int localGamerCount = 0;
            int localGamersMask = 0;
            if (localGamers != null)
            {
                foreach (var g in localGamers)
                {
                    // JCF: I have no reason to believe this could occur. Just trying to ensure
                    //      any exceptions that occur from within this method are as informative as possible.
                    if (g == null)
                        throw new ArgumentException("localGamers contains a null element.");

                    int mask = 1 << (int)g.PlayerIndex;
                    if ((localGamersMask & mask) == 0)
                    {
                        localGamersMask |= mask;
                        localGamerCount++;
                    }
                }
            }

            // JCF: Does this match XNA, or is this test only done if the maxLocalGamers parameter is passed?
            if (localGamerCount < 1 || localGamerCount > 4)
                throw new ArgumentException("Must be between 1 and 4 localGamers.");

            int gameMode = sessionType == NetworkSessionType.Ranked ? 1 : 0;

            var firstLocalGamer = localGamers.FirstOrDefault();

            List<MonoGame.Switch.SessionInformation> sessionlist;
            var searchResult = MonoGame.Switch.Network.TrySearch(gameMode, firstLocalGamer, searchProperties, out sessionlist);
            if (searchResult != 0)
                throw new NetErrorException(firstLocalGamer.UserId, (int)searchResult, 0);

            // Filter SessionInformation(s) into AvailableNetworkSession(s).
            var list = new List<AvailableNetworkSession>();
            if (sessionlist != null && sessionlist.Count > 0)
            {
                var slotsNeeded = localGamers.Count();

                for (var i = 0; i < sessionlist.Count; i++)
                {
                    var info = sessionlist[i];

                    // Only include sessions with enough empty slots for all the local players who will be joining it.                    
                    var slotsAvailable = info.MaxMembers - info.NumMembers;
                    if (slotsAvailable < slotsNeeded)
                    {
                        Console.WriteLine("Skipping available because not enough slots available.");
                        continue;
                    }

                    var availSession = new AvailableNetworkSession(info, localGamersMask, slotsNeeded);
                    list.Add(availSession);
                }
            }

            var result = new AvailableNetworkSessionCollection(list);

            return result;
#endif
#if PLAYSTATION4
            if (sessionType == NetworkSessionType.Local)
                throw new ArgumentException("Find cannot be used from NetworkSession objects of session type NetworkSessionType.Local.");

            // JCF: Does this match XNA?
            if (localGamers == null)
                localGamers = Gamer.SignedInGamers;

            int localGamerCount = 0;
            int localGamersMask = 0;
            if (localGamers != null)
            {
                foreach (var g in localGamers)
                {
                    // JCF: I have no reason to believe this could occur. Just trying to ensure
                    //      any exceptions that occur from within this method are as informative as possible.
                    if (g == null)
                        throw new ArgumentException("localGamers contains a null element.");

                    int mask = 1 << (int)g.PlayerIndex;
                    if ((localGamersMask & mask) == 0)
                    {
                        localGamersMask |= mask;
                        localGamerCount++;
                    }
                }
            }

            // JCF: Does this match XNA, or is this test only done if the maxLocalGamers parameter is passed?
            if (localGamerCount < 1 || localGamerCount > 4)
                throw new ArgumentException("Must be between 1 and 4 localGamers.");

            searchProperties[NetworkSessionProperties.RankedSession] = sessionType == NetworkSessionType.Ranked ? 1 : 0;

            var firstLocalGamer = localGamers.FirstOrDefault();

            // The sony api only takes a single UserId when searching, so we just pass the first localGamer.
            // But really we are finding sessions which can be joined by all 'localGamers'.            
            var searchRequest = new SearchSessionsRequest(firstLocalGamer.UserId, SearchFlags.Random | SearchFlags.NatRestricted);            
            searchProperties.Set(searchRequest);

            ToolkitResult searchResult;
            var sessionlist = Matching.SearchSessions(searchRequest, out searchResult);
            if (searchResult != ToolkitResult.Ok)
                throw new NetErrorException(firstLocalGamer.UserId, (int)searchResult);

            // Filter SessionInformation(s) into AvailableNetworkSession(s).
            var list = new List<AvailableNetworkSession>();
            if (sessionlist != null && sessionlist.Count > 0)
            {
                var slotsNeeded = localGamers.Count();

                for (var i = 0; i < sessionlist.Count; i++)
                {
                    var info = sessionlist[i];

                    // Only include sessions with enough empty slots for all the local players who will be joining it.                    
                    var slotsAvailable = info.MaxMembers - info.NumMembers;
                    if (slotsAvailable < slotsNeeded)
                    {
                        Console.WriteLine("Skipping available because not enough slots available.");
                        continue;
                    }
					
                    var availSession = new AvailableNetworkSession(info, localGamersMask, slotsNeeded);
                    list.Add(availSession);
                }
            }

            var result = new AvailableNetworkSessionCollection(list);

            return result;
#endif
        }

        #endregion

        #region Static Methods - Join

        public static IAsyncResult BeginJoin(
            AvailableNetworkSession availableSession,
            AsyncCallback callback,
            Object asyncState)
        {
            Console.WriteLine("BeginJoin - 0");

            var task = new Task<NetworkSession>(
                () =>
                {
                    Console.WriteLine("BeginJoin - inside task");
                    return Join(availableSession);
                });
            task.Start();

            Console.WriteLine("BeginJoin - 1");

            return task.AsApm(callback, asyncState);
        }

        public static NetworkSession EndJoin(IAsyncResult result)
        {
            Console.WriteLine("EndJoin - 0");

            NetworkSession returnValue = null;
            try
            {
                /*
                // Retrieve the delegate.
                AsyncResult asyncResult = (AsyncResult)result;

                // Wait for the WaitHandle to become signaled.
                result.AsyncWaitHandle.WaitOne();

                // Call EndInvoke to retrieve the results.
                if (asyncResult.AsyncDelegate is JoinDelegate)
                {
                    returnValue = ((JoinDelegate)asyncResult.AsyncDelegate).EndInvoke(result);
                }
                */
                returnValue = ((Task<NetworkSession>)result).Result;
            }
            finally
            {
                Console.WriteLine("EndJoin - 1");

                // Close the wait handle.
                //result.AsyncWaitHandle.Close();
            }

            Console.WriteLine("EndJoin - 2");

            return returnValue;
        }

        public static NetworkSession Join(AvailableNetworkSession availableSession)
        {
            Console.WriteLine("NetworkSession.Join( availableSession )");

            var session = new NetworkSession(availableSession);

            return session;
        }

        public static IAsyncResult BeginJoinInvited(IEnumerable<SignedInGamer> localGamers, string sessionId, AsyncCallback callback, Object asyncState)
        {
            Console.WriteLine("BeginJoinInvited - 0");

            if (sessionId == null)
                throw new ArgumentNullException();

            JoinInvitedDelegate work = JoinInvited;
            var ret = work.BeginInvoke(localGamers, sessionId, callback, asyncState);

            Console.WriteLine("BeginJoinInvited - 1");

            return ret;
        }

        public static NetworkSession EndJoinInvited(IAsyncResult result)
        {
            Console.WriteLine("EndJoinInvited - 0");

            NetworkSession returnValue = null;
            try
            {
                // Retrieve the delegate.
                AsyncResult asyncResult = (AsyncResult)result;

                // Wait for the WaitHandle to become signaled.
                result.AsyncWaitHandle.WaitOne();

                // Call EndInvoke to retrieve the results.
                if (asyncResult.AsyncDelegate is JoinInvitedDelegate)
                {
                    returnValue = ((JoinInvitedDelegate)asyncResult.AsyncDelegate).EndInvoke(result);
                }
            }
            finally
            {
                Console.WriteLine("EndJoinInvited - 1");

                // Close the wait handle.
                result.AsyncWaitHandle.Close();
            }

            Console.WriteLine("EndJoinInvited - 2");

            return returnValue;
        }

        public static NetworkSession JoinInvited(IEnumerable<SignedInGamer> localGamers, string sessionId)
        {
            var session = new NetworkSession(localGamers, sessionId);

            return session;
        }

        public static NetworkSession JoinInvited(int maxLocalGamers, string sessionId)
        {
            if (maxLocalGamers < 1 || maxLocalGamers > 4)
                throw new ArgumentOutOfRangeException("maxLocalGamers must be between 1 and 4.");

            var localGamers = Gamer.SignedInGamers;
            var session = new NetworkSession(localGamers, sessionId);

            return session;
        }

#endregion

#region Public Methods - AddLocal, GameState Management, Update

        public void AddLocalGamer(SignedInGamer gamer)
        {
            if (gamer == null)
                throw new ArgumentNullException("gamer");

            var exists = AllGamers.GetByGamertag(gamer.Gamertag) != null;

            Console.WriteLine("NetworkSession.AddLocalGamer(); gamertag={0}, alreadyExists={1}", gamer.Gamertag, exists);

            if (exists)
                return;

#if SWITCH
            throw new NotImplementedException();
#else
            var userId = gamer.UserId;
            var onlineId = gamer.Gamertag;
            var joinRequest = new JoinSessionRequest(userId, onlineId, _matchingSession.Info);
            
            ToolkitResult resCode;
            var matchingSession = Matching.JoinSession(joinRequest, out resCode);

            if (resCode != ToolkitResult.Ok)
            {
                Console.WriteLine("Matching.JoinSession failed, error: {0:x6}", (int)resCode);
                throw new NetErrorException(userId, (int)resCode);
            }
#endif
        }

        /// <summary>
        /// Sets all local gamers to the NOT ready.
        /// </summary>
        public void ResetReady()
        {
            foreach (var gamer in _localGamers)
            {
                gamer.IsReady = false;
            }
        }

        /// <summary>
        /// Changes the session state from NetworkSessionState.Lobby to NetworkSessionState.Playing.
        /// </summary>
        public void StartGame()
        {
            if (!_isHost)            
                throw new InvalidOperationException("This NetworkSession is not the host.");
                                    
            if (_sessionState == NetworkSessionState.Ended || _sessionState == NetworkSessionState.Playing)
                throw new InvalidOperationException("The NetworkSession is in an invalid state to call this method");

            // Lock the session, preventing any more players from joining.
            //
            // What happens to players who have joined the session but to which signaling and/or rudp have
            // not yet been established? Presumably the host is unaware of their presence yet so
            // do we kick them out and start immediately, or do we wait for them to either finish joining
            // or fail joining, and then start?
            var ssc = new CommandSessionStateChange(NetworkSessionState.Playing, _sessionState);
            _commandQueue.Enqueue(new CommandEvent(ssc));
        }

        /// <summary>
        /// Changes the session state from NetworkSessionState.Playing to NetworkSessionState.Lobby.
        /// </summary>
        public void EndGame()
        {
            if (!_isHost)
                throw new InvalidOperationException("This NetworkSession is not the host.");

            if (_sessionState == NetworkSessionState.Ended || _sessionState == NetworkSessionState.Lobby)
                throw new InvalidOperationException("The NetworkSession is in an invalid state to call this method");


            var ssc = new CommandSessionStateChange(NetworkSessionState.Lobby, _sessionState);
            _commandQueue.Enqueue(new CommandEvent(ssc));
        }

        /// <summary>
        /// @see https://msdn.microsoft.com/en-us/library/microsoft.xna.framework.net.networksession.update.aspx
        /// Updates the state of the multiplayer session. Call this method at regular intervals—for example, from the Game.Update method.
        /// </summary>
        public void Update()
        {
            if (_isDisposed)
                return;

            if (_isHost)
            {
                if (_sessionProperties.Dirty)
                {
                    MonoGame.Switch.Network.UpdateSessionProperties(_sessionProperties);
                    _sessionProperties.MarkClean();
                }
            }
            
            // JCF: Hacky, don't really need this flag to be in Guide anymore since we should technically be
            //      able to tell what we need right here.. if the networksession is PlayerMatch or Ranked and
            //      it is currently in Playing state, then notify plus feature..
            try
            {
                if (Guide.RealtimeMultiplayerInUse)
                {
                    foreach (var localGamer in _localGamers)
                    {
                        if (localGamer == null)
                            continue;

                        //MonoGame.Switch.Network.NotifyOnline(localGamer.UserId, NpPlusFeature.RealtimeMultiplay);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while notifying plus feature... \n" + e);                
            }

            try
            {
                // Translate pia framework events into our internal NetworkSession events.
                UpdatePia();

                // Update each NetworkGamer before processing events since this call can generate events.
                foreach (var netgamer in _localGamers)
                {
                    GamerStates newState;
                    GamerStates prevState;
                    if (netgamer.Update(out newState, out prevState))
                    {
                        var cmd = new CommandSendGamerState(netgamer, newState, prevState);
                        var evt = new CommandEvent(CommandEventType.SendGamerState, cmd);
                        _commandQueue.Enqueue(evt);
                    }
                }

                // Process internal events.              
                while (_commandQueue.Count > 0)
                {
                    var command = (CommandEvent)_commandQueue.Dequeue();

                    if (command == null)
                    {
                        Console.WriteLine("NetworkSession.Update(); Warning: Dequeued CommandEvent is null.");
                        continue;
                    }

                    Console.WriteLine("Command: {0}", command.Command);

                    // Because a session ended event will dispose us.
                    if (!IsDisposed)
                    {
                        switch (command.Command)
                        {
                            case CommandEventType.SendData:
                                ProcessSendData((CommandSendData)command.CommandObject);
                                break;
                            case CommandEventType.ReceiveData:
                                ProcessReceiveData((CommandReceiveData)command.CommandObject);
                                break;
                            case CommandEventType.GamerJoined:
                                ProcessGamerJoined((CommandGamerJoined)command.CommandObject);
                                break;
                            case CommandEventType.GamerLeft:
                                ProcessGamerLeft((CommandGamerLeft)command.CommandObject);
                                break;
                            case CommandEventType.SessionStateChange:
                                ProcessSessionStateChange((CommandSessionStateChange)command.CommandObject);
                                break;
                            case CommandEventType.SendGamerState:
                                ProcessSendGamerState((CommandSendGamerState)command.CommandObject);
                                break;
                            case CommandEventType.ReceiveGamerState:
                                ProcessReceiveGamerState((CommandReceiveGamerState)command.CommandObject);
                                break;
                            case CommandEventType.HostChange:
                                ProcessHostChange((CommandHostChange)command.CommandObject);
                                break;

                        }
                    }

                    ((ICommand)command.CommandObject).Dispose();
                }

                // When a player logs out of PSN, they automatically will leave any session they are 
                // currently a member of. 
                //
                // However, they will not be informed of this event, since they aren't on PSN...
                //
                // Note that if the player signing out is a secondary local player, we actually will
				// detect that they left, since its the primary local player's matching session
				// that is being updated.
				// 
				// However, we preemptively issue the 'player left' event here, before it comes in through
				// the matching session, because otherwise the game code may react to that player's
				// 'IsSignedIntoLive' being false, and we want them to receive the left event
				// first.
                //
				// Also, for the primary local player, NetworkSession never gets informed that
                // they left (and potentially that the session is destroyed, if they were also the host).
                // Because, the primary local player's matching session will never receive events after 
                // the sign out occurs. 
				//
				// Essentially the primary local player leaving the session, or being disconnected,
				// is equivalent to the session ending, from this machine/network-session's perspective.
				// In that case we fire SessionEnded rather than GamerLeft.

                foreach (var localGamer in _localGamers)
                {
                    if (localGamer == null)
                        continue;

                    var sig = localGamer.SignedInGamer;
                    if (!sig.IsSignedInToLive)
                    {
                        // secondary local players not implemented on switch

                        //var matchingSession = Sessions.FindByOnlineId(sig.Gamertag);
                        //var isPrimary = Sessions.IsPrimary(matchingSession);
                        //if (isPrimary)
                        {
                            Console.WriteLine("Primary local gamer is not signed in to PSN... enquing a SessionEnded event.");

                            var cmd = new CommandSessionStateChange(NetworkSessionState.Ended, SessionState);
                            var evt = new CommandEvent(CommandEventType.SessionStateChange, cmd);
                            _commandQueue.Enqueue(evt);
                        }
                        //else
                        //{
                        //    Console.WriteLine("Secondary local gamer is not signed in to PSN... enqueing a GamerLeft event.");

                        //    var cmd = new CommandGamerLeft(sig.Gamertag);
                        //    var evt = new CommandEvent(CommandEventType.GamerLeft, cmd);
                        //    _commandQueue.Enqueue(evt);
                        //}                        
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("NetworkSession.Update(); Exception: " + ex);

                // throw?
            }
        }

        /// <summary>
        /// For debugging / developer usage.        
        /// </summary>
        public static void DumpState()
        {
            MonoGame.Switch.Network.DumpState();

            /*
            if (_matchingSession != null)
                _matchingSession.DumpState();
            */

            if (_currentSession != null)
            {
                foreach (var m in _currentSession._machines)
                {
                    Console.WriteLine(m.ToString());
                    foreach (var g in m.Gamers)
                    {
                        Console.WriteLine(g.ToString());
                    }
                }
            }
        }

#endregion

#region Private and Internal Methods

        private void UpdatePia()
        {
            //if (_matchingSession == null)
            //return;

            // process rudp events, perform polling.
            //_matchingSession.Update();
            

            // handle session events
            {
                MonoGame.Switch.StationId stationId;
                MonoGame.Switch.NetEventKind eventKind;
                int resultCode;

                while (MonoGame.Switch.Network.TryGetEvent(out stationId, out eventKind, out resultCode))
                {
                    Console.WriteLine("MonoGame.Switch.Network Event: {0}", eventKind);

                    switch (eventKind)
                    {
                        case MonoGame.Switch.NetEventKind.Joined:
                            {
                                var netgamer = AllGamers.GetByStationId(stationId);
                                
                                if (netgamer != null)
                                {
                                    Console.WriteLine("Gamer joining is already in this session.");
                                }
                                else
                                {
                                    var hostStationId = MonoGame.Switch.Network.GetHostStationId();
                                    var localStationId = MonoGame.Switch.Network.GetLocalStationId();

                                    var isNewPlayerHost = stationId == hostStationId;
                                    var isNewPlayerLocal = stationId == localStationId;

                                    var isLocalPlayerHost = hostStationId == localStationId;

                                    string displayName = MonoGame.Switch.Network.GetPlayerName(stationId);
                                    string gamertag = string.Format("{0}+0x{1:X}", displayName, stationId);

                                    // since we have only one participant per station, the participant id IS the station id, for us
                                    // (otherwise, we'd have to actually iterate all participants at this station, and add a gamer-joined event for all of them)
                                    NetworkSessionParticipantId internalId = new NetworkSessionParticipantId(stationId.id);

                                    var cmd = new CommandGamerJoined(stationId, internalId, displayName, gamertag, isNewPlayerHost, isNewPlayerLocal);

                                    Console.WriteLine("Allocating CommandGamerJoined; gamerTag: {0}, stationId: {1}, isHost: {2}, isLocal: {3}",
                                        cmd.GamerTag, cmd.StationId, isNewPlayerHost, isNewPlayerLocal);

                                    var evt = new CommandEvent(CommandEventType.GamerJoined, cmd);
                                    _commandQueue.Enqueue(evt);
                                }

                                break;
                            }

                        case MonoGame.Switch.NetEventKind.Kicked:
                        case MonoGame.Switch.NetEventKind.Left:
                            {
                                //var gamertag = onlineId;
                                var netgamer = AllGamers.GetByStationId(stationId);

                                if (netgamer == null)
                                {
                                    Console.WriteLine("NetworkGamer {0} leaving this session was not found.", stationId);
                                    break;
                                }

                                var cmd = new CommandGamerLeft(stationId);

                                var evt = new CommandEvent(CommandEventType.GamerLeft, cmd);

                                _commandQueue.Enqueue(evt);

                                break;
                            }

                        case MonoGame.Switch.NetEventKind.RoomDestroyed:
                            {
                                var cmd = new CommandSessionStateChange(NetworkSessionState.Ended, SessionState);
                                var evt = new CommandEvent(CommandEventType.SessionStateChange, cmd);

                                _commandQueue.Enqueue(evt);

                                break;
                            }
                        case MonoGame.Switch.NetEventKind.RoomOwnerChanged:
                            {
                                var cmd = new CommandHostChange(stationId, _hostingGamer.StationId);
                                var evt = new CommandEvent(CommandEventType.HostChange, cmd);

                                _commandQueue.Enqueue(evt);

                                break;
                            }
                        default:
                            {
                                Console.WriteLine("Unhandled event type.");
                                break;
                            }
                    }
                }
            }

            // Handle received packets
            while (true)
            {
                MonoGame.Switch.UdpPacket packet;
                if (!MonoGame.Switch.Network.CheckReceivedData(out packet))
                    break;

                if (IsGamerStatePacket(packet.buffer))
                {
                    // Should probably be parsing the byte[] directly instead of all these allocations.
                    using (var reader = new BinaryReader(new MemoryStream(packet.buffer)))
                    {
                        var header = reader.ReadChars(_gamerStatusHeader.Length);
                        var gamertag = reader.ReadString();
                        var newState = (GamerStates)reader.ReadInt32();
                        var prevState = (GamerStates)reader.ReadInt32();

                        var netgamerModified = AllGamers.GetByGamertag(gamertag);
                        if (netgamerModified == null)
                            throw new Exception("NetworkSession.UpdateSce(); Received a CommandReceiveGamerState but found no NetworkPlayer named " + gamertag);

                        if (!netgamerModified.IsLocal)
                            newState &= ~GamerStates.Local;
                        else
                            newState |= GamerStates.Local;

                        var cmd = new CommandReceiveGamerState(netgamerModified, newState, prevState);
                        var evt = new CommandEvent(CommandEventType.ReceiveGamerState, cmd);

                        _commandQueue.Enqueue(evt);
                    }
                }
                else
                {
                    var cmd = new CommandReceiveData(packet.fromStationId, packet.toStationId, packet.buffer, 0, packet.size);
                    var evt = new CommandEvent(CommandEventType.ReceiveData, cmd);

                    _commandQueue.Enqueue(evt);
                }                
            }
        }

        private readonly char[] _gamerStatusHeader = "#GAMER_STATUS".ToCharArray();

        private bool IsGamerStatePacket(byte[] buffer)
        {            
            // TODO: 
            // Currently only gamer status messages have a header, which is used to differentiate them from data messages 
            // ...but this is hacky.
            //
            // All messages should have a header composed of a 4cc and a 1 byte message type code, followed by the message payload.
            // Reject unrecognized messages.
            // Eg,
            // M(char), G(char), H(char), 0(byte), (data message)
            // M(char), G(char), H(char), 1(byte), (gamer status message) 

            if (buffer.Length < _gamerStatusHeader.Length)
                return false;

            for (var i = 0; i < _gamerStatusHeader.Length; i++)
            {
                if (buffer[i] != _gamerStatusHeader[i])
                    return false;
            }

            return true;
        }

        internal static byte[] GetBuffer(int size)
        {
            // TODO:
            // This method was written with the intention of pooling buffers (for sending/receiving data).
            //
            // Ideally it would work that way, so continously sending/receiving packets during gameplay
            // is not causing GC churn.
            //
            // The issue I ran in to was... it got complex tracking what happens to these buffers and at what
            // point we know they are done being used, and who would handle returning them to the pool.
            // Hence, it is not yet implemented.            
            return new byte[size];
        }

        /// <summary>
        /// Send data to the specified NetworkGamer in this NetworkSession.
        /// If 'recipient' is null it is sent to all NetworkGamer(s).        
        /// </summary>
        internal void SendData(byte[] data, int offset, int length, SendDataOptions options, NetworkGamer recipient, LocalNetworkGamer sender)
        {
            var buffer = GetBuffer(length);
            Array.Copy(data, offset, buffer, 0, length);

            var cmd = new CommandSendData(data, 0, length, options, recipient, sender);
            var evt = new CommandEvent(CommandEventType.SendData, cmd);
            _commandQueue.Enqueue(evt);
        }

        private unsafe void ProcessSendData(CommandSendData cmd)
        {
#if DEBUG
            var sb = new StringBuilder();
            for (int i = 0; i <  cmd._data.Length; i++)
            {
                var byt = cmd._data[i];
                sb.Append(byt.ToString("X2"));
                if (i != cmd._data.Length - 1)
                    sb.Append(" ");
            }
            Console.WriteLine("Sending Data ({0}):", cmd._data.Length);
            Console.WriteLine("   " + sb.ToString());
#endif
            fixed (byte* pData = cmd._data)
            {
                byte* buffer = pData + cmd._offset;

                if (cmd._recipient == null)
                {
                    int numStations = _machines.Count;//MonoGame.Switch.Network.GetNumStations();
                    for (var i = 0; i < numStations; i++)
                    {
                        MonoGame.Switch.StationId toStationId = _machines[i].StationId;
                        MonoGame.Switch.StationId fromStationId = cmd._sender.StationId;

                        // JCF: Not sure if data sent to everyone includes the sender or not.
                        //if (cmd._sender.Gamertag == _machines[i].m.OnlineId)
                            //continue;

                        MonoGame.Switch.Network.SendData(fromStationId, toStationId, buffer, cmd._length);
                    }
                }
                else
                {
                    MonoGame.Switch.Network.SendData(cmd._sender.StationId, cmd._recipient.StationId, buffer, cmd._length);
                }
            }
        }

        private void ProcessReceiveData(CommandReceiveData cmd)
        {
#if DEBUG
            var sb = new StringBuilder();
            for (int i = 0; i < cmd._data.Length; i++)
            {
                var byt = cmd._data[i];
                sb.Append(byt.ToString("X2"));
                if (i != cmd._data.Length-1)
                    sb.Append(" ");
            }
            Console.WriteLine("Receiving Data ({0}):", cmd._data.Length);
            Console.WriteLine("   " + sb.ToString());
#endif
            // The public API for reading data is on LocalNetworkGamer
            // hence we just queue the data for processing there.

            foreach (var localGamer in LocalGamers)
            {
                lock (localGamer._receivedData)
                {
                    localGamer._receivedData.Enqueue(cmd);
                }
            }
        }

        private void ProcessSessionStateChange(CommandSessionStateChange cmd)
        {
            if (_sessionState == cmd.NewState)
                return;

            _sessionState = cmd.NewState;
            
            if (cmd.NewState == NetworkSessionState.Lobby && cmd.OldState == NetworkSessionState.Playing)
            {
                ResetReady();

                // Post changes to leaderboards.
				// JCF: XNA does in fact finalize writting of scores to leaderboards at this point.
				//      However we cannot make this very-slow, blocking call in the main thread.
				//      It is currently let up to the user's code to call CommitEntries whereever
				//      they can appropriately (probably from a task with an appropriate 'working'
				//      popup.
				/*
                foreach (var g in _localGamers)
                {
                    g.LeaderboardWriter.CommitEntries();
                }
				*/

                if (GameEnded != null)
                {
                    GameEnded(this, new GameEndedEventArgs());
                }
            }

            switch (cmd.NewState)
            {
                case NetworkSessionState.Lobby:
                {
                    if (cmd.OldState == NetworkSessionState.Playing)
                    {
                        if (_isHost)
                        {
							Locked = false;
							
							/*
                            var req = new ModifySessionRequest(_hostingGamer.UserId)
                            {
                                IsClosed = false,
                                IsHidden = false,                                
                            };
                            
                            var flags = _sessionProperties[NetworkSessionProperties.SessionFlags].Value;
                            flags &= ~NetworkSessionProperties.LockedFlag;
                            _sessionProperties[NetworkSessionProperties.SessionFlags] = flags;

                            req.AttributeType = SessionAttributeType.Search;                            
                            req.SetAttribute("SessionFlags", (uint)flags);

                            var npResult = Matching.ModifySession(req);
                            if (npResult != ToolkitResult.Ok)
                            {
                                Console.WriteLine("Matching.ModifySession returned error : " + npResult);

                                // Hopefully any error here is 'session doesn't exist, its already gone'
                                // not 'oops we now have an unlocked session in Playing state'...

                                //throw new NetErrorException(_hostingGamer.UserId, (int)npResult);
                            } 
                            */
                        }                        

                        ResetReady();

                        if (GameEnded != null)
                        {
                            GameEnded(this, new GameEndedEventArgs());
                        }
                    }                                        

                    break;
                }
                case NetworkSessionState.Ended:
                {
                    ResetReady();

                    if (SessionEnded != null)
                    {   
                        // Bad news is, we do not actually know if the host leaving the session
                        // is what triggered the session to end.
                        // So we make the following assumptions:
                        // 1. By default the reason is 'host left'.
                        // 2. If you do not currently have internet connection, the reason is 'disconnected'.                        
                        // 3. If the primary local player is not signed in to psn, the reason is 'disconnected'.
                        var reason = NetworkSessionEndReason.HostEndedSession;
                        string desc = "default";

                        if (!GamerServicesDispatcher.NetworkOnline)
                        {
                            desc = "not currently connected to the internet";
                            reason = NetworkSessionEndReason.Disconnected;
                        }
                        else
                        {
                            var g = _localGamers.FirstOrDefault();
                            if (g != null && !g.SignedInGamer._isSignedIntoPSN)
                            {
                                desc = string.Format("primary gamer {0} is not signed in to PSN", g.Gamertag);                                
                                reason = NetworkSessionEndReason.Disconnected;
                            }
                        }

                        Console.WriteLine("Triggering SessionEnded : reason={0}, desc={1}", reason, desc);
                        SessionEnded(this, new NetworkSessionEndedEventArgs(reason));
                    }

                    // JCF: Calling dispose here will block the main thread...
                    Dispose();

                    break;
                }
                case NetworkSessionState.Playing:
                {
                    if (_isHost)
                    {
						Locked = true;
						
                        /*
                        var req = new ModifySessionRequest(_hostingGamer.UserId)
                        {
                            IsClosed = true,
                            IsHidden = true,                            
                        };

                        var flags = _sessionProperties[NetworkSessionProperties.SessionFlags].Value;
                        flags |= NetworkSessionProperties.LockedFlag;
                        _sessionProperties[NetworkSessionProperties.SessionFlags] = flags;

                        req.AttributeType = SessionAttributeType.Search;
                        req.SetAttribute("SessionFlags", (uint)flags);

                        var npResult = Matching.ModifySession(req);
                        if (npResult != ToolkitResult.Ok)
                        {
                            Console.WriteLine("Matching.ModifySession returned error : " + npResult);

                            // Hopefully any error here is 'session doesn't exist, its already gone'
                            // not 'oops we now have an unlocked session in Playing state'...

                            //throw new NetErrorException(_hostingGamer.UserId, (int)npResult);
                        }
                        */
                    }

                    if (GameStarted != null)
                    {
                        GameStarted(this, new GameStartedEventArgs());
                    }
                    break;
                }
            }            
        }

        private void ProcessSendGamerState(CommandSendGamerState cmd)
        {
            Console.WriteLine("NetworkSession.ProcessSendGamerState(); Gamer={0}, IsRead={1}", cmd.Gamer.Gamertag, cmd.NewState.HasFlag(GamerStates.Ready));

            using (var writer = new PacketWriter())
            {
                writer.Write(_gamerStatusHeader);
                writer.Write(cmd.Gamer.Gamertag);
                writer.Write((int)cmd.NewState);
                writer.Write((int)cmd.PrevState);

                writer.Flush();

                SendData(writer.Data, 0, writer.Data.Length, SendDataOptions.ReliableInOrder, null, cmd.Gamer);
            }
        }

        private void ProcessReceiveGamerState(CommandReceiveGamerState cmd)
        {
            Console.WriteLine("NetworkSession.ProcessReceiveGamerState(); Gamer={0}, IsRead={1}", cmd.Gamer.Gamertag, cmd.NewState.HasFlag(GamerStates.Ready));

            cmd.Gamer.Set(cmd.NewState);
        }

        private void ProcessGamerJoined(CommandGamerJoined cmd)
        {
            Console.WriteLine("NetworkSession.ProcessGamerJoined(); StationId: {0}, DisplayName: {1}, GamerTag: {2}, IsLocal: {3}, IsHost: {4}, InternalId: {5}",
                cmd.StationId, cmd.DisplayName, cmd.GamerTag, cmd.State.HasFlag(GamerStates.Local), cmd.State.HasFlag(GamerStates.Host), cmd.InternalId);

            NetworkGamer gamer = null;

            foreach (var g in _allGamers)
            {
                if (g.Id == cmd.InternalId)
                {
                    Console.WriteLine("NetworkSession.ProcessGamerJoined(); gamer already exists in _allGamers.");
                    gamer = g;
                    break;
                }
            }

            if (gamer == null)
            {
                if ((cmd.State & GamerStates.Local) != 0)
                {
                    // jcf: hack... how do we match these two up anyway?
                    //      Note: this only works because we only have one local player per session allowed, currently.
                    var sig = Gamer.SignedInGamers.GetByPlayerIndex(PlayerIndex.One);
                    if (sig == null)
                        throw new Exception("NetworkSession.ProcessGamerJoined(); Gamer.SignedInGamers does not contain gamertag " + cmd.GamerTag);

                    // now we actually have a stationid...
                    sig.Gamertag = cmd.GamerTag;
                    sig.StationId = cmd.StationId;

                    gamer = new LocalNetworkGamer(this, sig, cmd.InternalId, cmd.State);
                    gamer.DisplayName = cmd.DisplayName;
                    gamer.UserId = sig.UserId;
                    gamer.OnlineId = sig.OnlineId;
                    gamer.StationId = cmd.StationId;
                    gamer.Gamertag = cmd.GamerTag;

                    //System.Diagnostics.Debug.Assert(cmd.StationId == sig.StationId);
                    //System.Diagnostics.Debug.Assert(cmd.GamerTag == ((Gamer)sig).GamerTag);

                    _allGamers.AddGamer(gamer);
                    _localGamers.AddGamer((LocalNetworkGamer)gamer);

                    var machine = GetLocalMachine();
                    machine.Gamers.AddGamer(gamer);
                    gamer.Machine = machine;
                }
                else
                {
                    gamer = new NetworkGamer(this, cmd.InternalId, cmd.State);
                    gamer.DisplayName = cmd.DisplayName;
                    gamer.Gamertag = cmd.GamerTag;
                    gamer.OnlineId = new MonoGame.Switch.OnlineId(cmd.StationId.id);
                    gamer.StationId = cmd.StationId;

                    _allGamers.AddGamer(gamer);
                    _remoteGamers.AddGamer(gamer);

                    //var member = _matchingSession.GetMemberByOnlineId(cmd.GamerTag);
                    //var conInfo = member.ConnectionInfo;

                    var machine = GetRemoteMachine(cmd.StationId);
                    machine.Gamers.AddGamer(gamer);
                    gamer.Machine = machine;
                }

                if ((cmd.State & GamerStates.Host) != 0)
                    _hostingGamer = gamer;
                                                
                if (_gamerJoined != null)
                    _gamerJoined(this, new GamerJoinedEventArgs(gamer));                                           
            }
        }

        private NetworkMachine GetLocalMachine()
        {
            foreach (var m in _machines)
            {
                if (m.Gamers.Count > 0 && m.Gamers[0].IsLocal)
                    return m;
            }

            var mach = new NetworkMachine()
            {
                StationId = MonoGame.Switch.Network.GetLocalStationId(),
            };
            _machines.Add(mach);

            return mach;
        }

        private NetworkMachine GetRemoteMachine(MonoGame.Switch.StationId stationId)
        {
            foreach (var m in _machines)
            {
                if (m.StationId == stationId)
                    return m;
            }

            var mach = new NetworkMachine()
            {
                StationId = stationId,
            };
            _machines.Add(mach);

            return mach;
        }

        private void ProcessGamerLeft(CommandGamerLeft cmd)
        {
            Console.WriteLine("NetworkSession.ProcessGamerLeft(); StationId={0}", cmd.StationId);

            var gamer = AllGamers.GetByStationId(cmd.StationId);
            if (gamer == null)
            {
                Console.WriteLine("NetworkSession.ProcessGamerLeft(); gamer was not found, skipping.");
                return;
            }

            _allGamers.RemoveGamer(gamer);

            if (gamer is LocalNetworkGamer)
            {
                _localGamers.RemoveGamer((LocalNetworkGamer)gamer);

                //int retCode = MonoGame.Switch.Network.Leave(gamer.Gamertag);
                //Console.WriteLine("Sessions.Leave() returned {0}", retCode);
            }
            else
            {
                _remoteGamers.RemoveGamer(gamer);

                gamer.Machine.Gamers.RemoveGamer(gamer);
                if (gamer.Machine.Gamers.Count == 0)
                {
                    _machines.Remove(gamer.Machine);
                }
            }

            if (GamerLeft != null)
            {
                GamerLeft(this, new GamerLeftEventArgs(gamer));
            }

            gamer.Dispose();
        }

        private void ProcessHostChange(CommandHostChange cmd)
        {
            Console.WriteLine("NetworkSession.ProcessHostChange(); NewHost={0}, OldHost={1}", cmd.NewHost, cmd.OldHost);

            var newHost = AllGamers.GetByStationId(cmd.NewHost);
            var oldHost = AllGamers.GetByStationId(cmd.OldHost);

            if (newHost == null)
            {
                Console.WriteLine("Warning: Gamer for NewHost with StationId '{0}' was not found.", cmd.NewHost);
            }
            else
            {
                newHost._gamerState |= GamerStates.Host;
            }

            if (oldHost != null)
                oldHost._gamerState &= ~GamerStates.Host;

            _hostingGamer = newHost;
            _isHost = _hostingGamer.IsLocal;

            var evt = HostChanged;
            if (evt != null)
            {    
                var arg = new HostChangedEventArgs(newHost, oldHost);
                evt(this, arg);
            }
        }

#endregion
    }
}
