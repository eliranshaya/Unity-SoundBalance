using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SoundBalance.EditorTools
{
    /// <summary>
    /// Editor window that balances the loudness of a set of AudioClips to match a
    /// reference clip (BS.1770 LUFS) and exports the results as OGG files into a
    /// timestamped folder under Assets/BalancedSounds/.
    /// </summary>
    public class SoundBalanceWindow : EditorWindow
    {
        private const string OutputRoot = "Assets/BalancedSounds";
        private const float PeakCeiling = 0.99f;

        private AudioClip _referenceClip;
        private readonly List<AudioClip> _clips = new List<AudioClip>();
        private float _oggQuality = 0.5f;
        private Vector2 _scroll;
        private readonly List<string> _lastResults = new List<string>();
        private string _lastOutputFolder;

        [MenuItem("Tools/Sound Balance")]
        public static void Open()
        {
            var window = GetWindow<SoundBalanceWindow>("Sound Balance");
            window.minSize = new Vector2(360f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Reference Sound", EditorStyles.boldLabel);
            _referenceClip = (AudioClip)EditorGUILayout.ObjectField(_referenceClip, typeof(AudioClip), false);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"Sounds To Balance ({_clips.Count})", EditorStyles.boldLabel);
            DrawDropArea();
            DrawClipList();

            EditorGUILayout.Space(10f);
            _oggQuality = EditorGUILayout.Slider(new GUIContent("OGG Quality", "Vorbis VBR quality (0.5 ≈ 160kbps stereo)"), _oggQuality, 0.1f, 1f);

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(_referenceClip == null || _clips.Count == 0))
            {
                if (GUILayout.Button("Balance", GUILayout.Height(36f)))
                    RunBalance();
            }

            DrawResults();
        }

        private void DrawDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag AudioClips or folders here", EditorStyles.helpBox);

            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (UnityEngine.Object dragged in DragAndDrop.objectReferences)
                        AddDraggedObject(dragged);
                    GUI.changed = true;
                }
                evt.Use();
            }
        }

        private void AddDraggedObject(UnityEngine.Object dragged)
        {
            if (dragged is AudioClip clip)
            {
                AddClip(clip);
                return;
            }

            // Folder: add every AudioClip inside it (recursively)
            string path = AssetDatabase.GetAssetPath(dragged);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { path }))
                {
                    var found = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (found != null)
                        AddClip(found);
                }
            }
        }

        private void AddClip(AudioClip clip)
        {
            if (!_clips.Contains(clip))
                _clips.Add(clip);
        }

        private void DrawClipList()
        {
            if (_clips.Count == 0)
                return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(160f));
            int removeIndex = -1;
            for (int i = 0; i < _clips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(_clips[i], typeof(AudioClip), false);
                if (GUILayout.Button("X", GUILayout.Width(22f)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
                _clips.RemoveAt(removeIndex);
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear All"))
                _clips.Clear();
        }

        private void DrawResults()
        {
            if (_lastResults.Count == 0)
                return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Last Run", EditorStyles.boldLabel);
            foreach (string line in _lastResults)
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_lastOutputFolder) && GUILayout.Button("Show Output Folder"))
            {
                var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_lastOutputFolder);
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                    Selection.activeObject = folder;
                }
            }
        }

        private void RunBalance()
        {
            _lastResults.Clear();
            string outputFolder = $"{OutputRoot}/{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            try
            {
                EditorUtility.DisplayProgressBar("Sound Balance", "Measuring reference...", 0f);
                float[] refSamples = AudioClipSampleReader.Read(_referenceClip, out int refChannels, out int refRate);
                double refLufs = LoudnessMeter.IntegratedLoudness(refSamples, refChannels, refRate);
                if (double.IsInfinity(refLufs))
                {
                    EditorUtility.DisplayDialog("Sound Balance", "The reference sound is silent - cannot balance against it.", "OK");
                    return;
                }

                Directory.CreateDirectory(outputFolder);
                _lastResults.Add($"Reference {_referenceClip.name}: {refLufs:F2} LUFS");

                var usedNames = new HashSet<string>();
                for (int i = 0; i < _clips.Count; i++)
                {
                    AudioClip clip = _clips[i];
                    EditorUtility.DisplayProgressBar("Sound Balance", $"Balancing {clip.name} ({i + 1}/{_clips.Count})", (i + 1f) / (_clips.Count + 1f));
                    try
                    {
                        BalanceClip(clip, refLufs, outputFolder, usedNames);
                    }
                    catch (Exception e)
                    {
                        _lastResults.Add($"{clip.name}: FAILED - {e.Message}");
                        Debug.LogError($"[SoundBalance] {clip.name} failed: {e}");
                    }
                }

                AssetDatabase.Refresh();
                _lastOutputFolder = outputFolder;
                _lastResults.Add($"Done -> {outputFolder}");
                Debug.Log($"[SoundBalance] {string.Join("\n", _lastResults)}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void BalanceClip(AudioClip clip, double refLufs, string outputFolder, HashSet<string> usedNames)
        {
            float[] samples = AudioClipSampleReader.Read(clip, out int channels, out int sampleRate);
            double lufs = LoudnessMeter.IntegratedLoudness(samples, channels, sampleRate);
            if (double.IsInfinity(lufs))
            {
                _lastResults.Add($"{clip.name}: skipped (silent)");
                return;
            }

            double gainDb = refLufs - lufs;
            float gain = (float)Math.Pow(10.0, gainDb / 20.0);
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
                float abs = Mathf.Abs(samples[i]);
                if (abs > peak) peak = abs;
            }

            string note = "";
            if (peak > PeakCeiling)
            {
                float limit = PeakCeiling / peak;
                for (int i = 0; i < samples.Length; i++)
                    samples[i] *= limit;
                note = $" (limited, peak was {20f * Mathf.Log10(peak):+0.0} dBFS)";
            }

            string baseName = $"{clip.name}_balanced_by_{_referenceClip.name}";
            string fileName = baseName;
            int suffix = 1;
            while (!usedNames.Add(fileName))
                fileName = $"{baseName}_{++suffix}";

            byte[] ogg = OggFileWriter.Encode(samples, channels, sampleRate, _oggQuality);
            File.WriteAllBytes($"{outputFolder}/{fileName}.ogg", ogg);
            _lastResults.Add($"{clip.name}: {lufs:F2} LUFS, gain {gainDb:+0.00;-0.00} dB{note}");
        }

    }
}
