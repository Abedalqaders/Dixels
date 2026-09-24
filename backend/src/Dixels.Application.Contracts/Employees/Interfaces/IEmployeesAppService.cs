using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Employees;

public interface IEmployeesAppService : IApplicationService
{
    /// <summary>Every user in the "employee" role, each with its current building
    /// assignment (if any) — paged, optionally filtered by name/username/email and/or by
    /// assigned building. Admin-only — see <see cref="GetMyBuildingAsync"/> for the
    /// employee-facing, scoped-to-self equivalent.</summary>
    Task<PagedResultDto<EmployeeDto>> GetListAsync(GetEmployeesInput input);

    /// <summary>Sets (or, with a null <c>BuildingId</c>, clears) which single building an
    /// employee can see. Admin-only.</summary>
    Task<EmployeeDto> AssignBuildingAsync(Guid employeeUserId, AssignEmployeeBuildingDto input);

    /// <summary>The current user's own assigned building, or null if they haven't been
    /// assigned one yet. Available to any authenticated user — this is what keeps an
    /// employee's own view scoped to just their building instead of the full admin list.</summary>
    Task<BuildingDto?> GetMyBuildingAsync();
}
