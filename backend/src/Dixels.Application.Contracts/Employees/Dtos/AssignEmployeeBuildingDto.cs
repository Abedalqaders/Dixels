using System;

namespace Dixels.Employees;

public class AssignEmployeeBuildingDto
{
    /// <summary>Null clears the employee's building assignment.</summary>
    public Guid? BuildingId { get; set; }
}
