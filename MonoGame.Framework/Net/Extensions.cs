using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.GamerServices;

namespace Microsoft.Xna.Framework.Net
{
    public delegate string ItemToString<T>(T item);

    public static class Extensions
    {
        private const string CommentFormat = "\n#----------------------------------------------------------------------------#\n";
        
        public static string FormatDivider(string label = null)
        {
            if (string.IsNullOrEmpty(label))
                return CommentFormat;

            label = " " + label + " ";
            var src = CommentFormat.Length / 2 - label.Length / 2;
            var dst = src + label.Length;

            return CommentFormat.Substring(0, src) + label + CommentFormat.Substring(dst);
        }
        
        public static IEnumerable<SignedInGamer> Yield(this SignedInGamer obj)
        {
            yield return obj;
        }

        public static string NullOrToString(this object obj)
        {
            if (obj == null)
                return "[null_obj]";

            var str = obj.ToString();
            if (string.IsNullOrEmpty(str))
                return "[empty_string]";

            return str;
        }

        public static string NullOrJoin(this Array array)
        {
            if (array == null)
                return "[null]";

            var sb = new StringBuilder();
            //lock (_sb)
            {
                sb.Clear();

                sb.Append("{ ");

                for (var i = 0; i < array.Length; i++)
                {
                    var item = array.GetValue(i);
                    sb.Append(item.ToString());
                    
                    if (i < (array.Length - 1))
                        sb.Append(", ");
                }

                sb.Append(" }");

                return sb.ToString();
            }
        }

        public static string NullOrGamertag(this Gamer gamer)
        {
            if (gamer == null)
                return "[null]";

            if (string.IsNullOrEmpty(gamer.Gamertag))
                return "[unset gamertag]";

            return gamer.Gamertag;
        }

        public static SignedInGamer FindByGamertag(this IEnumerable<SignedInGamer> collection, string gamerTag)
        {
            foreach (var g in collection)
            {
                if (gamerTag.Equals(g.Gamertag))
                    return g;
            }

            return null;
        }

        public static void PrintItems<T>(string label, IEnumerable<T> items, ItemToString<T> tostring)
        {
            lock (_sb)
            {
                _sb.Clear();

                _sb.Append(label);
                _sb.Append(" : ");
                _sb.Append("{ ");

                var last = items.Last();
                foreach (var i in items)
                {
                    _sb.Append(tostring(i));
                    if (!i.Equals(last))
                        _sb.Append(", ");
                }

                _sb.Append(" }");

                Console.WriteLine(_sb.ToString());
            }
        }

        private static readonly StringBuilder _sb = new StringBuilder();

        public static void PrintCallstack()
        {
#if SWITCH
            // not implemented on brute platforms
            return;  
#endif
            lock (_sb)
            {
                _sb.Clear();

                var stack = new System.Diagnostics.StackTrace(1);

                var frames = stack.GetFrames();

                for (var i = 0; i < frames.Length; ++i)
                {
                    var frame = frames[i];
                    var method = frame.GetMethod();

                    var className = method.DeclaringType.Name;
                    var methodName = method.Name;

                    _sb.Append("    ");
                    _sb.Append(className);
                    _sb.Append(".");
                    _sb.Append(methodName);
                    _sb.Append("()");
                    _sb.Append("\n");
                }

                Console.WriteLine(_sb.ToString());
            }
        }

        /// <summary>
        /// Delegate for returning the key for a given object.
        /// </summary>       
        public delegate TKey GetKeyMethod<TKey, TObj>(TObj obj);

