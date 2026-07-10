using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using Newtonsoft.Json.Linq;

namespace ListForge.Tests;

public class MainFlowIntegrationTests
{
    private static SizeConfig Config => SizeConfig.Default();

    [Fact]
    public void ValidInput_ValidatesProcessesBuildsTextOutputAndJson()
    {
        var result = RunMainFlow(
            "CARLA,12,G\nANA,7,M,NINA,O+",
            ListSortMode.Original);

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Empty(result.ValidationIssues);
        Assert.Equal("CARLA,12,G,,\nANA,7,M,NINA,O+", result.OutputText);
        Assert.Equal(["CARLA", "ANA"], JsonNames(result));
        Assert.Equal(["12", "7"], JsonNumbers(result));
        Assert.Equal("NINA", result.Orders[1]["Nickname"]);
        Assert.Equal("O+", result.Orders[1]["BloodType"]);
    }

    [Fact]
    public void ValidInput_OriginalSortKeepsInputOrderInTextAndJson()
    {
        var result = RunMainFlow(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ListSortMode.Original);

        Assert.Equal("CARLA,12,G\nANA,7,M\nBRUNO,3,P", result.OutputText);
        Assert.Equal(["CARLA", "ANA", "BRUNO"], JsonNames(result));
    }

    [Fact]
    public void ValidInput_AscendingSortKeepsTextAndJsonInSameOrder()
    {
        var result = RunMainFlow(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ListSortMode.Ascending);

        Assert.Equal("ANA,7,M\nBRUNO,3,P\nCARLA,12,G", result.OutputText);
        Assert.Equal(["ANA", "BRUNO", "CARLA"], JsonNames(result));
        Assert.Equal(["7", "3", "12"], JsonNumbers(result));
    }

    [Fact]
    public void ValidInput_DescendingSortKeepsTextAndJsonInSameOrder()
    {
        var result = RunMainFlow(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ListSortMode.Descending);

        Assert.Equal("CARLA,12,G\nBRUNO,3,P\nANA,7,M", result.OutputText);
        Assert.Equal(["CARLA", "BRUNO", "ANA"], JsonNames(result));
        Assert.Equal(["12", "3", "7"], JsonNumbers(result));
    }

    [Fact]
    public void InvalidInput_ReturnsExpectedValidationLineBeforeProcessing()
    {
        var result = RunMainFlow(
            "ANA,10,G\nBIA,12,ZZ\nCARLA,1,P",
            ListSortMode.Original);

        Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
        var issue = Assert.Single(result.ValidationIssues);
        Assert.Equal(2, issue.LineNumber);
        Assert.Equal("tamanho não reconhecido", issue.Message);
        Assert.Empty(result.OutputText);
        Assert.Empty(result.Orders);
    }

    [Fact]
    public void QuantityNicknameAndBloodType_AreExpandedInTextAndJson()
    {
        var result = RunMainFlow(
            "ANA,10,2-G,NINA,O+",
            ListSortMode.Original);

        Assert.Equal("ANA,10,G,NINA,O+\nANA,10,G,NINA,O+", result.OutputText);
        var order = Assert.Single(result.Orders);
        Assert.Equal("ANA", order["Name"]);
        Assert.Equal("NINA", order["Nickname"]);
        Assert.Equal("O+", order["BloodType"]);
        Assert.Equal("2-G", order["ShortSleeve"]);
    }

    [Fact]
    public void SockSize_StaysInTextOutputAndIsNotExportedToJson()
    {
        var result = RunMainFlow(
            "JOANA,10,JUVENIL",
            ListSortMode.Original);

        Assert.Equal("JOANA,10,JUVENIL", result.OutputText);
        Assert.DoesNotContain("JUVENIL", result.JsonPreview);
        Assert.DoesNotContain(result.Orders, order => order.ContainsKey("Socks"));
        Assert.Equal("", result.Orders.Single()["ShortSleeve"]);
    }

    [Fact]
    public void ValidationError_DoesNotConsumeTrialCredit()
    {
        using var env = MainFlowTestEnvironment.Create(isTrial: true, limit: 2);

        var result = RunMainFlow(
            "ANA,10,G\nSEM TAM,20",
            ListSortMode.Original);

        Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
        Assert.Single(result.ValidationIssues);
        Assert.Equal(2, TrialManager.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void SuccessfulProcessing_ConsumesTrialCreditOnlyInTrialMode()
    {
        using var env = MainFlowTestEnvironment.Create(isTrial: true, limit: 2);

        var result = RunMainFlow(
            "ANA,10,G",
            ListSortMode.Original);

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Empty(result.ValidationIssues);
        Assert.Equal(1, TrialManager.RemainingProcessings);
        Assert.True(File.Exists(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void TrialMode_BlocksMainFlowWhenCreditsAreExhausted()
    {
        using var env = MainFlowTestEnvironment.Create(isTrial: true, limit: 1);
        _ = RunMainFlow("ANA,10,G", ListSortMode.Original);

        var blocked = RunMainFlow("BIA,11,M", ListSortMode.Original);

        Assert.Equal(ProcessingWorkflowStatus.TrialLimitReached, blocked.Status);
        Assert.Equal(0, TrialManager.RemainingProcessings);
    }

    [Fact]
    public void CompleteMode_DoesNotConsumeCreditsOrCreateTrialState()
    {
        using var env = MainFlowTestEnvironment.Create(isTrial: false, limit: 1);

        var result = RunMainFlow(
            "ANA,10,G",
            ListSortMode.Original);

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Empty(result.ValidationIssues);
        Assert.Equal(int.MaxValue, TrialManager.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    private static ProcessingWorkflowResult RunMainFlow(string input, ListSortMode sortMode)
    {
        var service = new ProcessingWorkflowService();
        return service.Execute(new ProcessingWorkflowRequest(input, ",", Config, "original", sortMode));
    }

    private static string[] JsonNames(ProcessingWorkflowResult result) =>
        ReadOrdersFromPreview(result)
            .Select(order => (string?)order["Name"] ?? "")
            .ToArray();

    private static string[] JsonNumbers(ProcessingWorkflowResult result) =>
        ReadOrdersFromPreview(result)
            .Select(order => (string?)order["Number"] ?? "")
            .ToArray();

    private static IEnumerable<JObject> ReadOrdersFromPreview(ProcessingWorkflowResult result)
    {
        if (string.IsNullOrWhiteSpace(result.JsonPreview))
            return [];

        var root = JObject.Parse(result.JsonPreview);
        return root["orders"]?.Children<JObject>() ?? [];
    }

    private sealed class MainFlowTestEnvironment : IDisposable
    {
        private readonly string _root;

        private MainFlowTestEnvironment(string root)
        {
            _root = root;
        }

        public static MainFlowTestEnvironment Create(bool isTrial, int limit)
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-MainFlowTests", Guid.NewGuid().ToString("N"));
            var appDir = Path.Combine(root, "app");
            var stateDir = Path.Combine(root, "state");

            ConfigManager.SetDirectoriesForTesting(appDir, stateDir);
            TrialManager.SetTrialModeForTesting(isTrial, limit);

            return new MainFlowTestEnvironment(root);
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
