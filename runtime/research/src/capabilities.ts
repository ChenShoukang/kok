import type { NativeTool, TaskSpec } from './contracts.ts';

const groups: Record<string, string[]> = {
  presentation: ['create_presentation', 'read_presentation', 'edit_presentation', 'copy_presentation', 'design_presentation', 'inspect_presentation'],
  coding: ['workspace_info', 'search_workspace', 'read_workspace_file', 'git_workspace', 'apply_workspace_patch', 'list_folder', 'read_text_file', 'write_text_file', 'run_command', 'start_command_job', 'read_command_job', 'wait_command_job', 'cancel_command_job'],
  computer: ['open_browser', 'read_browser', 'browser_action'],
  research: ['search_library', 'search_items', 'read_items', 'read_document', 'web_search', 'fetch_url', 'index_pdf_pages'],
  video: ['index_video_frames'],
};
const guidance: Record<string, string> = {
  research: 'Use actual sources, follow continuation offsets when full reading is required, and separate facts, inference and unknowns.',
  document: 'Create an actual editable native document. Use hostReadBacks directly when they cover the current saved version.',
  presentation: 'Create/edit an actual PPTX. Preserve other slides and object identity; text-only outlines do not satisfy a requested deck.',
  coding: 'Work only in the configured workspace. Preserve existing user changes. Use paged reads and exact SHA-256 guarded patches. Preserve existing changes. Run applicable checks and report actual exit codes. For verification declare changed source files in command job inputs; a successful check only proves those exact versions. Long commands may use approved command jobs; wait for their result before claiming success.',
  image: 'Use an actual image executor when available. Describe unavailable generation/editing abilities honestly; do not substitute an editing plan for pixels.',
  video: 'A requested video needs an actual playable output; scripts and frame indexes are intermediate evidence, not a rendered video.',
  music: 'A requested audio/music deliverable needs playable sound from an available engine; lyrics, instructions and unrendered MIDI are intermediate work.',
  spreadsheet: 'Use verified data and actual computations. Do not invent spreadsheet values or claim a workbook was saved without a real tool receipt.',
  computer: 'Local browser tools only verify explicitly approved loopback HTTP apps, with a fresh pageVersion and control IDs. They cannot operate remote websites or the desktop. Operate only through available authorized application/browser tools, verify the resulting state, and never claim clicks or external submissions happened without a receipt.',
};
export function toolCapability(name: string): string | undefined { return Object.keys(groups).find(group => groups[group].includes(name)); }
export function selectNativeTools(tools: NativeTool[]): NativeTool[] {
  // The native scope/profile is authoritative. Never silently remove an explicitly granted tool group.
  return tools.filter(tool => !['update_plan', 'finish_task', 'delegate_task', 'delegate_parallel'].includes(tool.function.name));
}
export function capabilityInstructions(tools: NativeTool[], spec?: TaskSpec): string {
  const active = new Set(spec?.capabilities ?? tools.map(t => toolCapability(t.function.name)).filter((id): id is string => !!id));
  return [...active].map(id => guidance[id] ? '[' + id + '] ' + guidance[id] : '').filter(Boolean).join('\n');
}
