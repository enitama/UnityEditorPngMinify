using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public class PngMinifyEditorWindow : EditorWindow
{
    private DefaultAsset _targetFolder;
    private string _pngquantPath;
    private int _qualityValue = 100;
    private StringBuilder _outputBuilder = new StringBuilder();
    private Vector2 _outputScrollPosition;
    private bool _isProcessRunning = false;

    [MenuItem("Tools/PngMinify")]
    public static void ShowWindow()
    {
        GetWindow<PngMinifyEditorWindow>("PngMinify");
    }

    private void OnGUI()
    {
        GUILayout.Label("PngMinify 0.0.1", EditorStyles.boldLabel);

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Path to pngquant:", GUILayout.Width(100));
        if (GUILayout.Button("Browse"))
        {
            _pngquantPath = EditorUtility.OpenFilePanel("Select File", "", "");
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Target folder:", GUILayout.Width(100));
        _targetFolder = EditorGUILayout.ObjectField(_targetFolder, typeof(DefaultAsset), false) as DefaultAsset;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Quality:", GUILayout.Width(100));
        _qualityValue = EditorGUILayout.IntSlider(_qualityValue, 0, 100);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (!_isProcessRunning)
        {
            if (GUILayout.Button("Execute"))
            {
                if (!EnsurePngquant(_pngquantPath))
                {
                    return;
                }
                string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
                var images = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
                Debug.Log("png files: " + string.Join(",", images));
                RunPngquant(_pngquantPath, images);
            }
        }
        else
        {
            GUILayout.Label("Running...");
        }

        GUILayout.Space(10);

        _outputScrollPosition = EditorGUILayout.BeginScrollView(_outputScrollPosition, GUILayout.Height(200));
        GUILayout.TextArea(_outputBuilder.ToString());
        EditorGUILayout.EndScrollView();
    }

    private void RunPngquant(string path, IEnumerable<string> images)
    {
        var sb = new StringBuilder();
        var startInfo = new ProcessStartInfo();
        startInfo.CreateNoWindow = true;
        startInfo.FileName = path;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        // ArgumentList unavailable in Unity 2019.
        startInfo.Arguments = $"--verbose --quality {_qualityValue} " + string.Join(" ", images.Select(x => $"\"{x}\""));
        var process = new Process();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;
        _outputBuilder.AppendLine("[Running]");
        process.OutputDataReceived += (s, e) =>
        {
            _outputBuilder.AppendLine(e.Data);
            Repaint();
        };
        process.ErrorDataReceived += (s, e) =>
        {
            _outputBuilder.AppendLine("[Error]" + e.Data);
            Repaint();
        };
        process.Exited += (s, e) =>
        {
            _outputBuilder.AppendLine("[Finished] Error code: " + process.ExitCode);
            _isProcessRunning = false;
            Repaint();
        };
        _isProcessRunning = true;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private bool EnsurePngquant(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("Path is blank!");
            return false;
        }
        var sb = new StringBuilder();
        var startInfo = new ProcessStartInfo();
        startInfo.CreateNoWindow = true;
        startInfo.FileName = path;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardError = true;
        var process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (!stderr.Contains("pngquant, 2."))
        {
            EditorUtility.DisplayDialog("Error", "Compatible pngquant not found", "OK");
            return false;
        }
        return true;
    }
}
