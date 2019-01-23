using System.IO;
using System.Runtime.Remoting.Messaging;
using Microsoft.Xna.Framework.Net;
//using Sce.PlayStation4.Network;
//using Sce.PlayStation4.System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Xna.Framework.GamerServices
{
    public sealed class LeaderboardReader : IDisposable
    {
        private static LeaderboardReader _last;

        public delegate void ReadFinishedCallback(IAsyncResult async);

        private delegate LeaderboardReader ReadDelegate(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage);

        private delegate void PageDelegate();

        private int _curPosition;
        private readonly int _pageSize;
        private readonly int _userId;

        private bool _friendsOnly;
        private List<LeaderboardEntry> _allFriendEntries;

#if SWITCH
#else
        private readonly NpScoreRequest _request;
        private readonly NpScoreTitleContext _context;
        private NpScoreRankings _leaderboardRankings;
#endif

        private LeaderboardIdentity _boardIdent;

        private readonly List<LeaderboardEntry> _entries;
        private readonly ReadOnlyCollection<LeaderboardEntry> _readonlyEntries;

        public int TotalLeaderboardSize
        {
            get
            {
                // jcf: todo
                return 0;

                /*
                //return _entries.Count;
                if (_leaderboardRankings == null)
                    return 0;

                if (_friendsOnly)
                    return _allFriendEntries.Count;

                return (int)_leaderboardRankings.TotalPlayers;
                */
            }
        }

        private LeaderboardReader(int userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
#if SWITCH
#else
            if (_last != null)
            {
                _last.Dispose();
                _last = null;
            }

            _userId = userId;
            _curPosition = startPos;
            _pageSize = sizeOfPage;
            _boardIdent = id;

            _allFriendEntries = new List<LeaderboardEntry>();
            _entries = new List<LeaderboardEntry>(25);
            _readonlyEntries = new ReadOnlyCollection<LeaderboardEntry>(_entries);

            NpResult contextRes;
            _context = NpScoreTitleContext.Create(userId, out contextRes);
            _context.Initialize(0);

            System.Diagnostics.Debug.Assert(contextRes == NpResult.Ok, string.Format("LeaderboardReader() ContextCreation failed! {0}", contextRes));

            NpCommunityError requestRes;
            _request = NpScoreRequest.Create(_context, out requestRes);

            System.Diagnostics.Debug.Assert(requestRes == NpCommunityError.Ok,
                string.Format("LeaderboardReader() RequestCreation failed! {0}", requestRes));
                
            _last = this;
#endif
        }

#region Synchronous API

        public static LeaderboardReader ReadFriends(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            Console.WriteLine("LeaderboardReader.ReadFriends()");

#if SWITCH
            return null;
#else
            var reader = new LeaderboardReader(userId, id, startPos, sizeOfPage);
            reader._friendsOnly = true;

            reader._leaderboardRankings = new NpScoreRankings((uint)reader._pageSize, true, false, true);
            var requestRes = reader._request.GetFriendsRanking((uint)reader._boardIdent.Key, reader._context, reader._leaderboardRankings);

            CheckResult(userId, (int)requestRes);

            reader.UpdateChanges();

            reader.MergeLocalCache(false);

            reader._curPosition = 0;
            reader.ClipAllFriendsToPage();

            return reader;
#endif
        }

        public static LeaderboardReader ReadRange(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            Console.WriteLine("LeaderboardReader.ReadRange()");

#if SWITCH
            return null;
#else
            var reader = new LeaderboardReader(userId, id, startPos, sizeOfPage);
            reader._friendsOnly = false;

            reader._leaderboardRankings = new NpScoreRankings((uint)reader._pageSize, true, false, true);
            var requestRes = reader._request.GetRankingsByRange((uint)reader._boardIdent.Key, (uint)reader._curPosition, reader._leaderboardRankings);

            CheckResult(userId, (int)requestRes);

            reader.UpdateChanges();

            reader.MergeLocalCache(true);

            return reader;
#endif
        }

        /// <summary>
        /// Allocate a LeaderboardReader which is populated with ratings for the current player and those nearby on the same leaderboard.
        /// Note that startPos is not actually used.
        /// </summary>        
        public static LeaderboardReader ReadOwn(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage)
        {
            Console.WriteLine("LeaderboardReader.ReadOwn()");

#if SWITCH
            return null;
#else
            var reader = new LeaderboardReader(userId, id, startPos, sizeOfPage);
            reader._friendsOnly = false;

            reader._leaderboardRankings = new NpScoreRankings((uint)reader._pageSize, true, false, true);
            var requestRes = reader._request.GetOwnRanking((uint)reader._boardIdent.Key, reader._context, reader._leaderboardRankings);

            CheckResult(userId, (int)requestRes);

            reader.UpdateChanges();

            reader.MergeLocalCache(true);

            reader._curPosition = (int)reader._leaderboardRankings.FirstInRange;

            return reader;
#endif
        }

        public void PageDown()
        {
#if SWITCH
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
            object state)
        {
            Console.WriteLine("LeaderboardReader.BeginReadFriends(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            ReadDelegate method = ReadFriends;
            return method.BeginInvoke(userId, id, startPos, sizeOfPage, callback, state);
        }

        public static IAsyncResult BeginReadOwn(MonoGame.Switch.UserId userId, LeaderboardIdentity id, int startPos, int sizeOfPage, AsyncCallback callback, object state)
        {
            ReadDelegate method = ReadOwn;
            return method.BeginInvoke(userId, id, startPos, sizeOfPage, callback, state);
        }

        public static IAsyncResult BeginReadRange(MonoGame.Switch.UserId userId,
            LeaderboardIdentity id,
            int startPos,
            int sizeOfPage,
            AsyncCallback callback,
            object state)
        {
            ReadDelegate method = ReadRange;
            return method.BeginInvoke(userId, id, startPos, sizeOfPage, callback, state);
        }

        public static LeaderboardReader EndRead(IAsyncResult result)
        {
            Console.WriteLine("LeaderboardReader.EndRead()");

            LeaderboardReader returnValue = null;
            try
            {
                AsyncResult asyncResult = (AsyncResult)result;

                Console.WriteLine("LeaderboardReader.EndRead(); 1, asyncResult = {0}", asyncResult.NullOrToString());

                result.AsyncWaitHandle.WaitOne();

                if (asyncResult.AsyncDelegate is ReadDelegate)
                {
                    returnValue = ((ReadDelegate)asyncResult.AsyncDelegate).EndInvoke(result);
                    Console.WriteLine("LeaderboardReader.EndRead(); 2, asyncResult.AsyncDelegate = {0}", asyncResult.AsyncDelegate.NullOrToString());
                }
            }
            finally
            {
                result.AsyncWaitHandle.Close();
            }

            return returnValue;
        }

        public IAsyncResult BeginPageUp(AsyncCallback callback, object state)
        {
            Console.WriteLine("LeaderboardReader.BeginPageUp(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            PageDelegate method = PageUp;
            return method.BeginInvoke(callback, state);
        }

        public IAsyncResult BeginPageDown(AsyncCallback callback, object state)
        {
            Console.WriteLine("LeaderboardReader.BeginPageDown(); callback = {0}, state = {1}", callback.NullOrToString(), state.NullOrToString());

            PageDelegate method = PageDown;
            return method.BeginInvoke(callback, state);
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
            try
            {
                var asyncResult = (AsyncResult)result;

                result.AsyncWaitHandle.WaitOne();

                if (asyncResult.AsyncDelegate is PageDelegate)
                {
                    ((PageDelegate)asyncResult.AsyncDelegate).EndInvoke(result);
                }
            }
            finally
            {
                result.AsyncWaitHandle.Close();
            }
        }

#endregion

        //private static void CheckResult(int userId, int code)
        //{
        //    if (code != 0)
        //    {
        //        throw new NetErrorException(userId, code);
        //    }
        //}

#if SWITCH
#else
        private void UpdateChanges()
        {
            Console.WriteLine("LeaderboardReader.UpdateChanges(); numResults={0}", _leaderboardRankings.NumResults);

            _entries.Clear();
            _allFriendEntries.Clear();

            var numResults = _leaderboardRankings.NumResults;
            for (var x = 0; x < numResults; x++)
            {
                _leaderboardRankings.Index = x;

                var rank = (int)_leaderboardRankings.RankAtIndex;
                var score = _leaderboardRankings.ScoreValueAtIndex;
                var gameInfo = _leaderboardRankings.GameInfoAtIndex;
                var gamertag = _leaderboardRankings.UserOnlineIdAtIndex;

                if (string.IsNullOrWhiteSpace(gamertag))
                    continue;

                var entry = new LeaderboardEntry()
                {
                    Ranking = rank,
                    Rating = score,
                    Gamer = (Gamer)new RemoteGamer(gamertag)
                };
                if (gameInfo != null)
                    entry.GameInfo = new MemoryStream(gameInfo);

                Console.WriteLine("LeaderboardReader.UpdateChanges(); Entry[{0}] : {1}", x, entry);

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

            var existing = GetEntryForGamer(cachedEntry.Gamer.Gamertag);
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
                var firstSerialRank = _leaderboardRankings.FirstInRange;
                if (cachedEntry.Ranking < firstSerialRank && _entries.Count >= _pageSize)
                {
                    Console.WriteLine(
                        "LeaderboardReader.MergeLocalCache(); could not include cached entry because it belongs before the current page.");
                    return;
                }

                var lastSerialRank = _leaderboardRankings.LastInRange;
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

        private LeaderboardEntry GetEntryForGamer(string gamertag)
        {
            foreach (var e in _entries)
            {
                if (e.Gamer.Gamertag.Equals(gamertag))
                    return e;
            }

            return null;
        }
#endif
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
}

