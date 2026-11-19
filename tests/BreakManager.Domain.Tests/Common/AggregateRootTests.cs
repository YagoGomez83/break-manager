using BreakManager.Domain.Common;

namespace BreakManager.Domain.Tests.Common;

public sealed class AggregateRootTests
{
    private sealed record SomethingHappened(Guid AggregateId) : IDomainEvent;

    private sealed class SampleAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void DoSomething() => Raise(new SomethingHappened(Id));
    }

    [Fact]
    public void NewAggregate_HasNoDomainEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Raise_RegistersEventsInOrder()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.DoSomething();
        aggregate.DoSomething();

        Assert.Equal(2, aggregate.DomainEvents.Count);
        Assert.All(aggregate.DomainEvents, e => Assert.Equal(new SomethingHappened(aggregate.Id), e));
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void DomainEvents_CannotBeMutatedFromOutside()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.DoSomething();

        Assert.IsNotType<List<IDomainEvent>>(aggregate.DomainEvents, exactMatch: false);
    }
}
