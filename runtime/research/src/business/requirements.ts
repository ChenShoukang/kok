import type { Contract } from './planning.ts';
import type { RequirementUpdate } from './requirement-schema.ts';
export type { RequirementUpdate } from './requirement-schema.ts';
export function applyRequirementChanges(current: Contract, changes: RequirementUpdate['changes']): Contract {
  const result = { ...current, RequiredSourceIds: [...current.RequiredSourceIds], RequiredSections: [...current.RequiredSections] };
  if (changes.purpose !== undefined) result.Purpose = changes.purpose;
  if (changes.audience !== undefined) result.Audience = changes.audience;
  if (changes.document?.required !== undefined) { result.RequiresDocument = changes.document.required; if (changes.document.required) result.ChatOnly = false; }
  if (changes.document?.format !== undefined) result.RequiredDocumentFormat = changes.document.format;
  if (changes.document?.sections !== undefined) result.RequiredSections = [...new Set(changes.document.sections)];
  if (changes.presentation?.required !== undefined) { result.RequiresPresentation = changes.presentation.required; if (changes.presentation.required) result.ChatOnly = false; }
  if (changes.presentation?.pages !== undefined) result.RequestedPages = changes.presentation.pages;
  if (changes.length) { result.MinLength = changes.length.min; result.MaxLength = changes.length.max; result.LengthUnit = ['none', 'hanzi', 'characters', 'words'].indexOf(changes.length.unit); }
  if (changes.targetedEdit !== undefined) result.RequiresTargetedEdit = changes.targetedEdit;
  if (changes.fullRead !== undefined) result.RequiresFullSourceCoverage = changes.fullRead;
  if (changes.sourceIds) result.RequiredSourceIds = [...new Set(changes.sourceIds)];
  if (changes.verifyCode !== undefined) result.CodeVerification = changes.verifyCode;
  if (changes.chatOnly !== undefined) result.ChatOnly = changes.chatOnly;
  if (result.ChatOnly) { result.RequiresDocument = false; result.RequiresPresentation = false; result.RequiresTargetedEdit = false; }
  if (!result.RequiresPresentation) result.RequestedPages = 0;
  if (!result.RequiresDocument) { result.RequiredDocumentFormat = ''; result.RequiredSections = []; result.RequiredCount = 1; if (!result.ChatOnly) { result.MinLength = 0; result.MaxLength = 0; result.LengthUnit = 0; } }
  return result;
}
// A model interpretation may fill gaps or add checks; it cannot silently remove an already
// host-verifiable obligation. Explicit user changes are first replayed by the existing fast path.
export function preserveAcceptance(before: Contract, after: Contract) {
  if (before.ChatOnly && !after.ChatOnly || before.CodeVerification !== null && after.CodeVerification !== before.CodeVerification) throw new Error('This update conflicts with an explicit chat-only or code-verification directive; ask the user to clarify the change');
  if (before.RequiresDocument && !after.RequiresDocument || before.RequiresPresentation && !after.RequiresPresentation || before.RequiresTargetedEdit && !after.RequiresTargetedEdit || before.RequiresFullSourceCoverage && !after.RequiresFullSourceCoverage) throw new Error('This update would remove an active delivery/read/edit requirement; ask the user to clarify the change');
  if (before.RequiredDocumentFormat && after.RequiredDocumentFormat !== before.RequiredDocumentFormat || before.RequestedPages && after.RequestedPages !== before.RequestedPages) throw new Error('This update conflicts with the current explicit format/page requirement');
  if (before.MinLength && after.MinLength < before.MinLength || before.MaxLength && (!after.MaxLength || after.MaxLength > before.MaxLength) || (before.MinLength || before.MaxLength) && after.LengthUnit !== before.LengthUnit) throw new Error('This update would relax an active measured-length requirement');
  if (before.RequiredSections.some(section => !after.RequiredSections.includes(section)) || before.RequiresFullSourceCoverage && before.RequiredSourceIds.some(id => !after.RequiredSourceIds.includes(id))) throw new Error('This update would remove a required section or source');
}
// Shared with the existing code acceptance rules; no second vocabulary for the same directives.
export function verificationDirective(raw: string, positive: string): boolean | undefined {
  if (/(?:不要|无需|不必|不用)[^。；;，,\n]{0,30}(?:测试|运行|编译|验证)|do not run|skip tests/i.test(raw)) return false;
  if (/运行|执行|重跑|编译|构建|验证|测试|\b(?:run|rerun|execute|build|verify|test|check)\b/i.test(positive)) return true;
  return undefined;
}
