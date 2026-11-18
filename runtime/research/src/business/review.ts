export interface Finding { code: string; severity: string; message: string; page: number; shapeId: string }
export interface Shape { id: string; kind: string; text: string; x: number; y: number; w: number; h: number; fontSize: number; rotation: number }
export interface SectionCheck { title: string; status: 'present' | 'missing' | 'empty'; line: number; bodyCharacters: number }
export interface ReviewInput {
  id: string; kind: 'document' | 'presentation'; fileVersion: string; text?: string; recovery?: boolean; sections?: string[];
  format?: string; measurements?: { hanzi: number; characters: number; words: number };
  requirement?: { minChars: number; maxChars: number; lengthUnit: number; requiredFormat: string; purpose?: string; audience?: string } | null;
  deck?: { width: number; height: number; unsupported: number; slides: { shapes: Shape[] }[] };
}
export function reviewArtifact(input: ReviewInput) {
  const report = { version: 1, id: input.id, kind: input.kind, fileVersion: input.fileVersion,
    method: input.kind === 'document' ? 'saved-document-source' : 'saved-pptx-ooxml',
    status: '', scope: '', rendered: false, visualReviewed: false, truncated: false,
    errors: 0, warnings: 0, pages: 0, characters: 0, words: 0, measurements: input.measurements || null,
    contentRequirements: { purpose: input.requirement?.purpose || '', audience: input.requirement?.audience || '', assessment: 'not_assessed' },
    deliveryCheck: null as null | { unit: string; value: number; min: number; max: number; withinBounds: boolean; formatMatches: boolean }, headings: [] as string[], sectionChecks: [] as SectionCheck[], findings: [] as Finding[] };
  const add = (code: string, message: string, severity = 'warning', page = 0, shapeId = '') => {
    severity === 'error' ? report.errors++ : report.warnings++;
    const finding = { code, severity, message, page, shapeId };
    if (report.findings.length < 100) report.findings.push(finding);
    else {
      report.truncated = true;
      // A long warning list must not hide a later definite error from native delivery acceptance.
      if (severity === 'error') { const warning = report.findings.findIndex(row => row.severity !== 'error'); if (warning >= 0) report.findings[warning] = finding; }
    }
  };
  if (input.kind === 'document') {
    const text = input.text || ''; report.characters = text.length;
    if (input.requirement && input.measurements) {
      const r = input.requirement, unit = r.lengthUnit === 3 ? 'words' : r.lengthUnit === 2 ? 'characters' : 'hanzi', value = input.measurements[unit];
      report.deliveryCheck = { unit, value, min: r.minChars, max: r.maxChars, withinBounds: (!r.minChars || value >= r.minChars) && (!r.maxChars || value <= r.maxChars), formatMatches: !r.requiredFormat || r.requiredFormat.toUpperCase() === input.format?.toUpperCase() };
    }
    report.words = (text.match(/[\p{L}\p{N}]+/gu) || []).length;
    report.scope = '结构与文本检查，不验证事实正确性或 DOCX 分页外观。';
    const sections = [...new Set(input.sections || [])], key = (value: string) => value.trim().replace(/[：:]$/, '').replace(/\s+/g, ' ').toLowerCase();
    const lines = text.replaceAll('\r\n', '\n').replaceAll('\r', '\n').split('\n');
    const headings: { title: string; line: number; level: number }[] = []; let fence = '', fenceLength = 0;
    lines.forEach((raw, index) => {
      const line = raw.trim(), fenced = line.match(/^(`{3,}|~{3,})/);
      if (fenced) { if (!fence) { fence = fenced[1][0]; fenceLength = fenced[1].length; } else if (fenced[1][0] === fence && fenced[1].length >= fenceLength) fence = ''; return; }
      if (fence) return;
      const markdown = line.match(/^(#{1,6})\s+(.+?)(?:\s+#+)?$/);
      const numbered = line.match(/^(?:第[一二三四五六七八九十百\d]+[章节]\s*|[一二三四五六七八九十百]+[、.．]\s*|[（(][一二三四五六七八九十百\d]+[）)]\s*|\d+(?:\.\d+)*[.．、]\s+)(.+)$/);
      if (markdown) headings.push({ title: markdown[2].trim(), line: index, level: markdown[1].length });
      else if (numbered) headings.push({ title: numbered[1].trim(), line: index, level: 2 });
      // DOCX/TXT often have plain headings. A standalone exact required title is still required.
      else if (sections.some(section => key(section) === key(line))) headings.push({ title: line, line: index, level: 2 });
    });
    report.headings = headings.slice(0, 40).map(heading => lines[heading.line].trim());
    report.sectionChecks = sections.map(title => {
      const matches = headings.filter(heading => key(heading.title) === key(title));
      let bodyCharacters = 0, line = 0;
      for (const heading of matches) {
        const end = headings.find(next => next.line > heading.line && next.level <= heading.level)?.line ?? lines.length;
        // Headings/markup alone do not supply a section body. Nested headings may contain real content.
        const headingLines = new Set(headings.filter(next => next.line > heading.line && next.line < end).map(next => next.line));
        const count = lines.slice(heading.line + 1, end).filter((_, offset) => !headingLines.has(heading.line + 1 + offset)).join('\n').replace(/[\s#*_`~\-]/g, '').length;
        if (!line || count > bodyCharacters) { line = heading.line + 1; bodyCharacters = count; }
      }
      return { title, status: !matches.length ? 'missing' : bodyCharacters ? 'present' : 'empty', line, bodyCharacters };
    });
    if (input.recovery) add('unsaved_recovery', '文档含有尚未写入成品文件的恢复内容。', 'error');
    if (!text.trim()) add('empty_document', '文档正文为空；若不是空白模板，请补充内容。');
    if (text.includes('\uFFFD')) add('replacement_character', '正文含有字符替换符，请检查编码或复制内容。');
    if ((text.match(/^\s*```+/gm) || []).length % 2) add('unclosed_code_fence', 'Markdown 代码围栏可能没有闭合。');
    for (const section of report.sectionChecks) if (section.status !== 'present') add(section.status === 'missing' ? 'missing_section' : 'empty_section', (section.status === 'missing' ? '未检出独立章节：' : '章节缺少正文：') + '“' + section.title + '”。', 'error');
  } else {
    const deck = input.deck; if (!deck) throw new Error('Missing actual presentation snapshot');
    report.pages = deck.slides.length; report.scope = '实际 PPTX 的文字与几何检查；文字容量为估算，不等同于 Office 渲染或视觉审核。';
    if (deck.unsupported) add('unsupported_objects', '存在图表、组合、动画或其他未完整解析的对象，需使用目标办公软件进一步检查。');
    deck.slides.forEach((slide, index) => {
      const page = index + 1, textShapes = slide.shapes.filter(shape => shape.text?.trim());
      if (!textShapes.length && !slide.shapes.some(shape => shape.kind === 'image') && !deck.unsupported) add('empty_slide', '该页没有可见文字或图片，请确认是否有意留白。', 'warning', page);
      for (const shape of slide.shapes) {
        report.characters += (shape.text || '').length;
        const outside = shape.x >= deck.width || shape.y >= deck.height || shape.x + shape.w <= 0 || shape.y + shape.h <= 0;
        const clipped = shape.x < -.02 || shape.y < -.02 || shape.x + shape.w > deck.width + .02 || shape.y + shape.h > deck.height + .02;
        if (outside) add('outside_canvas', '对象完全位于页面外。', deck.unsupported ? 'warning' : 'error', page, shape.id);
        else if (clipped) add('clipped_object', '对象超出页面边界，可能被裁切。', 'warning', page, shape.id);
        if (!shape.text?.trim()) continue;
        if (shape.text.length > 40 && shape.fontSize < 16) add('small_body_text', '较长正文的字号小于 16pt，请结合用途检查可读性。', 'warning', page, shape.id);
        const columns = Math.max(1, shape.w * 72 / Math.max(1, shape.fontSize));
        const lines = shape.text.split('\n').reduce((sum, line) => sum + Math.max(1, Math.ceil([...line].reduce((n, ch) => n + (ch.codePointAt(0)! > 255 ? 1 : .55), 0) / columns)), 0);
        if (lines * shape.fontSize * 1.12 > shape.h * 72 + shape.fontSize * .5) add('estimated_text_overflow', '按文字、字号与文本框估算可能溢出；请渲染后确认，必要时缩短正文或调整文本框。', 'warning', page, shape.id);
      }
      const compared = textShapes.slice(0, 200);
      for (let a = 0; a < compared.length; a++) for (let b = a + 1; b < compared.length; b++) {
        const left = compared[a], right = compared[b]; if (Math.abs(left.rotation) > .1 || Math.abs(right.rotation) > .1) continue;
        const w = Math.min(left.x + left.w, right.x + right.w) - Math.max(left.x, right.x), h = Math.min(left.y + left.h, right.y + right.h) - Math.max(left.y, right.y);
        if (w > .02 && h > .02 && w * h > Math.min(left.w * left.h, right.w * right.h) * .18) add('text_overlap', '两个文本框明显相交，请检查遮挡；另一对象：' + right.id + '。', 'warning', page, left.id);
      }
      if (textShapes.length > 200) add('overlap_check_limited', '本页文字对象较多，仅对前 200 个进行两两相交检查。', 'warning', page);
    });
  }
  report.status = report.errors ? 'issues_found' : report.warnings ? 'warnings' : 'checked'; return report;
}

export function reviewSources(input: { sources: { id: string; name: string; removed: boolean; parsed: boolean; complete: boolean; valid: number; stale: number; detail: string }[] }) {
  return input.sources.map(source => ({ id: source.id, name: source.name,
    status: source.removed ? 'unavailable' : source.complete ? 'complete' : source.valid ? 'partial' : source.stale ? 'stale' : 'unread',
    parsed: source.parsed, processed: source.valid > 0, fullyRead: source.complete, recordedRanges: source.valid,
    staleRanges: source.stale, detail: source.detail.slice(0, 500), citationStatus: 'not_semantically_verified' }));
}
