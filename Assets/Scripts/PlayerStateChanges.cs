using System;

// One notification per committed operation (including multi-item crafting/gacha).
public static class PlayerStateChanges
{
    public static event Action Changed;
    private static int batchDepth;
    private static bool pending;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Changed = null;
        batchDepth = 0;
        pending = false;
    }

    public static IDisposable BeginBatch()
    {
        batchDepth++;
        return new Batch();
    }

    public static void Notify()
    {
        if (batchDepth > 0) pending = true;
        else Changed?.Invoke();
    }

    private sealed class Batch : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (--batchDepth == 0 && pending)
            {
                pending = false;
                Changed?.Invoke();
            }
        }
    }
}
