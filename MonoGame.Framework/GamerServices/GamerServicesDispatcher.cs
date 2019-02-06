
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework.Net;
#if SWITCH
#else
using Sce.PlayStation4.Network;
using Sce.PlayStation4.Network.ToolkitNp;
using Sce.PlayStation4.System;
#endif

namespace Microsoft.Xna.Framework.GamerServices
{
    public static partial class GamerServicesDispatcher
    {
        private static bool _networkOnline;
        private static ThreadSafeQueue<IGamerServicesEvent> _events;
        private static Stopwatch _stopwatch;


        public static bool NetworkOnline
        {
            get { return _networkOnline; }
        }
        
        public static bool IsInitialized { get; private set; }

        public static IntPtr WindowHandle { get; set; }  

        public static unsafe void Initialize(IServiceProvider serviceProvider, sbyte defaultAge)
        {
            Console.WriteLine("GamerServicesDispatcher.Initialize()");
            //Extensions.PrintCallstack();
			
            if (IsInitialized)
                throw new Exception("GamerServicesDispatcher.Initialize(); Error: Already initialized");

            _events = new ThreadSafeQueue<IGamerServicesEvent>();

            //SystemService.OnEvent += OnSystemServiceEvent;

            //UserService.OnLogin += UserService_OnLogin;
            //UserService.OnLogout += UserService_OnLogout;
            MonoGame.Switch.UserService.Initialize();
            MonoGame.Switch.Ranking.Initialize();

            _networkOnline = MonoGame.Switch.Network.ConnectedToInternet();
            Console.WriteLine("GamerServicesDispatcher.Initialize(); ConnectedToInternet={0}", _networkOnline);

            //            NetCtlState netState;
            //            int netRes = NetCtl.GetState(out netState);
            //            if (netRes < 0)
            //                throw new Exception("GamerServicesDispatcher.Initialize(); NetCtrl.GetState returned error : " + netRes);

            //            Console.WriteLine("GamerServicesDispatcher.Initialize(); NetCtlState={0}", netState);
            //            _networkOnline = netState == NetCtlState.StateIpObtained;

            //            var tkres = ToolkitNpLibrary.Initialize();
            //            if (tkres != ToolkitResult.Ok)
            //                throw new Exception("GamerServicesDispatcher.Initialize(); ToolkitNpLibrary.Initialize returned error : " + tkres);

            //            Console.WriteLine("GamerServicesDispatcher.Initialize(); ToolkitNpLibrary.Initialize returned {0}", tkres);

            //            // ESRB rating : T (13+)
            //            // PEGI rating : 7 (7+)
            //            // Since we are not submitting different versions for different regions
            //            // we use the least restrictive.
            //            var npres = Np.SetContentRestriction(defaultAge);
            //            /*
            //#if SCEE
            //            var npres = Np.SetContentRestriction(13);            
            //#else
            //            var npres = Np.SetContentRestriction(defaultAge);            
            //#endif
            //             * */
            //            if(npres != NpResult.Ok)
            //                throw new Exception("GamerServicesDispatcher.Initialize(); Np.SetContentRestriction returned error : " + npres);

            //			Console.WriteLine("GamerServicesDispatcher.Initialize(); Np.SetContentRestriction returned {0}", npres);            

            //            tkres = Matching.Initialize(NetworkSessionProperties.AttributeConfig);
            //            if (tkres != ToolkitResult.Ok)
            //                throw new Exception("GamerServicesDispatcher.Initialize(); Matching.Initialize returned error : " + tkres);  

            //            Console.WriteLine("GamerServicesDispatcher.Initialize(); Matching.Initialize returned {0}", tkres);

            _stopwatch = new Stopwatch();
            _stopwatch.Start();

            IsInitialized = true;

            /*
            npres = Np.RegisterPlusEventCallback(Np_PlusEventCallback);
            if (npres != NpResult.Ok)
            {
                var msg = string.Format("GamerServicesDispatcher.Initialize(); Np.RegisterPlusEventCallback returned error : {0:x6}", npres);
                Console.WriteLine(msg);
                throw new Exception(msg);
            }
            */

            /*
            npres = Np.RegisterStateCallback(Np_StateCallback);
            if (npres != NpResult.Ok)
            {
                var msg = string.Format("GamerServicesDispatcher.Initialize(); Np.RegisterStateCallback returned error : {0:x6}", npres);
                Console.WriteLine(msg);
                throw new Exception(msg);
            }
            */
        }
        /*
        private static void Np_PlusEventCallback(int userid, NpPlusEventType eventtype)
        {
            Console.WriteLine("Np_PlusEventCallback(); userId={0}, eventtype={1}", userid, eventtype);
        }
        */

