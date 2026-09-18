using ZXing.Net.Maui;

namespace Ecocell.Mobile.Services.Scanner;

public sealed class MauiQrScanner : IQrScanner
{
    public async Task<QrScanResult> ScanAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return new(QrScanStatus.Cancelled);

        if (!BarcodeScanning.IsSupported)
            return new(QrScanStatus.Unsupported);

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            return new(QrScanStatus.PermissionDenied);

        var hostPage = await MainThread.InvokeOnMainThreadAsync(
            () => Application.Current?.Windows.FirstOrDefault()?.Page);
        if (hostPage is null)
            return new(QrScanStatus.Unsupported);

        var scannerPage = new QrScannerPage();
        await MainThread.InvokeOnMainThreadAsync(
            () => hostPage.Navigation.PushModalAsync(scannerPage));

        using var cancellation = ct.Register(() => _ = scannerPage.CancelAsync());
        var result = await scannerPage.Result;

        return ct.IsCancellationRequested
            ? new(QrScanStatus.Cancelled)
            : result;
    }
}
