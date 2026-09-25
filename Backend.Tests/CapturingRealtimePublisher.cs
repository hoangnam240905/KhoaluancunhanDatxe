using Backend.DTOs.Realtime;
using Backend.Services;

namespace Backend.Tests;

internal sealed class CapturingRealtimePublisher : IRealtimePublisher
{
    private readonly List<RealtimeEventDto> _events = [];
    private readonly object _gate = new();

    public IReadOnlyList<RealtimeEventDto> Events
    {
        get
        {
            lock (_gate)
                return [.. _events];
        }
    }

    public Task PublishAsync(RealtimeEventDto evt, RealtimeAudience audience)
    {
        lock (_gate)
            _events.Add(evt);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_gate)
            _events.Clear();
    }
}
