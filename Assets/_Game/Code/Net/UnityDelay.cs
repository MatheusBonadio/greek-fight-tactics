using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Net
{
    internal static class UnityDelay
    {
        public static async Task WaitAsync(TimeSpan duration, CancellationToken cancellation) =>
            await Awaitable.WaitForSecondsAsync((float)duration.TotalSeconds, cancellation);
    }
}
