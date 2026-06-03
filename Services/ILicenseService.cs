namespace ListForge.Services;

public interface ILicenseService
{
    string Edition { get; }
    bool IsTrial { get; }
    int ProcessingLimit { get; }
    int RemainingProcessings { get; }
    bool CanProcess { get; }
    string ProcessingStatusSuffix { get; }

    void ConsumeSuccessfulProcessing();
}
