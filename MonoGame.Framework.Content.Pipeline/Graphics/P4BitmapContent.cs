// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    public class P4BitmapContent : PalettedBitmapContent
    {
        public P4BitmapContent(int width, int height)
            : base(4, width, height)
        {
        }

        /// <summary>
        /// Gets the corresponding GPU texture format for the specified bitmap type.
        /// </summary>
        /// <param name="format">Format being retrieved.</param>
        /// <returns>The GPU texture format of the bitmap type.</returns>
        public override bool TryGetFormat(out SurfaceFormat format)
        {
            format = SurfaceFormat.P4;
            return true;
        }

        /// <summary>
        /// Returns a string description of the bitmap.
        /// </summary>
        /// <returns>Description of the bitmap.</returns>
        public override string ToString()
        {
            return "P4 " + Width + "x" + Height;
        }
    }
}
