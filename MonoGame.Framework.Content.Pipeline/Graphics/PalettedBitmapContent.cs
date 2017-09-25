// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    public abstract class PalettedBitmapContent : BitmapContent
    {
        internal Color[] _colors;
        internal byte[] _colorData;  
        internal byte[] _indices;

        internal SurfaceFormat _format;
        internal int _bitsPerIndex;

        public PalettedBitmapContent(int bitsPerIndex, int width, int height)
            : base(width, height)
        {
            if (bitsPerIndex != 4 && bitsPerIndex != 8)
                throw new ArgumentException("Invalid bitsPerIndex value");
            
            _bitsPerIndex = bitsPerIndex;
            TryGetFormat(out _format);


            int numBits = width*height*_bitsPerIndex;
            int rem = numBits % 8;
            if (rem != 0)
            {
                // ???
                numBits += rem;
            }
            int numBytes = numBits / 8;
            _indices = new byte[numBytes];

            int numColors = (int)Math.Pow(2, _bitsPerIndex);
            _colors = new Color[numColors];

            int numColorBytes = numColors * 4;
            _colorData = new byte[numColorBytes];
        }

        public override byte[] GetPixelData()
        {
            var result = new byte[_indices.Length];
            Array.Copy(_indices, result, _indices.Length);
            return result;
        }

        public override void SetPixelData(byte[] sourceData)
        {
            Array.Copy(sourceData, _indices, _indices.Length);
        }

        protected override bool TryCopyFrom(BitmapContent sourceBitmap, Rectangle sourceRegion, Rectangle destinationRegion)
        {
            SurfaceFormat sourceFormat;
            if (!sourceBitmap.TryGetFormat(out sourceFormat))
                return false;

            SurfaceFormat format;
            TryGetFormat(out format);

            // A shortcut for copying the entire bitmap to another bitmap of the same type and format
            if (format == sourceFormat && (sourceRegion == new Rectangle(0, 0, Width, Height)) && sourceRegion == destinationRegion)
            {
                SetPixelData(sourceBitmap.GetPixelData());
                return true;
            }

            // Destination region copy is not yet supported
            if (destinationRegion != new Rectangle(0, 0, Width, Height))
                return false;

            // If the source is not Vector4 or requires resizing, send it through BitmapContent.Copy
            if (!(sourceBitmap is PixelBitmapContent<Vector4>) || sourceRegion.Width != destinationRegion.Width || sourceRegion.Height != destinationRegion.Height)
            {
                try
                {
                    BitmapContent.Copy(sourceBitmap, sourceRegion, this, destinationRegion);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            // iterate all of source bitmap's pixels collecting unique colors for the pallete
            // and storing indices.
            var bmp = sourceBitmap as PixelBitmapContent<Vector4>;
            HashSet<Color> palette = new HashSet<Color>(); 
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var colf = bmp.GetPixel(x, y);
                    var col = new Color(colf.X, colf.Y, colf.Z, colf.W);                    
                    palette.Add(col);
                }
            }

            Array.Clear(_colors, 0, _colors.Length);
            palette.CopyTo(_colors);

            Array.Clear(_indices, 0, _indices.Length);
            int pixelIndex = 0;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var colf = bmp.GetPixel(x, y);
                    var col = new Color(colf.X, colf.Y, colf.Z, colf.W);
                    int colorIndex = Array.IndexOf<Color>(_colors, col);
                    if (colorIndex == -1)
                    {
                        throw new Exception("Logic error in PalettedBitmapContent processing");
                    }
                    
                    _indices[pixelIndex] = (byte)colorIndex;
                    pixelIndex++;
                }
            }

            // Copy from Color[] to bit-equivalent byte[]
            {
                Array.Clear(_colorData, 0, _colorData.Length);

                unsafe
                {
                    int dstIndex = 0;
                    fixed (byte* dst = _colorData)
                    {
                        fixed (Color* src = _colors)
                        {
                            for (int i = 0; i < _colors.Length; i++)
                            {
                                var col = src[i];

                                dst[dstIndex + 0] = col.R;
                                dst[dstIndex + 1] = col.G;
                                dst[dstIndex + 2] = col.B;
                                dst[dstIndex + 3] = col.A;
                                
                                dstIndex += 4;
                            }
                        }
                    }
                }
            }

            return true; 
        }

        protected override bool TryCopyTo(BitmapContent destinationBitmap, Rectangle sourceRegion, Rectangle destinationRegion)
        {
            SurfaceFormat destinationFormat;
            if (!destinationBitmap.TryGetFormat(out destinationFormat))
                return false;

            SurfaceFormat format;
            TryGetFormat(out format);

            // A shortcut for copying the entire bitmap to another bitmap of the same type and format
            var fullRegion = new Rectangle(0, 0, Width, Height);
            if ((format == destinationFormat) && (sourceRegion == fullRegion) && (sourceRegion == destinationRegion))
            {
                destinationBitmap.SetPixelData(GetPixelData());
                return true;
            }

            // No other support for copying from a paletted texture yet
            return false;
        }
    }
}
