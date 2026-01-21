// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace Microsoft.Xna.Framework.Audio
{
    partial class WaveBank
    {
        private SoundEffectInstance PlatformCreateStream(int trackIndex, StreamInfo info, ref bool streaming)
        {
            throw new NotImplementedException("XACT streaming is not implemented on this platform.");
        }
    }
}

