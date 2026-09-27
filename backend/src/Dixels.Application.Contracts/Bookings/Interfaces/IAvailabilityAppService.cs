using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Bookings;

public interface IAvailabilityAppService : IApplicationService
{
    /// <summary>The current user's bookable building and its spaces, or null when they aren't assigned to one.</summary>
    Task<BookableBuildingDto?> GetMyBuildingAsync();
}
