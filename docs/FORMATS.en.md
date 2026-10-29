# Format compatibility · kok v0.7.1

[中文](FORMATS.md) · English

Open **Settings → Supported formats** for the same registry in the app. All extensions below can be imported and classified. “External app recommended” requires separately installed software; it does not promise built-in playback.

| Category | Extensions | Scope | Details |
| --- | --- | --- | --- |
| Video | MP4 / M4V / WEBM / MOV / MKV | Codec dependent | Use MP4 with H.264 / AAC, or WebM with VP8 / VP9 / Opus. MKV requires decodable tracks; multiple audio track selection is not provided. A file extension does not guarantee playback; HEVC, AV1 and other codecs depend on the runtime, system and hardware. |
| Video | AVI / WMV / MPG / MPEG / TS / M2TS / FLV / 3GP | External app recommended | Can be imported and organized. Use a system player; built-in decoding is not guaranteed and no transcoder is bundled. |
| Photos | JPG / JPEG / PNG / GIF / WEBP / BMP / AVIF | Built-in viewer | Built-in preview and simple editing. Editing animated images saves a still frame. Edits are limited to 4096 px on the longer side and saved as PNG / JPEG. WebP / AVIF thumbnails may show a format icon. |
| Photos | TIF / TIFF / HEIC / HEIF | External app recommended | Can be imported and organized. Open with a system photo app; built-in preview and editing are not guaranteed. |
| Music | MP3 / WAV / OGG / OGA / FLAC / M4A / AAC / OPUS | Codec dependent | MP3, PCM WAV, FLAC, Vorbis / Opus and AAC support depends on the actual encoding and WebView2. Containers such as WAV may contain unsupported codecs. |
| Music | WMA / AIFF / APE | External app recommended | Can be organized into albums. A system player is recommended. |
| Documents | PDF | Built-in viewer | Built-in PDF preview, text layer, annotations and positioned text. No OCR for scans. Annotations are saved in kok, not written into the original PDF. |
| Documents | PPTX | Built-in viewer | Built-in slide preview, notes and basic editing with PPTX copies. Complex masters, charts, animations and embedded objects are not fully reproduced. |
| Documents | DOCX | Built-in viewer | Extracts body text for reading, insertion and annotation. Original Office layout, images, table layout and macros are not preserved. |
| Documents | CSV / MD / MARKDOWN / LOG / JSON / HTML / HTM | Built-in viewer | Read as plain text. Markdown / HTML formatting is not rendered, and CSV is not shown as a spreadsheet. Supports UTF-8, UTF-16 and GB18030. |
| Documents | DOC / RTF / XLSX / XLS / PPT | External app recommended | Can be imported and organized. Open with an installed copy of Office, WPS or another system app. |
| Books | EPUB / TXT | Built-in viewer | Reads DRM-free EPUB body text and TXT sections. EPUB images, complex layout and interactive footnotes are not reproduced. TXT supports UTF-8, UTF-16 and GB18030. |
| Books | MOBI / AZW / AZW3 / FB2 | External app recommended | Can be imported and organized. Use a system reader; DRM is not decrypted. |

Media support depends on the container **and its codecs**, runtime and system. H.264/AAC MP4 is a practical default; HEVC is conditional and installing a system extension does not guarantee every file. See [Microsoft’s codec guidance](https://learn.microsoft.com/en-us/troubleshoot/microsoft-edge/development/video-playback-issues). [MDN’s image guide](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types) describes browser image support; TIFF/HEIC are not promised by kok.

## Subtitles, fonts and exports

- External text subtitles: **SRT, VTT, ASS, SSA, SUB (MicroDVD only), SBV, LRC, TTML, DFXP, SMI (SAMI)**. Same-name and language-suffix discovery is supported. ASS/SSA render text without effects; TTML/DFXP use basic clock/second/millisecond timing, without full styling or frame/tick timing. No embedded/bitmap subtitle extraction. Optional local Whisper or a separate speech API can generate SRT captions.
- Subtitle limit: 8 MB, 50,000 cues per file; auto-discovery loads up to 20 tracks.
- Fonts: TTF / OTF up to 20 MB each. Photo copies: PNG / JPEG. Reading-copy and PDF annotation exports: HTML. Translation copies: DOCX / EPUB / PDF / TXT / Markdown, plus SRT subtitles. PPTX exports rebuild visible basic elements; complex layouts may be lost. See [PPTX scope](PRESENTATION_AGENT_0.7.1.en.md).
- DOCX/EPUB readers extract text, not the original layout. Markdown/HTML remain plain text; content is not executed. Scanned PDFs need external OCR to select text. Text has a 24 MB limit; EPUB content has a 48 MB total limit and up to 2,000 chapters. No DRM decryption.

## Actual decoding results

Tested 2026-10-23 on Windows x64, WebView2 **155.0.4283.62**. These are original generated fixtures, not an exhaustive certification of all profiles, bit depths, DRM, color spaces or malformed files. The test reads real files through kok's native byte-range route and checks playback time / decoded image pixels, rather than relying only on canPlayType.

| Fixture / codec | Actual result |
| --- | --- |
| h264-aac.mp4 | Decoded |
| h264-aac.m4v | Decoded |
| h264-aac.mov | Decoded |
| vp8-opus.webm | Decoded |
| vp9-opus.webm | Decoded |
| h264-aac.mkv | Decoded |
| mpeg4-mp3.avi | Use an external app |
| wmv2-wma2.wmv | Use an external app |
| h264-aac.ts | Use an external app |
| mp3.mp3 | Decoded |
| pcm.wav | Decoded |
| flac.flac | Decoded |
| vorbis.ogg | Decoded |
| opus.opus | Decoded |
| aac.m4a | Decoded |
| adts.aac | Decoded |
| wma.wma | Use an external app |
| pcm.aiff | Use an external app |
| photo.jpg | Decoded |
| photo.png | Decoded |
| photo.gif | Decoded |
| photo.webp | Decoded |
| photo.bmp | Decoded |
| photo.avif | Decoded |
| photo.tiff | Use an external app |

19 fixtures decoded; 6 required an external app in this environment. HEIC/HEIF, APE, MPEG/FLV/3GP and other unlisted encodings were not fixture-tested and have no blanket compatibility guarantee. Native tests also verify all 10 subtitle parsers, PDF UI tests verify rendered text and annotations, and DOCX/EPUB/text tests verify extraction and persistence.

To reproduce: run a kok debug window, then `node tests/compatibility.cjs <path-to-ffmpeg.exe>`. FFmpeg is a test-only fixture generator and is not shipped or required to run kok. Run `node tools/write-format-docs.cjs` to update this matrix from the registry and test results.
