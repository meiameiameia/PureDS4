using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows
{
    internal sealed class MacroExecutionCoordinator
    {
        private sealed class Session
        {
            internal readonly object Gate = new object();
            internal CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal readonly List<Task> Tasks = new List<Task>();
            internal readonly Dictionary<string, Task> Queues = new Dictionary<string, Task>();
            internal readonly List<Action> HeldReleases = new List<Action>();
            internal bool Suspended;
        }

        private readonly Session[] sessions = Enumerable.Range(0, Global.MAX_DS4_CONTROLLER_COUNT)
            .Select(_ => new Session()).ToArray();

        internal Task Schedule(int device, string queue, Action<CancellationToken> play)
        {
            Session session = sessions[device];
            lock (session.Gate)
            {
                if (session.Suspended) return Task.CompletedTask;
                session.Tasks.RemoveAll(task => task.IsCompleted);
                CancellationToken token = session.Cancellation.Token;
                Task previous = queue != null && session.Queues.TryGetValue(queue, out Task queued)
                    ? queued : Task.CompletedTask;
                Task task = Task.Run(async () =>
                {
                    try
                    {
                        await previous.WaitAsync(token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        play(token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                    catch (Exception ex) { AppLogger.LogToGui("Macro failed: " + ex.Message, false); }
                });
                session.Tasks.Add(task);
                if (queue != null) session.Queues[queue] = task;
                return task;
            }
        }

        internal void RegisterCleanup(int device, Action release)
        {
            Session session = sessions[device];
            lock (session.Gate)
                session.HeldReleases.Add(release);
        }

        internal void CompleteCleanup(int device, Action release)
        {
            Session session = sessions[device];
            lock (session.Gate) session.HeldReleases.Remove(release);
        }

        internal bool SuspendAndDrain(int device, TimeSpan timeout)
        {
            Session session = sessions[device];
            Task[] tasks;
            lock (session.Gate)
            {
                session.Suspended = true;
                session.Cancellation.Cancel();
                tasks = session.Tasks.ToArray();
            }
            if (!Task.WhenAll(tasks).Wait(timeout)) return false;
            lock (session.Gate)
            {
                foreach (Action release in session.HeldReleases.ToArray())
                {
                    release();
                    session.HeldReleases.Remove(release);
                }
                session.Tasks.Clear();
                session.Queues.Clear();
            }
            return true;
        }

        internal bool Resume(int device)
        {
            Session session = sessions[device];
            lock (session.Gate)
            {
                if (!session.Suspended) return true;
                // A timed-out or failed drain cannot be replaced by a new
                // session that races its still-owned input and cleanup work.
                if (session.Tasks.Any(task => !task.IsCompleted) || session.HeldReleases.Count != 0)
                    return false;
                // Old tasks retain their cancelled token even if a rollback
                // starts admitting macros from a new managed session.
                CancellationTokenSource previous = session.Cancellation;
                _ = Task.WhenAll(session.Tasks).ContinueWith(_ => previous.Dispose(), TaskScheduler.Default);
                session.Cancellation = new CancellationTokenSource();
                session.Suspended = false;
                return true;
            }
        }
    }

    internal sealed class MacroInputOwnership
    {
        private readonly object gate = new object();
        private readonly Dictionary<(int Code, bool Scan), Dictionary<object, Action<bool>>> held = new();

        internal void Set(object owner, int code, bool scan, bool down, Action<bool> emit)
        {
            lock (gate)
            {
                var key = (code, scan);
                if (!held.TryGetValue(key, out var owners))
                {
                    if (!down) return;
                    held[key] = owners = new Dictionary<object, Action<bool>>();
                }
                if (down)
                {
                    if (owners.ContainsKey(owner)) return;
                    bool firstOwner = owners.Count == 0;
                    // Emission can fail after touching the output handler.
                    // Retain ownership so finally/drain can still release it.
                    owners.Add(owner, emit);
                    if (firstOwner) emit(true);
                }
                else if (owners.ContainsKey(owner))
                {
                    if (owners.Count == 1) emit(false);
                    owners.Remove(owner);
                    if (owners.Count == 0) held.Remove(key);
                }
            }
        }

        internal void Release(object owner)
        {
            lock (gate)
            {
                foreach (var entry in held.ToArray())
                {
                    if (!entry.Value.TryGetValue(owner, out Action<bool> emit)) continue;
                    Set(owner, entry.Key.Code, entry.Key.Scan, false, emit);
                }
            }
        }
    }
}
