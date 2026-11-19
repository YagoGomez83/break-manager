using BreakManager.Domain.Identity;

namespace BreakManager.Domain.Tests.Identity;

/// <summary>Hasher reversible y trivial, solo para tests.</summary>
internal sealed class FakePinHasher : IPinHasher
{
    public string Hash(Pin pin) => "hash:" + pin.Value;

    public bool Verify(string pinHash, string candidate) => pinHash == "hash:" + candidate;
}
