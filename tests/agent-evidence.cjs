'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 let browser,page,sourceId,reportId;const bodies=[],errors=[],output=path.resolve('test-output/agent-evidence');fs.mkdirSync(output,{recursive:true});
 const quote='最终预算8万元，60人，不收集手机号。',claim='采用最终预算8万元，60人，不收集手机号。';
 const draft='# 活动报告\n## 目标\n按最终修订开展社区阅读活动。\n## 流程\n准备、实施、复盘，其他内容保留。\n## 风险\n## 待确认事项\n日期和地点仍未确认。';
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const body=JSON.parse(raw);bodies.push(body);const receipts=body.messages.filter(m=>m.role==='tool').map(m=>{try{return JSON.parse(m.content).result;}catch{return null;}}).filter(Boolean);
  reportId=receipts.findLast(r=>r.id&&r.artifactReview?.kind==='document')?.id||reportId;
  let calls=[];const n=bodies.length;
  if(n===1)calls=[{name:'review_claims',args:{claims:[{claim,sourceId,quote,relationship:'supports'}]}}];
  if(n===2)calls=[{name:'read_items',args:{ids:[sourceId],max_chars:12000}},{name:'create_document',args:{name:'章节检查报告',format:'docx',text:draft}}];
  if(n===4)calls=[{name:'replace_document_text',args:{id:reportId,old_text:'## 风险\n',new_text:'## 风险\n'+claim+'\n'}}];
  if(n===5)calls=[{name:'review_claims',args:{artifact_id:reportId,claims:[{claim,sourceId,quote,relationship:'supports'},{claim,sourceId:reportId,quote:claim,relationship:'supports'}]}}];
  const delta=calls.length?{role:'assistant',tool_calls:calls.map((call,index)=>({index,id:'evidence-'+randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}))}:{role:'assistant',content:'报告已保存，所有章节均已完成，已核对来源。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:randomUUID(),object:'chat.completion.chunk',model:'evidence-fixture',choices:[{index:0,delta,finish_reason:calls.length?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:60}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(30000);page.on('pageerror',error=>errors.push(error.message));await page.waitForFunction(()=>!$('#startup'));
  sourceId=await page.evaluate(async({endpoint,quote})=>{await native('saveAiConfig',{endpoint,model:'evidence-fixture',clearKey:true,automatic:false});const source=await native('createDocument',{name:'最终修订资料',format:'md',text:'# 最终修订\n'+quote+'\n日期和地点待确认。'});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'selected',context:[source.id],requestLimit:9});AgentUi.render();return source.id;},{endpoint:'http://127.0.0.1:'+model.address().port+'/v1',quote});
  await page.locator('#agent-input').fill('根据所选资料创建一份DOCX报告，报告应包含目标、流程、风险和待确认事项。完整阅读资料并核对结论，保留已有内容。');await page.locator('.agent-send').click();
  await page.waitForSelector('[data-agent-run]');await page.locator('[data-agent-run]').click();
  await page.waitForSelector('[data-agent-run]');const waiting=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));
  const broken=waiting.messages.find(m=>m.tool==='create_document'&&m.status==='done').result.artifactReview;assert.equal(broken.errors,1);assert(broken.sectionChecks.some(row=>row.title==='风险'&&row.status==='empty'));
  assert.equal(waiting.messages.filter(m=>m.tool==='create_document').length,1);assert(bodies[3].messages.some(m=>m.content?.includes?.('章节缺少正文')),'Host must reject the premature final answer with the exact section gap');
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  const task=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));assert.equal(task.status,'completed',task.error);assert.equal(task.options.artifactReviewVersion,3);assert.equal(bodies.length,6);
  const reviews=task.messages.filter(m=>m.tool==='review_claims'&&m.status==='done').map(m=>m.result.claimReview);assert.equal(reviews[0].claims[0].status,'unread');assert.equal(reviews[1].claims[0].status,'anchored');assert.equal(reviews[1].claims[0].artifactStatus,'located');assert.equal(reviews[1].semanticVerified,false);
  const ui=await page.locator('[data-claim-review]').last().textContent();assert.match(ui,/原文已定位且已读/);assert.match(ui,/仍是模型判断/);assert.match(ui,/结论已在该版成品中定位/);assert.match(ui,/此来源为成品，仍需核对原始资料依据/);assert.equal(reviews[1].claims[1].sourceRole,'artifact');
  const saved=await page.evaluate(id=>native('documentSource',{id}),reportId);assert.equal(saved.text,draft.replace('## 风险\n','## 风险\n'+claim+'\n'));assert.equal(await page.evaluate(()=>state.items.filter(i=>i.ext==='DOCX').length),1);assert.deepEqual(errors,[]);
  await page.screenshot({path:path.join(output,'evidence-result.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:bodies.length,sourceStatuses:reviews.map(r=>r.claims[0].status),savedReportLocated:true,emptySectionRepaired:true,otherTextPreserved:true,semanticVerified:false},null,2));
  console.log('Evidence desktop: real source/read anchors, saved DOCX claims, host rejection of empty sections, exact repair and preserved report text passed.');
 }catch(error){if(page){await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});const task=await page.evaluate(()=>AgentUi.state.watch?native('agentTaskRead',{taskId:AgentUi.state.watch}):null).catch(()=>null);fs.writeFileSync(path.join(output,'failure.json'),JSON.stringify({bodies,task},null,2));}throw error;}finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(resolve=>model.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
