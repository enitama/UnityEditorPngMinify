using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace io.github.enitama.pngminify.Editor
{
    internal class ImageInfo
    {
        public string Path { get; set; }
        public long BytesBefore { get; set; }
        public long BytesAfter { get; set; }
    }

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
                    var imagePaths = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
                    Debug.Log("png files: " + string.Join(",", imagePaths));
                    var images = GetImageInfos(imagePaths).ToList();
                    RunPngquantOnImages(_pngquantPath, images);
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

        private IEnumerable<ImageInfo> GetImageInfos(IEnumerable<string> images)
        {
            return images.Select(path => new ImageInfo { Path = path, BytesBefore = new FileInfo(path).Length });
        }

        private async void RunPngquantOnImages(string path, List<ImageInfo> images)
        {
            _isProcessRunning = true;
            Repaint();
            foreach (var image in images)
            {
                int exitCode = await RunPngquant(path, image);
            }
            // Use MB not MiB.
            double mbBefore = images.Aggregate(0L, (acc, x) => acc + x.BytesBefore) / 1000.0 / 1000.0;
            double mbAfter = images.Aggregate(0L, (acc, x) => acc + (x.BytesAfter > 0 ? x.BytesAfter : x.BytesBefore)) / 1000.0 / 1000.0;
            _outputBuilder.AppendLine($"Reduced {mbBefore} MB to {mbAfter} MB");
            _isProcessRunning = false;
            Repaint();
        }

        private Task<int> RunPngquant(string path, ImageInfo image)
        {
            var tcs = new TaskCompletionSource<int>();
            var sb = new StringBuilder();
            var startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.FileName = path;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            // ArgumentList unavailable in Unity 2019.
            startInfo.Arguments = $"--verbose --quality {_qualityValue} \"{image.Path}\"";
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
                string newPath = Path.Combine(Path.GetDirectoryName(image.Path), Path.GetFileNameWithoutExtension(image.Path) + "-fs8" + Path.GetExtension(image.Path));
                var newFileInfo = new FileInfo(newPath);
                if (newFileInfo.Exists)
                {
                    Debug.Log($"Found {newPath}, before {image.BytesBefore}, after {newFileInfo.Length}");
                    image.BytesAfter = newFileInfo.Length;
                }
                else
                {
                    Debug.Log($"Could not find {newPath}, before {image.BytesBefore}");
                }
                tcs.SetResult(process.ExitCode);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return tcs.Task;
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
}
