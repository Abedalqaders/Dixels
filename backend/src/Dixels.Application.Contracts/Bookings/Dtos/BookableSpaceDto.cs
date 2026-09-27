using System;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

public class BookableSpaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid SpaceTypeId { get; set; }
    public string SpaceTypeName { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? MinAttendees { get; set; }
    public FieldValueDto<int[]> Days { get; set; } = new();
    public FieldValueDto<OperatingWindowDto> Hours { get; set; } = new();
    public FieldValueDto<int> MaxDurationMinutes { get; set; } = new();
}
