// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;

namespace Microsoft.Xna.Framework.Graphics
{

    // TODO: We should convert the types below 
    // into the start of a Shader reflection API.

    internal enum SamplerType
    {
        Sampler2D = 0,
        SamplerCube = 1,
        SamplerVolume = 2,
        Sampler1D = 3,
    }

    internal struct SamplerInfo
    {
        public SamplerType type;
        public int textureSlot;
        public int samplerSlot;
        public string name;
		public SamplerState state;

        // TODO: This should be moved to EffectPass.
        public int parameter;
    }

    internal struct VertexAttribute
    {
        public VertexElementUsage usage;
        public int index;
        public string name;
        public int location;
    }

    internal partial class Shader : GraphicsResource
	{
        /// <summary>
        /// Returns the platform specific shader profile identifier.
        /// </summary>
        public static int Profile { get { return PlatformProfile(); } }

        /// <summary>
        /// A hash value which can be used to compare shaders.
        /// </summary>
        internal int HashKey { get; private set; }

        public SamplerInfo[] Samplers { get; private set; }

	    public int[] CBuffers { get; private set; }

        public ShaderStage Stage { get; private set; }

        public VertexAttribute[] Attributes { get; private set; }

        internal Shader(GraphicsDevice device, BinaryReader reader)
        {
            GraphicsDevice = device;

            var isVertexShader = reader.ReadBoolean();
            Stage = isVertexShader ? ShaderStage.Vertex : ShaderStage.Pixel;

            var shaderLength = reader.ReadInt32();
            var shaderBytecode = reader.ReadBytes(shaderLength);

            var samplerCount = (int)reader.ReadByte();
            Samplers = new SamplerInfo[samplerCount];
            for (var s = 0; s < samplerCount; s++)
            {
                ref SamplerInfo samp = ref Samplers[s];

                samp.type = (SamplerType)reader.ReadByte();
                samp.textureSlot = reader.ReadByte();
                samp.samplerSlot = reader.ReadByte();

				if (reader.ReadBoolean())
				{
                    samp.state = new SamplerState();
					samp.state.AddressU = (TextureAddressMode)reader.ReadByte();
					samp.state.AddressV = (TextureAddressMode)reader.ReadByte();
					samp.state.AddressW = (TextureAddressMode)reader.ReadByte();
                    samp.state.BorderColor = new Color(
                        reader.ReadByte(), 
                        reader.ReadByte(), 
                        reader.ReadByte(), 
                        reader.ReadByte());
					samp.state.Filter = (TextureFilter)reader.ReadByte();
					samp.state.MaxAnisotropy = reader.ReadInt32();
					samp.state.MaxMipLevel = reader.ReadInt32();
					samp.state.MipMapLevelOfDetailBias = reader.ReadSingle();
				}

                samp.name = reader.ReadString();
                samp.parameter = reader.ReadByte();
            }

            var cbufferCount = (int)reader.ReadByte();
            CBuffers = new int[cbufferCount];
            for (var c = 0; c < cbufferCount; c++)
                CBuffers[c] = reader.ReadByte();

            var attributeCount = (int)reader.ReadByte();
            Attributes = new VertexAttribute[attributeCount];
            for (var a = 0; a < attributeCount; a++)
            {
                ref VertexAttribute attr = ref Attributes[a];
                attr.name = reader.ReadString();
                attr.usage = (VertexElementUsage)reader.ReadByte();
                attr.index = reader.ReadByte();
                attr.location = reader.ReadInt16();
            }

            PlatformConstruct(isVertexShader, shaderBytecode);
        }

        internal protected override void GraphicsDeviceResetting()
        {
            PlatformGraphicsDeviceResetting();
        }
	}
}

