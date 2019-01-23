
﻿using System;
﻿using System.Runtime.Remoting.Messaging;
﻿using System.Threading;
﻿using System.Threading.Tasks;
#if SWITCH
#else
using Sce.PlayStation4.Network;
﻿using Sce.PlayStation4.System;
#endif

namespace Microsoft.Xna.Framework.GamerServices
{
    public class SignedInGamer : Gamer
    {
#if SWITCH
#else
        private TrophySet _trophySet = new TrophySet();
#endif

        private delegate AchievementCollection GetAchievementsDelegate();
        private delegate void AwardAchievementDelegate(string key);

        public static event EventHandler<SignedInEventArgs> SignedIn;
        public static event EventHandler<SignedOutEventArgs> SignedOut;

        internal bool _isSignedIntoPSN;

        public bool IsSignedInToLive
        {
            get { return _isSignedIntoPSN && GamerServicesDispatcher.NetworkOnline; }
        }
        
        public bool IsGuest { get; internal set; }

        public GamerPrivileges Privileges { get; internal set; }

        private PlayerIndex _playerIndex;

        public PlayerIndex PlayerIndex
        {
            get
            {
                return _playerIndex;
                /*
#if SWITCH
                return (PlayerIndex)MonoGame.Switch.UserService.GetPlayerIndexByUserId(UserId);
#else
                return (PlayerIndex)UserService.GetPlayerIndexByUserId(UserId);
#endif
                */
            }
            internal set
            {
                _playerIndex = value;
            }
        }

#region Init, Destroy

        public SignedInGamer()
        {            
        }

        public override void Dispose()
        {
            if (IsDisposed)
                return;

            base.Dispose();
#if PLAYSTATION4
            if (_trophySet != null)
                _trophySet.Dispose();
            _trophySet = null;
#endif
        }

#endregion
        
        public FriendCollection GetFriends()
        {
            return null;
        }

        internal static void TriggerSignedIn(object sender, SignedInEventArgs args)
        {
            if (SignedIn != null)
            {
                var e = SignedIn;
                e(sender, args);
            }
        }

        internal static void TriggerSignedOut(object sender, SignedOutEventArgs args)
        {
            if (SignedOut != null)
            {
                var e = SignedOut;
                e(sender, args);
            }
        }

#region Achievements

        /*
        private AchievementCollection GetAchievements()
        {
            lock (_lock)
            {
                if (_trophySetDirty)
                {
                    if (_trophySet != null)
                        _trophySet.Dispose();

                    _trophySet = new TrophySet(UserId);
                    _trophySetDirty = false;
                }

                var collection = new AchievementCollection();
                for (var i = 0; i < _trophySet.Count; i++)
                {
                    var trophy = _trophySet.GetTrophy(i);
                    var achievement = new Achievement()
                    {                    
                        DisplayBeforeEarned = !trophy.hidden,
                        IsEarned = trophy.unlocked,
                        EarnedDateTime = new DateTime(trophy.timestamp),
                        EarnedOnline = true,                        
                    };

                    unsafe
                    {
                        fixed (sbyte* ptr = &(trophy.name[0]))
                        {
                            achievement.Name = new string(ptr);                            
                        }

                        fixed (sbyte* ptr = &(trophy.description[0]))
                        {
                            achievement.Description = new string(ptr);
                        }
                    }


                    var key = "Achievement";

                    if (i >= 10)
                        key += "0";

                    key += i;

                    achievement.Key = key;

                    collection.Add(achievement);
                }

                return collection;
            }
        }
        */

        public IAsyncResult BeginAwardAchievement(string key, AsyncCallback callback, Object state)
        {
            Console.WriteLine("SignedInGamer.BeginAwardAchievement");

            AwardAchievementDelegate work = AwardAchievement;
            return work.BeginInvoke(key, callback, state);
        }

        public void EndAwardAchievement(IAsyncResult result)
        {                   
            Console.WriteLine("SignedInGamer.EndAwardAchievement - begin");
#if SWITCH
#endif
#if PLAYSTATION4
            try
            {                
                var asyncResult = (AsyncResult)result;
                
                result.AsyncWaitHandle.WaitOne();

                if (asyncResult.AsyncDelegate is AwardAchievementDelegate)
                {
                    ((AwardAchievementDelegate)asyncResult.AsyncDelegate).EndInvoke(result);
                }
            }
            finally
            {                
                result.AsyncWaitHandle.Close();
            }
#endif

            Console.WriteLine("SignedInGamer.EndAwardAchievement - end");
        }

        public IAsyncResult BeginGetAchievements(AsyncCallback callback, Object state)
        {
            throw new NotImplementedException();
        }

        public AchievementCollection EndGetAchievements(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

#if PLAYSTATION4
        public void InstallAchievements()
        {
            //Extensions.PrintCallstack();

            //if (Guide.IsVisible)
                //return;

            lock (_trophySet)
            {
                if (_trophySet.Initialized)
                    return;

                var res = _trophySet.TryInit(UserId, 0);
                if (res != TrophyResult.Ok)
                {
                    Guide.ShowErrorAsync(UserId, (int)res);
                }
            }
        }
#endif

        private void AwardAchievement(string key)
        {
#if SWITCH
#endif
#if PLAYSTATION4
            lock (_trophySet)
            {                
                if (_trophySet.Initialized)
                {
                    var digits = key.Substring(key.Length - 2);
                    var id = int.Parse(digits);

                    bool plat;
                    var res = _trophySet.Unlock(id, out plat);
                    if (res != TrophyResult.Ok && res != TrophyResult.ErrorTrophyAlreadyUnlocked)
                    {
                        Guide.ShowErrorAsync(UserId, (int)res);
                    }
                }
            }
#endif
        }

        #endregion
    }
}
