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
    public async Task Handle_ResourceWithReviews_ReturnsMappedList()
    {
        var resourceId = Guid.NewGuid();
        var review     = Review.Create(Guid.NewGuid(), resourceId, Guid.NewGuid(), 5, "Ótimo!");
        _repo.Setup(r => r.GetByResourceAsync(resourceId, default))
            .ReturnsAsync(new List<Review> { review });

        var result = await MakeHandler().Handle(
            new GetResourceReviewsQuery(resourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Stars.Should().Be(5);
        result.Value[0].Comment.Should().Be("Ótimo!");
    }
}
