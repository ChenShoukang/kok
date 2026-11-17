import test from 'node:test';
import assert from 'node:assert/strict';
import { reviewEvidence } from '../src/business/evidence.ts';
import { reviewArtifact } from '../src/business/review.ts';
import { deriveContract } from '../src/business/planning.ts';
import { deliveryProblem } from '../src/business/completion.ts';

const text = '最终修订\n预算8万元，60人。\n日期待确认。';
const source = (extra = {}) => ({ id: 'revision', name: '最终修订', domain: 'document', version: 'v2', text, available: true, reads: [], ...extra });
const claim = (extra = {}) => ({ claim: '预算为8万元。', sourceId: 'revision', quote: '预算8万元，60人。', relationship: 'supports', ...extra });
const review = (s = source(), c = claim(), extra = {}) => reviewEvidence({ claims: [c], sources: [s], ...extra }).claims[0];
test('evidence: locating unread quotations does not grant reading, and coverage gaps remain gaps', () => {
  const unread = review(); assert.equal(unread.quoteVerified, true); assert.equal(unread.status, 'unread'); assert.equal(unread.readingVerified, false);
  const rows = [{ start: 0, end: 8, version: 'v2', callId: 'a' }, { start: 9, end: text.length, version: 'v2', callId: 'b' }];
  assert.equal(review(source({ reads: rows })).status, 'unread');
  rows.push({ start: 8, end: 9, version: 'v2', callId: 'c' });
  const anchored = review(source({ reads: rows })); assert.equal(anchored.status, 'anchored'); assert.equal(anchored.startLine, 2); assert.equal(anchored.endLine, 2); assert.equal(anchored.semanticVerified, false); assert.equal(anchored.relationshipAuthority, 'caller_proposed');
});
test('evidence: changed source versions, invented quotations and ambiguous copies are explicit', () => {
  assert.equal(review(source(), claim({ sourceVersion: 'v1' })).status, 'stale');
  assert.equal(review(source({ reads: [{ start: 0, end: text.length, version: 'v1', callId: 'old' }] })).status, 'stale_reading');
  assert.equal(review(source(), claim({ quote: '预算12万元' })).status, 'not_found');
  assert.equal(review(source({ text: '预算8万元。预算8万元。' }), claim({ quote: '预算8万元。' })).status, 'ambiguous');
  assert.equal(review(source({ text: '预算8万元。预算8万元。' }), claim({ quote: '预算8万元。', offset: 6 })).quoteVerified, true);
  assert.equal(review(source({ available: false })).status, 'unavailable');
  assert.deepEqual(reviewEvidence({ claims: [claim()], sources: [] }).claims[0].warnings, []);
});
test('evidence: actual artifact location and numeric heuristics never certify semantic support', () => {
  const artifact = { id: 'report', version: 'file-v3', text: '报告\n预算为8万元。', available: true };
  const located = review(source(), claim(), { artifact }); assert.equal(located.artifactStatus, 'located'); assert.equal(located.artifactVersion, 'file-v3');
  const wrong = review(source(), claim({ claim: '预算为12万元。' }), { artifact }); assert.equal(wrong.artifactStatus, 'not_found'); assert.deepEqual(wrong.warnings, ['claim_numbers_need_review']); assert.equal(wrong.semanticVerified, false);
  const self = review(source({ role: 'artifact' })); assert(self.warnings.includes('artifact_used_as_source')); assert.equal(self.semanticVerified, false);
});
const input = (goal, adjustments = []) => ({ goal, adjustments, allowed: [], intent: 'assist', approvalPolicy: 'each', scope: { mode: 'all' } });
test('sections: only explicit heading lists are acceptance requirements; newer lists and cancellation win', () => {
  assert.deepEqual(deriveContract(input('创建报告，报告应包含目标、流程、风险和待确认事项。')).RequiredSections, ['目标','流程','风险','待确认事项']);
  assert.deepEqual(deriveContract(input('写DOCX报告，章节要求：目标、风险。', ['栏目改为背景、结论'])).RequiredSections, ['背景','结论']);
  assert.deepEqual(deriveContract(input('写DOCX报告，章节要求：目标、风险。', ['章节要求改为背景、结论、待确认事项，长度和DOCX格式保持。保存后核对原文。'])).RequiredSections, ['背景','结论','待确认事项']);
  assert.equal(deriveContract(input('创建报告，章节：目标、风险', ['栏目改为背景、结论'])).RequiresTargetedEdit, false);
  assert.deepEqual(deriveContract(input('写报告，预算、人数和日期都要准确')).RequiredSections, []);
  assert.deepEqual(deriveContract(input('创建报告，报告应包含8万元、60人和不收集手机号。')).RequiredSections, []);
  assert.deepEqual(deriveContract(input('写报告，章节：目标、风险', ['取消章节要求'])).RequiredSections, []);
  assert.deepEqual(deriveContract(input('写报告，章节：目标、风险', ['不要报告，只做PPT', '创建一份新报告'])).RequiredSections, []);
  assert.deepEqual(deriveContract(input('Create a document. Required sections: Purpose, Risks and Unknowns.')).RequiredSections, ['Purpose','Risks','Unknowns']);
});
const document = (text, sections) => reviewArtifact({ id: 'report', kind: 'document', fileVersion: 'saved-v1', text, sections });
test('sections: prose mentions, empty headings and headings inside code are not complete sections', () => {
  const report = document('# 活动方案\n目标、风险是必需栏目。\n## 目标\n\n## 流程\n先准备。\n```md\n## 风险\n代码内文字\n```', ['目标','风险']);
  assert.deepEqual(report.sectionChecks.map(s => s.status), ['empty','missing']); assert.equal(report.errors, 2);
  const fixed = document('# 活动方案\n## 目标\n让参与者阅读。\n## 风险\n日期未定，不编造。', ['目标','风险']); assert.equal(fixed.errors, 0); assert.deepEqual(fixed.sectionChecks.map(s => s.line), [2,4]);
});
test('sections: required plain TXT headings, numbered headings and nested bodies are recognized', () => {
  assert.equal(document('目标\n提高阅读兴趣。\n风险：\n日期待确认。', ['目标','风险']).errors, 0);
  assert.equal(document('一、目标\n明确范围。\n（二）风险\n资料不足。', ['目标','风险']).errors, 0);
  assert.equal(document('## 风险\n### 日期\n具体日期未知。', ['风险']).sectionChecks[0].status, 'present');
  assert.equal(document('## 风险\n### 日期\n', ['风险']).sectionChecks[0].status, 'empty');
});
test('completion: actual saved report defects block completion and a targeted repair clears the gap', () => {
  const facts = { id: 'report', exists: true, owned: true, readable: true, format: 'DOCX', length: 500, readBack: true, qualityErrors: ['章节缺少正文：“风险”。'] };
  const evidence = { requirement: { requiresDocumentArtifact: true, requiresPresentationArtifact: false, requiresTargetedEdit: false, chatOnly: false, minChars: 0, maxChars: 0, requiredFormat: 'DOCX', requiredSourceIds: [] }, document: facts, codeProblem: '', sourcesFull: true };
  assert.match(deliveryProblem(evidence), /必需章节/); assert.equal(deliveryProblem({ ...evidence, document: { ...facts, qualityErrors: [] } }), '');
});
