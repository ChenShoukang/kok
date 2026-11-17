import { applyRequirementChanges, verificationDirective, type RequirementUpdate } from './requirements.ts';
export interface Contract {
  RequiresDocument: boolean; RequiresPresentation: boolean; RequiredDocumentFormat: string;
  RequestedPages: number; MinLength: number; MaxLength: number; LengthUnit: number;
  RequiresTargetedEdit: boolean; EditVersionHint: string; RequiresFullSourceCoverage: boolean;
  RequiredSourceIds: string[]; RequiredSections: string[]; ChatOnly: boolean; RequiredCount: number; CodeVerification: boolean | null;
}
export interface PlanningInput {
  goal: string; adjustments: string[]; allowed: string[]; intent: string; approvalPolicy: string;
  requirementsVersion?: number; requirementUpdates?: RequirementUpdate[];
  scope: { mode: string; ids?: string[] }; explicitTools: string[] | null;
  features: { memory: boolean; delegation: boolean; sandbox: boolean; search: boolean; parent: boolean };
  environment: { workspace: boolean; rendering: boolean; tools: string[]; selectedFormats: string[] };
  context: { taskId: string; goal: string; domain: string; workspaceId: string; files: string[] };
}
export const actions = (text: string): string => text.replace(/(?:不要|请勿|无需|无须|不必|不得|禁止|不再|避免|不用|不需要|不创建|不新建|不新增|不生成|不制作|不输出|不导出|不保存|不做|不写|不改|不修改|不编辑|不渲染|别(?=[做创写用生保改加删动])|\bdo\s+not\b|\bdon['’]t\b|\bnever\b|\bwithout\b|\bno\s+need\s+to\b)[^。！？；;，,\.\r\n]*?(?=而是|但是|但要|instead|\bbut\b|\bhowever\b|[。！？；;，,\.\r\n]|$)/gi, clause => ' '.repeat(clause.length));
const pres = /PPTX?|PowerPoint|presentations?|slides?|\bdeck\b|幻灯|演示文稿|演示稿|课件|演示/i;
const doc = /报告|文档|文章|纪要|总结|摘要|笔记|方案|简报|讲稿|说明|手册|备忘|记录|清单|report|document|essay|summary|memo|brief|notes?|article|proposal/i;
const create = /生成|制作|创建|新建|起草|撰写|写|整理成|汇总成|输出|保存|另存|导出|做成|交付|convert|create|make|build|generate|save|export|write|draft|compose|produce|deliver/i;
const edit = /修改|改成|改为|改到|替换|修订|更正|编辑|改写|重写|润色|排版|扩写|精简|缩短|删除|插入|追加|添加|增加|补充|更新|调整|续写|replace|revise|edit|change|update|remove|delete|insert|append|rewrite|polish|adjust/i;
const metadata = /标签|分类|收藏|备注|重命名|文件名|移入|移出|归档|\b(?:notes?|tags?|collections?|favorites?)\b/i;
const body = /正文|内容|文字|段落|预算|人数|金额|数字|日期|开头|结尾|章节|句子|表格|第\s*\d+\s*(?:段|节|行|章)|body|paragraph|sentence|text/i;
const chat = /(?:聊天|对话|会话)(?:里|中|内)?[^。；;\n]{0,24}(?:回复|回答|说明|给出)|直接(?:在)?(?:聊天|对话|会话)|(?:仅|只)回复|直接回复|回复即可|(?:reply|answer|respond)\s+(?:only\s+)?in\s+(?:the\s+)?(?:chat|conversation)|chat[- ]only|just\s+(?:reply|answer)|reply\s+directly/i;
const noFile = /(?:不要|不用|不需要|无需|无须|不必|别|勿)(?:再)?(?:创建|新建|新增|生成|保存|输出|写|导出|做)(?:为|成|到)?(?:任何|一个|一份)?(?:新)?(?:的)?(?:文件|文档|报告|成果|成品)|(?:do\s+not|don['’]t|never|without|no\s+need\s+to)\s+(?:create|save|write|export|produce)\s+(?:a\s+|an\s+|any\s+)?(?:new\s+)?(?:file|document|report|artifact)|不需要(?:任何)?文件|不用文件|(?:no|without)\s+(?:new\s+)?(?:file|document|artifact)/i;
const full = /全文|全部|完整|每份|每一份|所有资料|所有来源|所有文档|所有文件|逐字|逐份|通读|读全|精读|\bfull[- ]?text\b|\bcomplete\b|\bentire\b|\bwhole\b/i;
const source = /资料|文档|来源|材料|素材|报告|sources?|documents?|files?|materials?|reports?/i;
export const coding = (text: string): boolean => /代码|程序|工作区|仓库|TypeScript|JavaScript|Python|C#|\b(?:coding|code|repository|workspace|bug)\b|\.(?:ts|js|py|cs)\b/i.test(text);
const sample = /只看开头|只看前面|只读开头|只读前\s*\d*\s*页|初步意见|初步了解|粗略|简单浏览|只浏览|只看摘要|只看结论|只要大意|选一份|其中一份|brief\s+(?:summary|overview)|pick\s+one/i;
const number = (value: string): number => {
  if (/^\d+$/.test(value)) return Number(value);
  const digits: Record<string, number> = { 零: 0, 一: 1, 二: 2, 两: 2, 三: 3, 四: 4, 五: 5, 六: 6, 七: 7, 八: 8, 九: 9 };
  const parts = value.split('十'); return parts.length === 2 ? (parts[0] ? digits[parts[0]] || 0 : 1) * 10 + (digits[parts[1]] || 0) : digits[value] || 0;
};
const empty = (allowed: string[]): Contract => ({ RequiresDocument: false, RequiresPresentation: false, RequiredDocumentFormat: '', RequestedPages: 0, MinLength: 0, MaxLength: 0, LengthUnit: 0, RequiresTargetedEdit: false, EditVersionHint: '', RequiresFullSourceCoverage: false, RequiredSourceIds: [...allowed], RequiredSections: [], ChatOnly: false, RequiredCount: 1, CodeVerification: null });

// Only explicit section lists become acceptance criteria. Ordinary lists of facts are not headings.
function sectionEvents(raw: string) {
  const events: { index: number; sections: string[] }[] = [];
  const pattern = /(?:(?:报告|文档)(?:应|需|须|必须|需要)?(?:包含|包括|分为)|(?:必需|必须|要求的|报告|文档)?(?:章节|栏目)(?:要求)?\s*(?:改为|改成|包括|包含|为|是|[:：])|(?:required\s+sections?|sections?\s+required)\s*[:：])\s*([^。；;\n]+)/gi;
  for (const match of raw.matchAll(pattern)) {
    const list = match[1].split(/[，,]\s*(?=(?:字数|长度|格式|页数|篇幅|其他|其余|保留|保持|不变|完成|保存|读取|阅读|核对|检查|不要|不用|同时|请|然后|length\b|format\b|keep\b|preserve\b|save\b|read\b|check\b))/i)[0].replace(/\.\s*$/, '').replace(/(?:这|等|共)?\s*[一二三四五六七八九十\d]*\s*(?:个|项|部分)?(?:章节|栏目|部分)\s*$/, '');
    const sections = list.split(/、|，|,|和|以及|\s+and\s+/i).map(title => title.trim().replace(/^[“”"'「『]|[“”"'」』]$/g, '').trim());
    const explicit = /章节|栏目|sections?/i.test(match[0]);
    const sectionName = /^(?:背景|摘要|引言|目标|目的|用途|受众|范围|流程|步骤|执行计划|实施计划|风险|风险与应对|结论|建议|待确认事项|参考资料|来源|预算|人员安排|时间安排)$/;
    if ((explicit || sections.every(title => sectionName.test(title))) && sections.length >= (explicit ? 1 : 2) && sections.length <= 12 && sections.every(title => title.length > 0 && title.length <= 30 && !/[。!?！？:：]/.test(title))) events.push({ index: match.index, sections: [...new Set(sections)] });
  }
  for (const match of raw.matchAll(/(?:取消|不限制|不限)(?:所有|全部)?(?:章节|栏目)(?:要求)?/g)) events.push({ index: match.index, sections: [] });
  return events.sort((a, b) => a.index - b.index);
}

// A later message updates only the named deliverable. Negation is evaluated per clause,
// so cancelling a deck cannot hide the report requested after the comma (or vice versa).
function cancelledKind(raw: string, kind: RegExp): boolean {
  const modifiers = '(?:这份|那份|当前的|新的|一份|一套|一个|任何|新|再|the\\s+|a\\s+|an\\s+|new\\s+|\\s)*';
  const direct = new RegExp('(?:取消|不做|不制作|不生成|(?:不要|不用|不需要|无需)(?:再)?(?:创建|新建|生成|制作|交付|输出|导出|做)?|\\b(?:cancel|drop|skip)\\s+)' + modifiers + '(?:' + kind.source + ')', 'i');
  const reversed = new RegExp('(?:' + kind.source + ')(?:先|暂时|现在|这次|目前|也|都|就|暂|\\s)*(?:取消|不做|不制作|不生成|不用了|不要了)', 'i');
  return raw.split(/[，,。；;\n]|\bbut\b|但(?:是)?/i).some(clause => {
    if (!kind.test(clause) || /不要取消|别取消|不取消|do not cancel|don['’]t cancel/i.test(clause)) return false;
    if (/重新|重复|再次|重做|\bagain\b/i.test(clause) && /保留|已有|现有|原有|\b(?:existing|keep|retain)\b/i.test(raw)) return false;
    return direct.test(clause) || reversed.test(clause);
  });
}
function specificationOnly(clause: string): boolean {
  return /格式|页数|字数|长度|篇幅|输出类型|(?:章节|栏目)(?:要求)?\s*(?:改为|改成|包含|包括|为|是|[:：])|\b(?:format|page count|word count|length)\b/i.test(clause)
    && !/正文|内容|段落|标题|排版|字体|布局|\b(?:body|paragraph|layout|font)\b/i.test(clause);
}

export function deriveContract(input: PlanningInput): Contract {
  let result = empty(input.allowed), fullRead: boolean | undefined;
  const messages = [input.goal, ...(input.adjustments || [])];
  for (const [messageIndex, raw] of messages.entries()) {
    if (!raw) continue;
    const positive = actions(raw).replace(/(?:已经|刚刚|刚才|上次|之前|此前|已)(?:生成|创建|制作|保存|导出|写好|写完)|\b(?:already|previously)\s+(?:created|generated|saved|exported|written)\b/gi, reference => ' '.repeat(reference.length));
    const clauses = positive.split(/[，,。；;\n]|并|同时|以及|另外|\band\b|\balso\b/i);
    const positiveDoc = clauses.some(clause => create.test(clause) && doc.test(clause) && !pres.test(clause)) || /(?:保存|创建|生成|写|导出|create|write|save|export)[^。\n]{0,40}\b(?:DOCX|Markdown|MD|TXT)\b/i.test(positive);
    const pages = [...positive.matchAll(/([0-9]{1,3}|[一二三四五六七八九十两]{1,3})\s*(?:页|张|片|slides?)/gi)].at(-1);
    const requestedPages = pages ? number(pages[1]) : 0;
    const bodyAction = positive.split(/[，,。；;、\n]/).some(part => { const action = part.replace(/最终修订|最新修订|修订版/g, '资料版本'); return !specificationOnly(action) && edit.test(action) && (!metadata.test(action) || body.test(action)) && (/这份|该份|上述|刚才|已有|现有|原文|文档|报告|幻灯片|第\s*\d+\s*(?:页|段|章)|existing|current|this|selected/i.test(action) || body.test(action)); });
    const readingReference = /读取|阅读|读完|回读|检查|核对|分析|解读|\b(?:read|inspect|check|review|analy[sz]e)\b/i.test(positive);
    const makePresentation = clauses.some(clause => pres.test(clause) && (create.test(clause) || /做\s*(?:一[份套个]|[0-9一二三四五六七八九十两]+\s*页)?\s*(?:PPT|幻灯片|演示文稿)|^\s*(?:请|帮我|给我)?\s*(?:要|来)[^。；;\n]{0,20}(?:PPT|幻灯片|演示文稿)/i.test(clause)));
    const positivePres = pres.test(positive) && (makePresentation || requestedPages > 0 && !bodyAction && !readingReference);
    const replacePres = /改(?:成|为)[^。；;\n]{0,24}(?:PPT|幻灯片|演示文稿)|(?:instead|convert|turn)[^。\n]{0,35}(?:presentation|slides?|PPT)/i.test(positive);
    const replaceDoc = /改(?:成|为)[^。；;\n]{0,24}(?:写|创建|生成|保存|制作|做成|做一份)[^。\n]{0,20}(?:报告|文档|文章|DOCX|TXT|Markdown)|(?:instead|convert|turn)[^。\n]{0,35}(?:report|document|DOCX)/i.test(positive);
    if (replacePres && !(result.RequiresPresentation && !result.RequiresDocument)) result = { ...empty(input.allowed), RequiresPresentation: true };
    if (replaceDoc && !(result.RequiresDocument && !result.RequiresPresentation)) result = { ...empty(input.allowed), RequiresDocument: true };
    if (positiveDoc || replaceDoc) { result.RequiresDocument = true; result.ChatOnly = false; }
    if (positivePres || replacePres) { result.RequiresPresentation = true; result.ChatOnly = false; }
    if (positivePres && positiveDoc && !/并|同时|以及|另外|和|、|\band\b|\balso\b/i.test(positive) && !replaceDoc) result.RequiresDocument = false;
    const negatives = (raw.match(/(?:不要|无需|无须|不必|不用|不需要|别|勿|do\s+not|don['’]t|without)[^。；;，,\n]*/gi) || [])
      .filter(part => !(/重新|重复|再次|重做|\bagain\b/i.test(part) && /保留|已有|现有|原有|\b(?:existing|keep|retain)\b/i.test(raw)));
    if (!positiveDoc && negatives.some(part => /创建|新建|生成|保存|输出|写|导出|做|create|save|write|export|produce/i.test(part) && /文档|报告|文件|文章|总结|摘要|document|report|file|article|summary/i.test(part))) result.RequiresDocument = false;
    if (!positivePres && negatives.some(part => /做|创建|新建|生成|制作|写|输出|导出|make|create|build|generate/i.test(part) && pres.test(part))) result.RequiresPresentation = false;
    if (!positiveDoc && (cancelledKind(raw, doc) || /(?:只|仅)保留[^。；;\n]{0,16}(?:PPT|幻灯片|演示文稿)/i.test(raw))) result.RequiresDocument = false;
    if (!positivePres && (cancelledKind(raw, pres) || /(?:只|仅)保留[^。；;\n]{0,16}(?:报告|文档)/i.test(raw))) result.RequiresPresentation = false;
    if (requestedPages > 0 && requestedPages <= 100 && (positivePres || result.RequiresPresentation && !bodyAction && !readingReference)) result.RequestedPages = requestedPages;
    if (/取消.{0,6}页数|不限页数|不限制页数/.test(raw)) result.RequestedPages = 0;
    const lengthEvents: { index: number; min: number; max: number; unit: number }[] = [];
    for (const match of positive.matchAll(/(?:不少于|至少|不低于|超过|多于|不超过|至多|最多|低于|小于|约|大约)?\s*(\d{1,6})\s*(?:[-–—~～至到]\s*(\d{1,6})\s*)?(汉字|中文字符|字符|字|英文单词|单词|words?|characters?)/gi)) {
      const a = Number(match[1]), b = match[2] ? Number(match[2]) : 0, unit = /单词|words?/i.test(match[3]) ? 3 : /字符|characters?/i.test(match[3]) ? 2 : 1;
      let min = b ? Math.min(a, b) : a, max = b ? Math.max(a, b) : a;
      if (/不少于|至少|不低于|超过|多于/.test(match[0])) max = 0;
      else if (/不超过|至多|最多|低于|小于/.test(match[0])) min = 0;
      else if (/约|大约/.test(match[0])) { min = Math.floor(a * .8); max = Math.ceil(a * 1.2); }
      lengthEvents.push({ index: match.index, min, max, unit });
    }
    for (const match of raw.matchAll(/取消.{0,6}(?:字数|长度)|不限(?:制)?(?:字数|长度)|字数不限|长度不限|字数随意|篇幅随意|无字数要求|不限制字数|不要(?:限制)?字数/gi)) lengthEvents.push({ index: match.index, min: 0, max: 0, unit: 0 });
    const length = lengthEvents.sort((a, b) => a.index - b.index).at(-1);
    if (length) { result.MinLength = length.min; result.MaxLength = length.max; result.LengthUnit = length.unit; }
    const format = [...positive.matchAll(/(?<![A-Za-z])(DOCX|Markdown|MD|TXT)(?![A-Za-z])/gi)].at(-1)?.[1].toUpperCase();
    if (format) result.RequiredDocumentFormat = format === 'MARKDOWN' ? 'MD' : format;
    if (result.RequiresDocument) for (const event of sectionEvents(raw)) result.RequiredSections = event.sections;
    const count = [...positive.matchAll(/([一二三四五六七八九十两\d]{1,3})\s*份\s*(?:报告|文档|总结|文章)/g)].at(-1);
    if (count) result.RequiredCount = Math.max(1, number(count[1]));
    const all = [...raw.matchAll(/全部|所有|\ball\s+(?:the\s+)?(?:sources?|documents?|files?|materials?)/gi)].at(-1);
    const only = [...raw.matchAll(/(?:只|仅|only)[^。；;，,\n]{0,24}(?:第\s*([一二三四五六七八九十两\d]{1,3})\s*(?:份|个|篇)|\b(first|second|third|fourth)\s+(?:source|document|file))/gi)].at(-1);
    if (only && (!all || only.index >= all.index)) { const ordinal = only[1] ? number(only[1]) : ['first', 'second', 'third', 'fourth'].indexOf(only[2].toLowerCase()) + 1; if (ordinal >= 1 && ordinal <= input.allowed.length) result.RequiredSourceIds = [input.allowed[ordinal - 1]]; }
    else if (all) result.RequiredSourceIds = [...input.allowed];
    const fullEvents: { index: number; value: boolean }[] = [];
    for (const match of positive.matchAll(new RegExp(full.source, 'gi'))) if (source.test(positive) || /通读|精读|全文|full[- ]?text/i.test(match[0])) fullEvents.push({ index: match.index, value: true });
    for (const match of raw.matchAll(/只看开头|只看前面|只读开头|只读前\s*\d*\s*页|不用通读|不需要通读|不要通读|不用全文|无需全文|不要全文|不用读全|不用读完|不必读全|只看摘要|只看结论|粗略浏览|初步意见/gi)) fullEvents.push({ index: match.index, value: false });
    if (fullEvents.length) fullRead = fullEvents.sort((a, b) => a.index - b.index).at(-1)!.value;
    if (bodyAction && !((positiveDoc || positivePres) && !body.test(positive))) { result.RequiresTargetedEdit = true; result.ChatOnly = false; }
    else if (/先不要修改|暂不修改|不要修改|别修改|无需修改|只分析|仅分析|先分析|只阅读|只读不写|分析即可/.test(raw)) result.RequiresTargetedEdit = false;
    if ((chat.test(raw) || noFile.test(raw)) && !bodyAction && !positiveDoc && !positivePres) {
      result.ChatOnly = true; result.RequiresDocument = false; result.RequiresPresentation = false; result.RequiresTargetedEdit = false;
      if (!chat.test(raw) || !length) { result.MinLength = 0; result.MaxLength = 0; result.LengthUnit = 0; }
    }
    if (!result.RequiresPresentation) result.RequestedPages = 0;
    if (!result.RequiresDocument) result.RequiredSections = [];
    if (!result.RequiresDocument && !result.ChatOnly) { result.MinLength = 0; result.MaxLength = 0; result.LengthUnit = 0; result.RequiredCount = 1; }
    const hint = [...raw.matchAll(/最终修订|最终版|最新版|旧版|新版|原版|第\s*\d+\s*版/g)].at(-1); if (hint && result.RequiresTargetedEdit) result.EditVersionHint = hint[0];
    const verification = verificationDirective(raw, positive); if (input.requirementsVersion === 1 && verification !== undefined) result.CodeVerification = verification;
    if (input.requirementsVersion === 1) for (const update of input.requirementUpdates || []) if (update.userRevision === messageIndex) {
      result = applyRequirementChanges(result, update.changes); if (update.changes.fullRead !== undefined) fullRead = update.changes.fullRead;
    }
  }
  result.RequiresFullSourceCoverage = fullRead ?? ((result.RequiresDocument || result.RequiresPresentation) && input.scope.mode === 'selected' && input.allowed.length > 0 && source.test(messages.join('\n')) && !sample.test(messages.join('\n')));
  if (!result.RequiresDocument) { result.RequiredDocumentFormat = ''; result.RequiredSections = []; } if (!result.RequiresTargetedEdit) result.EditVersionHint = '';
  if (input.intent === 'discuss' || input.approvalPolicy === 'read_only') result = { ...empty(input.allowed), RequiresFullSourceCoverage: result.RequiresFullSourceCoverage, RequiredSourceIds: result.RequiredSourceIds };
  return result;
}

const groups = {
  core: ['search_library', 'read_items', 'review_sources', 'review_claims', 'list_collections', 'recall_result', 'calculate', 'update_plan', 'finish_task'],
  read: ['read_document', 'read_presentation', 'read_image', 'read_clips', 'load_skill'],
  document: ['read_document', 'create_document', 'replace_document_text', 'inspect_document', 'load_skill'],
  presentation: ['read_document', 'read_presentation', 'create_presentation', 'copy_presentation', 'edit_presentation', 'inspect_presentation', 'load_skill'],
  edit: ['read_document', 'read_presentation', 'create_document', 'replace_document_text', 'edit_presentation', 'load_skill'],
  media: ['read_document', 'read_presentation', 'read_image', 'index_pdf_pages', 'index_video_frames', 'generate_subtitles', 'load_skill'],
  organize: ['read_document', 'create_document', 'set_collection', 'add_tags', 'write_note', 'open_item', 'load_skill'],
  workspace: ['workspace_info', 'search_workspace', 'read_workspace_file', 'git_workspace', 'list_folder', 'run_command', 'apply_workspace_patch', 'start_command_job', 'read_command_job', 'wait_command_job', 'cancel_command_job'],
};
export function planTask(input: PlanningInput) {
  const contract = deriveContract(input), text = actions([input.goal, ...(input.adjustments || [])].join('\n'));
  const context = { ...input.context, files: [...(input.context.files || [])] };
  if (context.domain === 'coding' && (contract.RequiresDocument || contract.RequiresPresentation || /所选资料|所选文档|资料库/.test(text))) { context.taskId = ''; context.domain = ''; context.files = []; }
  const isCode = coding(text) || context.domain === 'coding';
  const isDeck = contract.RequiresPresentation || pres.test(text) || context.domain === 'presentation' || input.environment.selectedFormats.includes('PPTX');
  const isDoc = contract.RequiresDocument || doc.test(text) || context.domain === 'document';
  const isEdit = contract.RequiresTargetedEdit || edit.test(text), isRead = /总结|概括|解释|分析|阅读|summar|explain|analy|compare|describe|read/i.test(text);
  const isMedia = /视频|字幕|音频|音乐|图片|照片|截图|video|subtitle|audio|music|image|photo|pdf/i.test(text), isOrganize = /整理|分类|标签|收藏|归档|collection|tag|organize|classify/i.test(text);
  const readonly = input.intent === 'discuss' || input.approvalPolicy === 'read_only';
  const selected = new Set<string>(groups.core); const add = (names: readonly string[]) => names.forEach(name => selected.add(name));
  if (readonly) add(groups.read);
  else { if (isDeck) add(groups.presentation); if (isDoc) add(groups.document); if (isEdit) add(groups.edit); if (isMedia) add(groups.media); if (isOrganize) add(groups.organize); if (!isDeck && !isDoc && !isEdit && !isMedia && !isOrganize) add(isRead ? groups.read : [...groups.document, ...groups.read, 'set_collection', 'add_tags', 'write_note', 'open_item']); }
  if (isDoc) selected.add('inspect_document'); if (isDeck) selected.add('inspect_presentation');
  const pureCode = isCode && !contract.RequiresDocument && !contract.RequiresPresentation && !input.environment.selectedFormats.length && !isDeck && !/资料库|所选资料|所选文档|根据[^。\n]{0,50}(?:资料|报告|文档)/.test(text);
  if (pureCode) { selected.clear(); add(['calculate', 'recall_result', 'update_plan', 'finish_task']); }
  if (input.features.memory) add(readonly ? ['search_memory'] : ['search_memory', 'remember_memory']);
  if (input.features.delegation && !readonly && !input.features.parent) add(['delegate_task', 'delegate_parallel']);
  if (input.features.sandbox && !readonly) selected.add('run_code'); if (input.features.search) add(['web_search', 'fetch_url']);
  const browser = /浏览器|本地网页|browser|127\.0\.0\.1|localhost/i.test(text);
  if (input.environment.workspace && (isCode || browser)) { add(groups.workspace.filter(name => !readonly || !['run_command', 'apply_workspace_patch', 'start_command_job', 'cancel_command_job'].includes(name))); if (!pureCode) add(readonly ? ['read_text_file'] : ['read_text_file', 'write_text_file']); if (browser) add(readonly ? ['read_browser'] : ['open_browser', 'read_browser', 'browser_action']); }
  if (isDeck && !readonly && input.environment.rendering && /渲染|视觉|预览|高质量|render|visual|preview/i.test(text)) selected.add('render_presentation');
  add(readonly ? ['list_mcp_tools'] : ['list_mcp_tools', 'mcp_call']);
  if (!contract.RequiresDocument && !contract.RequiresTargetedEdit) selected.delete('create_document');
  if (!contract.RequiresPresentation && !contract.RequiresTargetedEdit) for (const name of ['create_presentation', 'copy_presentation']) selected.delete(name);
  let tools = input.explicitTools ? [...new Set(input.explicitTools)] : [...selected]; tools = tools.filter(name => input.environment.tools.includes(name));
  return { version: 1, language: 'TypeScript', contract, tools, context, coding: pureCode, renderingRequired: renderingRequired([input.goal, ...input.adjustments]) };
}
export function renderingRequired(messages: string[]): boolean {
  let required = false; for (const message of messages) { if (/(?:不要|无需|不用|不必)[^。；;，,\n]{0,12}渲染|(?:do not|don't|skip)\s+render/i.test(message)) required = false; else if (/渲染|\brender(?:ing)?\b/i.test(actions(message))) required = true; } return required;
}
