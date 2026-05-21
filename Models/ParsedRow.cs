using System.Collections.Generic;

namespace ListForge.Models;

/// <summary>
/// Immutable parsed row — mirrors Python's ParsedRow frozen dataclass.
/// </summary>
public sealed record ParsedRow(
    string Name,
    string Number,
    IReadOnlyList<string> Tams,
    string S2,
    string S3);