        /*
        private static unsafe void Np_StateCallback(int userid, NpState state, IntPtr npidhandle, byte* npidopt)
        {
            Console.WriteLine("Np_StateCallback(); userId={0}, state={1}", userid, state);        
        }
        */

        //private static void OnSystemServiceEvent(SystemServiceEvent evt)
        //{
        //    Console.WriteLine("GamerServicesDispatcher.OnSystemServiceEvent(); evtType=" + evt.EventType);

        //    if (evt.EventType == SystemServiceEventType.SessionInvitation)
        //    {
        //        var invitationEvent = evt.AsSessionInvitation();
        //        var obj = new Tuple<string, string>(invitationEvent.RecipientOnlineId, invitationEvent.SessionId);
        //        NetworkSession.HandleInvitationAccepted(obj);
        //    }
        //}

        public static void Update()
        {
            var dt = (float)_stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();
            Update(dt);
        }

        internal static void Update(float elapsedSeconds)
        {
            MonoGame.Switch.UserService.Update(elapsedSeconds);
            MonoGame.Switch.Network.Update(elapsedSeconds);
            MonoGame.Switch.Ranking.Update();

            //SystemService.Update(true);

            IGamerServicesEvent e;
            while (_events.TryDequeue(out e))
                e.Dispatch();

            //FlushSwitchEvents();

            // JCF: This is not technically correct for all games.
            //      Really the flag for 'is using realtime multiplayer' should be
            //      per gamer, because just because a gamer is signed in doesn't 
            //      necessarily mean they are playing/participating in the current game/session.
            //
            //      Ideally such a flag could be set automatically when they
            //      join/create a NetworkSession and unset when it is disposed.
            /*
            if (Guide.RealtimeMultiplayerInUse)
            {
                foreach (var g in SignedInGamer.SignedInGamers)
                {                  
                    Np.NotifyPlusFeature(g.UserId, NpPlusFeature.RealtimeMultiplay);
                }
            }
            */

#if PLAYSTATION4
            var ret = Np.CheckCallback();
            if (ret != NpResult.Ok)
            {
                var msg = string.Format("GamerServicesDispatcher.Update(); Np.CheckCallback returned error : {0:x6}", ret);
                Console.WriteLine(msg);

                throw new Exception(msg);
            }
#endif
        }

        private static void FlushPlatformEvents()
        {
#if PLAYSTATION4
            var e = new ToolkitUserEvent();
            while (ToolkitNpLibrary.TryGetEvent(ref e))
            {
                Console.WriteLine("GamerServicesDispatcher.FlushToolkitEvents(); userId={0}, onlineId={1}, state={2}, eventType={3}", e.UserId.NullOrToString(), e.OnlineId.NullOrToString(), e.UserState, e.Type);                                

                switch (e.Type)
                {
                    case ToolkitUserEventType.LoggedIntoPsn:
                    {
                        var gamer = SignedInGamer.SignedInGamers.GetByUserId(e.UserId);
                        if (gamer == null)
                        {
                            throw new Exception(
                                string.Format("GamerServicesDispatcher.FlushToolkitEvents(); error : no SignedInGamer with id={0} was found.",
                                    e.UserId));
                        }
                        gamer._isSignedIntoPSN = true;

                        string onlineId;
                        if (Np.GetOnlineId(gamer.UserId, out onlineId) == NpResult.Ok)
                            gamer.Gamertag = onlineId;

                        break;
                    }                        
                    case ToolkitUserEventType.LoggedOutOfPsn:
                    {
                        var gamer = SignedInGamer.SignedInGamers.GetByUserId(e.UserId);
                        if (gamer == null)
                        {
                            throw new Exception(
                                string.Format("GamerServicesDispatcher.FlushToolkitEvents(); error : no SignedInGamer with id={0} was found.",
                                    e.UserId));
                        }
                        gamer._isSignedIntoPSN = false;
                        break;
                    }
                    case ToolkitUserEventType.NetworkDown:
                    {
                        _networkOnline = false;
                        break;
                    }
                    case ToolkitUserEventType.NetworkUp:
                    {
                        _networkOnline = true;

                        foreach (var g in SignedInGamer.SignedInGamers)
                        {
                            string onlineId;
                            if (Np.GetOnlineId(g.UserId, out onlineId) == NpResult.Ok)
                                g.Gamertag = onlineId;
                        }
                        break;
                    }
                    default:
                        break;
                }
            }
#endif
        }

