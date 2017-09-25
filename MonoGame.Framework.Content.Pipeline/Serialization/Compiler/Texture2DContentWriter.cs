// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    [ContentTypeWriter]
    class Texture2DWriter : BuiltInContentWriter<Texture2DContent>
    {
        protected internal override void Write(ContentWriter output, Texture2DContent value)
        {
            var mipmaps = value.Faces[0];   // Mipmap chain.
            var level0 = mipmaps[0];        // Most detailed mipmap level.

            SurfaceFormat format;
            if (!level0.TryGetFormat(out format))
                throw new Exception("Couldn't get Format for TextureContent.");

            output.Write((int)format);

            // Pack the original size and the actual size together
            // to avoid breaking the file format.
            var width = (int)((value.OriginalWidth << 16) + level0.Width);
            var height = (int)((value.OriginalHeight << 16) + level0.Height);
            output.Write(width);
            output.Write(height);

            output.Write(mipmaps.Count);    // Number of mipmap levels.

            foreach (var level in mipmaps)
            {
                if (format == SurfaceFormat.P4 || format == SurfaceFormat.P8)
                {
                    var pbmp = level as PalettedBitmapContent;
                    var paletteData = pbmp._colorData;
                    output.Write(paletteData.Length);
                    output.Write(paletteData);
                }

                var pixelData = level.GetPixelData();
                output.Write(pixelData.Length);
                output.Write(pixelData);
            }
        }
    }
}
