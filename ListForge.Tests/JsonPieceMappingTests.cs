using System;
using System.IO;
using System.Linq;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class JsonPieceMappingTests
{
    private static SizeConfig Config => SizeConfig.Default();

    [Fact]
    public void DisabledAdvancedMapping_KeepsCurrentJsonBehavior()
    {
        var rows = ListProcessor.ProcessText("JOAO,10,P,G,M", ",", Config);

        var defaultOrders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);
        var disabledOrders = ListProcessor.BuildOrdersFromOrderlist(
            rows,
            Config,
            "original",
            JsonPieceMappingOptions.Disabled);

        Assert.Equal(defaultOrders, disabledOrders);
    }

    [Fact]
    public void BasicMapping_UsesDefaultPieceOrderForSameGenderSizes()
    {
        var rows = ListProcessor.ProcessText("JOAO,5,G,M", ",", Config);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        var order = Assert.Single(orders);
        Assert.Equal("MA", order["Gender"]);
        Assert.Equal("1-G", order["ShortSleeve"]);
        Assert.Equal("1-M", order["LongSleeve"]);
    }

    [Fact]
    public void BasicMapping_SplitsDifferentGenderAndKeepsDefaultPiecePosition()
    {
        var rows = ListProcessor.ProcessText("ANA,5,G,BLM", ",", Config);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(2, orders.Count);
        Assert.Equal("MA", orders[0]["Gender"]);
        Assert.Equal("1-G", orders[0]["ShortSleeve"]);
        Assert.Equal("", orders[0]["LongSleeve"]);
        Assert.Equal("FE", orders[1]["Gender"]);
        Assert.Equal("", orders[1]["ShortSleeve"]);
        Assert.Equal("1-BLM", orders[1]["LongSleeve"]);
    }

    [Fact]
    public void EnabledAdvancedMapping_AppliesSizesToConfiguredPieceOrder()
    {
        var rows = ListProcessor.ProcessText("JOAO,10,3-P,4-G,20-M", ",", Config);
        var options = new JsonPieceMappingOptions(true,
            [PieceTypeMapper.Tanktop, PieceTypeMapper.ShortSleeve, PieceTypeMapper.Short]);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config, "original", options);

        var order = Assert.Single(orders);
        Assert.Equal("3-P", order["Tanktop"]);
        Assert.Equal("4-G", order["ShortSleeve"]);
        Assert.Equal("20-M", order["Short"]);
    }

    [Fact]
    public void EnabledAdvancedMapping_KeepsSameGenderPiecesInSameOrder()
    {
        var rows = ListProcessor.ProcessText("JOAO,5,G,M", ",", Config);
        var options = new JsonPieceMappingOptions(true,
            [PieceTypeMapper.ShortSleeve, PieceTypeMapper.Short]);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config, "original", options);

        var order = Assert.Single(orders);
        Assert.Equal("MA", order["Gender"]);
        Assert.Equal("1-G", order["ShortSleeve"]);
        Assert.Equal("1-M", order["Short"]);
    }

    [Fact]
    public void EnabledAdvancedMapping_SplitsDifferentGenderPieces()
    {
        var rows = ListProcessor.ProcessText("ANA,5,G,BLM", ",", Config);
        var options = new JsonPieceMappingOptions(true,
            [PieceTypeMapper.ShortSleeve, PieceTypeMapper.Tanktop]);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config, "original", options);

        Assert.Equal(2, orders.Count);
        Assert.Equal("MA", orders[0]["Gender"]);
        Assert.Equal("1-G", orders[0]["ShortSleeve"]);
        Assert.Equal("", orders[0]["Tanktop"]);
        Assert.Equal("FE", orders[1]["Gender"]);
        Assert.Equal("", orders[1]["ShortSleeve"]);
        Assert.Equal("1-BLM", orders[1]["Tanktop"]);
    }

    [Fact]
    public void EnabledAdvancedMapping_PreservesTextOutput()
    {
        var service = new ProcessingWorkflowService();
        var defaultResult = service.Execute(new ProcessingWorkflowRequest(
            "JOAO,10,3-P,4-G,20-M",
            ",",
            Config,
            "original",
            ListSortMode.Original));
        var request = new ProcessingWorkflowRequest(
            "JOAO,10,3-P,4-G,20-M",
            ",",
            Config,
            "original",
            ListSortMode.Original,
            new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop, PieceTypeMapper.ShortSleeve, PieceTypeMapper.Short]));

        var result = service.Execute(request);

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Equal(defaultResult.OutputText, result.OutputText);
        var order = Assert.Single(result.Orders);
        Assert.Equal("3-P", order["Tanktop"]);
        Assert.Equal("4-G", order["ShortSleeve"]);
        Assert.Equal("20-M", order["Short"]);
    }

    [Fact]
    public void EstimateRequiredSlots_UsesEmptyColumnPosition()
    {
        var service = new JsonPieceMappingService();

        var slots = service.EstimateRequiredSlots(",,G\n,,,P", ",", Config);

        Assert.Equal(2, slots);
    }

    [Fact]
    public void EnabledAdvancedMapping_UsesEmptyColumnPosition()
    {
        var rows = ListProcessor.ProcessText(",,G\n,,,P", ",", Config);
        var options = new JsonPieceMappingOptions(true,
            [PieceTypeMapper.Tanktop, PieceTypeMapper.Vest]);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config, "original", options);

        Assert.Equal(2, orders.Count);
        Assert.Equal("1-G", orders[0]["Tanktop"]);
        Assert.Equal("", orders[0]["Vest"]);
        Assert.Equal("", orders[1]["Tanktop"]);
        Assert.Equal("1-P", orders[1]["Vest"]);
    }

    [Fact]
    public void EnabledAdvancedMapping_FailsWhenOrderHasFewerPositionsThanInput()
    {
        var service = new ProcessingWorkflowService();

        var result = service.Execute(new ProcessingWorkflowRequest(
            "JOAO,10,P,G,M",
            ",",
            Config,
            "original",
            ListSortMode.Original,
            new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop, PieceTypeMapper.ShortSleeve])));

        Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationIssues, issue =>
            issue.Message == "A ordem personalizada dos tipos de peça possui menos posições do que os tamanhos encontrados na lista.");
    }

    [Fact]
    public void EnabledAdvancedMapping_FailsWhenOrderIsEmpty()
    {
        var service = new ProcessingWorkflowService();

        var result = service.Execute(new ProcessingWorkflowRequest(
            "JOAO,10,P",
            ",",
            Config,
            "original",
            ListSortMode.Original,
            new JsonPieceMappingOptions(true, [])));

        Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationIssues, issue => issue.Message == "ordem personalizada sem tipos configurados");
    }

    [Fact]
    public void EnabledAdvancedMapping_FailsWhenPieceTypeRepeats()
    {
        var service = new ProcessingWorkflowService();

        var result = service.Execute(new ProcessingWorkflowRequest(
            "JOAO,10,P,G",
            ",",
            Config,
            "original",
            ListSortMode.Original,
            new JsonPieceMappingOptions(true, [PieceTypeMapper.ShortSleeve, PieceTypeMapper.ShortSleeve])));

        Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationIssues, issue => issue.Message == "A ordem personalizada não pode repetir o mesmo tipo de peça.");
    }

    [Fact]
    public void EnabledAdvancedMapping_RespectsSortedRowsAndSizePosition()
    {
        var service = new ProcessingWorkflowService();

        var result = service.Execute(new ProcessingWorkflowRequest(
            "BRUNO,9,P,G\nANA,7,M,GG",
            ",",
            Config,
            "original",
            ListSortMode.Ascending,
            new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop, PieceTypeMapper.Vest])));

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Equal("ANA", result.Orders[0]["Name"]);
        Assert.Equal("1-M", result.Orders[0]["Tanktop"]);
        Assert.Equal("1-GG", result.Orders[0]["Vest"]);
        Assert.Equal("BRUNO", result.Orders[1]["Name"]);
        Assert.Equal("1-P", result.Orders[1]["Tanktop"]);
        Assert.Equal("1-G", result.Orders[1]["Vest"]);
    }

    [Fact]
    public void AdvancedMappingValidation_DoesNotConsumeTrialCredit()
    {
        using var env = TrialTestEnvironment.Create(limit: 2);
        var service = new ProcessingWorkflowService();

        var result = service.Execute(new ProcessingWorkflowRequest(
            "JOAO,10,P,G,M",
            ",",
            Config,
            "original",
            ListSortMode.Original,
            new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop])));

        Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
        Assert.Equal(2, TrialManager.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    private sealed class TrialTestEnvironment : IDisposable
    {
        private readonly string _root;

        private TrialTestEnvironment(string root)
        {
            _root = root;
        }

        public static TrialTestEnvironment Create(int limit)
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-JsonPieceMappingTests", Guid.NewGuid().ToString("N"));
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            TrialManager.SetTrialModeForTesting(true, limit);
            return new TrialTestEnvironment(root);
        }

        public void Dispose()
        {
            TrialManager.SetTrialModeForTesting(null);

            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Temporary test directories can be left for the OS to clean up.
            }
        }
    }
}
