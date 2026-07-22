using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace SoundBalance.EditorTools
{
    /// <summary>
    /// Reads raw PCM samples from an AudioClip asset. If the clip's import settings
    /// prevent reading lossless data (compressed/streaming), they are temporarily
    /// switched to PCM/DecompressOnLoad and restored afterwards.
    /// </summary>
    public static class AudioClipSampleReader
    {
        public static float[] Read(AudioClip clip, out int channels, out int sampleRate)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            bool settingsChanged = false;
            AudioImporterSampleSettings originalSettings = default;
            bool originalLoadInBackground = false;

            if (importer != null)
            {
                originalSettings = importer.defaultSampleSettings;
                originalLoadInBackground = importer.loadInBackground;
                bool needsReimport = originalSettings.loadType != AudioClipLoadType.DecompressOnLoad
                                     || originalSettings.compressionFormat != AudioCompressionFormat.PCM
                                     || originalSettings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate
                                     || originalLoadInBackground;
                if (needsReimport)
                {
                    AudioImporterSampleSettings temp = originalSettings;
                    temp.loadType = AudioClipLoadType.DecompressOnLoad;
                    temp.compressionFormat = AudioCompressionFormat.PCM;
                    temp.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.defaultSampleSettings = temp;
                    importer.loadInBackground = false;
                    importer.SaveAndReimport();
                    settingsChanged = true;
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }

            try
            {
                channels = clip.channels;
                sampleRate = clip.frequency;

                clip.LoadAudioData();
                var timeout = DateTime.UtcNow.AddSeconds(15);
                while (clip.loadState == AudioDataLoadState.Loading && DateTime.UtcNow < timeout)
                    Thread.Sleep(10);

                var samples = new float[(long)clip.samples * clip.channels];
                if (!clip.GetData(samples, 0))
                    throw new InvalidOperationException("AudioClip.GetData failed - could not read sample data.");
                return samples;
            }
            finally
            {
                if (settingsChanged && importer != null)
                {
                    importer.defaultSampleSettings = originalSettings;
                    importer.loadInBackground = originalLoadInBackground;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
