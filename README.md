# Unity-SoundBalance

A Unity Editor tool that balances the loudness of multiple AudioClips to match a
**reference sound**, and exports the results as **OGG** files.

Loudness is measured in **LUFS (ITU-R BS.1770)** — the broadcast standard for
*perceived* loudness — so all balanced sounds actually *sound* equally loud,
instead of just having equal peak levels. Perfect for keeping game SFX
consistent: no more effects that blast your ears or get lost in the mix.

## Requirements

- Unity **2021.3** or newer

## Installation

Install via Unity's Package Manager using the Git URL.

1. Open your project in Unity.
2. Go to **Window → Package Manager**.
3. Click the **+** button in the top-left and choose **Add package from git URL…**
4. Paste the URL below and click **Add**:

```
https://github.com/eliranshaya/Unity-SoundBalance.git?path=/Packages/com.eliranshaya.soundbalance
```

To lock to a specific version, append the tag:

```
https://github.com/eliranshaya/Unity-SoundBalance.git?path=/Packages/com.eliranshaya.soundbalance#1.0.3
```

You can also add it manually by editing your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.eliranshaya.soundbalance": "https://github.com/eliranshaya/Unity-SoundBalance.git?path=/Packages/com.eliranshaya.soundbalance"
  }
}
```

## Quick start

1. Open **Tools → Sound Balance**.
2. Drag a reference AudioClip into the **Reference Sound** field — this is the
   sound whose loudness all other sounds will match.
3. Drag the AudioClips you want to balance into the drop area. You can drag
   single clips or **entire folders** (all clips inside are added recursively).
4. Optionally adjust the **OGG Quality** slider (0.5 ≈ 160 kbps stereo).
5. Click **Balance**.

The balanced files are written to a new timestamped folder, so previous runs are
never overwritten:

```
Assets/BalancedSounds/2026-07-22_14-30-05/
    Explosion_balanced_by_Generic1.ogg
    Pickup_balanced_by_Generic1.ogg
```

Each file is named `{clip}_balanced_by_{reference}.ogg`, and the window shows a
summary of the measured loudness and applied gain for every clip. Click
**Show Output Folder** to jump to the results in the Project window.

## How it works

- Every clip's integrated loudness is measured per **ITU-R BS.1770-4**
  (K-weighting + gating), the same algorithm used by broadcast loudness meters
  and pyloudnorm.
- Each clip is then gained so its loudness matches the reference clip exactly.
- If the gain would cause digital clipping, a peak limiter caps the output
  (and the summary tells you it happened).
- Source assets and their import settings are **never modified** — compressed
  clips are temporarily reimported as PCM for a lossless read, then restored.

Supports any audio format Unity can import: wav, ogg, mp3, aiff, flac, and more.

## Credits

OGG encoding uses [OggVorbisEncoder](https://github.com/SteveLillis/.NET-Ogg-Vorbis-Encoder) (MIT),
embedded as source (no DLL dependencies) so it works on every Unity version out of the box.

## License

MIT — see [LICENSE](Packages/com.eliranshaya.soundbalance/LICENSE.md).
