using System.Collections.Generic;

namespace Dixels.SpaceManagement;

/// <summary>
/// Returned by every level's <c>UpdateConstraintsAsync</c> (Building/Floor/Space). The save
/// has already gone through by the time this is returned — <see cref="Warnings"/> is
/// informational (e.g. a descendant whose own override no longer fits a just-tightened
/// parent), never a rejection. <see cref="ConcurrencyStamp"/> is the fresh value to send on
/// the next save.
/// </summary>
public class ConstraintsSaveResultDto
{
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}
