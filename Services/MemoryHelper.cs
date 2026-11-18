using System;
using System.Threading;

namespace LuxPresenter.Services;

public static class MemoryHelper
{
    private static int _isTrimming = 0;

    /// <summary>
    /// Performs non-blocking garbage collection when closing large resources (videos/databases).
    /// Does NOT unmap working set pages (no EmptyWorkingSet) to prevent UI hitching, page faults, and window lag.
    /// </summary>
    public static void TrimMemoryAsync(int delayMs = 300)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (Interlocked.CompareExchange(ref _isTrimming, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (delayMs > 0)
                {
                    Thread.Sleep(delayMs);
                }

                GC.Collect(1, GCCollectionMode.Optimized, false);
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _isTrimming, 0);
            }
        });
    }
}
