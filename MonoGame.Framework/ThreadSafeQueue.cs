using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Xna.Framework
{
    public class ThreadSafeQueue<T> where T : class
    {
        private readonly List<T> _list;        

        public ThreadSafeQueue()
        {
            _list = new List<T>();
        }
 
        public bool TryDequeue(out T item)
        {
            lock (_list)
            {
                item = null;
                if (_list.Any())
                {
                    item = _list[0];
                    _list.RemoveAt(0);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void Enqueue(T item)
        {
            lock (_list)
            {
                _list.Add(item);
            }
        }
    }
}
