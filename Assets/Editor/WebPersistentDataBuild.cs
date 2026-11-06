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
        // Keep product/company identifiers (and save paths) unchanged; use the centralized game name for the tab.
        html = html.Replace("<title>Unity Web Player | " + PlayerSettings.productName + "</title>", "<title>" + Localization.Source("title.game.name") + "</title>");
        // Match the browser viewport on desktop and touch devices; the Title scene handles its own narrow layout.
        // Keep the known Default loader structure, URLs and persistence hook for the existing Vercel packager.
        if (!html.Contains("id=\"language-battle-viewport\""))
        {
            html = html.Replace("</head>", @"<meta name=""viewport"" content=""width=device-width, initial-scale=1, viewport-fit=cover"">
    <style id=""language-battle-viewport"">
      html, body { margin:0; width:100%; height:100%; overflow:hidden; background:#19130e; }
      #unity-container { position:fixed!important; inset:0!important; width:100%!important; height:100%!important; transform:none!important; }
      #unity-canvas { display:block; width:100%!important; height:100%!important; touch-action:none; }
      #unity-footer { display:none; }
    </style>
  </head>");
            File.WriteAllText(path, html, new UTF8Encoding(false));
        }
        // Fail visibly if a different template needs its own integration; never silently
        // produce a build that reports successful saves only in its in-memory filesystem.
        if (!Regex.IsMatch(html, @"(?m)^\s*config\.autoSyncPersistentDataPath\s*=\s*true\s*;"))
            throw new BuildFailedException("Enable config.autoSyncPersistentDataPath = true before createUnityInstance in the Web template.");
        Debug.Log("Web persistent data: automatic IndexedDB synchronization enabled.");
    }
}
