using BreakManager.Agent.Core.Activity;

namespace BreakManager.Agent.Core.Tests.Activity;

public sealed class ActivitySourceContractTests
{
    private sealed class FakeActivitySource : IActivitySource
    {
        public event EventHandler? ActivityDetected;

        public void Pulse() => ActivityDetected?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void ActivityDetected_IsAnAbstractPulseWithoutInputData()
    {
        // ADR-0003 y ADR-0006: el agente solo conoce "hubo actividad", nunca coordenadas, botones ni teclas.
        var activityEvent = typeof(IActivitySource).GetEvent(nameof(IActivitySource.ActivityDetected));

        Assert.NotNull(activityEvent);
        Assert.Equal(typeof(EventHandler), activityEvent.EventHandlerType);
    }

    [Fact]
    public void ActivityDetected_NotifiesSubscribers()
    {
        var source = new FakeActivitySource();
        var pulses = 0;
        source.ActivityDetected += (_, _) => pulses++;

        source.Pulse();
        source.Pulse();

        Assert.Equal(2, pulses);
    }
}
