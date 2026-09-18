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

        PermissionStatus permission;
        try
        {
            var permissionRequest = MainThread.InvokeOnMainThreadAsync(
                () => Permissions.RequestAsync<Permissions.Camera>());
            permission = await permissionRequest.WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new(QrScanStatus.Cancelled);
        }

        if (permission != PermissionStatus.Granted)
            return new(QrScanStatus.PermissionDenied);

        if (ct.IsCancellationRequested)
            return new(QrScanStatus.Cancelled);

        var hostPage = await MainThread.InvokeOnMainThreadAsync(
            () => Application.Current?.Windows.FirstOrDefault()?.Page);
        if (hostPage is null)
            return new(QrScanStatus.Unsupported);

        var scannerPage = new QrScannerPage();
        using var cancellation = ct.Register(() =>
            MainThread.BeginInvokeOnMainThread(() => _ = scannerPage.CancelAsync()));

        var presented = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (ct.IsCancellationRequested)
                return false;

            await hostPage.Navigation.PushModalAsync(scannerPage);
            return true;
        });

        if (!presented)
            return new(QrScanStatus.Cancelled);

        var result = await scannerPage.Result;

        return ct.IsCancellationRequested
            ? new(QrScanStatus.Cancelled)
            : result;
    }
}
