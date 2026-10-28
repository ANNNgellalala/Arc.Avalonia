using System;
using System.Collections.Generic;

namespace Arc.Avalonia.Blocks.Team;

/// <summary>
/// One person in the sample workspace. Blocks are page-level compositions, so they share one believable
/// team rather than inventing unrelated names on every page. All data is sample content in code.
/// </summary>
internal sealed record TeamPerson(
    string Name,
    string Role,
    string Team,
    string Email,
    string Location,
    string Timezone,
    string Focus)
{
    public string FirstName
    {
        get
        {
            var space = Name.IndexOf(' ');
            return space > 0 ? Name[..space] : Name;
        }
    }

    public string Initials
    {
        get
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant()
            };
        }
    }
}

internal static class TeamData
{
    public static readonly IReadOnlyList<TeamPerson> People = new[]
    {
        new TeamPerson("Maya Chen", "Staff product designer", "Design", "maya.chen@northwind.co",
            "San Francisco", "PT (UTC-8)", "Design systems, onboarding and the editor surface."),
        new TeamPerson("Jonas Weber", "Staff engineer", "Engineering", "jonas.weber@northwind.co",
            "Berlin", "CET (UTC+1)", "Sync engine, offline drafts and performance budgets."),
        new TeamPerson("Priya Raman", "Platform lead", "Engineering", "priya.raman@northwind.co",
            "Bengaluru", "IST (UTC+5:30)", "API platform, permissions and the release pipeline."),
        new TeamPerson("Tomas Novak", "Brand designer", "Design", "tomas.novak@northwind.co",
            "Prague", "CET (UTC+1)", "Brand, editorial illustration and the marketing site."),
        new TeamPerson("Lena Fischer", "Program manager", "Operations", "lena.fischer@northwind.co",
            "Amsterdam", "CET (UTC+1)", "Release coordination, support readiness and rituals."),
        new TeamPerson("Sam Okafor", "Frontend engineer", "Engineering", "sam.okafor@northwind.co",
            "Lagos", "WAT (UTC+1)", "Component library, accessibility and design tokens."),
        new TeamPerson("Alice Moreau", "UX researcher", "Design", "alice.moreau@northwind.co",
            "Paris", "CET (UTC+1)", "Interview programmes, retention studies and synthesis."),
        new TeamPerson("Diego Alvarez", "Support lead", "Operations", "diego.alvarez@northwind.co",
            "Mexico City", "CT (UTC-6)", "Escalations, help centre and the feedback loop.")
    };

    public static TeamPerson Person(string name) =>
        Find(name) ?? throw new InvalidOperationException($"Unknown sample person '{name}'");

    public static TeamPerson? Find(string name)
    {
        foreach (var person in People)
        {
            if (string.Equals(person.Name, name, StringComparison.Ordinal))
            {
                return person;
            }
        }

        return null;
    }

    public static readonly IReadOnlyList<string> Teams = new[] { "Design", "Engineering", "Operations" };

    public static int CountIn(string team)
    {
        var count = 0;
        foreach (var person in People)
        {
            if (string.Equals(person.Team, team, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
