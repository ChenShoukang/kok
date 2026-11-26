export interface EvidenceClaim { claim: string; sourceId: string; quote: string; sourceVersion?: string; offset?: number; relationship: 'supports' | 'contradicts' | 'inference' | 'unverified' }
export interface EvidenceSource { id: string; name: string; domain: string; version: string; text: string; available: boolean; role?: 'input' | 'artifact'; reads: { start: number; end: number; version: string; callId: string }[] }

export function rangeCovered(start: number, end: number, orderedReads: EvidenceSource['reads']) {
  let cursor = start;
  for (const row of orderedReads) {
    if (row.end <= cursor) continue;
    if (row.start > cursor) break;
    cursor = row.end;
    if (cursor >= end) return true;
  }
  return false;
}

// Exact quotation/coverage checks only. Semantic relationships are proposals by the caller.
export function reviewEvidence(input: { claims: EvidenceClaim[]; sources: EvidenceSource[]; artifact?: { id: string; version: string; text: string; available: boolean } }) {
  const results = input.claims.map((claim, index) => {
    const source = input.sources.find(row => row.id === claim.sourceId);
    const result = { claimIndex: index + 1, claim: claim.claim, sourceId: claim.sourceId, sourceName: source?.name || '', quote: claim.quote,
      sourceVersion: source?.version || '', sourceRole: source?.role || 'input', domain: source?.domain || '', status: 'unavailable', offset: -1, endOffset: -1, startLine: 0, endLine: 0,
      artifactId: input.artifact?.id || '', artifactVersion: input.artifact?.version || '', artifactStatus: !input.artifact ? 'not_checked' : !input.artifact.available ? 'unavailable' : input.artifact.text.includes(claim.claim) ? 'located' : 'not_found',
      quoteVerified: false, readingVerified: false, semanticVerified: false, relationship: claim.relationship, relationshipAuthority: 'caller_proposed',
      readCallIds: [] as string[], warnings: [] as string[] };
    if (source && (source.role === 'artifact' || source.id === input.artifact?.id)) result.warnings.push('artifact_used_as_source');
    if (!source?.available) return result;
    if (claim.sourceVersion && claim.sourceVersion !== source.version) { result.status = 'stale'; return result; }
    const quote = claim.quote.replaceAll('\r\n', '\n').replaceAll('\r', '\n');
    if (!quote.trim()) { result.status = 'not_found'; return result; }
    const start = claim.offset === undefined ? source.text.indexOf(quote) : claim.offset;
    if (start < 0 || source.text.slice(start, start + quote.length) !== quote) { result.status = 'not_found'; return result; }
    if (claim.offset === undefined && source.text.indexOf(quote, start + 1) >= 0) { result.status = 'ambiguous'; return result; }
    result.quoteVerified = true; result.offset = start; result.endOffset = start + quote.length;
    result.startLine = source.text.slice(0, start).split('\n').length;
    result.endLine = source.text.slice(0, Math.max(start, result.endOffset - 1)).split('\n').length;
    const current = source.reads.filter(row => row.version === source.version);
    result.readingVerified = rangeCovered(start, result.endOffset, [...current].sort((a, b) => a.start - b.start));
    result.status = result.readingVerified ? 'anchored' : source.reads.length && !current.length ? 'stale_reading' : 'unread';
    result.readCallIds = current.filter(row => row.start < result.endOffset && row.end > start).map(row => row.callId).filter((id, i, ids) => ids.indexOf(id) === i);
    // A narrow warning, not a semantic verdict: exact numeric tokens differ even though the quote exists.
    const numbers = (text: string): string[] => text.match(/\d+(?:\.\d+)?/g) || [];
    if (claim.relationship === 'supports' && numbers(claim.claim).some(number => !numbers(quote).includes(number))) result.warnings.push('claim_numbers_need_review');
    return result;
  });
  return { version: 1, method: 'current-source-exact-quote-and-native-reading', semanticVerified: false,
    anchored: results.filter(row => row.status === 'anchored').length, total: results.length, claims: results,
    scope: 'Text sources only. Verifies quotation location, source version and recorded reading. Optional artifact checks locate the verbatim claim in a saved managed document. Support, contradiction and inference remain caller assessments. This check adds no reading coverage.' };
}
