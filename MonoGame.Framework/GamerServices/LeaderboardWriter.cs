using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Net;
using System;
using System.Collections.Generic;
#if SWITCH
#else
using Sce.PlayStation4.Network;
using Sce.PlayStation4.System;
#endif

namespace Microsoft.Xna.Framework.GamerServices
{
    public class WriteLeaderboardsEventArgs
    {
    }    

    public sealed class LeaderboardWriter : IDisposable
    {
        private struct PendingEntry
        {
            public LeaderboardIdentity Leaderboard;
            public LeaderboardEntry Entry;
#if SWITCH
#else
            public NpScoreRequest Request;
#endif
        }

        private readonly Gamer _gamer;
#if SWITCH
#else
        private readonly NpScoreTitleContext _context;
#endif
        private readonly Dictionary<LeaderboardIdentity, LeaderboardEntry> _leaderboardIdentities;
        
        private static readonly Dictionary<LeaderboardIdentity, List<LeaderboardEntry>> _commitedEntries;

        internal static IEnumerable<LeaderboardEntry> GetCommitedEntriesForLeaderboard(LeaderboardIdentity id)
        {
            lock (_commitedEntries)
            {
                if (_commitedEntries.ContainsKey(id))
                {
                    foreach (var i in _commitedEntries[id])
                    {
                        yield return i;
                    }
                }
            }
        }

        internal static LeaderboardEntry GetInitialUserEntryForLeaderboard(LeaderboardIdentity id)
        {
            lock (_commitedEntries)
            {
                if (_commitedEntries.ContainsKey(id))
                {
                    foreach (var i in _commitedEntries[id])
                    {
                        if (i.Gamer == null)
                            continue;

                        if (i.Gamer.IsDisposed)
                            continue;

                        if (i.Gamer.UserId == MonoGame.Switch.UserService.GetInitialLocalUser())
                            return i;
                    }
                }
            }

            return null;
        }

        public LeaderboardWriter(Gamer gamer)
        {
            Console.WriteLine("LeaderboardWriter()");
            Extensions.PrintCallstack();

            _gamer = gamer;

            _leaderboardIdentities = new Dictionary<LeaderboardIdentity, LeaderboardEntry>();

#if SWITCH
#endif
#if PLAYSTATION4
            NpResult contextRes;
            _context = NpScoreTitleContext.Create(gamer.UserId, out contextRes);

            if (contextRes != NpResult.Ok ||
                _context == null)
            {
                Console.WriteLine("NpScoreTitleContext creation failed! {0}", contextRes);
                return;
            }

            NpCommunityError res;
            res = _context.Initialize(0);

            if (res != NpCommunityError.Ok)
            {
                Console.WriteLine("NpScoreTitleContext initialization failed! {0}", res);
                return;
            }
#endif
        }

        static LeaderboardWriter()
        {
            _commitedEntries = new Dictionary<LeaderboardIdentity, List<LeaderboardEntry>>();
        }

        private bool _disposed = false;

        public delegate void HandleWriteFinished(bool success);
        public event HandleWriteFinished WriteFinished;

        public LeaderboardEntry GetLeaderboard(LeaderboardIdentity aLeaderboardIdentity)
        {
            Console.WriteLine("LeaderboardWriter.GetLeaderboard( key={0} )", aLeaderboardIdentity.Key);

            LeaderboardEntry entry;

            if (!_leaderboardIdentities.TryGetValue(aLeaderboardIdentity, out entry))
            {
                entry = new LeaderboardEntry()
                {
                    Gamer = _gamer,                    
                };
                _leaderboardIdentities.Add(aLeaderboardIdentity, entry);
            }

            return entry;
        }

        public void CommitEntries()
        {
            var task = new Task(DoCommitEntries);
            task.Start();
        }

        private void DoCommitEntries()
        {
#if SWITCH
#endif
#if PLAYSTATION4
            Console.WriteLine("LeaderboardWriter.DoCommitEntries(); BEGIN");

            Console.WriteLine("LeaderboardWriter.DoCommitEntries(); if1");
            if (_context == null)
            {
                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); true1");
                return;
            }

            try
            {                
                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); _leaderboardIdentities.Count={0}", _leaderboardIdentities.Count);

                var pendingList = new List<PendingEntry>();

                foreach (var entry in _leaderboardIdentities)
                {
                    NpCommunityError res;
                    var request = NpScoreRequest.Create(_context, out res);
                    
                    if (res != NpCommunityError.Ok || request == null)
                    {                        
                        Console.WriteLine("LeaderboardWriter.DoCommitEntries(); Could not create necessary NpScoreRequest to CommitEntries(). {0}", res);
                        continue;
                    }

                    var boardId = (uint)entry.Key.Key;
                    var score = (long)entry.Value.Rating;
                    var gameInfo = entry.Value.GameInfo;

                    var gameInfoBytes = gameInfo == null ? null : gameInfo.ToArray();                    
                    var npResult = request.RecordScoreAsync(boardId, score, string.Empty, gameInfoBytes, 0);
                    if (npResult != NpCommunityError.Ok)
                        continue;

                    var item = new PendingEntry()
                    {
                        Leaderboard = entry.Key,
                        Entry = entry.Value,
                        Request = request,
                    };
                    pendingList.Add(item);
                }

                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); waiting for pending record(s), pending.Count=={0}", pendingList.Count);

                while (pendingList.Any())
                {
                    var item = pendingList[0];

                    Console.WriteLine("LeaderboardWriter.DoCommitEntries(); pending loopbefore WaitResult");

                    try
                    {
                        Console.WriteLine("LeaderboardWriter.DoCommitEntries(); before WaitResult");

                        uint tmpRank;
                        var npResult = item.Request.WaitAsync(out tmpRank);

                        Console.WriteLine("LeaderboardWriter.DoCommitEntries(); after WaitResult");

                        var leaderboardId = item.Leaderboard;
                        var entry = item.Entry;

                        lock (_commitedEntries)
                        {
                            if (!_commitedEntries.ContainsKey(leaderboardId))
                            {
                                _commitedEntries.Add(leaderboardId, new List<LeaderboardEntry>());
                            }

                            Console.WriteLine("LeaderboardWriter.DoCommitEntries(); if3");

                            if (npResult == NpCommunityError.Ok)
                            {
                                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); true3");
                                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); Adding to 'commited entries' cache.");

                                var list = _commitedEntries[leaderboardId];
                                if (!list.Contains(entry))
                                    list.Add(entry);
                                entry.Ranking = (int)tmpRank;
                            }
                            else
                            {
                                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); WaitResult returned error " + npResult);
                                //...
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("LeaderboardWriter.DoCommitEntries(); pending loop exception : " + e);
                        //throw;
                    }

                    pendingList.Remove(item);

                    item.Request.Dispose();
                    item.Request = null;
                }

                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); WriteFinished exists : {0}", WriteFinished != null);

                if (WriteFinished != null)
                    WriteFinished(true);
            }
            catch (Exception e)
            {
                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); exception : " + e);
                //throw;
            }
#endif
        }

#region IDisposable implementation

        void IDisposable.Dispose ()
        {
            if (_disposed)
                return;

            _disposed = true;

#if PLAYSTATION4
            if (_context != null)
                _context.Dispose();
#endif
        }

#endregion
    }
}

