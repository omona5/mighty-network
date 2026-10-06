using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Generated after every Web build so progress uses compressed payload sizes.
public sealed class WebBuildSizes : IPostprocessBuildWithReport
{
    [Serializable] private sealed class Entry { public string url; public long size; }
    [Serializable] private sealed class Manifest { public Entry[] files; }
    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        string root = report.summary.outputPath;
        string build = Path.Combine(root, "Build");
        var manifest = new Manifest
        {
            files = Directory.GetFiles(build).OrderBy(file => file).Select(file => new Entry
            {
                url = "Build/" + Path.GetFileName(file), size = new FileInfo(file).Length
            }).ToArray()
        };
        File.WriteAllText(Path.Combine(root, "build-sizes.json"), JsonUtility.ToJson(manifest, true));
    }
}
