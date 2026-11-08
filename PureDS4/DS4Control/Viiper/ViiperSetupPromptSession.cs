// PureDS4 — Copyright (C) 2026 meiameiameia
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Threading;

namespace DS4Windows
{
    /// <summary>
    /// Process-local notification state, separate from backend readiness.
    /// No settings, hardware, services, windows or installer operations.
    /// </summary>
    internal sealed class ViiperSetupPromptSession
    {
        private int shown;

        internal bool TryBeginPrompt(bool ready, bool suppressPrompt,
            bool forcePrompt)
        {
            // Explicit Set up/Repair always remains available. It also counts
            // as this session's prompt, so later automatic calls do not nag.
            if (forcePrompt)
            {
                Interlocked.Exchange(ref shown, 1);
                return true;
            }
            if (ready || suppressPrompt) return false;

            // Claim atomically: startup and late checks can observe the same
            // unhealthy state without opening duplicate dialogs. This latch
            // affects notifications only, never the prerequisite verdict.
            return Interlocked.CompareExchange(ref shown, 1, 0) == 0;
        }

        internal void Reset() => Interlocked.Exchange(ref shown, 0);
    }
}
