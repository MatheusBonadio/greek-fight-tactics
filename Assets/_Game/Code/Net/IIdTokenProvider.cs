using System.Threading;
using System.Threading.Tasks;

namespace Game.Net
{
    public interface IIdTokenProvider
    {
        Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellation);
    }
}
