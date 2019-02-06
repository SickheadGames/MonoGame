using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Net;
using System;
using System.Collections.Generic;
using System.IO;
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
        }

        private readonly Gamer _gamer;

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
            Console.WriteLine("LeaderboardWriter.DoCommitEntries(); BEGIN");
            bool success = true;
            try
            {                
                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); _leaderboardIdentities.Count={0}", _leaderboardIdentities.Count);

                int startupResultCode = MonoGame.Switch.Ranking.TryStartup(this._gamer.UserId);
                if (startupResultCode != 0)
                {
                    Console.WriteLine("Ranking.TryStartup returned error code {0}", startupResultCode);

                    throw new NetErrorException(this._gamer.UserId, startupResultCode, 0);
                }

                foreach (var e in _leaderboardIdentities)
                {
                    var entry = e.Value;
                    var lb = e.Key;

                    var category = (uint)lb.Key;
                    var score = (uint)entry.Rating;
                    var data = entry.GameInfo?.ToArray();
                    var userName = entry.Gamer.DisplayName;

                    var item = new MonoGame.Switch.Ranking.Item();
                    item.Group0 = 0;
                    item.Group1 = 0;
                    item.Category = category;
                    item.Score = score;
                    item.Data = data;
                    item.UserName = userName;

                    int resultCode = MonoGame.Switch.Ranking.TryUpload(ref item);
                    if (resultCode != 0)
                    {
                        Console.WriteLine("Ranking.TryUpload returned error code {0}", resultCode);
                        continue;
                    }

                    entry.Ranking = (int)item.Ranking;

                    lock (_commitedEntries)
                    {
                        if (!_commitedEntries.ContainsKey(lb))
                        {
                            _commitedEntries.Add(lb, new List<LeaderboardEntry>());
                        }

                        var list = _commitedEntries[lb];
                        if (!list.Contains(entry))
                            list.Add(entry);
                    }

                    Console.WriteLine("LeaderboardWriter.DoCommitEntries(); loop");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("LeaderboardWriter.DoCommitEntries(); exception : " + e);
                success = false;
            }

            Console.WriteLine("LeaderboardWriter.DoCommitEntries(); END");

            try
            {
                var cb = WriteFinished;
                if (cb != null)
                    cb(success);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occured within WriteFinished callback?");
            }
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

