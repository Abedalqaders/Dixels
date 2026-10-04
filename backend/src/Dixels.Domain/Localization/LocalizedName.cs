namespace Dixels.Localization;

/// <summary>A name as typed in one language: ("en", "Meeting room"), ("ar", "غرفة اجتماعات").</summary>
public sealed record LocalizedName(string Language, string Name);
