using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows
{
    internal static class OutputBindingRetrySequence
    {
        internal static async Task<bool> RunAsync(
            IReadOnlyList<int> delaysMilliseconds,
            Func<int, CancellationToken, Task> delay,
            Func<bool> continueAfterAttempt,
            CancellationToken cancellationToken)
        {
            if (delaysMilliseconds == null)
                throw new ArgumentNullException(nameof(delaysMilliseconds));
            if (delay == null)
                throw new ArgumentNullException(nameof(delay));
            if (continueAfterAttempt == null)
                throw new ArgumentNullException(nameof(continueAfterAttempt));

            foreach (int delayMilliseconds in delaysMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await delay(delayMilliseconds, cancellationToken).
                    ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!continueAfterAttempt())
                    return false;
            }

            return !cancellationToken.IsCancellationRequested;
        }
    }
}
