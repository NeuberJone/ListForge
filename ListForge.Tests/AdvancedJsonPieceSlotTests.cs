using System;
using System.Collections.Generic;
using System.Linq;
using ListForge.Core;
using ListForge.ViewModels;

namespace ListForge.Tests;

public class AdvancedJsonPieceSlotTests
{
    [Fact]
    public void SetAvailablePieceTypes_DoesNotDuplicateOptions()
    {
        var slot = new AdvancedJsonPieceSlot(1, PieceTypeMapper.Pants);

        slot.SetAvailablePieceTypes(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PieceTypeMapper.Short,
                PieceTypeMapper.Tanktop,
            },
            PieceTypeMapper.Pants);
        slot.SetAvailablePieceTypes(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PieceTypeMapper.ShortSleeve,
                PieceTypeMapper.Vest,
            },
            PieceTypeMapper.Pants);

        Assert.Equal(slot.AvailablePieceOptions.Count, slot.AvailablePieceOptions.Select(option => option.Key).Distinct().Count());
        Assert.DoesNotContain(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.ShortSleeve);
        Assert.DoesNotContain(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.Vest);
        Assert.Contains(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.Short);
        Assert.Contains(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.Pants);
    }

    [Fact]
    public void SetAvailablePieceTypes_HidesOptionsUsedByOtherSlots()
    {
        var slot = new AdvancedJsonPieceSlot(1, PieceTypeMapper.Pants);

        slot.SetAvailablePieceTypes(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PieceTypeMapper.Short,
                PieceTypeMapper.Tanktop,
            },
            PieceTypeMapper.Pants);

        Assert.Contains(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.Pants);
        Assert.DoesNotContain(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.Short);
        Assert.DoesNotContain(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.Tanktop);
        Assert.Contains(slot.AvailablePieceOptions, option => option.Key == PieceTypeMapper.LongSleeve);
    }
}
