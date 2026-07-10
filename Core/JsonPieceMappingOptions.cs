using System.Collections.Generic;
using System.Linq;

namespace ListForge.Core;

public sealed record JsonPieceMappingOptions(bool UseCustomOrder, IReadOnlyList<string> PieceOrder)
{
    public static JsonPieceMappingOptions Disabled { get; } = new(false, []);

    public IReadOnlyList<string> NormalizedOrder =>
        PieceOrder
            .Where(piece => !string.IsNullOrWhiteSpace(piece))
            .Select(PieceTypeMapper.NormalizeKey)
            .Where(PieceTypeMapper.IsKnownKey)
            .ToList();
}
