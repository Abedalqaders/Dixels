using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Users;

/// <summary>Someone in my building I can invite to a booking: just enough to pick them.</summary>
public class ColleagueDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class GetColleaguesInput
{
    public const int MinFilterLength = 2;
    public const int MaxMaxResultCount = 20;

    /// <summary>Part of a name, user name or email. Under <see cref="MinFilterLength"/> characters finds no one.</summary>
    [StringLength(128)]
    public string? Filter { get; set; }

    /// <summary>At most <see cref="MaxMaxResultCount"/>; more is capped.</summary>
    [Range(1, int.MaxValue)]
    public int MaxResultCount { get; set; } = 10;
}
