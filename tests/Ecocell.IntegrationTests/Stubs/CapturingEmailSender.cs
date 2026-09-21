using System.Collections.Concurrent;
using Ecocell.Api.Enums;
using Ecocell.Api.Services.Email;

namespace Ecocell.IntegrationTests.Stubs;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _capturedCodes = new(StringComparer.OrdinalIgnoreCase);

    public Task SendAsync(
        string email,
        EmailType type,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken ct = default)
    {
        if (variables is not null && variables.TryGetValue("code", out var code))
            _capturedCodes[email] = code;

        return Task.CompletedTask;
    }

    public string? GetCapturedCode(string email) =>
        _capturedCodes.TryGetValue(email, out var code) ? code : null;

    public void Clear() => _capturedCodes.Clear();
}
