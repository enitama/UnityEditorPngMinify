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
        private int _selectedLanguage = 0;

        // TODO: Adopt a better localization method.
        private Dictionary<string, string> SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "ja", "日本語" } };
        private Dictionary<string, Dictionary<string, string>> Localizations = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "en", new Dictionary<string, string>
                {
                    { "lbl.pngquantpath", "Path to pngquant:" },
                    { "lbl.targetfolder", "Target folder:" },
                    { "lbl.imagequality", "Quality:" },
                    { "lbl.running", "Running" },
                    { "msg.pngquantblank", "Please specify path to pngquant." },
                    { "msg.pngquantincompatbile", "Couldn't find compatible pngquant!" },
                    { "msg.reduced", "Reduced {0} to {1} ({2}% savings)" },
                    { "btn.execute", "Execute" },
                    { "log.running", "[Running]" },
                    { "log.exited", "[Exited]" },
                    { "log.exitcode", "Exit code: {0}" },
                }
            },
            {
                "ja", new Dictionary<string, string>
                {
                    { "lbl.pngquantpath", "pngquantのパス:" },
                    { "lbl.targetfolder", "ターゲットフォルダー:" },
                    { "lbl.imagequality", "画質:" },
                    { "lbl.running", "実行中" },
                    { "msg.pngquantblank", "pngquantのパスを指定してください。" },
                    { "msg.pngquantincompatbile", "互換性のあるpngquantが見つかりませんでした！" },
                    { "msg.reduced", "{0}を{1}に縮小しました（{2}%の節約）" },
                    { "btn.execute", "実行" },
                    { "log.running", "[実行中]" },
                    { "log.exited", "[終了]" },
                    { "log.exitcode", "終了コード: {0}" },
                }
            }
        };
        private string GetLocalizedString(string name, params object[] args) => GetLocalizedStringForLang(SupportedLanguages.Keys.ElementAt(_selectedLanguage), name, args);
        private string GetLocalizedStringForLang(string code, string name, params object[] args) => string.Format(Localizations[code][name], args);

        [MenuItem("Tools/PngMinify")]
        public static void ShowWindow()
        {
            GetWindow<PngMinifyEditorWindow>("PngMinify");
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("PngMinify 0.0.3", EditorStyles.boldLabel);
            _selectedLanguage = EditorGUILayout.Popup(_selectedLanguage, SupportedLanguages.Values.ToArray(), GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetLocalizedString("lbl.pngquantpath"), GUILayout.Width(100));
            if (GUILayout.Button("Browse"))
            {
                _pngquantPath = EditorUtility.OpenFilePanel("Select File", "", "");
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetLocalizedString("lbl.targetfolder"), GUILayout.Width(100));
            _targetFolder = EditorGUILayout.ObjectField(_targetFolder, typeof(DefaultAsset), false) as DefaultAsset;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetLocalizedString("lbl.imagequality"), GUILayout.Width(100));
            _qualityValue = EditorGUILayout.IntSlider(_qualityValue, 0, 100);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (!_isProcessRunning)
            {
                if (GUILayout.Button(GetLocalizedString("btn.execute")))
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
                GUILayout.Label(GetLocalizedString("lbl.running"));
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
                _outputBuilder.AppendLine(GetLocalizedString("log.running"));
                int exitCode = await RunPngquant(path, image, _qualityValue, output =>
                {
                    _outputBuilder.AppendLine(output);
                    Repaint();
                });
                _outputBuilder.AppendLine(GetLocalizedString("log.exited") + GetLocalizedString("log.exitcode", exitCode));
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
            }
            // Use MB not MiB.
            double mbBefore = images.Aggregate(0L, (acc, x) => acc + x.BytesBefore) / 1000.0 / 1000.0;
            double mbAfter = images.Aggregate(0L, (acc, x) => acc + (x.BytesAfter > 0 ? x.BytesAfter : x.BytesBefore)) / 1000.0 / 1000.0;
            double saved = (mbAfter - mbBefore) / mbBefore;
            _outputBuilder.AppendLine(GetLocalizedString("msg.reduced", $"{mbBefore} MB", $"{mbAfter} MB", saved * 100));
            _isProcessRunning = false;
            Repaint();
        }

        private static Task<int> RunPngquant(string path, ImageInfo image, int quality, Action<string> outputHandler)
        {
            var tcs = new TaskCompletionSource<int>();
            var sb = new StringBuilder();
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = $"--verbose --quality {quality} --skip-if-larger \"{image.Path}\""
            };
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s, e) => outputHandler(e.Data);
            process.ErrorDataReceived += (s, e) => outputHandler(e.Data);
            process.Exited += (s, e) => tcs.SetResult(process.ExitCode);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return tcs.Task;
        }

        private bool EnsurePngquant(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Error", GetLocalizedString("msg.pngquantblank"), "OK");
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
                EditorUtility.DisplayDialog("Error", GetLocalizedString("msg.pngquantincompatible"), "OK");
                return false;
            }
            return true;
        }
    }
}
