// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    public abstract class TextureProfile
    {
        private static readonly LoadedTypeCollection<TextureProfile> _profiles = new LoadedTypeCollection<TextureProfile>();

        /// <summary>
        /// Find the profile for this target platform.
        /// </summary>
        /// <param name="platform">The platform target for textures.</param>
        /// <returns></returns>
        public static TextureProfile ForPlatform(TargetPlatform platform)
        {
            var profile = _profiles.FirstOrDefault(h => h.Supports(platform));
            if (profile != null)
                return profile;

            throw new PipelineException("There is no supported texture profile for the '" + platform + "' platform!");
        }

        /// <summary>
        /// Returns true if this profile supports texture processing for this platform.
        /// </summary>
        public abstract bool Supports(TargetPlatform platform);

        /// <summary>
        /// Determines if the texture format will require power-of-two dimensions and/or equal width and height.
        /// </summary>
        /// <param name="context">The processor context.</param>
        /// <param name="format">The desired texture format.</param>
        /// <param name="requiresPowerOfTwo">True if the texture format requires power-of-two dimensions.</param>
        /// <param name="requiresSquare">True if the texture format requires equal width and height.</param>
        /// <returns>True if the texture format requires power-of-two dimensions.</returns>
        public abstract void Requirements(ContentProcessorContext context, TextureProcessorOutputFormat format, out bool requiresPowerOfTwo, out bool requiresSquare);

        /// <summary>
        /// Performs conversion of the texture content to the correct format.
        /// </summary>
        /// <param name="context">The processor context.</param>
        /// <param name="content">The content to be compressed.</param>
        /// <param name="format">The user requested format for compression.</param>
        /// <param name="isSpriteFont">If the texture has represents a sprite font, i.e. is greyscale and has sharp black/white contrast.</param>
        public void ConvertTexture(ContentProcessorContext context, TextureContent content, TextureProcessorOutputFormat format, bool isSpriteFont)
        {
            // We do nothing in this case.
            if (format == TextureProcessorOutputFormat.NoChange)
                return;

            // VITA HACK!
            /*
            var face = content.Faces[0][0];
            if (face.Width == 1920 && face.Height == 1080)
            {
                var widthNew = 960;
                var heightNew = 540;

                // Store the original size for use at runtime.
                var content2d = content as Texture2DContent;
                content2d.OriginalWidth = face.Width;
                content2d.OriginalHeight = face.Height;

                // Alert the user of the resize we just did.
                context.Logger.LogWarning(string.Empty, content.Identity, "Texture was resized {0}x{1} to {2}x{3}!", face.Width, face.Height, widthNew, heightNew);

                var newFace = face.Resize(widthNew, heightNew);
                content.Faces[0].Clear();
                content.Faces[0].Add(newFace);
            }
            */

            // (yet another) VITA HACK!
            if (context.TargetPlatform == TargetPlatform.PSVita)
            {
                if (format == TextureProcessorOutputFormat.Color)
                {
                    // If this texture's color-space fits within a 4-bit or 8-bit paletted texture
                    // then convert it to that format, since it loses no quality.
                    // note: this (and the subsequent conversion to a paletted texture) greatly slows down texture processing 
                    //       do to being written super inefficiently!
                    {
                        HashSet<Color> palette = new HashSet<Color>();
                        for (var i = 0; i < content.Faces.Count; i++)
                        {
                            var mipchain = content.Faces[i];
                            for (var j = 0; j < mipchain.Count; j++)
                            {
                                var bmp = mipchain[j] as PixelBitmapContent<Vector4>;
                                for (int y = 0; y < bmp.Height; y++)
                                {
                                    for (int x = 0; x < bmp.Width; x++)
                                    {
                                        var colf = bmp.GetPixel(x, y);
                                        var col = new Color(colf.X, colf.Y, colf.Z, colf.W);
                                        palette.Add(col);

                                        if (palette.Count > 255)
                                        {
                                            goto done;
                                        }
                                    }
                                }
                            }
                        }

                    done:

                        /*
                        if (palette.Count <= 16)
                        {

                        }
                        else*/
                        if (palette.Count <= 255)
                        {
                            context.Logger.LogMessage("{0} unique colors - outputing a paletted texture", palette.Count);

                            var mipchain = content.Faces[0];
                            var face = mipchain[0];

                            mipchain.Clear();

                            var newFace = new P8BitmapContent(face.Width, face.Height);
                            BitmapContent.Copy(face, newFace);

                            mipchain.Add(newFace);

                            return;
                        }

                        //context.Logger.LogMessage("{0} unique colors - outputing a full color texture", palette.Count);
                    }

                    // otherwise output full color

                    content.ConvertBitmapType(typeof(PixelBitmapContent<Color>));
                    return;
                }
            }

            // Handle this common compression format.
            if (format == TextureProcessorOutputFormat.Color16Bit)
            {
                GraphicsUtil.CompressColor16Bit(content);
                return;
            }

            try
            {
                // All other formats require platform specific choices.
                PlatformCompressTexture(context, content, format, isSpriteFont);
            }
            catch (EntryPointNotFoundException ex)
            {
                context.Logger.LogImportantMessage("Could not find the entry point to compress the texture. " + ex.ToString());
                throw ex;
            }
            catch (DllNotFoundException ex)
            {
                context.Logger.LogImportantMessage("Could not compress texture. Required shared lib is missing. " + ex.ToString());
                throw ex;
            }
            catch (Exception ex)
            {
                context.Logger.LogImportantMessage("Could not convert texture. " + ex.ToString());
                throw ex;
            }
        }

        protected abstract void PlatformCompressTexture(ContentProcessorContext context, TextureContent content, TextureProcessorOutputFormat format, bool isSpriteFont);
    }
}
