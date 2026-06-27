using FluentAssertions;
using Horafy.Infrastructure.Messaging;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Horafy.Infrastructure.Tests.Notifications;

public sealed class OutboxProcessorServiceTests
{
    private static HorafyDbContext MakeContext()
    {
        var opts = new DbContextOptionsBuilder<HorafyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorafyDbContext(opts);
    }

    private static IServiceScopeFactory MakeScopeFactory(HorafyDbContext context, IBus bus)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(HorafyDbContext))).Returns(context);
        serviceProvider.Setup(sp => sp.GetService(typeof(IBus))).Returns(bus);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return scopeFactory.Object;
    }

    [Fact]
    public async Task ProcessBatchAsync_UnprocessedMessage_MarksAsProcessed()
    {
        var context = MakeContext();
        var bus     = new Mock<IBus>();
        var msg     = new OutboxMessage
        {
            Id         = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type       = typeof(FakeEvent).AssemblyQualifiedName!,
            Content    = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-06-06T00:00:00+00:00"}"""
        };
        context.Set<OutboxMessage>().Add(msg);
        await context.SaveChangesAsync();

        var processor = new OutboxProcessorService(
            MakeScopeFactory(context, bus.Object), NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessBatchAsync(default);

        var stored = await context.Set<OutboxMessage>().FindAsync(msg.Id);
        stored!.ProcessedAt.Should().NotBeNull();
        stored.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessBatchAsync_PublishThrows_IncrementsRetryCount()
    {
        var context = MakeContext();
        var bus     = new Mock<IBus>();
        bus.Setup(b => b.Publish(It.IsAny<object>(), It.IsAny<Type>(), default))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        var msg = new OutboxMessage
        {
            Id         = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type       = typeof(FakeEvent).AssemblyQualifiedName!,
            Content    = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-06-06T00:00:00+00:00"}"""
        };
        context.Set<OutboxMessage>().Add(msg);
        await context.SaveChangesAsync();

        var processor = new OutboxProcessorService(
            MakeScopeFactory(context, bus.Object), NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessBatchAsync(default);

        var stored = await context.Set<OutboxMessage>().FindAsync(msg.Id);
        stored!.RetryCount.Should().Be(1);
        stored.ProcessedAt.Should().BeNull();
        stored.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessBatchAsync_MaxRetriesReached_SkipsMessage()
    {
        var context = MakeContext();
        var bus     = new Mock<IBus>();
        var msg     = new OutboxMessage
        {
            Id         = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type       = typeof(FakeEvent).AssemblyQualifiedName!,
            Content    = "{}",
            RetryCount = 3
        };
        context.Set<OutboxMessage>().Add(msg);
        await context.SaveChangesAsync();

        var processor = new OutboxProcessorService(
            MakeScopeFactory(context, bus.Object), NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessBatchAsync(default);

        bus.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<Type>(), default), Times.Never);
    }

    private sealed record FakeEvent : Horafy.Domain.Events.Base.DomainEvent;
}
