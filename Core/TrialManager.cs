using System.IO;
using ListForge.Config;
using Newtonsoft.Json;

namespace ListForge.Core;

public static class TrialManager
{
    private sealed class TrialState
    {
        public int UsedProcessings { get; set; }
    }

    public static bool IsTrial => ConfigManager.IsTrialBuild;

    public static int Limit => ConfigManager.TrialProcessingLimit;

    public static int UsedProcessings => IsTrial ? LoadState().UsedProcessings : 0;

    public static int RemainingProcessings => IsTrial ? Math.Max(0, Limit - UsedProcessings) : int.MaxValue;

    public static bool HasCredits => !IsTrial || RemainingProcessings > 0;

    public static string StatusSuffix =>
        IsTrial ? $" | Trial: {RemainingProcessings}/{Limit} processamento(s) restante(s)" : "";

    public static void ConsumeSuccessfulProcessing()
    {
        if (!IsTrial)
            return;

        var state = LoadState();
        if (state.UsedProcessings >= Limit)
        {
            AppLogger.Warning("Trial", "Tentativa de processar sem créditos Trial disponíveis.");
            throw new InvalidOperationException("Limite de processamentos da versão Trial atingido.");
        }

        state.UsedProcessings++;
        SaveState(state);
        AppLogger.Info("Trial", $"Crédito Trial consumido. Usado: {state.UsedProcessings}/{Limit}.");
    }

    private static TrialState LoadState()
    {
        if (!File.Exists(ConfigManager.TrialStatePath))
            return new TrialState();

        try
        {
            var json = File.ReadAllText(ConfigManager.TrialStatePath);
            return JsonConvert.DeserializeObject<TrialState>(json) ?? new TrialState();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Trial", "Falha ao ler estado Trial. Usando estado vazio.", ex, ConfigManager.TrialStatePath);
            return new TrialState();
        }
    }

    private static void SaveState(TrialState state)
    {
        try
        {
            Directory.CreateDirectory(ConfigManager.AppDir);
            File.WriteAllText(
                ConfigManager.TrialStatePath,
                JsonConvert.SerializeObject(state, Formatting.Indented));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Trial", "Falha ao salvar estado Trial.", ex, ConfigManager.TrialStatePath);
            throw;
        }
    }
}
