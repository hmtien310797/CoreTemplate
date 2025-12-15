#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public class OptionalPackagesInstallerWindow : EditorWindow
{
    private static readonly List<OptionalPackage> Options = new()
    {
        OptionalPackage.OpenUpm(
            displayName: "UniTask (Cysharp)",
            packageName: "com.cysharp.unitask",
            versionOrRange: "2.5.10",
            scopes: new[] { "com.cysharp" }
        )
    };

    private readonly Dictionary<string, bool> _selected = new();

    [MenuItem("Tools/EssentialCore/Optional Packages Installer")]
    public static void Open()
    {
        var w = GetWindow<OptionalPackagesInstallerWindow>("Optional Packages");
        w.minSize = new Vector2(520, 320);
        w.Show();
    }

    private void OnEnable()
    {
        foreach (var o in Options)
            _selected.TryAdd(o.PackageName, false);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Select optional packages to install", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This will edit Packages/manifest.json (project-level) and trigger Unity Package Manager to download packages.",
            MessageType.Info
        );

        GUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            foreach (var o in Options)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _selected[o.PackageName] = EditorGUILayout.ToggleLeft(o.DisplayName, _selected[o.PackageName], GUILayout.Width(260));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"{o.PackageName}  {o.VersionOrRange}", EditorStyles.miniLabel);
                }
            }
        }

        GUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All"))
            {
                foreach (var k in _selected.Keys.ToList()) _selected[k] = true;
            }

            if (GUILayout.Button("Clear"))
            {
                foreach (var k in _selected.Keys.ToList()) _selected[k] = false;
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = _selected.Values.Any(v => v);
            if (GUILayout.Button("Install Selected", GUILayout.Height(28), GUILayout.Width(160)))
            {
                InstallSelected();
            }
            GUI.enabled = true;
        }
    }

    private void InstallSelected()
    {
        var chosen = Options.Where(o => _selected.TryGetValue(o.PackageName, out var on) && on).ToList();
        if (chosen.Count == 0) return;

        try
        {
            var manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifestPath))
            {
                EditorUtility.DisplayDialog("Error", $"manifest.json not found:\n{manifestPath}", "OK");
                return;
            }

            // Backup
            var backupPath = manifestPath + ".bak";
            File.Copy(manifestPath, backupPath, overwrite: true);

            var json = File.ReadAllText(manifestPath);
            var manifest = JsonUtility.FromJson<ManifestWrapper>(FixJsonForUnity(json));
            // JsonUtility không parse Dictionary tốt => dùng parser nhẹ dạng “string ops”
            // => Ở đây mình dùng cách an toàn hơn: thao tác text theo pattern đơn giản.

            json = EnsureScopedRegistryOpenUpm(json, chosen);
            json = EnsureDependencies(json, chosen);

            File.WriteAllText(manifestPath, json);

            // Trigger UPM resolve
            Client.Resolve();

            EditorUtility.DisplayDialog(
                "Done",
                $"Installed {chosen.Count} package(s).\nBackup created:\n{backupPath}\n\nUnity may take a moment to resolve packages.",
                "OK"
            );
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Error", e.Message, "OK");
        }
    }

    // ====== Minimal JSON manipulation (string-based) ======

    private static string EnsureScopedRegistryOpenUpm(string manifestJson, List<OptionalPackage> chosen)
    {
        // Nếu không có package nào cần OpenUPM registry thì bỏ qua
        if (chosen.All(c => c.Source != PackageSource.OpenUPM)) return manifestJson;

        // Đảm bảo có scopedRegistries
        if (!manifestJson.Contains("\"scopedRegistries\""))
        {
            // chèn scopedRegistries trước "dependencies"
            manifestJson = InsertBeforeDependencies(manifestJson,
                "\"scopedRegistries\": [],\n");
        }

        // Đảm bảo có entry OpenUPM registry
        if (!manifestJson.Contains("https://package.openupm.com"))
        {
            var openUpmEntry =
                "{\n" +
                "      \"name\": \"OpenUPM\",\n" +
                "      \"url\": \"https://package.openupm.com\",\n" +
                "      \"scopes\": []\n" +
                "    }";

            manifestJson = AddToScopedRegistriesArray(manifestJson, openUpmEntry);
        }

        // Ensure scopes include required scopes
        var neededScopes = chosen
            .Where(c => c.Source == PackageSource.OpenUPM)
            .SelectMany(c => c.Scopes ?? Array.Empty<string>())
            .Distinct()
            .ToList();

        foreach (var scope in neededScopes)
            manifestJson = EnsureOpenUpmScope(manifestJson, scope);

        return manifestJson;
    }

    private static string EnsureDependencies(string manifestJson, List<OptionalPackage> chosen)
    {
        // Đảm bảo có dependencies
        if (!manifestJson.Contains("\"dependencies\""))
            throw new Exception("manifest.json is missing \"dependencies\".");

        foreach (var p in chosen)
        {
            // Nếu đã có packageName trong dependencies thì bỏ qua
            if (manifestJson.Contains($"\"{p.PackageName}\"")) continue;

            manifestJson = AddDependency(manifestJson, p.PackageName, p.VersionOrRange);
        }

        return manifestJson;
    }

    private static string InsertBeforeDependencies(string json, string insertBlock)
    {
        var idx = json.IndexOf("\"dependencies\"", StringComparison.Ordinal);
        if (idx < 0) throw new Exception("manifest.json missing \"dependencies\" section.");
        // tìm vị trí đầu dòng của "dependencies"
        var lineStart = json.LastIndexOf('\n', idx);
        if (lineStart < 0) lineStart = 0;
        return json.Insert(lineStart + 1, "  " + insertBlock);
    }

    private static string AddToScopedRegistriesArray(string json, string newEntry)
    {
        // find "scopedRegistries": [
        var key = "\"scopedRegistries\"";
        var keyIdx = json.IndexOf(key, StringComparison.Ordinal);
        if (keyIdx < 0) throw new Exception("manifest.json missing scopedRegistries.");

        var arrayStart = json.IndexOf('[', keyIdx);
        if (arrayStart < 0) throw new Exception("scopedRegistries array not found.");

        var arrayEnd = FindMatchingBracket(json, arrayStart, '[', ']');
        var inside = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1).Trim();

        var insert = (string.IsNullOrWhiteSpace(inside) ? "\n    " : "\n    ,\n    ")
                     + IndentLines(newEntry, 4) + "\n  ";

        return json.Insert(arrayEnd, insert);
    }

    private static string EnsureOpenUpmScope(string json, string scope)
    {
        // Very small heuristic: insert scope into OpenUPM "scopes": [...]
        // Find OpenUPM entry by URL
        var urlIdx = json.IndexOf("https://package.openupm.com", StringComparison.Ordinal);
        if (urlIdx < 0) return json;

        // Find "scopes" after that
        var scopesIdx = json.IndexOf("\"scopes\"", urlIdx, StringComparison.Ordinal);
        if (scopesIdx < 0) return json;

        var arrStart = json.IndexOf('[', scopesIdx);
        var arrEnd = FindMatchingBracket(json, arrStart, '[', ']');

        var arrContent = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
        if (arrContent.Contains($"\"{scope}\"")) return json;

        var trimmed = arrContent.Trim();
        var add = string.IsNullOrWhiteSpace(trimmed) ? $"\n        \"{scope}\"\n      "
                                                     : $"\n        \"{scope}\",\n      {trimmed.TrimStart()}\n      ";
        // Replace full scopes array content
        return json.Substring(0, arrStart + 1) + add + json.Substring(arrEnd);
    }

    private static string AddDependency(string json, string name, string versionOrRange)
    {
        // Insert into dependencies object before closing }
        var depIdx = json.IndexOf("\"dependencies\"", StringComparison.Ordinal);
        if (depIdx < 0) throw new Exception("dependencies not found.");

        var objStart = json.IndexOf('{', depIdx);
        var objEnd = FindMatchingBracket(json, objStart, '{', '}');

        var inside = json.Substring(objStart + 1, objEnd - objStart - 1);
        var hasAny = inside.Trim().Length > 0;

        var entry = $"    \"{name}\": \"{versionOrRange}\"";
        var insert = (hasAny ? ",\n" : "\n") + entry + "\n";

        return json.Insert(objEnd, insert);
    }

    private static int FindMatchingBracket(string s, int start, char open, char close)
    {
        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            if (s[i] == open) depth++;
            else if (s[i] == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        throw new Exception($"Matching bracket '{close}' not found.");
    }

    private static string IndentLines(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = text.Split('\n');
        return string.Join("\n", lines.Select(l => pad + l));
    }

    private static string FixJsonForUnity(string json)
    {
        // JsonUtility cần object root với field; nhưng ở đây chỉ để “compile”, tool chính dùng string ops.
        return json;
    }

    [Serializable]
    private class ManifestWrapper { public object dummy; } // placeholder
}

internal enum PackageSource { OpenUPM, UnityRegistry }

[Serializable]
internal class OptionalPackage
{
    public string DisplayName;
    public string PackageName;
    public string VersionOrRange;
    public PackageSource Source;
    public string[] Scopes;

    public static OptionalPackage OpenUpm(string displayName, string packageName, string versionOrRange, string[] scopes)
        => new OptionalPackage
        {
            DisplayName = displayName,
            PackageName = packageName,
            VersionOrRange = versionOrRange,
            Source = PackageSource.OpenUPM,
            Scopes = scopes
        };
}
#endif
