using System;

// Compiled unchanged by the release/development gate probe. Ordinary resets are always public.
public static class ProgressResetAccess
{
    public static bool DevelopmentEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }
    public static bool Allows(ProgressResetKind kind) => kind == ProgressResetKind.NewGame || DevelopmentEnabled &&
        (kind == ProgressResetKind.Tutorials || kind == ProgressResetKind.Stages || kind == ProgressResetKind.DevelopmentNewGame);
    public static bool Run(ProgressResetKind kind, Func<bool> action) => Allows(kind) && action != null && action();
}
