namespace RateFreak.ProviderContractTests;

internal sealed class ManualClock : TimeProvider
{
    private long ticks;
    private readonly List<Timer> timers=[];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref ticks);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
    {
        lock(timers) { var timer=new Timer(this,callback,state); timers.Add(timer); timer.Change(dueTime,period); return timer; }
    }
    public void Advance(TimeSpan duration)
    {
        lock(timers)
        {
            ticks+=duration.Ticks;
            foreach(var timer in timers.ToArray()) timer.Fire(ticks);
        }
    }
    private sealed class Timer(ManualClock owner,TimerCallback callback,object? state) : ITimer
    {
        private long due=long.MaxValue; private TimeSpan period;
        public bool Change(TimeSpan dueTime,TimeSpan repeat)
        { lock(owner.timers) { due=dueTime==Timeout.InfiniteTimeSpan?long.MaxValue:owner.ticks+dueTime.Ticks; period=repeat; return true; } }
        public void Fire(long now)
        { if(due>now) return; due=period==Timeout.InfiniteTimeSpan?long.MaxValue:now+period.Ticks; callback(state); }
        public void Dispose() { lock(owner.timers) { due=long.MaxValue; owner.timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
