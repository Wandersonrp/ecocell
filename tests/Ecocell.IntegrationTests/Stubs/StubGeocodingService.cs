using Ecocell.Api.Services.External;
using Ecocell.Api.Shared;

namespace Ecocell.IntegrationTests.Stubs;

public sealed class StubGeocodingService : IGeocodingService
{
    public Task<ResultT<GeocodingCoordinates>> GeocodeAsync(GeocodingRequest request, CancellationToken ct) =>
        Task.FromResult(ResultT<GeocodingCoordinates>.Success(new GeocodingCoordinates(-23.5m, -46.6m)));
}
