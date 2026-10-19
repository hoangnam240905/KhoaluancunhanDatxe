using System.Threading.Channels;
using Backend.DTOs.Realtime;

namespace Backend.Services.Notifications;

/// <summary>
/// Hand-off between the publish path and <see cref="NotificationWriter"/>.
/// Events are published inside the SqliteWriteLock transaction (before commit); writing the
/// notification there with another DbContext would block on the SQLite write lock. The writer
/// drains this queue in the background, after the business transaction has committed.
/// </summary>
public sealed class NotificationQueue
{
    public const int Capacity = 500;

    private readonly Channel<RealtimeEventDto> _channel = Channel.CreateBounded<RealtimeEventDto>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

    /// <returns>false when the queue is full (the event is dropped; the toast still went out).</returns>
    public bool TryEnqueue(RealtimeEventDto evt) => _channel.Writer.TryWrite(evt);

    public ChannelReader<RealtimeEventDto> Reader => _channel.Reader;
}
