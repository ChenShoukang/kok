"""Generate original, local-only demo assets using the Python standard library."""
from pathlib import Path
import math
import struct
import zlib
import wave
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'samples'
OUT.mkdir(exist_ok=True)

def png(width, height, pixel):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            rows.extend(pixel(x, y))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows, 8)) + chunk(b'IEND', b'')

for variant, filename in enumerate(['山野来信.png', '落日与远山.png']):
    def landscape(x, y, v=variant):
        nx, ny = x / 960, y / 600
        a, b = ((220, 231, 213), (243, 237, 207)) if v == 0 else ((224, 212, 192), (247, 226, 186))
        color = tuple(int(a[i] * (1-ny) + b[i] * ny) for i in range(3))
        if (nx - (.73 if v == 0 else .33)) ** 2 + ((ny - .28) * .625) ** 2 < .005:
            color = (248, 241, 210) if v == 0 else (249, 236, 197)
        for n, level, rgb in [(1, .54, (159, 181, 150)), (2, .67, (119, 151, 127)), (3, .84, (74, 114, 96))]:
            horizon = level - .09 * math.sin(nx * 7 + n * 2.2 + v * 3) - .04 * math.sin(nx * 13 + n)
            if ny > horizon:
                color = rgb if v == 0 else tuple(min(255, c + a) for c, a in zip(rgb, (20, 0, -9)))
        return color
    (OUT / filename).write_bytes(png(960, 600, landscape))

for idx, name in enumerate(['林间小调.wav', '午后留白.wav']):
    rate, seconds = 22050, 12
    samples = bytearray()
    notes = [261.63, 329.63, 392.0, 523.25] if idx == 0 else [220.0, 261.63, 329.63, 440.0]
    for n in range(rate * seconds):
        t = n / rate
        envelope = min(1, t / 1.2, (seconds-t)/2)
        signal = sum(math.sin(2*math.pi*f*t) * (.6 + .4*math.sin(t*.4 + k)) for k, f in enumerate(notes)) / 4
        samples.extend(struct.pack('<h', int(signal * envelope * 4500)))
    with wave.open(str(OUT / name), 'wb') as audio:
        audio.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        audio.writeframes(samples)

(OUT/'欢迎来到 kok.txt').write_text('欢迎来到 kok\n\n让喜欢的一切，都有一个好去处。\n\nkok 是一个轻量的 Windows 本地资料库。你可以把视频、照片、音乐、文档和电子书放在同一个空间，慢慢整理，随时继续。\n\n开始使用\n\n点击右上角「导入文件」，或打开旁边的菜单选择「导入文件夹」。原文件保持在原来的位置，不会被复制或上传。\n\n属于你的使用方式\n\n点击爱心收藏文件，在搜索框里查找文件名或笔记。阅读时调整字号与纸色，点击右上角的笔记按钮记录想法。播放进度与阅读位置会保存在本机。\n\n保持轻盈\n\n特殊格式可使用系统应用打开。移除收藏只删除索引，原文件会继续保留。资料库及笔记可以在设置里导出备份。\n\n这是 kok 自带的原创示例文本。', encoding='utf-8')
(OUT/'给日常一点留白.md').write_text('# 给日常一点留白\n\n一段音乐，一本书，一张留下来的照片。\n\n## 本周的小计划\n\n- 整理一个旅行文件夹\n- 读完一本喜欢的书\n- 给照片写一点当时的心情\n\n这是一份 Markdown 示例。kok 以纯文本方式阅读 Markdown，完整保留源内容。', encoding='utf-8')

