# Sound Balance

A Unity Editor tool that balances the loudness of multiple AudioClips to match a
reference sound and exports the results as OGG files.

Loudness is measured in **LUFS (ITU-R BS.1770)** — the broadcast standard for
perceived loudness — so all balanced sounds *sound* equally loud, not just have
equal peak levels.

## Usage

1. Open **Tools → Sound Balance**.
2. Drag a reference AudioClip into the **Reference Sound** field.
3. Drag the AudioClips (or whole folders) you want to balance into the drop area.
4. Click **Balance**.

The balanced OGG files are written to a new timestamped folder:

```
Assets/BalancedSounds/2026-07-21_14-30-05/
```

Existing files are never modified or deleted — every run creates a new folder.

## Features

- LUFS loudness matching (same algorithm as pyloudnorm / broadcast meters)
- Drag & drop single clips or entire folders
- Supports any format Unity imports (wav, ogg, mp3, aiff, flac...)
- OGG Vorbis export with adjustable quality
- Automatic peak limiting to prevent clipping (reported when applied)
- Original assets and their import settings are left untouched

## Installation

Via Package Manager → **Add package from git URL**:

```
https://github.com/eliranshaya/Unity-SoundBalance.git?path=Packages/com.eliranshaya.soundbalance
```

## Credits

OGG encoding uses [OggVorbisEncoder](https://github.com/SteveLillis/.NET-Ogg-Vorbis-Encoder) (MIT).

## License

MIT
