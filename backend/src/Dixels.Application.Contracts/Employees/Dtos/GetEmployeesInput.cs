using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Employees;

public class GetEmployeesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Name/username/email search — matches anywhere, case-insensitive.</summary>
    public string? Filter { get; set; }

    /// <summary>Only employees assigned to this building. Null returns everyone regardless
    /// of assignment (assigned to any building, or none).</summary>
    public Guid? BuildingId { get; set; }
}
