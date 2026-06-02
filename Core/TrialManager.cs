using System.IO;
using System.Security.Cryptography;
using System.Text;
using ListForge.Config;
using Newtonsoft.Json;

namespace ListForge.Core;

public static class TrialManager
{
    private sealed class TrialState
    {
        public int UsedProcessings { get; set; }
    }

    private enum LegacyMigrationResult
    {
        None,
        Migrated,
        Failed,
    }

    private const int StateVersion = 1;
    private static readonly byte[] StateEntropy = Encoding.UTF8.GetBytes("ListForge.Trial.State.v1");
    private static bool? _trialModeOverride;
    private static int? _trialLimitOverride;

    public static bool IsTrial => _trialModeOverride ?? ConfigManager.IsTrialBuild;

    public static int Limit => _trialLimitOverride ?? ConfigManager.TrialProcessingLimit;

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
        if (MigrateLegacyStateIfNeeded() == LegacyMigrationResult.Failed)
            return InvalidState();

        if (!File.Exists(ConfigManager.TrialStatePath))
            return new TrialState();

        try
        {
            var protectedBytes = File.ReadAllBytes(ConfigManager.TrialStatePath);
            var raw = ProtectedData.Unprotect(protectedBytes, StateEntropy, DataProtectionScope.CurrentUser);
            var state = DeserializeState(raw);
            return state.UsedProcessings < 0 ? InvalidState() : state;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Trial", "Possível estado Trial inválido ou corrompido.");
            AppLogger.Warning("Trial", $"Falha técnica ao ler estado interno do Trial: {ex.GetType().Name}.");
            return InvalidState();
        }
    }

    private static void SaveState(TrialState state)
    {
        try
        {
            Directory.CreateDirectory(ConfigManager.InternalStateDir);
            var raw = SerializeState(state);
            var protectedBytes = ProtectedData.Protect(raw, StateEntropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(ConfigManager.TrialStatePath, protectedBytes);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Trial", "Falha ao salvar estado interno do Trial.");
            AppLogger.Warning("Trial", $"Falha técnica ao salvar estado interno do Trial: {ex.GetType().Name}.");
            throw;
        }
    }

    private static LegacyMigrationResult MigrateLegacyStateIfNeeded()
    {
        if (File.Exists(ConfigManager.TrialStatePath) || !File.Exists(ConfigManager.LegacyTrialStatePath))
            return LegacyMigrationResult.None;

        try
        {
            var json = File.ReadAllText(ConfigManager.LegacyTrialStatePath);
            var legacyState = JsonConvert.DeserializeObject<TrialState>(json);
            var used = Math.Max(0, legacyState?.UsedProcessings ?? 0);

            SaveState(new TrialState { UsedProcessings = used });
            File.Delete(ConfigManager.LegacyTrialStatePath);
            AppLogger.Info("Trial", "Estado Trial migrado para armazenamento interno.");
            return LegacyMigrationResult.Migrated;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Trial", "Falha ao migrar estado interno do Trial.");
            AppLogger.Warning("Trial", $"Falha técnica ao migrar estado interno do Trial: {ex.GetType().Name}.");
            return LegacyMigrationResult.Failed;
        }
    }

    private static byte[] SerializeState(TrialState state)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(StateVersion);
        writer.Write(state.UsedProcessings);
        writer.Flush();
        return stream.ToArray();
    }

    private static TrialState DeserializeState(byte[] raw)
    {
        using var stream = new MemoryStream(raw);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        var version = reader.ReadInt32();
        if (version != StateVersion)
            throw new InvalidDataException("Unsupported Trial state version.");

        var used = reader.ReadInt32();
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected Trial state payload.");

        return new TrialState { UsedProcessings = used };
    }

    private static TrialState InvalidState() =>
        new() { UsedProcessings = Limit };

    internal static void SetTrialModeForTesting(bool? isTrial, int? limit = null)
    {
        _trialModeOverride = isTrial;
        _trialLimitOverride = limit;
    }
}
