import { rangeCovered, type EvidenceSource } from './evidence.ts';

const headcount = /(?<![\d.])([1-9]\d{0,5})\s*(?:名|人(?!员)|位)(?![\d.万千百])/gu;
const subject = /志愿者|工作人员|值班员|值班人员|读者|借阅者|观众|访客|参会人员|参加者|来宾/u;
const actual = /实际\s*(?:到场|到馆|到位|出席|入场|到岗)|(?:已|已经)\s*(?:到场|到馆|到位|出席|入场|到岗)|实到/u;
const attendance = /到场|到馆|到位|出席|入场|到岗|在场|实到/u;
const tentative = /预计|预估|暂估|计划|拟|待确认|尚未|未确认|未(?:到场|到馆|到位|出席|入场|到岗)|假设|示例|如果/u;
const instruction = /^\s*(?:[-*]\s+)?(?:核对|检查|核查|确认|复核|统计|登记|记录)|(?:请|需|待|须)\s*(?:核对|检查|核查|确认|复核|统计|登记|记录)|是否|不要|禁止|不得|不能|未见|未说明|未经/u;

type Location = { line?: number; page?: number; shapeId?: string };
type TextRow = Location & { text: string };
type Statement = TextRow & { offset: number; number: number; subject: string; confirmed: boolean; assertion: boolean };

function statements(row: TextRow): Statement[] {
  const result: Statement[] = [];
  for (const part of row.text.matchAll(/[^。！？；;\r\n、，,（）()]+/gu)) {
    const text = part[0], numbers = [...text.matchAll(headcount)];
    if (numbers.length !== 1) continue;
    const event = actual.exec(text);
    const uncertain = tentative.test(text);
    const requested = event ? instruction.test(text.slice(0, event.index)) : instruction.test(text);
    result.push({ ...row, text, offset: part.index, number: Number(numbers[0][1]), subject: subject.exec(text)?.[0] || '',
      confirmed: attendance.test(text) && !uncertain && !requested,
      assertion: !!event && !uncertain && !requested });
  }
  return result;
}

type Basis = { sourceId: string; sourceName: string; sourceVersion: string; quote: string; line: number; readCallIds: string[] };
type Observation = Statement & { basis: Basis };
type Issue = Location & { message: string; basis: Basis };

function observations(sources: EvidenceSource[]): Observation[] {
  const verified: Observation[] = [];
  for (const source of sources) {
    if (!source.available || source.role === 'artifact') continue;
    const reads = source.reads.filter(row => row.version === source.version).sort((a, b) => a.start - b.start);
    if (!reads.length) continue;
    let offset = 0, lineNumber = 0;
    for (const line of source.text.split('\n')) {
      lineNumber++;
      for (const claim of statements({ text: line })) {
        const start = offset + claim.offset, end = start + claim.text.length;
        if (!rangeCovered(start, end, reads)) continue;
        verified.push({ ...claim, basis: { sourceId: source.id, sourceName: source.name, sourceVersion: source.version,
          quote: claim.text.trim().slice(0, 160), line: lineNumber,
          readCallIds: [...new Set(reads.filter(row => row.start < end && row.end > start).map(row => row.callId))] } });
      }
      offset += line.length + 1;
    }
  }
  return verified;
}

export function reviewAttendance(rows: TextRow[], sources: EvidenceSource[], userInstructions: string[]): Issue[] {
  const claims = rows.flatMap(statements).filter(claim => claim.assertion);
  if (!claims.length) return [];
  const read = observations(sources), user = userInstructions.flatMap(text => statements({ text }));
  const issues: Issue[] = [];
  for (const claim of claims) {
    const sameNumber = (other: Statement) => other.number === claim.number &&
      (!claim.subject || !other.subject || other.subject === claim.subject);
    const matching = read.filter(sameNumber);
    if (matching.some(other => other.confirmed) || user.some(other => sameNumber(other) && other.assertion)) continue;
    const source = matching.find(other => other.subject && other.subject === claim.subject) ??
      matching.find(other => tentative.test(other.text)) ??
      (!claim.subject && matching.length === 1 && matching[0].subject ? matching[0] : undefined);
    if (!source) continue;
    issues.push({ line: claim.line, page: claim.page, shapeId: claim.shapeId, basis: source.basis,
      message: '实际出勤说法「' + claim.text.trim().slice(0, 150) + '」需要核对。已读原文《' + source.basis.sourceName +
        '》第 ' + source.basis.line + ' 行为「' + source.basis.quote + '」。这段原文只给出人数或预计值。请保留原口径，或读取明确记录实际出勤的原文。' });
    if (issues.length === 8) break;
  }
  return issues;
}

// KPI compositions store the value and its caption in adjacent editable text shapes.
export function attendanceRows(deck: { slides: { shapes: {id: string; text: string}[] }[] }): TextRow[] {
  return deck.slides.flatMap((slide, index) => {
    const shapes = slide.shapes.filter(shape => shape.text?.trim());
    return shapes.map((shape, n) => {
      const label = shape.text.trim(), previous = shapes[n - 1]?.text?.trim() || '';
      const count = /^[1-9]\d{0,5}\s*(?:名|人|位)$/u.test(previous) && actual.test(label.split(/[（(]/u)[0]);
      return { text: count ? previous + ' ' + label : label, page: index + 1, shapeId: shape.id };
    });
  });
}
