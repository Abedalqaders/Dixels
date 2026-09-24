using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Employees;

public class EmployeeDto : EntityDto<Guid>
{
    public string UserName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Surname { get; set; }
    public string? Email { get; set; }

    public Guid? AssignedBuildingId { get; set; }
    public string? AssignedBuildingName { get; set; }
}
