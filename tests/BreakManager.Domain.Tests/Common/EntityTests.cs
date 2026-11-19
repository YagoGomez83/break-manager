using BreakManager.Domain.Common;

namespace BreakManager.Domain.Tests.Common;

public sealed class EntityTests
{
    private sealed class Foo(Guid id) : Entity<Guid>(id);

    private sealed class Bar(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Entities_WithSameTypeAndId_AreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Foo(id), new Foo(id));
        Assert.True(new Foo(id) == new Foo(id));
        Assert.Equal(new Foo(id).GetHashCode(), new Foo(id).GetHashCode());
    }

    [Fact]
    public void Entities_WithDifferentId_AreNotEqual()
    {
        Assert.NotEqual(new Foo(Guid.NewGuid()), new Foo(Guid.NewGuid()));
        Assert.True(new Foo(Guid.NewGuid()) != new Foo(Guid.NewGuid()));
    }

    [Fact]
    public void Entities_WithSameIdButDifferentType_AreNotEqual()
    {
        var id = Guid.NewGuid();

        Assert.False(new Foo(id).Equals(new Bar(id)));
    }

    [Fact]
    public void Entity_IsNotEqualToNull()
    {
        var foo = new Foo(Guid.NewGuid());

        Assert.False(foo.Equals(null));
        Assert.False(foo == null);
    }
}
