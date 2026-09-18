using System.Net;
using Ecocell.Mobile.Models.Discards;
using Ecocell.Mobile.Services.Api;
using Ecocell.Shared.Enums;
using Ecocell.Shared.Requests.Discards;
using Ecocell.Shared.Responses.Discards;

namespace Ecocell.Mobile.ViewModels;

public enum ScanState
{
    Intro,
    LoadingPreview,
    Ready,
    NoMaterials,
    Submitting,
    Success,
    Error,
}

public sealed class ScanViewModel(IDiscardClient client)
{
    private readonly IDiscardClient _client = client;

    public ScanState State { get; private set; } = ScanState.Intro;
    public string QrCode { get; private set; } = string.Empty;
    public ResponseDiscardPreviewJson? Preview { get; private set; }
    public IList<DiscardItemDraft> Items { get; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task LoadPreviewAsync(string qrCode, CancellationToken ct = default)
    {
        QrCode = qrCode;
        Preview = null;
        Items.Clear();
        ErrorMessage = null;
        State = ScanState.LoadingPreview;

        try
        {
            var response = await _client.PreviewAsync(QrCode, ct);
            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                Preview = response.Content;
                if (Preview.AcceptedMaterials.Count == 0)
                {
                    State = ScanState.NoMaterials;
                    return;
                }

                Items.Add(new DiscardItemDraft());
                State = ScanState.Ready;
                return;
            }

            SetPreviewError(response.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Reset();
        }
        catch
        {
            ErrorMessage = "Falha de conexão. Verifique sua internet e tente novamente.";
            State = ScanState.Error;
        }
    }

    public void AddItem()
    {
        if (Preview is null)
            return;

        var usedMaterials = Items
            .Where(item => item.Material is not null)
            .Select(item => item.Material!.Value)
            .ToHashSet();

        if (Preview.AcceptedMaterials.Any(material => !usedMaterials.Contains(material)))
            Items.Add(new DiscardItemDraft());
    }

    public void RemoveItem(DiscardItemDraft item) => Items.Remove(item);

    public async Task<bool> SubmitAsync(CancellationToken ct = default)
    {
        var values = new List<(ElectronicMaterial Material, int Quantity, decimal ApproximateWeightKg)>();
        var selectedMaterials = Items
            .Where(item => item.Material is not null)
            .Select(item => item.Material!.Value)
            .ToArray();

        foreach (var item in Items)
        {
            var isDuplicated = item.Material is not null
                && selectedMaterials.Count(material => material == item.Material.Value) > 1;
            if (!item.TryGetValues(isDuplicated, out var material, out var quantity, out var approximateWeightKg))
                continue;

            values.Add((material, quantity, approximateWeightKg));
        }

        if (Items.Count == 0 || values.Count != Items.Count)
            return false;

        State = ScanState.Submitting;
        ErrorMessage = null;
        var request = new RequestRegisterDiscardJson
        {
            QrCode = QrCode,
            Items = values.Select(value => new RequestRegisterDiscardItemJson
            {
                Material = value.Material,
                Quantity = value.Quantity,
                ApproximateWeightKg = value.ApproximateWeightKg,
            }).ToArray(),
        };

        try
        {
            var response = await _client.RegisterAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                State = ScanState.Success;
                return true;
            }

            ErrorMessage = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => "Revise o formulário e informe um QR Code válido.",
                HttpStatusCode.NotFound => "Ponto de coleta não encontrado.",
                HttpStatusCode.Conflict => "O ponto de coleta ou os materiais selecionados não estão disponíveis.",
                _ => "Falha de conexão. Verifique sua internet e tente novamente.",
            };
            State = ScanState.Error;
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            State = ScanState.Ready;
            return false;
        }
        catch
        {
            ErrorMessage = "Falha de conexão. Verifique sua internet e tente novamente.";
            State = ScanState.Error;
            return false;
        }
    }

    public void Reset()
    {
        State = ScanState.Intro;
        QrCode = string.Empty;
        Preview = null;
        Items.Clear();
        ErrorMessage = null;
    }

    private void SetPreviewError(HttpStatusCode statusCode)
    {
        ErrorMessage = statusCode switch
        {
            HttpStatusCode.BadRequest => "Informe um QR Code válido.",
            HttpStatusCode.NotFound => "Ponto de coleta não encontrado.",
            HttpStatusCode.Conflict => "Ponto de coleta indisponível no momento.",
            _ => "Falha de conexão. Verifique sua internet e tente novamente.",
        };
        State = ScanState.Error;
    }
}
