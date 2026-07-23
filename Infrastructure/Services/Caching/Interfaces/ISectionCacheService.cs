using System.Threading.Tasks;

namespace InkPulse.Worker.Infrastructure.Services.Caching.Interfaces
{
    public interface ISectionCacheService
    {
        Task SetAsync(ICacheable value);
        Task<T?> GetAsync<T>(string identifier) where T : class, ICacheable;
        Task RemoveAsync<T>(string identifier) where T : class, ICacheable;
    }
}
