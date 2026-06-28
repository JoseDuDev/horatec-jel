using FluentAssertions;
using Horafy.Application.Features.Reviews.Queries;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Reviews;

public sealed class GetResourceReviewsQueryTests
{
    private readonly Mock<IReviewRepository> _repo = new();

    private GetResourceReviewsQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_ResourceWithReviews_ReturnsPagedListWithSummary()
    {
        var resourceId = Guid.NewGuid();
        var review     = Review.Create(Guid.NewGuid(), resourceId, Guid.NewGuid(), 5, "Ótimo!");
        _repo.Setup(r => r.GetByResourcePagedAsync(resourceId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Review> { review }, 1));
        _repo.Setup(r => r.GetRatingSummaryAsync(resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5d, 1));

        var result = await MakeHandler().Handle(
            new GetResourceReviewsQuery(resourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageStars.Should().Be(5d);
        result.Value.TotalReviews.Should().Be(1);
        result.Value.Page.Items.Should().HaveCount(1);
        result.Value.Page.Items[0].Stars.Should().Be(5);
        result.Value.Page.Items[0].Comment.Should().Be("Ótimo!");
    }

    [Fact]
    public async Task Handle_InvalidPaging_NormalizesToDefaults()
    {
        var resourceId = Guid.NewGuid();
        _repo.Setup(r => r.GetByResourcePagedAsync(resourceId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Review>(), 0));
        _repo.Setup(r => r.GetRatingSummaryAsync(resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0d, 0));

        var result = await MakeHandler().Handle(
            new GetResourceReviewsQuery(resourceId, PageNumber: 0, PageSize: 999), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.PageNumber.Should().Be(1);
        result.Value.Page.PageSize.Should().Be(20);
        _repo.Verify(r => r.GetByResourcePagedAsync(resourceId, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
