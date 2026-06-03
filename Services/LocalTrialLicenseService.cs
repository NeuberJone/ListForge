using ListForge.Config;
using ListForge.Core;

namespace ListForge.Services;

public sealed class LocalTrialLicenseService : ILicenseService
{
    public string Edition => IsTrial ? "Trial" : "Completo";
    public bool IsTrial => TrialManager.IsTrial;
    public int ProcessingLimit => TrialManager.Limit;
    public int RemainingProcessings => TrialManager.RemainingProcessings;
    public bool CanProcess => TrialManager.HasCredits;
    public string ProcessingStatusSuffix => TrialManager.StatusSuffix;

    public void ConsumeSuccessfulProcessing()
    {
        TrialManager.ConsumeSuccessfulProcessing();
    }
}
