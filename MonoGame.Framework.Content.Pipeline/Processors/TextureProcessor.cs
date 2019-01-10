// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.ComponentModel;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors
{
    [ContentProcessor(DisplayName="Texture - MonoGame")]
    public class TextureProcessor : ContentProcessor<TextureContent, TextureContent>
    {
        public TextureProcessor()
        {
            ColorKeyColor = new Color(255, 0, 255, 255);
            ColorKeyEnabled = true;
            PremultiplyAlpha = true;
        }

        [DefaultValueAttribute(typeof(Color), "255,0,255,255")]
        public virtual Color ColorKeyColor { get; set; }

        [DefaultValueAttribute(true)]
        public virtual bool ColorKeyEnabled { get; set; }

        public virtual bool GenerateMipmaps { get; set; }

        [DefaultValueAttribute(true)]
        public virtual bool PremultiplyAlpha { get; set; }

        public virtual bool ResizeToPowerOfTwo { get; set; }

        public virtual bool MakeSquare { get; set; }

        public virtual TextureProcessorOutputFormat TextureFormat { get; set; }

        public override TextureContent Process(TextureContent input, ContentProcessorContext context)
        {
            if (ColorKeyEnabled || ResizeToPowerOfTwo || MakeSquare || PremultiplyAlpha || GenerateMipmaps)
            {
                // Convert to floating point format for modifications. Keep the original format for conversion back later on if required.
                var originalType = input.Faces[0][0].GetType();
                try
                {
                    input.ConvertBitmapType(typeof(PixelBitmapContent<Vector4>));
                }
                catch (Exception ex)
                {
                    context.Logger.LogImportantMessage("Could not convert input texture for processing. " + ex.ToString());
                    throw ex; 
                }

                if (GenerateMipmaps)
                    input.GenerateMipmaps(true);

                for (int f = 0; f < input.Faces.Count; ++f)
                {
                    var face = input.Faces[f];

                    var fwidth = 0;
                    var fheight = 0;

                    for (int m = 0; m < face.Count; ++m)
                    {
                        var bmp = (PixelBitmapContent<Vector4>)face[m];

                        if (ColorKeyEnabled)
                        {
                            bmp.ReplaceColor(ColorKeyColor.ToVector4(), Vector4.Zero);
                        }

                        if (ResizeToPowerOfTwo)
                        {
                            //if (!GraphicsUtil.IsPowerOfTwo(bmp.Width) || !GraphicsUtil.IsPowerOfTwo(bmp.Height) || (MakeSquare && bmp.Height != bmp.Width))
                            if (m == 0)
                            {
                                fwidth = bmp.Width;
                                fheight = bmp.Height;
                            }
                            else
                            {
                                if (fwidth > 1)
                                    fwidth /= 2;
                                if (fheight > 1)
                                    fheight /= 2;
                            }

                            fwidth = GraphicsUtil.GetNextPowerOfTwo(fwidth);
                            fheight = GraphicsUtil.GetNextPowerOfTwo(fheight);
                            if (MakeSquare)
                                fwidth = fheight = Math.Max(fwidth, fheight);

                            if (fwidth != bmp.Width || fheight != bmp.Height)
                            {
                                var resized = new PixelBitmapContent<Vector4>(fwidth, fheight);
                                BitmapContent.Copy(bmp, resized);
                                bmp = resized;
                            }
                        }
                        else if (MakeSquare && bmp.Height != bmp.Width)
                        {
                            var newSize = Math.Max(bmp.Width, bmp.Height);
                            var resized = new PixelBitmapContent<Vector4>(newSize, newSize);
                            BitmapContent.Copy(bmp, resized);
                        }

                        if (PremultiplyAlpha)
                        {
                            for (int y = 0; y < bmp.Height; ++y)
                            {
                                var row = bmp.GetRow(y);
                                for (int x = 0; x < bmp.Width; ++x)
                                    row[x] = Color.FromNonPremultiplied(row[x]).ToVector4();
                            }
                        }

                        face[m] = bmp;
                    }
                }

                // If no change to the surface format was desired, change it back now before it early outs
                if (TextureFormat == TextureProcessorOutputFormat.NoChange)
                    input.ConvertBitmapType(originalType);
            }

            // Get the texture profile for the platform and let it convert the texture.
            var texProfile = TextureProfile.ForPlatform(context.TargetPlatform);
            texProfile.ConvertTexture(context, input, TextureFormat, false);	

            return input;
        }
    }
}
