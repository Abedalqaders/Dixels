using System;
using System.Collections.Generic;

namespace Dixels.Bookings;

public class BookableFloorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? FloorNumber { get; set; }
    public List<BookableSpaceDto> Spaces { get; set; } = new();
}
