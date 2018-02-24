// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#define UNSAFE

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MonoGame.Utilities
{
    internal class SpriteBatchItemSorter
    {
        private SpriteBatchItem[] _temp;

        struct Split
        {
            public int index;
            public int count;
            public bool split;
        }

        private readonly Stack<Split> _stack;

        public SpriteBatchItemSorter(int reserved)
        {
            _temp = new SpriteBatchItem[reserved];
            _stack = new Stack<Split>(16);
        }

        public void Sort(SpriteBatchItem[] array)
        {
            if (array.Length < 2)
                return;

            // Grow the array only if we have to.
            if (array.Length > _temp.Length)
                _temp = new SpriteBatchItem[array.Length];

            InnerSort(array, 0, array.Length);
        }

        public void Sort(SpriteBatchItem[] array, int index, int length)
        {
            if (length < 2)
                return;

            // Grow the array only if we have to.
            if (length > _temp.Length)
                _temp = new SpriteBatchItem[length];

            InnerSort(array, index, length);
        }

        private void InnerSort(SpriteBatchItem[] array, int first, int length)
        {
            _stack.Push(new Split { index = first, count = length, split = length > 2 });

            //unsafe
            //{
            //    fixed (SpriteBatchItem* pArray = array)
            //    {
            //    }
            //}
            
            while (_stack.Count > 0)
            {
                var pass = _stack.Pop();
                var mid = pass.index + pass.count / 2;
                var high = pass.index + pass.count;

                if (pass.split)
                {
                    // Resubmit this split for sorting later.
                    pass.split = false;
                    _stack.Push(pass);
                    
                    // Submit the last half of this split first.
                    var count = high - mid;
                    if (count > 1)
                        _stack.Push(new Split { index = mid, count = count, split = count > 2 });

                    // Submit the first half of the split second
                    // so that it is processed on the next loop.
                    count = mid - pass.index;
                    if (count > 1)
                        _stack.Push(new Split { index = pass.index, count = count, split = count > 2 });

                    continue;
                }

                // Sort this slice of the array.
                var i = pass.index;
                var j = mid;
                for (var k = 0; k < pass.count; k++)
                {
                    if (i == mid)
                        _temp[k] = array[j++];
                    else if (j == high)
                        _temp[k] = array[i++];
                    else if (array[j].CompareTo(array[i]) < 0)
                        _temp[k] = array[j++];
                    else
                        _temp[k] = array[i++];
                }

                // Copy the sort back to the original array.
                Array.Copy(_temp, 0, array, pass.index, pass.count);              
            }
        }
    }
}
