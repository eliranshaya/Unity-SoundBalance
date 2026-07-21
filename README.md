# Unity-SoundBalance

A Unity Editor tool that balances the loudness of multiple AudioClips to match a
reference sound and exports the results as OGG files.

Loudness is measured in **LUFS (ITU-R BS.1770)** — the broadcast standard for
perceived loudness — so all balanced sounds *sound* equally loud.

![Tools Menu](https://img.shields.io/badge/Unity-2021.3%2B-blue) ![License](https://img.shields.io/badge/license-MIT-green)

## Installation

Unity Package Manager → **Add package from git URL**:

```
https://github.com/eliranshaya/Unity-SoundBalance.git?path=Packages/com.eliranshaya.soundbalance
```

## Usage

1. Open **Tools → Sound Balance**.
2. Drag a reference AudioClip into the **Reference Sound** field.
3. Drag the AudioClips (or whole folders) you want to balance into the drop area.
4. Click **Balance**.

The balanced OGG files are written to a new timestamped folder, so previous runs
are never overwritten:

```
Assets/BalancedSounds/2026-07-21_14-30-05/
```

## Features

- LUFS loudness matching (same algorithm as pyloudnorm / broadcast meters)
- Drag & drop single clips or entire folders
- Supports any format Unity imports (wav, ogg, mp3, aiff, flac...)
- OGG Vorbis export with adjustable quality
- Automatic peak limiting to prevent clipping (reported when applied)
- Original assets and their import settings are left untouched

## Repository layout

This repository is a full Unity project used for development. The package itself
lives in [`Packages/com.eliranshaya.soundbalance`](Packages/com.eliranshaya.soundbalance).

## Credits

OGG encoding uses [OggVorbisEncoder](https://github.com/SteveLillis/.NET-Ogg-Vorbis-Encoder) (MIT).

## License

MIT
