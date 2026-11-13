import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { gunzipSync } from 'node:zlib';
import { BaseChatModel } from '@langchain/core/language_models/chat_models';
import { AIMessage } from '@langchain/core/messages';
import { ResearchKernel, monitoredFetch } from '../kernel.mjs';
import { StepSchema } from '../src/contracts.ts';
import { selectNativeTools, capabilityInstructions } from '../src/capabilities.ts';

class FixtureModel extends BaseChatModel {
  constructor(script){super({});this.script=script;this.calls=0;}
  _llmType(){return 'foundation-offline-model';}bindTools(){return this;}
  async _generate(messages){const value=await this.script(messages,this.calls++);return {generations:[{text:typeof value.content==='string'?value.content:'',message:new AIMessage(value)}]};}
}
test('actual v0.13 interrupt migrates through public checkpoint operations and finishes without rereading',async()=>{
  const fixture=JSON.parse(await fs.readFile(new URL('./fixtures/v013-pending.json',import.meta.url),'utf8'));let received='';
  const model=new FixtureModel(messages=>{received=JSON.stringify(messages);return {content:'旧任务已读到真实结果。'};});
  const kernel=new ResearchKernel({snapshot:fixture.result.snapshot,modelFactory:()=>model});
  const done=await kernel.step({...fixture.input,messages:[],resume:{[fixture.result.calls[0].interruptId]:{status:'done',text:'legacy-native-receipt'}}});
  assert.equal(done.kind,'final');assert.equal(model.calls,1);assert.match(received,/legacy-native-receipt/);assert.equal(done.snapshot.version,2);assert.equal(done.snapshot.nodeMajor,undefined);
  const saved=JSON.parse(gunzipSync(Buffer.from(done.snapshot.data,'base64')).toString('utf8'));assert.equal(saved.version,1);assert(saved.operations.some(op=>op.kind==='put'));assert(!Object.hasOwn(saved,'storage'));
  const continued=new ResearchKernel({snapshot:done.snapshot,modelFactory:()=>new FixtureModel(()=>({content:'跨进程继续成功。'}))});
  assert.equal((await continued.step({...fixture.input,messages:[{role:'user',content:'继续。'}]})).kind,'final');
});
test('typed protocol rejects incompatible versions and malformed task contracts before model invocation',()=>{
  const base={taskId:'typed',systemPrompt:'Goal',tools:[],messages:[]};
  assert.throws(()=>StepSchema.parse({...base,protocolVersion:99}));
  assert.throws(()=>StepSchema.parse({...base,taskSpec:{version:1,goal:'wrong'}}));
  assert.equal(StepSchema.parse({...base,protocolVersion:1}).taskId,'typed');
});
test('tool selection preserves every granted capability group while excluding formal and delegation controls',()=>{
  const names=['create_document','create_presentation','start_command_job','mcp_call','finish_task','delegate_parallel'];
  const tools=names.map(name=>({type:'function',function:{name,description:name,parameters:{type:'object',properties:{}}}}));
  assert.deepEqual(selectNativeTools(tools).map(t=>t.function.name),names.slice(0,4));
  const coding=capabilityInstructions(tools.filter(t=>t.function.name==='start_command_job'));assert.match(coding,/actual exit codes/);assert(!coding.includes('[music]'));
});
test('denied host permission prevents dispatch entirely',async()=>{
  let dispatched=0;
  const fetcher=monitoredFetch({endpoint:'https://configured.invalid/v1',pending:[],emit:()=>{},permit:async()=>{throw Error('cap');},fetchImpl:async()=>{dispatched++;return new Response('{}');}});
  await assert.rejects(fetcher('https://configured.invalid/v1/chat/completions',{body:'{}'}),/cap/);assert.equal(dispatched,0);
});
