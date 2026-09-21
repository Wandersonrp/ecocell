using Ecocell.Api.Entities;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class CreditScoreRequestTests
{
    [Fact]
    public void Constructor_ShouldStoreDiscardId_WhenDiscardIdIsValid()
    {
        var discardId = Guid.NewGuid();

        var request = new CreditScoreRequest(discardId);

        request.DiscardId.ShouldBe(discardId);
        request.DispatchedAt.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDiscardIdIsEmpty()
    {
        Should.Throw<ArgumentException>(() => new CreditScoreRequest(Guid.Empty));
    }

    [Fact]
    public void MarkAsDispatched_ShouldStoreUtcTimestamp_WhenRequestIsPending()
    {
        var request = new CreditScoreRequest(Guid.NewGuid());
        var dispatchedAt = new DateTime(2026, 9, 16, 18, 0, 0, DateTimeKind.Utc);

        request.MarkAsDispatched(dispatchedAt);

        request.DispatchedAt.ShouldBe(dispatchedAt);
        request.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void MarkAsDispatched_ShouldThrow_WhenTimestampIsNotUtc()
    {
        var request = new CreditScoreRequest(Guid.NewGuid());
        var local = new DateTime(2026, 9, 16, 18, 0, 0, DateTimeKind.Local);

        Should.Throw<ArgumentException>(() => request.MarkAsDispatched(local));
    }

    [Fact]
    public void MarkAsDispatched_ShouldThrow_WhenRequestWasAlreadyDispatched()
    {
        var request = new CreditScoreRequest(Guid.NewGuid());
        request.MarkAsDispatched(DateTime.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            request.MarkAsDispatched(DateTime.UtcNow));
    }
}
