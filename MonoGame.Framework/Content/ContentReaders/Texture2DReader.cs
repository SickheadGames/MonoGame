// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Content
{
    internal class Texture2DReader : ContentTypeReader<Texture2D>
    {
		internal Texture2DReader()
		{
			// Do nothing
		}

        protected internal override Texture2D Read(ContentReader reader, Texture2D existingInstance)
		{
			Texture2D texture = null;

            var surfaceFormat = (SurfaceFormat)reader.ReadInt32();
            uint width = reader.ReadUInt32();
            uint height = reader.ReadUInt32();
            int levelCount = reader.ReadInt32();
            int levelCountOutput = levelCount;

		    var device = reader.GraphicsDevice;

            var originalWidth = width;
		    if ((width & 0xFFFF0000) != 0)
		    {
                originalWidth = (uint)((width & 0xFFFF0000) >> 16);
                width = width & 0x0000FFFF;
		    }

            var originalHeight = height;
		    if ((height & 0xFFFF0000) != 0)
		    {
		        originalHeight = (uint)((height & 0xFFFF0000) >> 16);
                height = height & 0x0000FFFF;
		    }

            // If the system does not fully support Power of Two textures,
            // skip any mip maps supplied with any non PoT textures.
            if (levelCount > 1 && !device.GraphicsCapabilities.SupportsNonPowerOfTwo &&
                (!MathHelper.IsPowerOfTwo((int)width) || !MathHelper.IsPowerOfTwo((int)height)))
            {
                levelCountOutput = 1;
                System.Diagnostics.Debug.WriteLine(
                    "Device does not support non Power of Two textures. Skipping mipmaps.");
            }

			SurfaceFormat convertedFormat = surfaceFormat;
			switch (surfaceFormat)
			{
				case SurfaceFormat.Dxt1:
				case SurfaceFormat.Dxt1a:
                    if (!device.GraphicsCapabilities.SupportsDxt1)
						convertedFormat = SurfaceFormat.Color;
					break;
				case SurfaceFormat.Dxt1SRgb:
                    if (!device.GraphicsCapabilities.SupportsDxt1)
						convertedFormat = SurfaceFormat.ColorSRgb;
					break;
				case SurfaceFormat.Dxt3:
				case SurfaceFormat.Dxt5:
                    if (!device.GraphicsCapabilities.SupportsS3tc)
						convertedFormat = SurfaceFormat.Color;
					break;
				case SurfaceFormat.Dxt3SRgb:
				case SurfaceFormat.Dxt5SRgb:
                    if (!device.GraphicsCapabilities.SupportsS3tc)
						convertedFormat = SurfaceFormat.ColorSRgb;
					break;
				case SurfaceFormat.NormalizedByte4:
					convertedFormat = SurfaceFormat.Color;
					break;

                // TODO: What about other compressed formats?
                // Should we make this a platform function of sorts?
			}

            texture = existingInstance ?? new Texture2D(device, (int)width, (int)height, levelCountOutput > 1, convertedFormat);

            texture.OriginalWidth = (int)originalWidth;
            texture.OriginalHeight = (int)originalHeight;

#if OPENGL
            Threading.BlockOnUIThread(() =>
            {
#endif
                for (int level = 0; level < levelCount; level++)
			    {
				    var levelDataSizeInBytes = reader.ReadInt32();
                    var levelData = reader.ContentManager.GetScratchBuffer(levelDataSizeInBytes);
                    reader.Read(levelData, 0, levelDataSizeInBytes);
                    int levelWidth = (int)width >> level;
                    int levelHeight = (int)height >> level;

                    if (level >= levelCountOutput)
                        continue;

				    //Convert the image data if required
				    switch (surfaceFormat)
				    {
					    case SurfaceFormat.Dxt1:
                        case SurfaceFormat.Dxt1SRgb:
                        case SurfaceFormat.Dxt1a:
				            if (!reader.GraphicsDevice.GraphicsCapabilities.SupportsDxt1 && convertedFormat == SurfaceFormat.Color)
				            {
				                levelData = DxtUtil.DecompressDxt1(levelData, levelWidth, levelHeight);
				                levelDataSizeInBytes = levelData.Length;
				            }
				            break;
					    case SurfaceFormat.Dxt3:
					    case SurfaceFormat.Dxt3SRgb:
                            if (!reader.GraphicsDevice.GraphicsCapabilities.SupportsS3tc)
				                if (!reader.GraphicsDevice.GraphicsCapabilities.SupportsS3tc &&
				                    convertedFormat == SurfaceFormat.Color)
				                {
				                    levelData = DxtUtil.DecompressDxt3(levelData, levelWidth, levelHeight);
                                    levelDataSizeInBytes = levelData.Length;
                                }
				            break;
					    case SurfaceFormat.Dxt5:
					    case SurfaceFormat.Dxt5SRgb:
                            if (!reader.GraphicsDevice.GraphicsCapabilities.SupportsS3tc)
				                if (!reader.GraphicsDevice.GraphicsCapabilities.SupportsS3tc &&
				                    convertedFormat == SurfaceFormat.Color)
				                {
				                    levelData = DxtUtil.DecompressDxt5(levelData, levelWidth, levelHeight);
                                    levelDataSizeInBytes = levelData.Length;
                                }
				            break;
                        case SurfaceFormat.Bgra5551:
                            {
#if OPENGL
                                // Shift the channels to suit OpenGL
                                int offset = 0;
                                for (int y = 0; y < levelHeight; y++)
                                {
                                    for (int x = 0; x < levelWidth; x++)
                                    {
                                        ushort pixel = BitConverter.ToUInt16(levelData, offset);
                                        pixel = (ushort)(((pixel & 0x7FFF) << 1) | ((pixel & 0x8000) >> 15));
                                        levelData[offset] = (byte)(pixel);
                                        levelData[offset + 1] = (byte)(pixel >> 8);
                                        offset += 2;
                                    }
                                }
#endif
                            }
                            break;
					    case SurfaceFormat.Bgra4444:
						    {
#if OPENGL
                                // Shift the channels to suit OpenGL
							    int offset = 0;
							    for (int y = 0; y < levelHeight; y++)
							    {
								    for (int x = 0; x < levelWidth; x++)
								    {
									    ushort pixel = BitConverter.ToUInt16(levelData, offset);
									    pixel = (ushort)(((pixel & 0x0FFF) << 4) | ((pixel & 0xF000) >> 12));
									    levelData[offset] = (byte)(pixel);
									    levelData[offset + 1] = (byte)(pixel >> 8);
									    offset += 2;
								    }
							    }
#endif
						    }
						    break;
					    case SurfaceFormat.NormalizedByte4:
						    {
							    int bytesPerPixel = surfaceFormat.GetSize();
							    int pitch = levelWidth * bytesPerPixel;
							    for (int y = 0; y < levelHeight; y++)
							    {
								    for (int x = 0; x < levelWidth; x++)
								    {
									    int color = BitConverter.ToInt32(levelData, y * pitch + x * bytesPerPixel);
									    levelData[y * pitch + x * 4] = (byte)(((color >> 16) & 0xff)); //R:=W
									    levelData[y * pitch + x * 4 + 1] = (byte)(((color >> 8) & 0xff)); //G:=V
									    levelData[y * pitch + x * 4 + 2] = (byte)(((color) & 0xff)); //B:=U
									    levelData[y * pitch + x * 4 + 3] = (byte)(((color >> 24) & 0xff)); //A:=Q
								    }
							    }
						    }
						    break;
				    }
				
                    texture.SetData(level, null, levelData, 0, levelDataSizeInBytes);
			    }
#if OPENGL
            });
#endif
        			
			return texture;
		}
    }
}
