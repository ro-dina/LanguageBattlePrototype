using System;

// Kept independent of Unity APIs so the exact release/development code paths can be compiled and verified.
public static class DevelopmentCurrencyAccess
{
    public static bool Enabled
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
    public static bool Run(Action grant)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        grant(); return true;
#else
        return false;
#endif
    }
}
