import { actions, type Contract } from './planning.ts';
export interface CodeCall {
  tool: string; status: string; sequence: number; path: string; hash: string; workspaceId: string;
  jobState: string; exitCode: number | null; inputsCurrent: boolean; outputsVerified: boolean;
  checkedInputs: { path: string; sha256: string; workspaceId: string }[];
}
export interface CodeEvidence {
  goal: string; adjustments: string[]; intent: string; approvalPolicy: string; workspaceVersion: number; workspaceId: string;
  calls: CodeCall[]; files: { path: string; hash: string; available: boolean }[];
}
export function codeProblem(input: CodeEvidence): string {
  const equal = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();
  const lastByPath = (calls: CodeCall[]) => [...new Map(calls.map(call => [call.path.toLowerCase(), call])).values()];
  const mutations = lastByPath(input.calls.filter(call => ['apply_workspace_patch', 'write_text_file'].includes(call.tool) && call.status === 'done' && call.hash));
  const reads = lastByPath(input.calls.filter(call => call.tool === 'read_workspace_file' && call.status === 'done'));
  if (!mutations.length && (input.intent === 'discuss' || input.approvalPolicy === 'read_only')) return '';
  const messages = [input.goal, ...input.adjustments], goal = actions(messages.join('\n'));
  let writeRequested = false, declined = false;
  for (const raw of messages) {
    const positive = actions(raw);
    if (/先不要修改|暂不修改|不要修改|别修改|无需修改|只分析|仅分析|先分析|只阅读|只读不写|分析即可|\b(?:do not edit|don't edit|read.only)\b/i.test(raw)) writeRequested = false;
    else if (/修复|修改|实现|新增|添加|重构|优化|完善|\b(?:fix|implement|refactor|change|edit|optimize)\b|(?:^|[。！？；;\n])\s*(?:please\s+)?add\s+/i.test(positive)) writeRequested = true;
    if (/(?:不要|无需|不必|不用)[^。；;，,\n]{0,30}(?:测试|运行|编译|验证)|do not run|skip tests/i.test(raw)) declined = true;
    else if (/运行|执行|重跑|编译|构建|验证|\b(?:run|rerun|execute|build|verify|test|check)\b/i.test(positive)) declined = false;
  }
  const analysis = /^(?:请|帮我|只|仅|先|\s)*(?:解释|分析|讨论|阅读|评估|审查|查看|explain\b|analy[sz]e\b|discuss\b|review\b|read\b|describe\b)/i.test(goal);
  const thenWrite = /(?:然后|并|再|\band\b|\bthen\b)[^。！？;\n]{0,30}(?:修复|修改|实现|新增|优化|完善|\bfix\b|\bedit\b|\bimplement\b|\boptimize\b)/i.test(goal);
  if (!mutations.length && analysis && !thenWrite) return '';
  let targets = mutations.map(change => reads.find(read => read.sequence > change.sequence && equal(read.path, change.path)) || change);
  if (!targets.length && input.workspaceVersion === 1 && writeRequested) targets = reads;
  if (!targets.length) return input.workspaceVersion === 1 && writeRequested ? '代码任务尚无目标文件读取或可验证改动，请先检查实际代码。' : '';
  for (const target of targets) {
    if (target.workspaceId !== input.workspaceId) return '工作区已变化，旧工作区的补丁和验证不能证明当前结果。';
    const file = input.files.find(file => equal(file.path, target.path));
    if (!file?.available) return '代码文件当前不可读取：' + target.path;
    if (file.hash !== target.hash) return '代码文件在补丁后已变化，请重新读取并验证当前版本：' + target.path;
  }
  const verify = !declined && (/测试|编译|运行|验证|\b(?:test|build|verify|run|check)\b/i.test(goal) || !mutations.length && writeRequested);
  if (!verify) return '';
  const receipts = input.calls.filter(call => call.status === 'done' && call.exitCode === 0 && call.inputsCurrent && call.outputsVerified && (!call.jobState || call.jobState === 'completed'));
  const proven = (target: CodeCall) => receipts.some(receipt => receipt.checkedInputs.some(row => equal(row.path, target.path) && row.sha256 === target.hash && row.workspaceId === input.workspaceId));
  if (!mutations.length && targets.some(proven)) return '';
  const missing = targets.find(target => !proven(target));
  return missing ? '当前代码版本尚无成功的实际验证。请使用 start_command_job 的 inputs 声明改动文件，运行适用测试并等待实际退出码 0：' + missing.path : '';
}

interface ArtifactFacts { id: string; name: string; exists: boolean; owned: boolean; format: string; length: number; pages: number; readable: boolean; readBack: boolean; detail: string; qualityErrors: string[] }
export interface DeliveryEvidence {
  requirement: { requiresDocumentArtifact: boolean; requiresPresentationArtifact: boolean; requiresTargetedEdit: boolean; chatOnly: boolean; requestedPages: number; minChars: number; maxChars: number; requiredFormat: string; requiresFullSourceCoverage: boolean; requiredSourceIds: string[] };
  document: ArtifactFacts | null; presentation: ArtifactFacts | null; edit: ArtifactFacts | null;
  workspaceVersion: number; codeProblem: string; chatLength: number; sourcesFull: boolean; sourceDetail: string;
  renderingRequired: boolean; renderingComplete: boolean;
}
export function deliveryProblem(input: DeliveryEvidence): string {
  const r = input.requirement, within = (n: number) => (!r.minChars || n >= r.minChars) && (!r.maxChars || n <= r.maxChars);
  if (input.codeProblem) return input.codeProblem;
  if (!r.chatOnly && r.requiresDocumentArtifact) {
    const doc = input.document;
    if (!doc) return '任务要求实际文档成果，请使用 create_document 创建所需格式，并核对已保存文件；已有读取与有效改动会保留。';
    if (!doc.exists || !doc.owned || !doc.readable) return '最终文档成果已移除或不可读取，请恢复实际文件后再完成任务：' + doc.id;
    if (r.requiredFormat && doc.format.toUpperCase() !== r.requiredFormat.toUpperCase()) return '最终文档格式为 ' + doc.format + '，目标要求 ' + r.requiredFormat + '；请交付正确格式的实际文件。';
    if (!within(doc.length)) return '最终文档长度实测 ' + doc.length + '，要求 ' + r.minChars + '–' + (r.maxChars || '不限') + '；请只调整该成果后重新核对。';
    if (!doc.readBack) return '文档成果在创建或修改后还没有充分的当前版本回读，请继续读取正文：' + doc.id + '。' + doc.detail;
  }
  if (!r.chatOnly && r.requiresPresentationArtifact) {
    const deck = input.presentation;
    if (!deck) return '任务要求实际 PPTX，请创建或复制可编辑演示文稿，并核对已保存的全部页面。';
    if (!deck.exists || !deck.owned || !deck.readable) return '最终演示文稿已移除或无法重新打开，请修复实际 PPTX 文件：' + deck.id;
    if (r.requestedPages && deck.pages !== r.requestedPages) return '演示文稿实际 ' + deck.pages + ' 页，要求 ' + r.requestedPages + ' 页（含封面）；请只调整页数不符部分后核对全部页面。';
    if (!deck.readBack) return '演示文稿在创建或修改后还没有覆盖全部页面的当前回读：' + deck.id + '。' + deck.detail;
    if (deck.qualityErrors.length) return '实际 PPTX 仍有结构问题，请使用 inspect_presentation 定位并只修复受影响的对象或页面：' + deck.qualityErrors.slice(0, 3).join('；');
    if (input.renderingRequired && !input.renderingComplete) return '任务要求实际渲染，但当前 PPTX 版本尚未覆盖全部渲染页面。请使用 render_presentation 分批渲染；无法渲染时如实说明剩余限制。';
  }
  if (!r.chatOnly && r.requiresTargetedEdit && input.workspaceVersion !== 1) {
    if (!input.edit) return '这是定点改稿任务，但当前任务没有成功且有效的原文件修改记录。请读取目标、批准精确修改，再核对改后文件。';
    if (!input.edit.readBack) return '改稿已完成，但修改后还没有当前有效的回读，请核对改后文件：' + input.edit.id;
    if (!input.edit.readable || input.edit.qualityErrors.length) return '改后文件仍有未解决的结构问题，请检查实际文件并只修复受影响部分：' + input.edit.id + '。' + input.edit.qualityErrors.slice(0, 3).join('；');
  }
  if (r.chatOnly && !within(input.chatLength)) return '聊天正文长度实测 ' + input.chatLength + '，要求 ' + r.minChars + '–' + (r.maxChars || '不限') + '；请补足或精简后完成。';
  if (r.requiresFullSourceCoverage && r.requiredSourceIds.length && !input.sourcesFull) return '任务要求读完全部来源，但仍有未覆盖内容：' + input.sourceDetail + ' 请继续真实读取，保留已完成工作。';
  return '';
}
