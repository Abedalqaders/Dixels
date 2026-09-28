using System;
using System.Threading.Tasks;
using Dixels.Users;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace Dixels.Bookings;

/// <summary>
/// Decides where a user may book. Today that's exactly their one assigned building. Every
/// booking path goes through this class, so letting people book in more buildings later
/// (e.g. a multi-building assignment) changes this one file, not every caller.
/// </summary>
public class BookingAccessChecker : DomainService
{
    private readonly IIdentityUserRepository _userRepository;

    public BookingAccessChecker(IIdentityUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>The building this user can book in, or null when they aren't assigned to one.</summary>
    public async Task<Guid?> FindBookableBuildingIdAsync(Guid userId)
    {
        // includeDetails: false — only the BuildingId column is needed, not roles/claims/logins.
        var user = await _userRepository.FindAsync(userId, includeDetails: false);
        return user?.GetBuildingId();
    }

    public async Task EnsureCanBookAsync(Guid userId, Guid buildingId)
    {
        if (await FindBookableBuildingIdAsync(userId) != buildingId)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingNotAssignedToBuilding);
        }
    }
}
