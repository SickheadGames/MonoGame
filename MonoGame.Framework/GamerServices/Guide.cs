#region License
/*
Microsoft Public License (Ms-PL)
MonoGame - Copyright © 2009 The MonoGame Team

All rights reserved.

This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
accept the license, do not use the software.

1. Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
U.S. copyright law.

A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

3. Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
purpose and non-infringement.
*/
#endregion License

#region Using clause
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;
using System.Runtime.CompilerServices;
#if SWITCH
#else
using Sce.PlayStation4.Input;
using Sce.PlayStation4.Network;
using Sce.PlayStation4.Network.ToolkitNp;
using Sce.PlayStation4.System;
#endif

#endregion Using clause

namespace Microsoft.Xna.Framework.GamerServices
{


	public static class Guide
    {
#region Private Data

        private static bool _isScreenSaverEnabled;
        private static bool _isTrialMode;
        private static int _isVisible;
        private static bool _simulateTrialMode;	  

#endregion

#region Properties
        public static bool IsScreenSaverEnabled
        {
            get
            {
                return _isScreenSaverEnabled;
            }
            set
            {
                _isScreenSaverEnabled = value;
            }
        }

        public static bool IsTrialMode
        {
            get
            {
                return _isTrialMode;
            }
            set
            {
                _isTrialMode = value;
            }
        }

        public static bool IsVisible
        {
            get
            {
                return _isVisible > 0;
            }
            set
            {                
                //Console.WriteLine("Guide.Visible={0} (was:{1})", value, _isVisible);
                //Extensions.PrintCallstack();

                if (value)
                    _isVisible++;
                else
                {
                    _isVisible--;
                    if (_isVisible < 0)
                        _isVisible = 0;
                }                
            }
        }

        public static bool SimulateTrialMode
        {
            get
            {
                return _simulateTrialMode;
            }
            set
            {
                _simulateTrialMode = value;
            }
        }

        public static GameWindow Window
        {
            get;
            set;
        }
        #endregion

        #region Show Keyboard Input

        #region Privates

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern static string GetKeyboardInput(
            PlayerIndex playerIndex,
            string title,
            string description,
            string defaultText,
            bool usePasswordMode);
     
        #endregion

        public static IAsyncResult BeginShowKeyboardInput(PlayerIndex playerIndex,
                                                          string title,
                                                          string description,
                                                          string defaultText,
                                                          AsyncCallback callback,
                                                          object state)
        {
            return BeginShowKeyboardInput(playerIndex, title, description, defaultText, callback, state, false);
        }

        public static IAsyncResult BeginShowKeyboardInput(PlayerIndex playerIndex,
                                                          string title,
                                                          string description,
                                                          string defaultText,
                                                          AsyncCallback callback,
                                                          object state,
                                                          bool usePasswordMode)
        {
            Console.WriteLine("Guide.BeginShowKeyboardInput() playerIndex={0}, title={1}, description={2}", playerIndex, title, description);

            IsVisible = true;

            var task = new Task<string>(
                () =>
                {
                    Console.WriteLine("BeginJoin - inside task");
                    return GetKeyboardInput(playerIndex, title, description, defaultText, usePasswordMode);
                });
            task.Start();

            return task.AsApm(callback, state);
        }        

        public static string EndShowKeyboardInput(IAsyncResult result)
        {
            Console.WriteLine("Guide.EndShowKeyboardInput()");

            string returnValue = null;
            try
            {
                returnValue = ((Task<string>)result).Result;
            }
            finally
            {
                IsVisible = false;
            }

            return returnValue;
        }

#endregion

#region Show Message Box

        public static IAsyncResult BeginShowMessageBox(string title,
                                               string text,
                                               IEnumerable<string> buttons,
                                               int focusButton,
                                               MessageBoxIcon icon,
                                               AsyncCallback callback,
                                               object state)
        {
            throw new NotImplementedException();
        }

        public static IAsyncResult BeginShowMessageBox(PlayerIndex player,
                                                       string title,
                                                       string text,
                                                       IEnumerable<string> buttons,
                                                       int focusButton,
                                                       MessageBoxIcon icon,
                                                       AsyncCallback callback,
                                                       object state)
        {
            throw new NotImplementedException();
        }

