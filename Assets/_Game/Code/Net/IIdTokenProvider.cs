using System.Threading;
using System.Threading.Tasks;

namespace Game.Net
{
    public interface IIdTokenProvider
    {
        string Uid { get; }

        Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellation);
    }
}
