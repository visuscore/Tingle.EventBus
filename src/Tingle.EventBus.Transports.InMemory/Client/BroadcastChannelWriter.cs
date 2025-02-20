﻿using System.Threading.Channels;

namespace Tingle.EventBus.Transports.InMemory.Client;

internal class BroadcastChannelWriter<T> : ChannelWriter<T>
{
    public BroadcastChannelWriter(ICollection<ChannelWriter<T>>? children = null)
    {
        Children = children ?? new List<ChannelWriter<T>>();
    }

    public ICollection<ChannelWriter<T>> Children { get; }

    /// <inheritdoc/>
    public override bool TryWrite(T item) => Children.All(w => w.TryWrite(item));

    /// <inheritdoc/>
    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Waiting is not supported.");
    }
}
