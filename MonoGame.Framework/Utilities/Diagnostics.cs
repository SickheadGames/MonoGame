using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonoGame.Utilities
{
    public interface IProfileLogger
    {
        void PushScope();
        void PopScope();

        /// <summary>
        /// Dispose the returned object is equivalent to calling PopScope.
        ///
        /// ie,
        /// logger.Log("CategoryFoo");
        /// using (logger.PushScopeToken())
        ///     logger.Log("ChildBar");
        ///
        /// </summary>
        IDisposable PushScopeToken();

        void Log(string line);
        void Log(string format, params object[] args);
    }

    public class HeirarchicalTimer : IDisposable
    {
        public readonly string Name;
        public readonly Stopwatch Stopwatch;
        public readonly HeirarchicalTimer Parent;
        public readonly List<HeirarchicalTimer> Children;
        public readonly ProfileThread Owner;

        public HeirarchicalTimer(string name, HeirarchicalTimer parent, ProfileThread owner)
        {
            Name = name;
            Stopwatch = new Stopwatch();
            Parent = parent;
            Children = new List<HeirarchicalTimer>();
            Owner = owner;
        }

        public void Start()
        {
            Stopwatch.Start();
        }

        public void Stop()
        {
            Stopwatch.Stop();
        }

        void IDisposable.Dispose()
        {
            Owner.End(this);
        }

        public void Print(IProfileLogger logger)
        {
            logger.Log(this.ToString());

            using (var t = logger.PushScopeToken())
            {
                foreach (var c in Children)
                {
                    c.Print(logger);
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0} : {1}", Name, Stopwatch.Elapsed);
        }
    }

    public static class Profiles
    {
        private static List<Profile> _profiles = new List<Profile>();

        public static void Register(Profile profile)
        {
            lock (_profiles)
                _profiles.Add(profile);
        }

        public static void Unregister(Profile profile)
        {
            lock (_profiles)
                _profiles.Remove(profile);
        }

        public static IEnumerable<Profile> Enumerate()
        {
            lock (_profiles)
            {
                foreach (var p in _profiles)
                {
                    yield return p;
                }
            }
        }
    }

    public class ProfileThread
    {
        public readonly int ThreadId;
        public readonly Profile Profile;

        public HeirarchicalTimer Root;
        public HeirarchicalTimer Current;

        public ProfileThread(Profile ownerProfile, int ownerThreadId)
        {
            ThreadId = ownerThreadId;
            Profile = ownerProfile;
        }

        public HeirarchicalTimer Begin(string name)
        {
            if (Root == null)
            {
                Console.WriteLine("Begin '{0}'", name);

                Root = new HeirarchicalTimer(name, null, this);

                Current = Root;
                Current.Start();
            }
            else
            {
                HeirarchicalTimer child = null;
                foreach (var c in Current.Children)
                {
                    if (c.Name == name)
                    {
                        child = c;
                        break;
                    }
                }

                if (child == null)
                {
                    child = new HeirarchicalTimer(name, Current, this);
                    Current.Children.Add(child);
                }

                child.Start();

                Current = child;
            }

            return Current;
        }

        public void End(HeirarchicalTimer timer)
        {
            if (timer != Current)
            {
                throw new Exception(string.Format("Tried to End timer '{0}' but the current timer is '{1}'", timer, Current));
            }

            timer.Stop();

            if (timer.Parent == null)
            {
                // then presumably this is the root, so then this ProfileThread callstack has just unwound
                // merge it into its owner Profile whos keeping track of overall statistics.
                // ?? or they can just merge the data together later
                Console.WriteLine("ProfileThread exiting");
            }
            else
            {
                Current = timer.Parent;
            }
        }

        public override string ToString()
        {
            return Profile.Name;
        }
    }

    public class HeirarchicalTimerResult
    {
        public string Name;
        public TimeSpan Elapsed;
        public TimeSpan ElapsedChildren;
        public TimeSpan ElapsedOther;
        public List<HeirarchicalTimerResult> Children;

        public override string ToString()
        {
            if (Children.Count > 0)
            {
                return string.Format("{0} : Elapsed={1}, ElapsedChildren={2}, ElapsedOther={3}",
                    Name,
                    Elapsed,
                    ElapsedChildren,
                    ElapsedOther);
            }
            else
            {
                return string.Format("{0} : Elapsed={1}",
                    Name,
                    Elapsed);
            }
        }

        public void Print(IProfileLogger logger, int columnWidth)
        {
            string nameStr = Name;
            if (columnWidth > 0)
                nameStr = nameStr.PadRight(columnWidth);

            if (Children.Count > 0)
            {
                logger.Log("{0} : {1}     [ElapsedChildren={2}, ElapsedOther={3}]",
                    Elapsed,
                    nameStr,
                    ElapsedChildren,
                    ElapsedOther);

                int width = 0;
                foreach (var c in Children)
                {
                    int len = c.Name.Length;
                    if (len > width)
                    {
                        width = len;
                    }
                }

                using (logger.PushScopeToken())
                {
                    foreach (var c in Children)
                    {
                        c.Print(logger, width);
                    }
                }
            }
            else
            {
                logger.Log("{0} : {1}",
                    Elapsed,
                    nameStr);
            }
        }
    }

    public class Profile : IDisposable
    {
        public readonly string Name;
        //public readonly string Caller;
        //public readonly int LineNumber;

        private Dictionary<int, ProfileThread> _threads;

        private HeirarchicalTimerResult _merged;

        public Profile(
            string name
            //,
            //[CallerMemberName] string caller = null,
            //[CallerLineNumber] int lineNumber = 0
            )
        {
            Name = name;
            //Caller = caller;
            //LineNumber = lineNumber;

            _threads = new Dictionary<int, ProfileThread>();

            Profiles.Register(this);
        }

        public ProfileThread Get()
        {
            lock (_threads)
            {
#if AT_LEAST_DOT_NET_45
                var id = System.Environment.CurrentManagedThreadId;
#else
                var id = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif

                ProfileThread thread;
                if (!_threads.TryGetValue(id, out thread))
                {
                    thread = new ProfileThread(this, id);
                    _threads[id] = thread;
                }

                return thread;
            }
        }

        public void Stop()
        {
            lock (_threads)
            {
                foreach (var pair in _threads)
                {
                    var profileThread = pair.Value;

                    while (profileThread.Current != profileThread.Root)
                        profileThread.End(profileThread.Current);
                }
            }
        }

        public void Merge()
        {
            foreach (var thread in _threads.Values)
            {
                Merge(thread);
            }

            Console.WriteLine("Profile.Merge combined '{0}' ProfileThread(s).", _threads.Count);
        }

        private void Merge(ProfileThread thread)
        {
            Merge(_merged, thread.Root);
        }

        private void Merge(HeirarchicalTimerResult parentResult, HeirarchicalTimer currentTimer)
        {
            HeirarchicalTimerResult currentResult = null;
            if (parentResult == null)
            {
                parentResult = new HeirarchicalTimerResult();
                parentResult.Children = new List<HeirarchicalTimerResult>();

                currentResult = parentResult;
                _merged = parentResult;
            }

            if (currentResult == null)
            {
                currentResult = new HeirarchicalTimerResult();
                currentResult.Children = new List<HeirarchicalTimerResult>();
            }

            if (parentResult != currentResult)
            {
                parentResult.Children.Add(currentResult);
            }

            currentResult.Name = currentTimer.Name;
            currentResult.Elapsed = currentTimer.Stopwatch.Elapsed;
            currentResult.ElapsedChildren = TimeSpan.Zero;

            foreach (var childTimer in currentTimer.Children)
            {
                currentResult.ElapsedChildren += childTimer.Stopwatch.Elapsed;

                Merge(currentResult, childTimer);
            }

            currentResult.ElapsedOther = currentResult.Elapsed - currentResult.ElapsedChildren;
        }

        public void Print(IProfileLogger logger)
        {
            if (_merged == null)
            {
                logger.Log("Nothing to print : _merged is null.");
                return;
            }

            _merged.Print(logger, -1);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Profiles.Unregister(this);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Profiling() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
