using System.IO;
using System.Runtime.Remoting.Messaging;
using Microsoft.Xna.Framework.Net;
//using Sce.PlayStation4.Network;
//using Sce.PlayStation4.System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Xna.Framework.GamerServices
{
    public sealed class LeaderboardReader : IDisposable
    {
        private static LeaderboardReader _last;

        private readonly int _pageSize;
        private readonly MonoGame.Switch.UserId _userId;
        private readonly bool _friendsOnly;
        private readonly List<LeaderboardEntry> _allFriendEntries;
        private readonly LeaderboardIdentity _boardIdent;
        private readonly List<LeaderboardEntry> _entries;
        private readonly ReadOnlyCollection<LeaderboardEntry> _readonlyEntries;

        private MonoGame.Switch.Ranking.RequestMode _requestMode;
        private MonoGame.Switch.Ranking.GetRankResults _rankingInfo;
        private int _curPosition;

        public int TotalLeaderboardSize
        {
            get
            {
                if (_rankingInfo == null)
                    return 0;

                if (_friendsOnly)
                    return _allFriendEntries.Count;

                return _rankingInfo.TotalPlayers;
            }
        }

        private LeaderboardReader(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage, bool friendsOnly)
        {
            if (_last != null)
            {
                _last.Dispose();
                _last = null;
            }

            _userId = userId;
            _friendsOnly = friendsOnly;
            _curPosition = startPos;
            _pageSize = sizeOfPage;
            _boardIdent = id;

            _allFriendEntries = new List<LeaderboardEntry>();
            _entries = new List<LeaderboardEntry>(25);
            _readonlyEntries = new ReadOnlyCollection<LeaderboardEntry>(_entries);

            try
            {
                // jcf: hack, to prevent user input during startup process, since it isn't really cancelable
                Guide.IsVisible = true;

                int resultCode = MonoGame.Switch.Ranking.TryStartup(userId);
                if (resultCode != 0)
                {
                    throw new NetErrorException(userId, resultCode, 0);
                }
            }
            finally
            {
                Guide.IsVisible = false;
            }

            _last = this;
        }

#region Synchronous API

        private static LeaderboardReader _Read(MonoGame.Switch.Ranking.RequestMode mode, MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            var reader = new LeaderboardReader(userId, id, startPos, sizeOfPage, mode == MonoGame.Switch.Ranking.RequestMode.Friends);

            var rankingInfo = new MonoGame.Switch.Ranking.GetRankResults();

            int resultCode = MonoGame.Switch.Ranking.TryDownload(
                mode,
                reader._boardIdent.Key,
                startPos,
                sizeOfPage,
                rankingInfo);

            if (resultCode != 0)
            {
                throw new NetErrorException(userId, resultCode, 0);
            }

            reader._requestMode = mode;

            reader._rankingInfo = rankingInfo;

            reader.UpdateChanges();

            reader.MergeLocalCache(false);

            reader._curPosition = startPos;
            reader.ClipAllFriendsToPage();

            return reader;
        }

        public static LeaderboardReader ReadFriends(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            Console.WriteLine("LeaderboardReader.ReadFriends()");

            return _Read(MonoGame.Switch.Ranking.RequestMode.Friends, userId, id, startPos, sizeOfPage);
        }

        public static LeaderboardReader ReadRange(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            Console.WriteLine("LeaderboardReader.ReadRange()");

            return _Read(MonoGame.Switch.Ranking.RequestMode.Everyone, userId, id, startPos, sizeOfPage);
        }

        /// <summary>
        /// Allocate a LeaderboardReader which is populated with ratings for the current player and those nearby on the same leaderboard.
        /// Note that startPos is not actually used.
        /// </summary>        
        public static LeaderboardReader ReadOwn(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            Console.WriteLine("LeaderboardReader.ReadOwn()");

            return _Read(MonoGame.Switch.Ranking.RequestMode.Nearby, userId, id, startPos, sizeOfPage);
        }

        private void _ReadPos(int newPos)
        {
            if (_friendsOnly)
            {
                _curPosition = newPos;
                ClipAllFriendsToPage();

                return;
            }

            try
            {
                // jcf: hack, to prevent user input during startup process, since it isn't really cancelable
                //Guide.IsVisible = true;

                int resultCode = MonoGame.Switch.Ranking.TryStartup(_userId);
                if (resultCode != 0)
                {
                    throw new NetErrorException(_userId, resultCode, 0);
                }
            }
            finally
            {
                //Guide.IsVisible = false;
            }

            var requestRes = MonoGame.Switch.Ranking.TryDownload(_requestMode, _boardIdent.Key, newPos, _pageSize, _rankingInfo);

            System.Diagnostics.Debug.Assert(requestRes == 0, string.Format("LeaderboardReader.PageUp(); failed! {0}", requestRes));

            //CheckResult(_userId, (int)requestRes);

            _curPosition = newPos;

            UpdateChanges();

            MergeLocalCache(true);
        }

        public void PageDown()
        {
#if SWITCH
            var newPos = Math.Min(_curPosition + _pageSize, TotalLeaderboardSize);
            _ReadPos(newPos);
#else
            var newPos = Math.Min(_curPosition + _pageSize, TotalLeaderboardSize);
            
            if (_friendsOnly)
            {
                _curPosition = newPos;
                ClipAllFriendsToPage();

                return;
            }

            var requestRes = _request.GetRankingsByRange((uint)_boardIdent.Key, (uint)newPos, _leaderboardRankings);            

            System.Diagnostics.Debug.Assert(requestRes == NpCommunityError.Ok, string.Format("LeaderboardReader.PageDown(); failed! {0}", requestRes));

            CheckResult(_userId, (int)requestRes);

            _curPosition = newPos;

            UpdateChanges();

            MergeLocalCache(true);
#endif
        }

        public void PageUp()
        {
#if SWITCH
            var newPos = Math.Max(0, _curPosition - _pageSize);
            this._ReadPos(newPos);
#else
            var newPos = Math.Max(0, _curPosition - _pageSize);            

            if (_friendsOnly)
            {
                _curPosition = newPos;
                ClipAllFriendsToPage();

                return;
            }

            var requestRes = _request.GetRankingsByRange((uint)_boardIdent.Key, (uint)newPos, _leaderboardRankings);

            System.Diagnostics.Debug.Assert(requestRes == NpCommunityError.Ok, string.Format("LeaderboardReader.PageUp(); failed! {0}", requestRes));

            CheckResult(_userId, (int)requestRes);

            _curPosition = newPos;

            UpdateChanges();

            MergeLocalCache(true);
#endif
        }

#endregion

#region Asynchronous API

        public static IAsyncResult BeginReadFriends(MonoGame.Switch.UserId userId,
            LeaderboardIdentity id,
            int startPos,
            int sizeOfPage,
            AsyncCallback callback,
            ICancellable state)
        {
            Console.WriteLine("LeaderboardReader.BeginReadFriends(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            var task = new Task<LeaderboardReader>(
                () =>
                {
                    MonoGame.Switch.Ranking.WaitSafeCallTimeout();

                    if (state.IsCancelled)
                    {
                        Console.WriteLine("LeaderboardReader.BeginReadFriends(); was canceled, skipping Ranking call...");
                        throw new TaskCanceledException();
                    }
                    else
                    {
                        return ReadFriends(userId, id, startPos, sizeOfPage);
                    }
                });
            task.Start();

            return task.AsApm(callback, state);
        }

        public static IAsyncResult BeginReadOwn(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage, AsyncCallback callback, ICancellable state)
        {
            Console.WriteLine("LeaderboardReader.BeginReadOwn(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            var task = new Task<LeaderboardReader>(
                () =>
                {
                    MonoGame.Switch.Ranking.WaitSafeCallTimeout();

                    if (state.IsCancelled)
                    {
                        Console.WriteLine("LeaderboardReader.BeginReadOwn(); was canceled, skipping Ranking call...");
                        throw new TaskCanceledException();
                    }
                    else
                    {
                        return ReadOwn(userId, id, startPos, sizeOfPage);
                    }
                });
            task.Start();

            return task.AsApm(callback, state);
        }

        public static IAsyncResult BeginReadRange(MonoGame.Switch.UserId userId,
            LeaderboardIdentity id,
            int startPos,
            int sizeOfPage,
            AsyncCallback callback,
            ICancellable state)
        {
            Console.WriteLine("LeaderboardReader.BeginReadRange(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            var task = new Task<LeaderboardReader>(
                () =>
                {
                    MonoGame.Switch.Ranking.WaitSafeCallTimeout();

                    if (state.IsCancelled)
                    {
                        Console.WriteLine("LeaderboardReader.BeginReadRange(); was canceled, skipping Ranking call...");
                        throw new TaskCanceledException();
                    }
                    else
                    {
                        return ReadRange(userId, id, startPos, sizeOfPage);
                    }
                });
            task.Start();

            return task.AsApm(callback, state);
        }

        public static LeaderboardReader EndRead(IAsyncResult result)
        {
            Console.WriteLine("LeaderboardReader.EndRead()");

            LeaderboardReader returnValue = null;
            try
            {
                returnValue = ((Task<LeaderboardReader>)result).Result;
            }
            finally
            {
                Console.WriteLine("LeaderboardReader.EndRead - 1");
            }

            Console.WriteLine("LeaderboardReader.EndRead - 2");

            return returnValue;
        }

        public IAsyncResult BeginPageUp(AsyncCallback callback, ICancellable state)
        {
            Console.WriteLine("LeaderboardReader.BeginPageUp(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            var task = new Task<int>(
                () =>
                {
                    Console.WriteLine("BeginReadRange - inside task");
                    PageUp();
                    return 0;
                });
            task.Start();

            return task.AsApm(callback, state);
        }

        public IAsyncResult BeginPageDown(AsyncCallback callback, ICancellable state)
        {
            Console.WriteLine("LeaderboardReader.BeginPageDown(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            var task = new Task<int>(
                () =>
                {
                    Console.WriteLine("BeginPageDown - inside task");
                    PageDown();
                    return 0;
                });
            task.Start();

            return task.AsApm(callback, state);
        }

        public void EndPageDown(IAsyncResult result)
        {
            Console.WriteLine("LeaderboardReader.EndPageDown(); result={0}", result.NullOrToString());

            EndPageOp(result);
        }

        public void EndPageUp(IAsyncResult result)
        {
            Console.WriteLine("LeaderboardReader.EndPageUp(); result={0}", result.NullOrToString());

            EndPageOp(result);
        }

        private void EndPageOp(IAsyncResult result)
        {
            int returnValue = 0;
            try
            {
                returnValue = ((Task<int>)result).Result;
            }
            finally
            {
            }
        }

#endregion

        internal void HandleResultItem()
        {

        }

        private void UpdateChanges()
        {
            Console.WriteLine("LeaderboardReader.UpdateChanges(); numResults={0}", _rankingInfo.Items.Count);

            _entries.Clear();
            _allFriendEntries.Clear();

            for (var i = 0; i < _rankingInfo.Items.Count; i++)
            {
                var item = _rankingInfo.Items[i];

                var rank = item.Ranking;
                var score = item.Score;
                var gameInfo = item.Data;
                var userName = item.UserName;
                var principalId = item.PrincipalId;

                if (string.IsNullOrWhiteSpace(userName))
                    continue;

                var gamertag = string.Format("{0}+0x{1:X}", userName, principalId);

                var entry = new LeaderboardEntry()
                {
                    Ranking = (int)rank,
                    Rating = (long)score,
                    Gamer = new RemoteGamer(gamertag)
                    {
                        LeaderboardId = principalId,
                        DisplayName = userName,
                    },
                };
                if (gameInfo != null)
                    entry.GameInfo = new MemoryStream(gameInfo);

                Console.WriteLine("LeaderboardReader.UpdateChanges(); Entry[{0}] : {1}", i, entry);

                _entries.Add(entry);
            }
        }

        internal void MergeLocalCache(bool currentPageOnly)
        {
            // If initialUser has a ranking within the cache...
            // If npScore returned a rank for initialUser, remove it.
            // If the cached rank is within the range of ranks visible, include it.                        

            var cachedEntry = LeaderboardWriter.GetInitialUserEntryForLeaderboard(_boardIdent);
            if (cachedEntry == null)
                return;

            var existing = GetEntryForGamer(cachedEntry.Gamer.LeaderboardId);
            if (existing != null)
            {
                if (existing.Ranking != 0 && existing.Ranking > cachedEntry.Ranking)
                {
                    return;
                }

                _entries.Remove(existing);
            }

            if (cachedEntry.GameInfo != null)
                cachedEntry.GameInfo.Position = 0;

            if (currentPageOnly)
            {
                var firstSerialRank = _rankingInfo.FirstInRange;
                if (cachedEntry.Ranking < firstSerialRank && _entries.Count >= _pageSize)
                {
                    Console.WriteLine(
                        "LeaderboardReader.MergeLocalCache(); could not include cached entry because it belongs before the current page.");
                    return;
                }

                var lastSerialRank = _rankingInfo.LastInRange;
                if (cachedEntry.Ranking > lastSerialRank && _entries.Count >= _pageSize)
                {
                    Console.WriteLine("LeaderboardReader.MergeLocalCache(); could not include cached entry because it belongs after the current page.");
                    return;
                }
            }

            var idx = _entries.BinarySearch(cachedEntry.Ranking, (e) => e.Ranking);

            // There is already an entry with this ranking in the list.
            // Incriment the ranking of all entries below the one we are inserting.            
            if (idx >= 0)
            {
                for (var i = idx; i < _entries.Count; i++)
                {
                    _entries[i].Ranking++;
                }
            }

            if (idx < 0)
                idx = ~idx;

            _entries.Insert(idx, cachedEntry);

            // If there are now more entries than can be viewed on a single page, then trim the last entry.
            if (currentPageOnly)
            {
                if (_entries.Count > _pageSize)
                    _entries.RemoveAt(_entries.Count - 1);
            }
        }

        private void ClipAllFriendsToPage()
        {
            // On the first call copy the retrived entries.
            if (_allFriendEntries.Count == 0)
                _allFriendEntries.AddRange(_entries);
            
            _entries.Clear();

            for (var i = 0; i < _pageSize; i++)
            {
                var offset = i + _curPosition;
                if (offset >= _allFriendEntries.Count)
                    break;

                _entries.Add(_allFriendEntries[offset]);
            }
        }

        private LeaderboardEntry GetEntryForGamer(UInt64 principalId)
        {
            foreach (var e in _entries)
            {
                var remoteGamer = (e.Gamer as RemoteGamer);
                if (remoteGamer == null)
                {
                    throw new Exception("Expected all ");
                }
                if (e.Gamer.LeaderboardId.Equals(principalId))
                    return e;
            }

            return null;
        }

        public bool CanPageDown 
        {
            get
            {                
                return ( _curPosition + _pageSize ) < TotalLeaderboardSize;
            }
        }

        public bool CanPageUp 
        {
            get
            {
                return _curPosition > 1;
            }
        }

        public ReadOnlyCollection<LeaderboardEntry> Entries 
        {
            get
            {
                return _readonlyEntries;
            }
        }

#region IDisposable implementation

        private bool _disposed = false;

        public void Dispose ()
        {
            lock (this)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }


#if SWITCH
#else
            if (_request != null)
                _request.Dispose();

            if (_context != null)
                _context.Dispose();

            if (_leaderboardRankings != null)
                _leaderboardRankings.Dispose();
#endif
        }

#endregion
    }

    public interface ICancellable
    {
        bool IsCancelled { get; }
        void Cancel();
    }
}

