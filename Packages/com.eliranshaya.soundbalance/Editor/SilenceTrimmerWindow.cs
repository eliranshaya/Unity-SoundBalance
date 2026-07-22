using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SoundBalance.EditorTools
{
    /// <summary>
    /// Editor window for trimming silence from an AudioClip - automatically
    /// (threshold-based detection) or manually (draggable trim markers over the
    /// waveform) - with preview playback and OGG export.
    /// </summary>
    public class SilenceTrimmerWindow : EditorWindow
    {
        private const string OutputRoot = "Assets/TrimmedSounds";
        private const int WaveformTexWidth = 1024;
        private const int WaveformTexHeight = 128;

        private AudioClip _clip;
        private AudioClip _loadedClip;
        private float[] _samples;
        private int _channels;
        private int _sampleRate;
        private int _totalFrames;

        private int _startFrame;
        private int _endFrame;

        private float _thresholdDb = -60f;
        private float _paddingMs = 10f;
        private float _fadeMs = 3f;
        private float _oggQuality = 0.5f;

        private Texture2D _waveformTex;
        private int _dragMarker = -1; // 0 = start, 1 = end
        private string _lastResult;
        private string _lastOutputFolder;

        private AudioSource _previewSource;

        [MenuItem("Tools/Sound Toolkit/Silence Trimmer")]
        public static void Open()
        {
            var window = GetWindow<SilenceTrimmerWindow>("Silence Trimmer");
            window.minSize = new Vector2(420f, 430f);
        }

        private void OnDisable()
        {
            StopPreview();
            if (_previewSource != null)
                DestroyImmediate(_previewSource.gameObject);
            if (_waveformTex != null)
                DestroyImmediate(_waveformTex);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);
            _clip = (AudioClip)EditorGUILayout.ObjectField(_clip, typeof(AudioClip), false);

            if (_clip == null)
            {
                EditorGUILayout.HelpBox("Drag an AudioClip here to start trimming.", MessageType.Info);
                return;
            }

            if (_clip != _loadedClip)
                LoadClip();
            if (_samples == null)
                return;

            EditorGUILayout.Space(8f);
            DrawWaveform();
            DrawTrimFields();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Automatic Trim", EditorStyles.boldLabel);
            _thresholdDb = EditorGUILayout.Slider(new GUIContent("Threshold (dB)", "Anything quieter than this is considered silence"), _thresholdDb, -90f, -20f);
            _paddingMs = EditorGUILayout.Slider(new GUIContent("Padding (ms)", "Extra audio kept before/after the detected sound"), _paddingMs, 0f, 100f);
            if (GUILayout.Button("Auto Detect Silence"))
                AutoDetect();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            _fadeMs = EditorGUILayout.Slider(new GUIContent("Edge Fade (ms)", "Short fade at the cut points to avoid clicks"), _fadeMs, 0f, 20f);
            _oggQuality = EditorGUILayout.Slider(new GUIContent("OGG Quality"), _oggQuality, 0.1f, 1f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Preview", GUILayout.Height(28f)))
                PlayPreview();
            if (GUILayout.Button("■ Stop", GUILayout.Height(28f), GUILayout.Width(70f)))
                StopPreview();
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(_endFrame <= _startFrame))
            {
                if (GUILayout.Button("Export Trimmed OGG", GUILayout.Height(34f)))
                    ExportTrimmed();
            }

            if (!string.IsNullOrEmpty(_lastResult))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(_lastResult, EditorStyles.miniLabel);
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
        }

        private void LoadClip()
        {
            try
            {
                _samples = AudioClipSampleReader.Read(_clip, out _channels, out _sampleRate);
                _totalFrames = _samples.Length / _channels;
                _startFrame = 0;
                _endFrame = _totalFrames;
                _loadedClip = _clip;
                _lastResult = null;
                RebuildWaveformTexture();
            }
            catch (Exception e)
            {
                _samples = null;
                _loadedClip = _clip;
                EditorUtility.DisplayDialog("Silence Trimmer", $"Could not read '{_clip.name}': {e.Message}", "OK");
            }
        }

        private void RebuildWaveformTexture()
        {
            if (_waveformTex == null)
            {
                _waveformTex = new Texture2D(WaveformTexWidth, WaveformTexHeight, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
            }

            var bg = new Color(0.13f, 0.13f, 0.13f);
            var fg = new Color(0.38f, 0.78f, 0.62f);
            var pixels = new Color[WaveformTexWidth * WaveformTexHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = bg;

            float framesPerCol = (float)_totalFrames / WaveformTexWidth;
            int mid = WaveformTexHeight / 2;
            for (int x = 0; x < WaveformTexWidth; x++)
            {
                int from = (int)(x * framesPerCol);
                int to = Math.Min((int)((x + 1) * framesPerCol) + 1, _totalFrames);
                float peak = 0f;
                for (int f = from; f < to; f++)
                    for (int c = 0; c < _channels; c++)
                    {
                        float a = Mathf.Abs(_samples[f * _channels + c]);
                        if (a > peak) peak = a;
                    }

                int half = Mathf.Max(1, (int)(peak * (mid - 2)));
                for (int y = mid - half; y <= mid + half; y++)
                    pixels[y * WaveformTexWidth + x] = fg;
            }

            _waveformTex.SetPixels(pixels);
            _waveformTex.Apply(false);
        }

        private void DrawWaveform()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 130f, GUILayout.ExpandWidth(true));
            if (_waveformTex != null)
                GUI.DrawTexture(rect, _waveformTex, ScaleMode.StretchToFill);

            float startX = rect.x + (float)_startFrame / _totalFrames * rect.width;
            float endX = rect.x + (float)_endFrame / _totalFrames * rect.width;

            // Dim the cut-away regions
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, startX - rect.x, rect.height), new Color(0f, 0f, 0f, 0.55f));
            EditorGUI.DrawRect(new Rect(endX, rect.y, rect.xMax - endX, rect.height), new Color(0f, 0f, 0f, 0.55f));

            // Trim markers
            EditorGUI.DrawRect(new Rect(startX - 1f, rect.y, 2f, rect.height), Color.yellow);
            EditorGUI.DrawRect(new Rect(endX - 1f, rect.y, 2f, rect.height), Color.yellow);

            HandleWaveformMouse(rect);
        }

        private void HandleWaveformMouse(Rect rect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
            {
                float startX = rect.x + (float)_startFrame / _totalFrames * rect.width;
                float endX = rect.x + (float)_endFrame / _totalFrames * rect.width;
                _dragMarker = Mathf.Abs(evt.mousePosition.x - startX) <= Mathf.Abs(evt.mousePosition.x - endX) ? 0 : 1;
                MoveMarkerTo(evt.mousePosition.x, rect);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && _dragMarker >= 0)
            {
                MoveMarkerTo(evt.mousePosition.x, rect);
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                _dragMarker = -1;
            }
        }

        private void MoveMarkerTo(float mouseX, Rect rect)
        {
            int frame = Mathf.Clamp((int)((mouseX - rect.x) / rect.width * _totalFrames), 0, _totalFrames);
            if (_dragMarker == 0)
                _startFrame = Mathf.Min(frame, _endFrame - 1);
            else
                _endFrame = Mathf.Max(frame, _startFrame + 1);
            Repaint();
        }

        private void DrawTrimFields()
        {
            EditorGUILayout.BeginHorizontal();
            float startSec = EditorGUILayout.FloatField("Start (sec)", (float)_startFrame / _sampleRate);
            float endSec = EditorGUILayout.FloatField("End (sec)", (float)_endFrame / _sampleRate);
            EditorGUILayout.EndHorizontal();

            _startFrame = Mathf.Clamp((int)(startSec * _sampleRate), 0, _totalFrames - 1);
            _endFrame = Mathf.Clamp((int)(endSec * _sampleRate), _startFrame + 1, _totalFrames);

            float origLen = (float)_totalFrames / _sampleRate;
            float newLen = (float)(_endFrame - _startFrame) / _sampleRate;
            EditorGUILayout.LabelField($"Original: {origLen:F3}s    Trimmed: {newLen:F3}s    Removed: {origLen - newLen:F3}s", EditorStyles.miniLabel);
        }

        private void AutoDetect()
        {
            bool found = SilenceTrimmer.FindLoudRange(
                _samples, _channels, _sampleRate, _thresholdDb, _paddingMs,
                out _startFrame, out _endFrame);
            if (!found)
                EditorUtility.DisplayDialog("Silence Trimmer",
                    "The whole clip is below the threshold - nothing to keep. Try a lower threshold.", "OK");
            Repaint();
        }

        private void PlayPreview()
        {
            StopPreview();
            float[] trimmed = SilenceTrimmer.Extract(_samples, _channels, _sampleRate, _startFrame, _endFrame, _fadeMs);
            int frames = trimmed.Length / _channels;
            if (frames == 0)
                return;

            var previewClip = AudioClip.Create("SilenceTrimmerPreview", frames, _channels, _sampleRate, false);
            previewClip.SetData(trimmed, 0);

            if (_previewSource == null)
            {
                var go = new GameObject("SilenceTrimmerPreview") { hideFlags = HideFlags.HideAndDontSave };
                _previewSource = go.AddComponent<AudioSource>();
                _previewSource.spatialBlend = 0f;
            }
            _previewSource.clip = previewClip;
            _previewSource.Play();
        }

        private void StopPreview()
        {
            if (_previewSource != null && _previewSource.isPlaying)
                _previewSource.Stop();
        }

        private void ExportTrimmed()
        {
            float[] trimmed = SilenceTrimmer.Extract(_samples, _channels, _sampleRate, _startFrame, _endFrame, _fadeMs);

            string outputFolder = $"{OutputRoot}/{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            Directory.CreateDirectory(outputFolder);

            byte[] ogg = OggFileWriter.Encode(trimmed, _channels, _sampleRate, _oggQuality);
            string outPath = $"{outputFolder}/{_clip.name}_trimmed.ogg";
            File.WriteAllBytes(outPath, ogg);
            AssetDatabase.Refresh();

            float removed = (float)(_totalFrames - (_endFrame - _startFrame)) / _sampleRate;
            _lastResult = $"Saved {_clip.name}_trimmed.ogg (removed {removed:F3}s)";
            _lastOutputFolder = outputFolder;
            Debug.Log($"[SilenceTrimmer] {outPath} - removed {removed:F3}s of silence");
        }
    }
}