with zipfile.ZipFile(OUT/'整理生活的小清单.docx', 'w', zipfile.ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
    z.writestr('_rels/.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
    paragraphs = ['整理生活的小清单', '让每一种喜欢，都有自己的位置。', '照片：把一次旅行整理成一个文件夹。', '音乐：留一首适合午后听的曲子。', '文档：给灵感取一个容易找到的名字。', '读书：把喜欢的句子，写进随手记。', 'kok 的 DOCX 预览提取正文，复杂版式请在 Office 或 WPS 中查看。']
    z.writestr('word/document.xml','<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>'+''.join('<w:p><w:r><w:t>'+p+'</w:t></w:r></w:p>' for p in paragraphs)+'</w:body></w:document>')

with zipfile.ZipFile(OUT/'慢慢收藏生活.epub', 'w') as z:
    z.writestr('mimetype', 'application/epub+zip', compress_type=zipfile.ZIP_STORED)
    z.writestr('META-INF/container.xml','<container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0"><rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles></container>')
    z.writestr('OEBPS/content.opf','<package xmlns="http://www.idpf.org/2007/opf" unique-identifier="id" version="3.0"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">urn:kok:sample</dc:identifier><dc:title>慢慢收藏生活</dc:title><dc:language>zh-CN</dc:language><dc:creator>kok</dc:creator><meta property="dcterms:modified">2026-10-22T00:00:00Z</meta></metadata><manifest><item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>'+''.join(f'<item id="c{i}" href="c{i}.xhtml" media-type="application/xhtml+xml"/>' for i in range(1,4))+'</manifest><spine>'+''.join(f'<itemref idref="c{i}"/>' for i in range(1,4))+'</spine></package>')
    chapters=[('第一章 · 给喜欢的事物一个位置','生活里有很多值得留下的东西。也许是一张在路上拍下的照片，也许是一首反复听了很久的歌。\n\n整理不必是一个很大的工程。从一个文件夹开始，从一个容易找到的名字开始，让喜欢的事物慢慢有了自己的位置。'),('第二章 · 在阅读里慢下来','翻开一本书，就像走进一个安静的房间。窗外依然热闹，字里行间却留着自己的节奏。\n\n遇到喜欢的句子，可以写一点笔记。不是为了记住所有，而是为了留下此刻的自己。'),('第三章 · 留白也是一种收藏','收藏不只是越来越多。偶尔也可以停下来，重新听一首歌，重新看一张照片。\n\n给日常一点留白，给喜欢的事物一点时间。那些真正重要的东西，会在慢下来的时候重新发光。')]
    for i,(title,text) in enumerate(chapters,1):
        z.writestr(f'OEBPS/c{i}.xhtml','<html xmlns="http://www.w3.org/1999/xhtml"><head><title>'+title+'</title></head><body><h1>'+title+'</h1>'+''.join('<p>'+p+'</p>' for p in text.split('\n\n'))+'</body></html>')
    z.writestr('OEBPS/nav.xhtml','<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>目录</title></head><body><nav epub:type="toc"><ol>'+''.join(f'<li><a href="c{i}.xhtml">{t}</a></li>' for i,(t,_) in enumerate(chapters,1))+'</ol></nav></body></html>')

def pdf():
    lines=['KOK / A LITTLE SPACE FOR WHAT YOU LOVE','Your files. Your collection. Your own pace.','Videos, photographs, music, documents, and books.','Everything stays on this computer.','Use the native PDF toolbar to zoom, search and print.','This original sample document was created for kok.']
    content='BT /F1 18 Tf 60 750 Td '+f'({lines[0]}) Tj /F1 12 Tf '+''.join(f'0 -50 Td ({line}) Tj ' for line in lines[1:])+'ET'
    objects=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>','<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',f'<< /Length {len(content)} >>\nstream\n{content}\nendstream']
    result=bytearray(b'%PDF-1.4\n');offsets=[0]
    for i,obj in enumerate(objects,1):
        offsets.append(len(result));result.extend(f'{i} 0 obj\n{obj}\nendobj\n'.encode())
    xref=len(result);result.extend(f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode())
    for offset in offsets[1:]:result.extend(f'{offset:010} 00000 n \n'.encode())
    result.extend(f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode())
    return result
(OUT/'kok 使用指南.pdf').write_bytes(pdf())
print('Generated samples:', len(list(OUT.iterdir())))
(OUT/'一段安静的时光.zh.srt').write_text('1\n00:00:00,100 --> 00:00:01,400\n给日常一点留白。\n\n2\n00:00:01,400 --> 00:00:03,000\n留一个慢下来的角落。\n',encoding='utf-8')
