// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Graphics
{
    /// <summary>
    /// This class handles the queueing of batch items into the GPU by creating the triangle tesselations
    /// that are used to draw the sprite textures. This class supports int.MaxValue number of sprites to be
    /// batched and will process them into short.MaxValue groups (strided by 6 for the number of vertices
    /// sent to the GPU). 
    /// </summary>
	internal class SpriteBatcher
	{
        /*
         * Note that this class is fundamental to high performance for SpriteBatch games. Please exercise
         * caution when making changes to this class.
         */

        /// <summary>
        /// Initialization size for the batch item list and queue.
        /// </summary>
        private const int InitialBatchSize = 256;
        /// <summary>
        /// The maximum number of batch items that can be processed per iteration
        /// </summary>
        private const int MaxBatchSize = short.MaxValue / 6; // 6 = 4 vertices unique and 2 shared, per quad
        /// <summary>
        /// Initialization size for the vertex array, in batch units.
        /// </summary>
		private const int InitialVertexArraySize = 256;

        /// <summary>
        /// The list of batch items to process.
        /// </summary>
        private SpriteSortItem[] _sortItemList;
        private SpriteBatchItem[] _batchItemList;
        /// <summary>
        /// Index pointer to the next available SpriteBatchItem in _batchItemList.
        /// </summary>
        private int _batchItemCount;
        
        /// <summary>
        /// The target graphics device.
        /// </summary>
        private readonly GraphicsDevice _device;

        /// <summary>
        /// Vertex index array. The values in this array never change.
        /// </summary>
        private short[] _index;

        private VertexPositionColorTexture[] _vertexArray;

		public SpriteBatcher (GraphicsDevice device)
		{
            _device = device;

			_batchItemList = new SpriteBatchItem[InitialBatchSize];
            _sortItemList = new SpriteSortItem[InitialBatchSize];
            _batchItemCount = 0;

            for (int i = 0; i < InitialBatchSize; i++)
                _batchItemList[i] = new SpriteBatchItem();

            EnsureArrayCapacity(InitialBatchSize);
		}

        /// <summary>
        /// Reuse a previously allocated SpriteBatchItem from the item pool. 
        /// if there is none available grow the pool and initialize new items.
        /// </summary>
        /// <returns></returns>
        public SpriteBatchItem CreateBatchItem(float sortKey)
        {
            if (_batchItemCount >= _batchItemList.Length)
            {
                var oldSize = _batchItemList.Length;
                var newSize = oldSize + oldSize / 2; // grow by x1.5
                newSize = (newSize + 63) & (~63); // grow in chunks of 64.
                Array.Resize(ref _batchItemList, newSize);
                for (int i = oldSize; i < newSize; i++)
                    _batchItemList[i] = new SpriteBatchItem();

                Array.Resize(ref _sortItemList, newSize);

                EnsureArrayCapacity(Math.Min(newSize, MaxBatchSize));
            }

            var item = _batchItemList[_batchItemCount];

            SpriteSortItem sort;
            sort.Index = _batchItemCount;
            sort.SortKey = sortKey;
            _sortItemList[_batchItemCount] = sort;

            _batchItemCount++;
            return item;
        }

        /// <summary>
        /// Resize and recreate the missing indices for the index and vertex position color buffers.
        /// </summary>
        /// <param name="numBatchItems"></param>
        private unsafe void EnsureArrayCapacity(int numBatchItems)
        {
            int neededCapacity = 6 * numBatchItems;
            if (_index != null && neededCapacity <= _index.Length)
            {
                // Short circuit out of here because we have enough capacity.
                return;
            }
            short[] newIndex = new short[6 * numBatchItems];
            int start = 0;
            if (_index != null)
            {
                _index.CopyTo(newIndex, 0);
                start = _index.Length / 6;
            }
            fixed (short* indexFixedPtr = newIndex)
            {
                var indexPtr = indexFixedPtr + (start * 6);
                for (var i = start; i < numBatchItems; i++, indexPtr += 6)
                {
                    /*
                     *  TL    TR
                     *   0----1 0,1,2,3 = index offsets for vertex indices
                     *   |   /| TL,TR,BL,BR are vertex references in SpriteBatchItem.
                     *   |  / |
                     *   | /  |
                     *   |/   |
                     *   2----3
                     *  BL    BR
                     */
                    // Triangle 1
                    *(indexPtr + 0) = (short)(i * 4);
                    *(indexPtr + 1) = (short)(i * 4 + 1);
                    *(indexPtr + 2) = (short)(i * 4 + 2);
                    // Triangle 2
                    *(indexPtr + 3) = (short)(i * 4 + 1);
                    *(indexPtr + 4) = (short)(i * 4 + 3);
                    *(indexPtr + 5) = (short)(i * 4 + 2);
                }
            }
            _index = newIndex;

            _vertexArray = new VertexPositionColorTexture[4 * numBatchItems];
        }

        private const int SORT_RUN = 32;

        private unsafe static void InsertionSort(SpriteSortItem* arr, int left, int right)
        {
            for (int i = left + 1; i <= right; i++)
            {
                var temp = arr[i];
                int j = i - 1;
                while (j >= left && arr[j].SortKey > temp.SortKey)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = temp;
            }
        }

        private SpriteSortItem[] _leftArray;
        private SpriteSortItem[] _rightArray;

        private unsafe void MergeSort(SpriteSortItem* arr, int l, int m, int r)
        {
            // original array is broken in two parts  
            // left and right array  
            int len1 = m - l + 1, len2 = r - m;

            if (_leftArray == null || _leftArray.Length < len1)
                _leftArray = new SpriteSortItem[len1];
            if (_rightArray == null || _rightArray.Length < len2)
                _rightArray = new SpriteSortItem[len2];

            fixed (SpriteSortItem* left = _leftArray)
            fixed (SpriteSortItem* right = _rightArray)
            {
                for (var x = 0; x < len1; x++)
                    left[x] = arr[l + x];
                for (var x = 0; x < len2; x++)
                    right[x] = arr[m + 1 + x];

                int i = 0;
                int j = 0;
                int k = l;

                // After comparing, we merge those two array  
                // in larger sub array  
                while (i < len1 && j < len2)
                {
                    if (left[i].SortKey <= right[j].SortKey)
                    {
                        arr[k] = left[i];
                        i++;
                    }
                    else
                    {
                        arr[k] = right[j];
                        j++;
                    }
                    k++;
                }

                // Copy remaining elements  
                // of left, if any  
                while (i < len1)
                {
                    arr[k] = left[i];
                    k++;
                    i++;
                }

                // Copy remaining element  
                // of right, if any  
                while (j < len2)
                {
                    arr[k] = right[j];
                    k++;
                    j++;
                }
            }
        }

        private unsafe void TimSort(SpriteSortItem* arr, int n)
        {
            //Console.WriteLine($"TimSort {n}");

            // Sort individual subarrays of size RUN  
            for (var i = 0; i < n; i += SORT_RUN)
                InsertionSort(arr, i, Math.Min((i + (SORT_RUN-1)), (n - 1)));

            // Start merging from size RUN (or 32).  
            // It will merge  
            // to form size 64, then  
            // 128, 256 and so on ....  
            for (var size = SORT_RUN; size < n; size = 2 * size)
            {
                // Pick starting point of  
                // left sub array. We  
                // are going to merge  
                // arr[left..left+size-1]  
                // and arr[left+size, left+2*size-1]  
                // After every merge, we increase  
                // left by 2*size  
                for (var left = 0; left < n; left += 2 * size)
                {
                    // Find ending point of left sub array  
                    // mid+1 is starting point of  
                    // right sub array  
                    var mid = Math.Min((left + size - 1), (n - 1));
                    var right = Math.Min((left + 2 * size - 1), (n - 1));

                    // Merge sub array arr[left.....mid] &  
                    // arr[mid+1....right]  
                    MergeSort(arr, left, mid, right);
                }
            }
        }

        /// <summary>
        /// Sorts the batch items and then groups batch drawing into maximal allowed batch sets that do not
        /// overflow the 16 bit array indices for vertices.
        /// </summary>
        /// <param name="sortMode">The type of depth sorting desired for the rendering.</param>
        /// <param name="effect">The custom effect to apply to the drawn geometry</param>
        public unsafe void DrawBatch(SpriteSortMode sortMode, Effect effect)
		{
            //if (effect != null && effect.IsDisposed)
                //throw new ObjectDisposedException("effect");

			// nothing to do
            if (_batchItemCount == 0)
				return;
			
			// sort the batch items
			switch ( sortMode )
			{
			case SpriteSortMode.Texture :                
			case SpriteSortMode.FrontToBack :
			case SpriteSortMode.BackToFront :
                    fixed (SpriteSortItem* sorted = _sortItemList)
                        TimSort(sorted, _batchItemCount);
				break;
			}

            // Determine how many iterations through the drawing code we need to make
            int batchIndex = 0;
            int batchCount = _batchItemCount;

            
            unchecked
            {
                _device._graphicsMetrics._spriteCount += batchCount;
            }

            // Iterate through the batches, doing short.MaxValue sets of vertices only.
            while(batchCount > 0)
            {
                // setup the vertexArray array
                var startIndex = 0;
                var index = 0;
                Texture2D tex = null;

                int numBatchesToProcess = batchCount;
                if (numBatchesToProcess > MaxBatchSize)
                {
                    numBatchesToProcess = MaxBatchSize;
                }

                // Avoid the array checking overhead by using pointer indexing!
                fixed (VertexPositionColorTexture* vertexArrayFixedPtr = _vertexArray)
                fixed (SpriteSortItem* sortItemList = _sortItemList)
                {
                    var vertexArrayPtr = vertexArrayFixedPtr;

                    // Draw the batches
                    for (int i = 0; i < numBatchesToProcess; i++, batchIndex++, index += 4)
                    {
                        var sortIndex = sortItemList[batchIndex].Index;
                        SpriteBatchItem item = _batchItemList[sortIndex];

                        // if the texture changed, we need to flush and bind the new texture
                        var shouldFlush = !ReferenceEquals(item.Texture, tex);
                        if (shouldFlush)
                        {
                            FlushVertexArray(startIndex, index, effect, tex);

                            tex = item.Texture;
                            startIndex = index = 0;
                            vertexArrayPtr = vertexArrayFixedPtr;
                            _device.Textures[0] = tex;
                        }

                        // store the SpriteBatchItem data in our vertexArray
                        vertexArrayPtr->Position.X = item.vertexTL.X;
                        vertexArrayPtr->Position.Y = item.vertexTL.Y;
                        vertexArrayPtr->Position.Z = item.depth;
                        vertexArrayPtr->Color = item.color;
                        vertexArrayPtr->TextureCoordinate = item.texCoordTL;
                        vertexArrayPtr++;
                        vertexArrayPtr->Position.X = item.vertexTR.X;
                        vertexArrayPtr->Position.Y = item.vertexTR.Y;
                        vertexArrayPtr->Position.Z = item.depth;
                        vertexArrayPtr->Color = item.color;
                        vertexArrayPtr->TextureCoordinate.X = item.texCoordBR.X;
                        vertexArrayPtr->TextureCoordinate.Y = item.texCoordTL.Y;
                        vertexArrayPtr++;
                        vertexArrayPtr->Position.X = item.vertexBL.X;
                        vertexArrayPtr->Position.Y = item.vertexBL.Y;
                        vertexArrayPtr->Position.Z = item.depth;
                        vertexArrayPtr->Color = item.color;
                        vertexArrayPtr->TextureCoordinate.X = item.texCoordTL.X;
                        vertexArrayPtr->TextureCoordinate.Y = item.texCoordBR.Y;
                        vertexArrayPtr++;
                        vertexArrayPtr->Position.X = item.vertexBR.X;
                        vertexArrayPtr->Position.Y = item.vertexBR.Y;
                        vertexArrayPtr->Position.Z = item.depth;
                        vertexArrayPtr->Color = item.color;
                        vertexArrayPtr->TextureCoordinate = item.texCoordBR;
                        vertexArrayPtr++;

                        // Release the texture.
                        item.Texture = null;
                    }
                }
                // flush the remaining vertexArray data
                FlushVertexArray(startIndex, index, effect, tex);
                // Update our batch count to continue the process of culling down
                // large batches
                batchCount -= numBatchesToProcess;
            }
            // return items to the pool.  
            _batchItemCount = 0;
		}

        /// <summary>
        /// Sends the triangle list to the graphics device. Here is where the actual drawing starts.
        /// </summary>
        /// <param name="start">Start index of vertices to draw. Not used except to compute the count of vertices to draw.</param>
        /// <param name="end">End index of vertices to draw. Not used except to compute the count of vertices to draw.</param>
        /// <param name="effect">The custom effect to apply to the geometry</param>
        /// <param name="texture">The texture to draw.</param>
        private void FlushVertexArray(int start, int end, Effect effect, Texture texture)
        {
            if (start == end)
                return;

            var vertexCount = end - start;

            // If the effect is not null, then apply each pass and render the geometry
            if (effect != null)
            {
                var passes = effect.CurrentTechnique.Passes;
                foreach (var pass in passes)
                {
                    pass.Apply();

                    // Whatever happens in pass.Apply, make sure the texture being drawn
                    // ends up in Textures[0].
                    _device.Textures[0] = texture;

                    _device.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertexArray,
                        0,
                        vertexCount,
                        _index,
                        0,
                        (vertexCount / 4) * 2,
                        VertexPositionColorTexture.VertexDeclaration);
                }
            }
            else
            {
                // If no custom effect is defined, then simply render.
                _device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexArray,
                    0,
                    vertexCount,
                    _index,
                    0,
                    (vertexCount / 4) * 2,
                    VertexPositionColorTexture.VertexDeclaration);
            }
        }
	}
}

