using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Database;

public class CreditScorePersistenceTests : TestBase
{
    [Fact]
    public void Model_ShouldConfigureCreditRequestIndexes()
    {
        var entity = DbContext.Model.FindEntityType(typeof(CreditScoreRequest));
        entity.ShouldNotBeNull();

        entity!.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CreditScoreRequest.DiscardId)]))
            .IsUnique.ShouldBeTrue();
        entity.GetIndexes().ShouldContain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(CreditScoreRequest.DispatchedAt) }));
    }

    [Fact]
    public void Model_ShouldConfigureTransactionPrecisionAndUniqueDiscard()
    {
        var entity = DbContext.Model.FindEntityType(typeof(DepositorScoreTransaction));
        entity.ShouldNotBeNull();

        var points = entity!.FindProperty(nameof(DepositorScoreTransaction.Points));
        points.ShouldNotBeNull();
        points!.GetPrecision().ShouldBe(28);
        points.GetScale().ShouldBe(5);
        entity.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DepositorScoreTransaction.DiscardId)]))
            .IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Model_ShouldConfigureTotalPrecisionUniquenessAndConcurrency()
    {
        var entity = DbContext.Model.FindEntityType(typeof(DepositorTotalScore));
        entity.ShouldNotBeNull();

        var totalPoints = entity!.FindProperty(nameof(DepositorTotalScore.TotalPoints));
        totalPoints.ShouldNotBeNull();
        totalPoints!.GetPrecision().ShouldBe(28);
        totalPoints.GetScale().ShouldBe(5);
        totalPoints.IsConcurrencyToken.ShouldBeTrue();
        entity.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DepositorTotalScore.DepositorId)]))
            .IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Model_ShouldUseRestrictDeleteForCreditRelationships()
    {
        var entityTypes = new[]
        {
            DbContext.Model.FindEntityType(typeof(CreditScoreRequest)),
            DbContext.Model.FindEntityType(typeof(DepositorScoreTransaction)),
            DbContext.Model.FindEntityType(typeof(DepositorTotalScore)),
        };

        entityTypes.ShouldAllBe(entity => entity != null);
        entityTypes.SelectMany(entity => entity!.GetForeignKeys())
            .ShouldAllBe(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }
}
