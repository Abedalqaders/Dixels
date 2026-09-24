using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Dixels.SpaceManagement.Tests.TestFixtures;

/// <summary>
/// Loads the JSON fixtures under /shared/test-fixtures at the repo root — the same cases
/// the frontend's Vitest suite is tested against, so the two implementations of the same
/// rule (day/hour subset checks) can't silently drift apart.
/// </summary>
internal static class SharedFixtureLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<T> Load<T>(string fileName)
    {
        var path = FindFixtureFile(fileName);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<List<T>>(json, Options)
            ?? throw new InvalidOperationException($"Fixture file '{fileName}' deserialized to null.");
    }

    private static string FindFixtureFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "shared", "test-fixtures", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find shared test fixture '{fileName}' by walking up from '{AppContext.BaseDirectory}'.");
    }
}
