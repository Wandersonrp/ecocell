using System.Net;
using Shouldly;

namespace Ecocell.IntegrationTests.Features.Discard;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ListPendingDiscardsTests : IntegrationTestBase
{
    public ListPendingDiscardsTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await Client.GetAsync(
            $"api/v1/collector-points/{Guid.NewGuid():D}/discards/pending");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
