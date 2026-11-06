using System;
using System.IO;
using System.Text;
using UnityEngine;

// An atomic pointer selects one progress generation. Recovery never crosses this boundary.
// Old saves remain available for manual repair, but cannot resurrect a pre-reset character/ticket.
internal static class SaveGenerationStore
{
    [Serializable] private sealed class Pointer { public int version; public string generationId; }
    private const string PointerName = "save-generation.json";
    public static bool Resolve(string root, out string directory, out string error)
    {
        directory = root; error = null;
        try
        {
            string path = Path.Combine(root, PointerName);
            if (!File.Exists(path)) return true;
            if (new FileInfo(path).Length > 1024) throw new IOException("Oversized progress pointer.");
            var value = JsonUtility.FromJson<Pointer>(File.ReadAllText(path));
            if (value?.version != 1 || value.generationId == null || !value.generationId.StartsWith("progress-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(value.generationId.Substring(9), "N", out _)) throw new IOException("Invalid progress pointer.");
            directory = Path.Combine(root, value.generationId);
            // Never silently fall back to the old root when a selected generation is missing.
            if (!Directory.Exists(directory)) throw new IOException("Progress generation is missing.");
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        { error = "Progress generation cannot be read; original files are protected. " + e.Message; return false; }
    }
    public static bool Commit(string root, string expectedDirectory, SaveData data, out string directory, out string error)
    {
        directory = null; error = null; string temporary = null;
        try
        {
            if (!Resolve(root, out string active, out error) || active != expectedDirectory) return false;
            string id = "progress-" + Guid.NewGuid().ToString("N");
            string candidate = Path.Combine(root, id);
            var files = new SaveFileStore(candidate);
            // The ordinary validated atomic writer creates a new main and a same-generation backup.
            if (!files.Save(data) || !files.Save(data)) { error = files.LastError; return false; }
            string expected = JsonUtility.ToJson(data);
            foreach (string path in new[] { files.MainPath, files.MainPath + ".bak" })
                if (!SaveDataValidator.TryDecode(File.ReadAllText(path), out var verified, out error) || JsonUtility.ToJson(verified) != expected)
                    throw new IOException("Reset candidate failed read-back verification.");
            if (!Resolve(root, out active, out error) || active != expectedDirectory) return false;
            string pointer = Path.Combine(root, PointerName);
            temporary = pointer + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(new Pointer { version = 1, generationId = id }));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            // This rename is the commit point. No fallible file operation follows it.
            if (File.Exists(pointer)) File.Replace(temporary, pointer, null);
            else File.Move(temporary, pointer);
            directory = candidate; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        { error = "Reset save failed: " + e.Message; return false; }
        finally
        {
            try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception) { /* Incomplete pointer files are never loaded. */ }
        }
    }
}
