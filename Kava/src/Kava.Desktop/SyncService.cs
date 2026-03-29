using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kava.Desktop;

/// <summary>
/// Runs periodic background syncs for all CalDAV accounts.
/// Fires <see cref="SyncCompleted"/> after each cycle so the UI can refresh.
/// </summary>
public sealed class SyncService : IDisposable
{
    private readonly CalDavAccountService _accountService;
    private readonly TimeSpan _interval;
    private Timer? _timer;
    private int _running;
    private int _syncRequested;

    /// <summary>Raised on the thread-pool after a sync cycle completes with changes.</summary>
    public event Action? SyncCompleted;

    public SyncService(CalDavAccountService accountService, TimeSpan interval)
    {
        _accountService = accountService;
        _interval = interval;
    }

    public void Start()
    {
        // Check every 30 seconds; run sync if requested or on interval
        _timer ??= new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Request a sync on the next timer tick. Cheap to call — just sets a flag.
    /// </summary>
    public void RequestSync()
    {
        Interlocked.Exchange(ref _syncRequested, 1);
    }

    /// <summary>Trigger an immediate full sync (ignores CTag/sync-token).</summary>
    public async Task SyncNowAsync(CancellationToken ct = default)
    {
        Interlocked.Exchange(ref _syncRequested, 0);
        await RunSyncCycleAsync(ct, forceFullSync: true);
    }

    private int _tickCount;

    private async void OnTick(object? state)
    {
        var requested = Interlocked.Exchange(ref _syncRequested, 0) == 1;
        var intervalElapsed = Interlocked.Increment(ref _tickCount) % (_interval.TotalSeconds / 30) == 0;

        if (!requested && !intervalElapsed)
            return;

        try
        {
            await RunSyncCycleAsync();
        }
        catch
        {
            // Swallow — errors logged per-account in SyncAllAsync
        }
    }

    private async Task RunSyncCycleAsync(CancellationToken ct = default, bool forceFullSync = false)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return;

        try
        {
            var (synced, _) = await _accountService.SyncAllAsync(ct, forceFullSync);
            await RefreshCacheAsync();
            if (synced > 0)
                SyncCompleted?.Invoke();
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private async Task RefreshCacheAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await _accountService.GetEventsAsync(today.AddMonths(-6), today.AddMonths(6));
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
