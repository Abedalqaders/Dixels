using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Employees;

[Authorize]
public class EmployeesAppService : DixelsAppService, IEmployeesAppService
{
    private const string EmployeeRoleName = "employee";

    private readonly IdentityUserManager _userManager;
    private readonly IRepository<EmployeeBuildingAssignment, Guid> _assignmentRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;

    public EmployeesAppService(
        IdentityUserManager userManager,
        IRepository<EmployeeBuildingAssignment, Guid> assignmentRepository,
        IRepository<Building, Guid> buildingRepository)
    {
        _userManager = userManager;
        _assignmentRepository = assignmentRepository;
        _buildingRepository = buildingRepository;
    }

    [Authorize(DixelsPermissions.Employees.Default)]
    public async Task<PagedResultDto<EmployeeDto>> GetListAsync(GetEmployeesInput input)
    {
        // GetUsersInRoleAsync has no queryable/paged overload, so filtering and paging happen
        // in memory after the fetch — fine at "how many employees a company has" scale (the
        // same reasoning FindNarrowingConflicts elsewhere in this codebase leans on), and
        // avoids a bespoke SQL-level join from IdentityUser through AppUserRoles into
        // EmployeeBuildingAssignment just to filter by building.
        var employees = await _userManager.GetUsersInRoleAsync(EmployeeRoleName);
        var employeeIds = employees.Select(e => e.Id).ToList();

        var assignments = employeeIds.Count == 0
            ? new List<EmployeeBuildingAssignment>()
            : await _assignmentRepository.GetListAsync(a => employeeIds.Contains(a.EmployeeUserId));
        var assignmentByUserId = assignments.ToDictionary(a => a.EmployeeUserId);

        var buildingIds = assignments.Select(a => a.BuildingId).Distinct().ToList();
        var buildings = buildingIds.Count == 0
            ? new List<Building>()
            : await _buildingRepository.GetListAsync(b => buildingIds.Contains(b.Id));
        var buildingById = buildings.ToDictionary(b => b.Id);

        IEnumerable<IdentityUser> filtered = employees;

        if (input.BuildingId is not null)
        {
            filtered = filtered.Where(e =>
                assignmentByUserId.TryGetValue(e.Id, out var assignment) && assignment.BuildingId == input.BuildingId.Value);
        }

        if (!input.Filter.IsNullOrWhiteSpace())
        {
            var term = input.Filter!;
            filtered = filtered.Where(e =>
                Matches(e.UserName, term) || Matches(e.Name, term) || Matches(e.Surname, term) || Matches(e.Email, term));
        }

        var ordered = filtered.OrderBy(e => e.UserName).ToList();
        var page = ordered.Skip(input.SkipCount).Take(input.MaxResultCount);

        var result = page
            .Select(e => MapToDto(e, assignmentByUserId.GetValueOrDefault(e.Id), buildingById))
            .ToList();

        return new PagedResultDto<EmployeeDto>(ordered.Count, result);
    }

    private static bool Matches(string? value, string term)
    {
        return value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    // Fully-qualified route: ABP's conventional-controller routing doesn't auto-prepend the
    // "api/app/employees" controller prefix once an action carries its own explicit Http*
    // attribute (see BuildingsAppService.UpdateConstraintsAsync for the same reasoning).
    [HttpPut("api/app/employees/{employeeUserId}/building")]
    [Authorize(DixelsPermissions.Employees.Assign)]
    public async Task<EmployeeDto> AssignBuildingAsync(Guid employeeUserId, AssignEmployeeBuildingDto input)
    {
        var employee = await _userManager.GetByIdAsync(employeeUserId);
        if (!await _userManager.IsInRoleAsync(employee, EmployeeRoleName))
        {
            throw new BusinessException(DixelsDomainErrorCodes.UserIsNotAnEmployee);
        }

        var assignment = await _assignmentRepository.FindAsync(a => a.EmployeeUserId == employeeUserId);

        if (input.BuildingId is null)
        {
            if (assignment is not null)
            {
                await _assignmentRepository.DeleteAsync(assignment);
            }

            return MapToDto(employee, null, new Dictionary<Guid, Building>());
        }

        // GetAsync throws ABP's own EntityNotFoundException (404) if the id doesn't exist —
        // no bespoke "building not found" check needed here.
        var building = await _buildingRepository.GetAsync(input.BuildingId.Value);

        if (assignment is null)
        {
            assignment = new EmployeeBuildingAssignment(GuidGenerator.Create(), employeeUserId, building.Id);
            await _assignmentRepository.InsertAsync(assignment);
        }
        else
        {
            assignment.SetBuilding(building.Id);
            await _assignmentRepository.UpdateAsync(assignment);
        }

        return MapToDto(employee, assignment, new Dictionary<Guid, Building> { [building.Id] = building });
    }

    [HttpGet("api/app/employees/my-building")]
    public async Task<BuildingDto?> GetMyBuildingAsync()
    {
        var userId = CurrentUser.GetId();
        var assignment = await _assignmentRepository.FindAsync(a => a.EmployeeUserId == userId);
        if (assignment is null)
        {
            return null;
        }

        var building = await _buildingRepository.FindAsync(assignment.BuildingId);

        // The assigned building was (soft-)deleted after the assignment was made — treat
        // this the same as "not assigned" rather than surfacing a broken reference; the
        // admin needs to reassign, not the employee.
        return building is null ? null : MapBuildingToDto(building);
    }

    private static EmployeeDto MapToDto(IdentityUser employee, EmployeeBuildingAssignment? assignment, IReadOnlyDictionary<Guid, Building> buildingById)
    {
        var building = assignment is not null && buildingById.TryGetValue(assignment.BuildingId, out var b) ? b : null;

        return new EmployeeDto
        {
            Id = employee.Id,
            UserName = employee.UserName,
            Name = employee.Name,
            Surname = employee.Surname,
            Email = employee.Email,
            AssignedBuildingId = building?.Id,
            AssignedBuildingName = building?.Name,
        };
    }

    private static BuildingDto MapBuildingToDto(Building building)
    {
        return new BuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            BuildingNumber = building.BuildingNumber,
            Timezone = building.Timezone,
            Days = ConstraintDtoConversions.ToDayArray(building.Days),
            Hours = ConstraintDtoConversions.ToWindowDto(building.Hours),
            MaxDurationMinutes = building.MaxDurationMinutes,
            MaxHorizonDays = building.MaxHorizonDays,
            MinLeadMinutes = building.MinLeadMinutes,
            ConcurrencyStamp = building.ConcurrencyStamp,
        };
    }
}