        //internal static void Network_OnSessionEvent(MonoGame.Switch.StationId stationId, MonoGame.Switch.NetEventKind kind, int resultCode)
        //{
        //    var session = NetworkSession.GetCurrentSession();
        //    if (session != null)
        //    {
        //        MonoGame.Switch.Network.PushEvent(stationId, kind, resultCode);// session._commandQueue.Enqueue()
        //    }
        //}

        // Called by native code when a local user account connects to online services
        internal static void Network_OnServiceConnection(MonoGame.Switch.UserHandle userHandle)
        {
            Console.WriteLine("Network_OnServiceConnection");

            var gamer = SignedInGamer.SignedInGamers.GetByUserId(userHandle.id);
            if (gamer == null)
            {
                throw new Exception(
                    string.Format("GamerServicesDispatcher.Network_OnServiceConnection(); error : no SignedInGamer with id={0} was found.",
                        userHandle.id));
            }
            gamer._isSignedIntoPSN = true;

            MonoGame.Switch.OnlineId onlineId = MonoGame.Switch.UserService.GetLocalUserOnlineId(userHandle);
            gamer.OnlineId = onlineId;
            gamer.LeaderboardId = onlineId.id;

            // jcf: actually, the station id doesn't exist yet, that happens when a game is hosted/joined
            gamer.StationId = MonoGame.Switch.StationId.Invalid;

            // since gamertag has to be unique, and the switch 'nickname' doesn't, we append the onlineid to the end of it
            string nickname = MonoGame.Switch.UserService.GetLocalUserNickname(userHandle);
            gamer.Gamertag = string.Format("{0}+0x{1:X}", nickname, onlineId.id);
        }

        // Called by native code when a local user account disconnects from online services
        internal static void Network_OnServiceDisconnection(MonoGame.Switch.UserHandle userHandle)
        {
            Console.WriteLine("Network_OnServiceDisconnection");

            var gamer = SignedInGamer.SignedInGamers.GetByUserId(userHandle.id);
            if (gamer == null)
            {
                throw new Exception(
                    string.Format("GamerServicesDispatcher.Network_OnServiceDisconnection(); error : no SignedInGamer with id={0} was found.",
                        userHandle.id));
            }
            gamer._isSignedIntoPSN = false;
            gamer.StationId = MonoGame.Switch.StationId.Invalid;
            gamer.OnlineId = MonoGame.Switch.OnlineId.Invalid;
            gamer.LeaderboardId = 0;
            gamer.Gamertag = gamer.DisplayName;
        }

        // Called by native code when a local user account is closed
        internal static void UserService_OnClosed(MonoGame.Switch.UserHandle userHandle, int playerIndex)
        {
            Console.WriteLine("UserService_OnClosed( userId={0}, playerIndex={1} )", userHandle, playerIndex);

            var gamer = Gamer.SignedInGamers.GetByUserId(userHandle.id);
            if (gamer == null)
                throw new Exception();

            var e = new GamerSignOutEvent()
            {
                Sender = null,
                Args = new SignedOutEventArgs(gamer),
            };
            _events.Enqueue(e);
        }

        // Called by native code when a local user account is opened
        internal static void UserService_OnOpened(MonoGame.Switch.UserHandle userHandle, int playerIndex)
        {
            Console.WriteLine("UserService_OnOpened( userId={0}, playerIndex={1} )", userHandle, playerIndex);

            var name = MonoGame.Switch.UserService.GetLocalUserNickname(userHandle);
            var onlineId = MonoGame.Switch.UserService.GetLocalUserOnlineId(userHandle);

            // jcf: all local users have the same stationid.
            //      note that this will actually be StationId.Invalid until the service is actually started
            var stationId = MonoGame.Switch.StationId.Invalid;// MonoGame.Switch.Network.GetLocalStationId();

            var isGuest = false;
            
            var privileges = new GamerPrivileges();
            privileges._authorized = false;

            if (_networkOnline)
            {
                /*
                var ret = Np.CheckNpAvailability(onlineId);
                Console.WriteLine("Np.CheckNpAvailability() returned : " + ret);
                if (ret == NpResult.Ok)
                {
                    privileges._hasPlus = true;
                    privileges._oldEnough = true;
                }
                */
            }

            var gamer = new SignedInGamer
            {
                UserId = userHandle.id,
                OnlineId = onlineId,
                StationId = stationId,
                Gamertag = name,
                DisplayName = name,
                Privileges = privileges,
                IsGuest = isGuest,                            
            };

            //gamer.InstallAchievements();

            Console.WriteLine("UserService_OnOpened(); onlineId={0}, userName:{1}", onlineId.NullOrToString(), name.NullOrToString());

            var item = new GamerSignInEvent()
            {
                Sender = null,
                Args = new SignedInEventArgs(gamer),
            };
            _events.Enqueue(item);
        }
    }
}
