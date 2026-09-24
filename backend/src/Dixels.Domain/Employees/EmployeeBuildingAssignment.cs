using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Employees;

/// <summary>
/// Which single Building an employee is allowed to see. One row per employee — assigning a
/// new building overwrites the old one rather than adding a second row, since an employee
/// belongs to exactly one building at a time (see <see cref="SetBuilding"/>).
/// </summary>
public class EmployeeBuildingAssignment : AuditedAggregateRoot<Guid>
{
    public Guid EmployeeUserId { get; private set; }
    public Guid BuildingId { get; private set; }

    private EmployeeBuildingAssignment()
    {
        // EF Core
    }

    public EmployeeBuildingAssignment(Guid id, Guid employeeUserId, Guid buildingId)
        : base(id)
    {
        EmployeeUserId = employeeUserId;
        SetBuilding(buildingId);
    }

    public void SetBuilding(Guid buildingId)
    {
        BuildingId = buildingId;
    }
}
