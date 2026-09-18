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
        var presentationGate = new object();
        var presentationCompleted = false;
        using var cancellation = ct.Register(() =>
        {
            var shouldCancelPresentedPage = false;
            lock (presentationGate)
            {
                shouldCancelPresentedPage = presentationCompleted;
            }

            if (shouldCancelPresentedPage)
            {
                MainThread.BeginInvokeOnMainThread(() => _ = scannerPage.CancelAsync());
            }
        });

        var presented = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            lock (presentationGate)
            {
                if (ct.IsCancellationRequested)
                    return false;
            }

            await hostPage.Navigation.PushModalAsync(scannerPage);

            var cancelAfterPresentation = false;
            lock (presentationGate)
            {
                presentationCompleted = true;
                cancelAfterPresentation = ct.IsCancellationRequested;
            }

            if (cancelAfterPresentation)
                await scannerPage.CancelAsync();

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
