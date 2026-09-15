using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;

namespace Game.Net
{
    public sealed class SaveSynchronizer
    {
        public const int MaxPushAttempts = 3;
        public const double BaseBackoffSeconds = 2;

        private readonly LocalSaveStore local;
        private readonly FirestoreSaveClient remote;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;

        public SaveSynchronizer(LocalSaveStore local, FirestoreSaveClient remote, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            this.local = local ?? throw new ArgumentNullException(nameof(local));
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            this.delay = delay ?? UnityDelay.WaitAsync;
        }

        public SaveData Current { get; private set; }

        public bool HasPendingPush { get; private set; }

        public RemoteSaveStatus LastRemoteStatus { get; private set; } = RemoteSaveStatus.Unavailable;

        public void SaveLocal(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            local.Save(save);
            Current = save;
            HasPendingPush = true;
        }

        public async Task<bool> PushPendingAsync(CancellationToken cancellation)
        {
            var forceRefresh = false;
            for (var attempt = 1; HasPendingPush && Current != null && attempt <= MaxPushAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    try
                    {
                        await delay(TimeSpan.FromSeconds(BaseBackoffSeconds * Math.Pow(2, attempt - 2)), cancellation);
                    }
                    catch (OperationCanceledException)
                    {
                        LastRemoteStatus = RemoteSaveStatus.Cancelled;
                        return false;
                    }
                }

                var pushed = Current;
                var result = await remote.PushAsync(pushed, cancellation, forceRefresh);
                LastRemoteStatus = result.Status;
                switch (result.Status)
                {
                    case RemoteSaveStatus.Ok:
                        HasPendingPush = !ReferenceEquals(pushed, Current);
                        continue;
                    case RemoteSaveStatus.Unauthorized:
                        forceRefresh = true;
                        continue;
                    case RemoteSaveStatus.Unavailable:
                        continue;
                    default:
                        return false;
                }
            }

            return !HasPendingPush;
        }

        public async Task<SaveData> ReconcileAsync(CancellationToken cancellation)
        {
            var localSave = local.TryLoad(out var loaded, out _) ? loaded : null;
            var pulled = await remote.PullAsync(cancellation);
            LastRemoteStatus = pulled.Status;

            if (pulled.Status is not (RemoteSaveStatus.Ok or RemoteSaveStatus.NotFound))
            {
                Current = localSave;
                HasPendingPush = localSave != null;
                return Current;
            }

            var remoteSave = pulled.Status == RemoteSaveStatus.Ok ? pulled.Save : null;
            Current = SaveConflictResolver.PickNewest(localSave, remoteSave);
            if (Current == null)
            {
                HasPendingPush = false;
                return null;
            }

            if (ReferenceEquals(Current, remoteSave) && (localSave == null || remoteSave.Timestamp > localSave.Timestamp))
            {
                local.Save(remoteSave);
                HasPendingPush = false;
                return Current;
            }

            HasPendingPush = remoteSave == null || localSave.Timestamp > remoteSave.Timestamp;
            if (HasPendingPush)
            {
                await PushPendingAsync(cancellation);
            }

            return Current;
        }
    }
}
