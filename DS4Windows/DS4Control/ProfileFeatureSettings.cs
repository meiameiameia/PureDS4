/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    public enum AudioHapticsSourceKind : byte
    {
        ControllerAudio,
        SystemAudio,
        AppSession,
    }

    public enum AudioHapticsMode : byte
    {
        Mix,
        Replace,
    }

    public enum AudioHapticsBassFocus : byte
    {
        Deep,
        Balanced,
        Punchy,
        Wide,
    }

    public enum AudioHapticsResponse : byte
    {
        Subtle,
        Balanced,
        Strong,
    }

    public enum AudioHapticsAttack : byte
    {
        Soft,
        Balanced,
        Fast,
        Sharp,
    }

    public enum AudioHapticsRelease : byte
    {
        Tight,
        Balanced,
        Smooth,
        Long,
    }

    /// <summary>
    /// Per-profile audio-to-advanced-haptics settings. Defaults and ranges are
    /// shared by the UI, persistence layer, and native DS4Windows runtime.
    /// </summary>
    public sealed class AudioHapticsProfileSettings
    {
        public const int MinimumGainPercent = 0;
        public const int MaximumGainPercent = 200;
        public const int DefaultGainPercent = 100;

        public bool Enabled { get; set; }
        public bool StreamAppAudioToController { get; set; }
        public bool StreamAppAudioToHeadsetOnly { get; set; }
        public bool AutomaticGameDetection { get; set; }
        public AudioHapticsSourceKind Source { get; set; } = AudioHapticsSourceKind.SystemAudio;
        public AudioHapticsMode Mode { get; set; } = AudioHapticsMode.Mix;
        public int GainPercent { get; set; } = DefaultGainPercent;
        public AudioHapticsBassFocus BassFocus { get; set; } = AudioHapticsBassFocus.Balanced;
        public AudioHapticsResponse Response { get; set; } = AudioHapticsResponse.Balanced;
        public AudioHapticsAttack Attack { get; set; } = AudioHapticsAttack.Balanced;
        public AudioHapticsRelease Release { get; set; } = AudioHapticsRelease.Balanced;

        // App-session identity is deliberately redundant: Windows can recycle a
        // PID, while the Core Audio session identifiers remain stable enough to
        // restore a user's selection after an application restarts.
        public int ProcessId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ExecutableName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public string SessionIdentifier { get; set; } = string.Empty;
        public string SessionInstanceIdentifier { get; set; } = string.Empty;

        public AudioHapticsProfileSettings Normalize()
        {
            if (!Enum.IsDefined(typeof(AudioHapticsSourceKind), Source)) Source = AudioHapticsSourceKind.SystemAudio;
            if (!Enum.IsDefined(typeof(AudioHapticsMode), Mode)) Mode = AudioHapticsMode.Mix;
            if (!Enum.IsDefined(typeof(AudioHapticsBassFocus), BassFocus)) BassFocus = AudioHapticsBassFocus.Balanced;
            if (!Enum.IsDefined(typeof(AudioHapticsResponse), Response)) Response = AudioHapticsResponse.Balanced;
            if (!Enum.IsDefined(typeof(AudioHapticsAttack), Attack)) Attack = AudioHapticsAttack.Balanced;
            if (!Enum.IsDefined(typeof(AudioHapticsRelease), Release)) Release = AudioHapticsRelease.Balanced;
            GainPercent = Math.Clamp(GainPercent, MinimumGainPercent, MaximumGainPercent);
            DisplayName = (DisplayName ?? string.Empty).Trim();
            ExecutableName = (ExecutableName ?? string.Empty).Trim();
            ProcessPath = (ProcessPath ?? string.Empty).Trim();
            SessionIdentifier = (SessionIdentifier ?? string.Empty).Trim();
            SessionInstanceIdentifier = (SessionInstanceIdentifier ?? string.Empty).Trim();
            ProcessId = Math.Max(0, ProcessId);
            if (AutomaticGameDetection)
            {
                Source = AudioHapticsSourceKind.AppSession;
            }
            if (Source != AudioHapticsSourceKind.AppSession)
            {
                StreamAppAudioToController = false;
            }
            if (!StreamAppAudioToController)
            {
                StreamAppAudioToHeadsetOnly = false;
            }
            return this;
        }

        public AudioHapticsProfileSettings Clone() => new AudioHapticsProfileSettings
        {
            Enabled = Enabled,
            StreamAppAudioToController = StreamAppAudioToController,
            StreamAppAudioToHeadsetOnly = StreamAppAudioToHeadsetOnly,
            AutomaticGameDetection = AutomaticGameDetection,
            Source = Source,
            Mode = Mode,
            GainPercent = GainPercent,
            BassFocus = BassFocus,
            Response = Response,
            Attack = Attack,
            Release = Release,
            ProcessId = ProcessId,
            DisplayName = DisplayName,
            ExecutableName = ExecutableName,
            ProcessPath = ProcessPath,
            SessionIdentifier = SessionIdentifier,
            SessionInstanceIdentifier = SessionInstanceIdentifier,
        }.Normalize();

        public bool IsDefaultConfiguration() =>
            !Enabled && !StreamAppAudioToController &&
            !StreamAppAudioToHeadsetOnly &&
            !AutomaticGameDetection &&
            Source == AudioHapticsSourceKind.SystemAudio &&
            Mode == AudioHapticsMode.Mix && GainPercent == DefaultGainPercent &&
            BassFocus == AudioHapticsBassFocus.Balanced &&
            Response == AudioHapticsResponse.Balanced &&
            Attack == AudioHapticsAttack.Balanced &&
            Release == AudioHapticsRelease.Balanced && ProcessId == 0 &&
            string.IsNullOrWhiteSpace(DisplayName) &&
            string.IsNullOrWhiteSpace(ExecutableName) &&
            string.IsNullOrWhiteSpace(ProcessPath) &&
            string.IsNullOrWhiteSpace(SessionIdentifier) &&
            string.IsNullOrWhiteSpace(SessionInstanceIdentifier);
    }
}