        public static int? EndShowMessageBox(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

#endregion

#region Show Misc

        public static void ShowGamerCard(PlayerIndex playerIndex, Gamer gamer)
        {
            IsVisible = true;
            var task = new Task(() => DoShowGamerCard(playerIndex, gamer));
            task.Start();
        }

        private static void DoShowGamerCard(PlayerIndex playerIndex, Gamer gamer)
	    {
            Console.WriteLine("Guide.ShowGamerCard() playerIndex={0}, gamer={1}", playerIndex, gamer.NullOrGamertag());

            try
            {
                var localGamer = SignedInGamer.SignedInGamers.GetByPlayerIndex(playerIndex);
                if (localGamer == null)
                    throw new Exception(string.Format("Guide.ShowGamerCard(); SignedInGamer with {0} not found.", playerIndex));

                var targetGamer = gamer;
                if (targetGamer == null)
                    throw new Exception("Guide.ShowGamerCard(); targetGamer is null.");

                if (string.IsNullOrEmpty(targetGamer.Gamertag))
                    throw new Exception("Guide.ShowGamerCard(); targetGamer.Gamertag is null.");

                // Not implemented.
                //if (player.Privileges.AllowProfileViewing)
                //throw new GamerPrivilegeException();

#if PLAYSTATION4
            NpProfileDialog.Initialize();

            var res = NpProfileDialog.Open(NpProfileDialogMode.Normal, localGamer.UserId, targetGamer.Gamertag, 0);
            if (res != CommonDialogError.Ok)
                Console.WriteLine("Guide.ShowGamerCard(); localGamer={0}, targetGamer={1}; returned error : {2}", localGamer.NullOrGamertag(), gamer.NullOrGamertag(), res);

            var status = NpProfileDialog.UpdateStatus();
            while (status == CommonDialogStatus.Running)
            {
                Thread.Sleep(20);
                status = NpProfileDialog.UpdateStatus();
            }

            NpProfileDialog.Terminate();
#endif
#if SWITCH
                int resultCode = MonoGame.Switch.Friends.ShowUserDetailInfo(localGamer.UserId, targetGamer.OnlineId, localGamer.DisplayName, targetGamer.DisplayName);
#endif
            }
            catch( Exception e)
            {
                Console.WriteLine("Exception occured within Guide.DoShowGamerCard():");
                Console.WriteLine(e);
            }
            finally
            {
                IsVisible = false;
            }
	    }

        public static void ShowMarketplace(PlayerIndex playerIndex)
        {
            Console.WriteLine("Guide.ShowMarketplace() playerIndex={0}", playerIndex);

            throw new NotImplementedException();
        }

        public static void ShowSignIn(int paneCount, bool onlineOnly)
        {
            Console.WriteLine("Guide.ShowSignIn() paneCount={0}, onlineOnly={1}", paneCount, onlineOnly); 

            throw new NotImplementedException();
        }

	    public static void ShowGameInvite(string sessionId)
	    {
	        Console.WriteLine("Guide.ShowGameInvite() sessionId={0}", sessionId);

            throw new NotImplementedException();
	    }

        public static void ShowGameInvite(PlayerIndex playerIndex, IEnumerable<Gamer> recipients)
        {
            Console.WriteLine("Guide.ShowGameInvite() playerIndex={0}, recipients={1}", playerIndex, recipients == null ? "[null]" : recipients.Count().ToString());

            //if (NetworkSession._inviteAccepted.GetInvocationList().Length == 0)
            //{
            //    throw new Exception("Guide.ShowGameInvite(); Error: Not subscribed to NetworkSession.AcceptInvite.");
            //}
#if SWITCH
            throw new NotImplementedException();
#endif
#if PLAYSTATION4
            var session = NetworkSession.GetCurrentSession();
            if (session == null)
            {
                throw new Exception("Guide.ShowGameInvite(); Error: No active NetworkSession exists.");
            }

            if (recipients != null)
            {
                throw new NotImplementedException("Guide.ShowGameInvite(); Error: sending to a specific recipient is not implemented, pass null!");
            }

            /*
            var maxSlots = this.GetLobbyData().Value.GetLobbyType() == GameLobbyType.QuickSkirmish ? 2 : 4;
            var maxFriends = maxSlots - this.playersInLobby.Count;
            if (maxFriends == 0)
            {
                Global.LogFormattedInfo("PSNLOBBY", "Ignoring request to show invite dialog, lobby is full.");
                return;
            }
            */

            var task = System.Threading.Tasks.Task.Factory.StartNew(() => DoInvite(session));            
            task.ContinueWith(t => t.Exception.Handle(e => true), TaskContinuationOptions.OnlyOnFaulted);
#endif
        }

#if PLAYSTATION4
        private static void DoInvite(NetworkSession session)
	    {
            // Who is sending the invite??
	        var userId = UserService.InitialUser;

	        var maxInvites = session.MaxGamers - session.AllGamers.Count;

	        if (maxInvites <= 0)
	        {
	            Console.WriteLine("Ignoring request to show invite dialog, session is full.");
	            return;
	        }

            var msg = new InviteMessage(userId, maxInvites)
	        {                
	            AllowEditRecipients = true,
	            Body = "Join my game!",
                Icon = @"/app0/Content/session_image.jpg",
	        };

            Console.WriteLine("Guide.DoInvite(); userId={0}, maxFriends={1}", userId, maxInvites);

	        var result = Matching.InviteToSession(session._matchingSession, msg);

	        if (result != ToolkitResult.Ok)
	        {
	            Console.WriteLine("Friend invite call failed with error code " + result.ToString("X"));
	        }          
	    }
#endif

#endregion

#region Multiplayer Available

        public static bool MultiplayerAvailable(SignedInGamer gamer, MonoGame.Switch.NetworkMode mode)
        {
            if (!GamerServicesDispatcher.NetworkOnline)
                return false;

            if (!gamer.IsSignedInToLive)
                return false;

            if (!gamer.Privileges.AllowOnlineSessions)
                return false;

            if (!MonoGame.Switch.Network.IsAvailable(mode))
                return false;

            return true;
        }

        public static bool MultiplayerAvailable(IEnumerable<SignedInGamer> gamers, MonoGame.Switch.NetworkMode mode)
        {
            foreach (var g in gamers)
            {
                if (!MultiplayerAvailable(g, mode))
                    return false;
            }

            return true;                   
        }

        private static bool _realtimeMultiplayerInUse;

        public static bool RealtimeMultiplayerInUse
        {
            get { return _realtimeMultiplayerInUse; }
            set
            {
                _realtimeMultiplayerInUse = value;
            }
        }

        //public delegate bool UpdateMultiplayerAvailableDelegate(SignedInGamer gamer, MonoGame.Switch.NetworkMode mode);
        //public static IAsyncResult BeginUpdateMultiplayerAvailable(SignedInGamer gamer, AsyncCallback callback, Object asyncState)
        //{
        //    Console.WriteLine("Guide.BeginUpdateMultiplayerAvailable(); gamer={0}", gamer.NullOrGamertag());

        //    if (gamer == null)
        //        throw new ArgumentNullException();

        //    UpdateMultiplayerAvailableDelegate del = UpdateMultiplayerAvailable;
        //    return del.BeginInvoke(gamer, callback, asyncState);
        //}
        //public static bool EndUpdateMultiplayerAvailable(IAsyncResult result)
        //{
        //    Console.WriteLine("Guide.EndUpdateMultiplayerAvailable();");

        //    bool returnValue = false;
        //    try
        //    {                
        //        var asyncResult = (AsyncResult)result;
                
        //        result.AsyncWaitHandle.WaitOne();

        //        if (asyncResult.AsyncDelegate is UpdateNpAvailableDelegate)
        //            returnValue = ((UpdateNpAvailableDelegate)asyncResult.AsyncDelegate).EndInvoke(result);                
        //    }
        //    finally
        //    {
        //        result.AsyncWaitHandle.Close();
        //    }
        //    return returnValue;
        //}

	    public static bool UpdateMultiplayerAvailable(IEnumerable<SignedInGamer> gamers, MonoGame.Switch.NetworkMode mode)
	    {
	        foreach (var g in gamers)
	        {
	            if (!UpdateMultiplayerAvailable(g, mode))
	                return false;
	        }

	        return true;
	    }

        public static bool UpdateMultiplayerAvailable(SignedInGamer gamer, MonoGame.Switch.NetworkMode mode)
	    {
            Console.WriteLine("Guide.UpdateMultiplayerAvailable(); gamer={0}, mode={1}", gamer.NullOrGamertag(), mode);

            IsVisible = true;	        
            try
            {
                var userId = gamer.UserId;

                int startResult = MonoGame.Switch.Network.TryStart(userId, mode);
                if (startResult != 0)
                {
                    Console.WriteLine("Network.TryStart returned {0} for user '{0}'.", startResult, gamer.NullOrGamertag());

                    gamer.Privileges._authorized = false;

                    return false;
                }

                gamer.Privileges._authorized = true;

                return true;
            }
            finally
            {
                Console.WriteLine("Guide.UpdateMultiplayerAvailable(); finally");
                IsVisible = false;                
            }   
	    }

#if SWITCH
        private static void ShowErrorDialog(int userId, int errorCode)
        {
            //ErrorDialog.Initialize();
            //ErrorDialog.Open(errorCode, userId);

            //while (ErrorDialog.UpdateStatus() == ErrorDialogStatus.Running)
            //    Thread.Sleep(10);

            //ErrorDialog.Terminate();
        }

        public static void ShowErrorAsync(NetErrorException e)
        {
            //ShowErrorAsync(e.UserId, e.Code);
        }

        public static void ShowErrorAsync(int userId, int errorCode)
        {
            //var task = new Task(() => DoShowErrorAsync(userId, errorCode));
            //task.Start();
        }

        private static void DoShowErrorAsync(int userId, int errorCode)
        {
            //IsVisible = true;

            //ErrorDialog.Initialize();
            //ErrorDialog.Open(errorCode, userId);

            //while (ErrorDialog.UpdateStatus() == ErrorDialogStatus.Running)
            //    Thread.Sleep(10);

            //ErrorDialog.Terminate();

            //IsVisible = false;
        }
#endif
#if PLAYSTATION4
        private static void ShowErrorDialog(int userId, int errorCode)
        {
            ErrorDialog.Initialize();
            ErrorDialog.Open(errorCode, userId);

            while (ErrorDialog.UpdateStatus() == ErrorDialogStatus.Running)
                Thread.Sleep(10);

            ErrorDialog.Terminate();
        }

        private static bool IsPlayStationPlus(int userId)
        {
            // PlayStation Plus member?
            bool plusAvail;
            if (Np.CheckPlus(userId, NpPlusFeature.RealtimeMultiplay, out plusAvail) != NpResult.Ok)
                return false;

            if (plusAvail)
                return true;

            Console.WriteLine("PlayStation Plus not available. Launching commerce dialog.");

            NpCommerceDialog.Initialize();
            NpCommerceDialog.OpenPlus(NpPlusFeature.RealtimeMultiplay, userId);
            while (NpCommerceDialog.UpdateStatus() == CommonDialogStatus.Running)
                Thread.Sleep(10);

            bool isAuthorized;
            NpCommerceDialog.GetResult(out isAuthorized);
            Console.WriteLine("PlayStation Plus Purchase " + (isAuthorized ? "is authorized" : "NOT authorized"));

            NpCommerceDialog.Terminate();

            return isAuthorized;
        }

	    public static void ShowErrorAsync(NetErrorException e)
	    {
	        ShowErrorAsync(e.UserId, e.Code);    
	    }

	    public static void ShowErrorAsync(int userId, int errorCode)
	    {
            var task = new Task(() => DoShowErrorAsync(userId, errorCode));
            task.Start();            
	    }

        private static void DoShowErrorAsync(int userId, int errorCode)
        {
            IsVisible = true;

            ErrorDialog.Initialize();
            ErrorDialog.Open(errorCode, userId);

            while (ErrorDialog.UpdateStatus() == ErrorDialogStatus.Running)
                Thread.Sleep(10);

            ErrorDialog.Terminate();

            IsVisible = false;
        }
#endif

        #endregion
    }
}
