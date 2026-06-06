using FluentAssertions;
using Horafy.Domain.Entities.Reviews;
using Xunit;

namespace Horafy.Domain.Tests.Reviews;

public sealed class ReviewTests
{
    [Fact]
    public void Create_ValidData_SetsProperties()
    {
        var bookingId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var review = Review.Create(bookingId, resourceId, customerId, stars: 5, comment: "Ótimo!");

        review.BookingId.Should().Be(bookingId);
        review.ResourceId.Should().Be(resourceId);
        review.CustomerId.Should().Be(customerId);
        review.Stars.Should().Be(5);
        review.Comment.Should().Be("Ótimo!");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_InvalidStars_Throws(int stars)
    {
        var action = () => Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), stars, "ok");
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_ChangesStarsAndComment()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, "Bom");
        review.Update(5, "Excelente!");
        review.Stars.Should().Be(5);
        review.Comment.Should().Be("Excelente!");
    }
}
