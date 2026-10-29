#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;


// Runs before entering Play Mode, including runs started from the Editor Test Runner window.
public sealed class TestSaveIsolation : IPrebuildSetup, IPostBuildCleanup
{
    public const string FingerprintKey = "LanguageBattle.TestRealProfileFingerprint";
    public void Setup()
    {
        SessionState.SetString(FingerprintKey, RealProfileFingerprint());
        SessionState.SetString(PlayerSaveService.TestDirectorySessionKey,
            Path.Combine(Path.GetTempPath(), "LanguageBattleTestBootstrap", Guid.NewGuid().ToString("N")));
    }

    public static string RealProfileFingerprint()
    {
        string root = Path.Combine(Application.persistentDataPath, "LanguageBattle");
        if (!Directory.Exists(root)) return "missing";
        using (var sha = SHA256.Create())
            return string.Join(";", Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p)
                .Select(p => p + ":" + File.GetCreationTimeUtc(p).Ticks + ":" + File.GetLastWriteTimeUtc(p).Ticks + ":" + Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)))));
    }

    public void Cleanup() => SessionState.EraseString(PlayerSaveService.TestDirectorySessionKey);
}

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class TestProfileIsolationTests
{
    [UnityTest]
    public IEnumerator TestRunnerBootstrapNeverUsesRealPlayerProfile()
    {
        string realPath = Path.Combine(Application.persistentDataPath, "LanguageBattle", "save.json");
        var service = PlayerSaveService.Instance;
        if (service != null) Assert.That(service.Files.MainPath, Is.Not.EqualTo(realPath));
        Assert.That(SessionState.GetString(PlayerSaveService.TestDirectorySessionKey, ""), Is.Not.Empty);
        Assert.That(TestSaveIsolation.RealProfileFingerprint(),
            Is.EqualTo(SessionState.GetString(TestSaveIsolation.FingerprintKey, "uninitialized")),
            "Real player files must remain byte-for-byte unchanged across the test run.");
        Debug.Log("FOUNDATION_DEFAULT_SAVE_PATH " + realPath);
        yield return null;
    }
}
#endif
