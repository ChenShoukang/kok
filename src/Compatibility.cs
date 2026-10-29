namespace kok {
    // Import classification and the in-app compatibility list share this registry.
    public class FormatGroup {
        public string kind,mode,extensions,description;
        public FormatGroup(string kind,string mode,string extensions,string description) {
            this.kind=kind;this.mode=mode;this.extensions=extensions;this.description=description;
        }
    }
    public static class Compatibility {
        public static readonly FormatGroup[] Groups = {
            new FormatGroup("video","codec","mp4 m4v webm mov mkv","推荐 H.264 / AAC 的 MP4，或 VP8 / VP9 / Opus 的 WebM。MKV 仅限内核可解码的轨道，不提供多音轨切换。容器后缀不能保证编码可播放；HEVC、AV1 等取决于当前内核、系统与硬件。"),
            new FormatGroup("video","external","avi wmv mpg mpeg ts m2ts flv 3gp","可以收录和分类，建议使用系统播放器；不承诺内置解码，不包含转码器。"),
            new FormatGroup("photo","builtin","jpg jpeg png gif webp bmp avif","内置预览与简单编辑。动图编辑只保留当前静态画面；编辑长边最多 4096 px，另存 PNG / JPEG。WebP / AVIF 缩略图可能显示格式图标。"),
            new FormatGroup("photo","external","tif tiff heic heif","可以收录和分类，使用系统图片应用打开；不承诺内置预览或编辑。"),
            new FormatGroup("music","codec","mp3 wav ogg oga flac m4a aac opus","MP3、PCM WAV、FLAC、Vorbis / Opus 和 AAC 等取决于实际编码与 WebView2；WAV 等容器也可能包含不支持的编码。"),
            new FormatGroup("music","external","wma aiff ape","可以收录到专辑，建议使用系统播放器。"),
            new FormatGroup("document","builtin","pdf","内置 PDF 预览、文字层、标记与位置插入；扫描件不提供 OCR。批注保存在 kok，不能写回原 PDF。"),
            new FormatGroup("document","builtin","pptx","内置幻灯片预览、备注与基础编辑，可另存 PPTX；复杂母版、图表、动画和嵌入对象不能完整还原。"),
            new FormatGroup("document","builtin","docx","提取正文阅读、插入与批注，不保留 Office 原始排版、图片、表格布局或宏。"),
            new FormatGroup("document","builtin","csv md markdown log json html htm","按纯文本阅读，Markdown / HTML 不渲染排版，CSV 不显示电子表格。支持 UTF-8、UTF-16 和 GB18030。"),
            new FormatGroup("document","external","doc rtf xlsx xls ppt","可以收录和分类，使用已安装的 Office、WPS 或其他系统应用打开。"),
            new FormatGroup("book","builtin","epub txt","无 DRM 的 EPUB 正文与 TXT 分节阅读；EPUB 不还原图片、复杂排版与脚注交互。TXT 支持 UTF-8、UTF-16 和 GB18030。"),
            new FormatGroup("book","external","mobi azw azw3 fb2","可以收录和分类，使用系统阅读器；不解密 DRM。")
        };
    }
}
