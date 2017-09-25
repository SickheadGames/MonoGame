// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    public class P8BitmapContent : PalettedBitmapContent
    {
        public P8BitmapContent(int width, int height)
            : base(8, width, height)
        {
        }

        /// <summary>
        /// Gets the corresponding GPU texture format for the specified bitmap type.
        /// </summary>
        /// <param name="format">Format being retrieved.</param>
        /// <returns>The GPU texture format of the bitmap type.</returns>
        public override bool TryGetFormat(out SurfaceFormat format)
        {
            format = SurfaceFormat.P8;
            return true;
        }

        /// <summary>
        /// Returns a string description of the bitmap.
        /// </summary>
        /// <returns>Description of the bitmap.</returns>
        public override string ToString()
        {
            return "P8 " + Width + "x" + Height;
        }
    }
}