        /// <summary>
        /// Example: GuidSortedUnitList.BinarySearch( guid, e => e.Id );
        /// 
        /// It is safe to use Structures which implement IComparable as TKey.
        /// This will not generate garabage from boxing because this method is generic
        /// and does not need to do any casting.       
        /// </summary>        
        public static int BinarySearch<TKey, TObj>(this IList<TObj> list, TKey key, GetKeyMethod<TKey, TObj> getElementKey)
           where TKey : IComparable<TKey>
        {
            int min = 0;
            int max = list.Count - 1;

            while (min <= max)
            {
                int mid = (min + max) / 2;
                int comparison = -(key.CompareTo(getElementKey(list[mid])));

                if (comparison == 0)
                {
                    return mid;
                }

                if (comparison < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }

            return ~min;
        }

        /// <summary>
        /// For a list of elements sorted by key, where key is expressed by
        /// the GetKeyMethod(element), return the index of the first element
        /// with key greater than or equal to the search key.
        /// </summary>                
        public static int BinarySearchEqualOrGreater<KEY, OBJ>(this IList<OBJ> list,
                                                               KEY key,
                                                               GetKeyMethod<KEY, OBJ> getElementKey)
           where KEY : IComparable
        {
            // NOTE: Not thoroughly tested yet!

            if (list == null)
                throw new ArgumentNullException("list");

            if (list.Count == 0)
                return 0;
            var comp = Comparer<KEY>.Default;

            int lo = 0, hi = list.Count - 1;

            while (lo < hi)
            {
                int m = (hi + lo) / 2;

                if (comp.Compare(getElementKey(list[m]), key) < 0)
                    lo = m + 1;
                else
                    hi = m - 1;
            }

            if (comp.Compare(getElementKey(list[lo]), key) < 0)
                lo++;

            return lo;
        }

        internal static SignedInGamer GetByGamerTag(this IEnumerable<SignedInGamer> items, string key)
        {
            foreach (var i in items)
            {
                if (i.Gamertag == key)
                    return i;
            }

            return null;
        }

        internal static SignedInGamer GetByPlayerIndex(this IEnumerable<SignedInGamer> items, PlayerIndex key)
        {
            foreach (var i in items)
            {
                if (i.PlayerIndex == key)
                    return i;
            }

            return null;
        }

        internal static SignedInGamer GetByUserId(this IEnumerable<SignedInGamer> items, MonoGame.Switch.UserId key)
        {
            foreach (var i in items)
            {
                if (i.UserId == key)
                    return i;
            }

            return null;
        }

        //internal static NetworkGamer GetByInternalId(this IEnumerable<NetworkGamer> items, byte key)
        //{
        //    foreach (var i in items)
        //    {
        //        if (i.Id == key)
        //            return i;
        //    }

        //    return null;
        //}

        internal static NetworkGamer GetByGamertag(this IEnumerable<NetworkGamer> items, string key)
        {
            foreach (var i in items)
            {
                if (i.Gamertag.NotNullAndEquals(key))
                    return i;
            }

            return null;
        }

        internal static NetworkGamer GetByOnlineId(this IEnumerable<NetworkGamer> items, MonoGame.Switch.OnlineId key)
        {
            foreach (var i in items)
            {
                if (i.OnlineId == key)
                    return i;
            }

            return null;
        }

        internal static NetworkGamer GetByStationId(this IEnumerable<NetworkGamer> items, MonoGame.Switch.StationId key)
        {
            foreach (var i in items)
            {
                if (i.StationId == key)
                    return i;
            }

            return null;
        }

        internal static SignedInGamer GetByStationId(this SignedInGamerCollection gamers, MonoGame.Switch.StationId key)
        {
            foreach (var g in gamers)
            {
                if (g.StationId == key)
                    return g;
            }

            return null;
        }

        internal static SignedInGamer GetByOnlineId(this SignedInGamerCollection gamers, MonoGame.Switch.OnlineId key)
        {
            foreach (var g in gamers)
            {
                if (g.OnlineId == key)
                    return g;
            }

            return null;
        }

        internal static SignedInGamer GetByUserId(this SignedInGamerCollection gamers, MonoGame.Switch.UserId key)
        {
            foreach (var g in gamers)
            {
                if (g.UserId == key)
                    return g;
            }

            return null;
        }

        /// <summary>
        /// Returns true if 'a' is not null and equals 'b'.
        /// </summary>        
        internal static bool NotNullAndEquals(this string a, string b)
        {
            if (a == null)
                return false;

            return a.Equals(b);
        }
    }
}
