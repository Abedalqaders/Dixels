using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Bookings;

public interface IAvailabilityAppService : IApplicationService
{
    /// <summary>The current user's bookable building and its spaces, or null when they aren't assigned to one.</summary>
    Task<BookableBuildingDto?> GetMyBuildingAsync();

    /// <summary>Every space in my building checked against one window, with its day around it.</summary>
    Task<AvailabilitySearchResultDto> SearchAsync(SearchAvailabilityInput input);

    /// <summary>One space's open times, closures and bookings, day by day — for the booking form.</summary>
    Task<SpaceDaysDto> GetSpaceDaysAsync(Guid spaceId, GetSpaceDaysInput input);
}
