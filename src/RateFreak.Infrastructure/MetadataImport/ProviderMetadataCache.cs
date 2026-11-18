using System.Text;
using System.Text.Json;
using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.MetadataImport;

/// <summary>Instance-local public metadata cache. Provider transport gates still govern misses.</summary>
public sealed class ProviderMetadataCache(TimeProvider clock) : IDisposable
{
    private const int MaximumBytes = 16 * 1024 * 1024;
    private const int MaximumEntryBytes = 1024 * 1024;
    private const int MaximumEntries = 2048;
    private const int MaximumFlights = 64;
    private readonly object sync = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> recency = new();
    private readonly Dictionary<string, Flight> flights = new(StringComparer.Ordinal);
    private int bytes;
    private int activeOperations;
    private bool disposed;

    private sealed record Entry(byte[] Payload, long Started, TimeSpan Lifetime, int Size, LinkedListNode<string> Node);
    private sealed class Flight(TimeProvider clock) : IDisposable
    {
        public readonly CancellationTokenSource Cancellation = new(TimeSpan.FromSeconds(20), clock);
        public readonly TaskCompletionSource<byte[]> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Waiters;
        public void Dispose() => Cancellation.Dispose();
    }

    public async Task<T?> GetAsync<T>(string key, TimeSpan lifetime, Func<CancellationToken, Task<T?>> fetch, CancellationToken caller) where T : class
    {
        caller.ThrowIfCancellationRequested();
        Flight flight;
        bool start;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (entries.TryGetValue(key, out var entry))
            {
                if (clock.GetElapsedTime(entry.Started) < entry.Lifetime)
                {
                    recency.Remove(entry.Node); recency.AddLast(entry.Node);
                    return JsonSerializer.Deserialize<T>(entry.Payload);
                }
                Remove(key, entry);
            }
            start = !flights.TryGetValue(key, out flight!);
            if (start)
            {
                if (activeOperations >= MaximumFlights) throw new MediaProviderException(ProviderFailure.Unavailable, 1);
                flight = new Flight(clock); flights.Add(key, flight);
                activeOperations++;
            }
            flight.Waiters++;
        }
        if (start) _ = FetchAsync(key, lifetime, fetch, flight);
        try
        {
            var payload = await flight.Completion.Task.WaitAsync(caller);
            caller.ThrowIfCancellationRequested();
            // Every consumer owns its arrays/JsonElements, including coalesced callers.
            return JsonSerializer.Deserialize<T>(payload);
        }
        finally
        {
            lock (sync)
            {
                if (--flight.Waiters == 0 && !flight.Completion.Task.IsCompleted)
                {
                    if (flights.GetValueOrDefault(key) == flight) flights.Remove(key);
                    // Schedule token callbacks outside this lock to avoid reentrancy.
                    _ = flight.Cancellation.CancelAsync();
                }
            }
        }
    }

    private async Task FetchAsync<T>(string key, TimeSpan lifetime, Func<CancellationToken, Task<T?>> fetch, Flight flight) where T : class
    {
        try
        {
            var result = await fetch(flight.Cancellation.Token);
            flight.Cancellation.Token.ThrowIfCancellationRequested();
            var payload = JsonSerializer.SerializeToUtf8Bytes(result);
            var size = payload.Length + Encoding.UTF8.GetByteCount(key) + 128;
            lock (sync)
            {
                if (!disposed && !flight.Cancellation.IsCancellationRequested && result is not null && size <= MaximumEntryBytes)
                {
                    if (entries.TryGetValue(key, out var previous)) Remove(key, previous);
                    while (bytes + size > MaximumBytes || entries.Count >= MaximumEntries)
                    {
                        var oldest = recency.First!.Value; Remove(oldest, entries[oldest]);
                    }
                    var node = recency.AddLast(key);
                    entries.Add(key, new Entry(payload, clock.GetTimestamp(), lifetime, size, node)); bytes += size;
                }
                flight.Completion.TrySetResult(payload);
            }
        }
        catch (OperationCanceledException) { flight.Completion.TrySetException(new MediaProviderException(ProviderFailure.Timeout)); }
        catch (Exception error) { flight.Completion.TrySetException(error); }
        finally
        {
            lock (sync)
            {
                if (flights.GetValueOrDefault(key) == flight) flights.Remove(key);
                activeOperations--;
                if (flight.Waiters == 0 && flight.Completion.Task.IsFaulted) _ = flight.Completion.Task.Exception;
                flight.Dispose();
            }
        }
    }

    private void Remove(string key, Entry entry)
    {
        entries.Remove(key); recency.Remove(entry.Node); bytes -= entry.Size;
    }

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            foreach (var flight in flights.Values) _ = flight.Cancellation.CancelAsync();
            entries.Clear(); recency.Clear(); bytes = 0;
        }
    }
}
