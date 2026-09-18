namespace Ecocell.Mobile.Services.Scanner;

public enum QrScanStatus
{
    Detected,
    Cancelled,
    Unsupported,
    PermissionDenied,
}

public sealed record QrScanResult(QrScanStatus Status, string? Value = null);

public interface IQrScanner
{
    Task<QrScanResult> ScanAsync(CancellationToken ct = default);
}
