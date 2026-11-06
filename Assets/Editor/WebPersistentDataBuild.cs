using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Unity 6000.4's Default template leaves persistence opt-in commented out.
// Keep SaveFileStore's validated writer and reset generations unchanged; enable the
// engine's queued IndexedDB synchronization for progress AND independent settings.
public sealed class WebPersistentDataBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        string path = Path.Combine(report.summary.outputPath, "index.html");
        if (!File.Exists(path)) throw new BuildFailedException("Web index.html is missing; persistence could not be enabled.");
        string html = File.ReadAllText(path);
        const string optIn = "// config.autoSyncPersistentDataPath = true;";
        if (html.Contains(optIn))
        {
            html = html.Replace(optIn, "config.autoSyncPersistentDataPath = true;");
            File.WriteAllText(path, html, new UTF8Encoding(false));
        }
        // Fail visibly if a different template needs its own integration; never silently
        // produce a build that reports successful saves only in its in-memory filesystem.
        if (!Regex.IsMatch(html, @"(?m)^\s*config\.autoSyncPersistentDataPath\s*=\s*true\s*;"))
            throw new BuildFailedException("Enable config.autoSyncPersistentDataPath = true before createUnityInstance in the Web template.");
        Debug.Log("Web persistent data: automatic IndexedDB synchronization enabled.");
    }
}
