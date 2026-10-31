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
        else Publish();
    }

    private static void Publish()
    {
        foreach (Action callback in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { callback(); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
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
                Publish();
            }
        }
    }
}
