// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Media
{
    public sealed partial class VideoPlayer : IDisposable
    {
        #region Fields

        private MediaState _state;
        private Video _currentVideo;
        private float _volume = 1.0f;
        private bool _isLooped = false;
        private bool _isMuted = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value that indicates whether the object is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets a value that indicates whether the player is playing video in a loop.
        /// </summary>
        public bool IsLooped
        {
            get { return _isLooped; }
            set
            {
                if (_isLooped == value)
                    return;

                _isLooped = value;
                PlatformSetIsLooped();
            }
        }

        /// <summary>
        /// Gets or sets the muted setting for the video player.
        /// </summary>
        public bool IsMuted
        {
            get { return _isMuted; }
            set
            {
                if (_isMuted == value)
                    return;

                _isMuted = value;
                PlatformSetIsMuted();
            }
        }

        /// <summary>
        /// Gets the play position within the currently playing video.
        /// </summary>
        public TimeSpan PlayPosition
        {
            get
            {
                if (_currentVideo == null || State == MediaState.Stopped)
                    return TimeSpan.Zero;

                return PlatformGetPlayPosition();
            }

            set
            {
                if (_currentVideo == null)
                    throw new Exception("Cannot set PlayPosition until after playback has begun.");
                
                PlatformSetPlayPosition(value);
            }
        }

        /// <summary>
        /// Gets the media playback state, MediaState.
        /// </summary>
        public MediaState State
        { 
            get
            {
                // Give the platform code a chance to update 
                // the playback state before we return the result.
                PlatformGetState(ref _state);
                return _state;
            }
        }

        /// <summary>
        /// Gets the Video that is currently playing.
        /// </summary>
        public Video Video { get { return _currentVideo; } }

        /// <summary>
        /// Video player volume, from 0.0f (silence) to 1.0f (full volume relative to the current device volume).
        /// </summary>
        public float Volume
        {
            get { return _volume; }
            
            set
            {
                if (value < 0.0f || value > 1.0f)
                    throw new ArgumentOutOfRangeException();

                _volume = value;

                if (_currentVideo != null)
                    PlatformSetVolume();
            }
        }

        #endregion

        #region Public API

        public VideoPlayer()
        {
            _state = MediaState.Stopped;

            PlatformInitialize();
        }

        /// <summary>
        /// Retrieves a Texture2D containing the current frame of video being played.
        /// </summary>
        /// <returns>The current frame of video.</returns>
        public Texture2D GetTexture()
        {
            if (_currentVideo == null)
                return null;

            return PlatformGetTexture();
        }

        /// <summary>
        /// Pauses the currently playing video.
        /// </summary>
        public void Pause()
        {
            if (_currentVideo == null)
                return;

            PlatformPause();

            _state = MediaState.Paused;
        }

        /// <summary>
        /// Plays a Video.
        /// </summary>
        /// <param name="video">Video to play.</param>
        public void Play(Video video)
        {
            if (video == null)
                throw new ArgumentNullException("video is null.");

            if (_currentVideo == video)
            {
                var state = State;
							
                // No work to do if we're already
                // playing this video.
                if (state == MediaState.Playing)
                    return;

                // If we try to Play the same video
                // from a paused state, just resume it instead.
                if (state == MediaState.Paused)
                {
                    PlatformResume();
                    return;
                }
            }
            
            _currentVideo = video;

            PlatformPlay();

            _state = MediaState.Playing;
        }

        /// <summary>
        /// Resumes a paused video.
        /// </summary>
        public void Resume()
        {
            if (_currentVideo == null)
                return;

            var state = State;

            // No work to do if we're already playing
            if (state == MediaState.Playing)
                return;

            if (state == MediaState.Stopped)
            {
                PlatformPlay();
                return;
            }

            PlatformResume();

            _state = MediaState.Playing;
        }

        /// <summary>
        /// Stops playing a video.
        /// </summary>
        public void Stop()
        {
            if (_currentVideo == null)
                return;

            PlatformStop();

            _state = MediaState.Stopped;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Immediately releases the unmanaged resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                PlatformDispose(disposing);
                IsDisposed = true;
            }
        }

        #endregion

    }
}