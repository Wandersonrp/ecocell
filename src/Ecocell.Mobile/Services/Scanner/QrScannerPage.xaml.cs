using Ecocell.Shared.Utils;
using ZXing.Net.Maui;

namespace Ecocell.Mobile.Services.Scanner;

public partial class QrScannerPage : ContentPage
{
    private readonly TaskCompletionSource<QrScanResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _completed;

    public QrScannerPage()
    {
        InitializeComponent();
        BarcodeView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false,
        };
    }

    public Task<QrScanResult> Result => _completion.Task;

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs args)
    {
        var value = args.Results
            .Select(result => result.Value)
            .FirstOrDefault(candidate => CollectorPointQrCode.TryParse(candidate, out _));

        if (value is null || Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
            _ = CompleteAsync(new(QrScanStatus.Detected, value), completionClaimed: true));
    }

    private void OnCloseClicked(object? sender, EventArgs args)
    {
        _ = CompleteAsync(new(QrScanStatus.Cancelled));
    }

    public Task CancelAsync() => CompleteAsync(new(QrScanStatus.Cancelled));

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (Volatile.Read(ref _completed) == 0)
            _ = CompleteAsync(new(QrScanStatus.Cancelled));
    }

    private async Task CompleteAsync(QrScanResult result, bool completionClaimed = false)
    {
        if (!completionClaimed && Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        BarcodeView.IsDetecting = false;
        _completion.TrySetResult(result);

        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync();
    }
}
