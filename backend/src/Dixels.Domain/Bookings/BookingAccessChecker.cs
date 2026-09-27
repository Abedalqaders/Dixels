using System;
using System.Threading.Tasks;
using Dixels.Employees;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>
/// Decides where a user may book. Today that's exactly their one assigned building. Every
/// booking path goes through this class, so letting people book in more buildings later
/// (e.g. a multi-building assignment) changes this one file, not every caller.
/// </summary>
public class BookingAccessChecker : DomainService
{
    private readonly IRepository<EmployeeBuildingAssignment, Guid> _assignmentRepository;

    public BookingAccessChecker(IRepository<EmployeeBuildingAssignment, Guid> assignmentRepository)
    {
        _assignmentRepository = assignmentRepository;
    }

    /// <summary>The building this user can book in, or null when they aren't assigned to one.</summary>
    public async Task<Guid?> FindBookableBuildingIdAsync(Guid userId)
    {
        var assignment = await _assignmentRepository.FindAsync(a => a.EmployeeUserId == userId);
        return assignment?.BuildingId;
    }

    public async Task EnsureCanBookAsync(Guid userId, Guid buildingId)
    {
        if (await FindBookableBuildingIdAsync(userId) != buildingId)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingNotAssignedToBuilding);
        }
    }
}
