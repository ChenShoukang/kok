'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 let browser,page,sourceId,deckId,target,firstSlide;const bodies=[],errors=[];
 const output=path.resolve('test-output/agent-reviews');fs.mkdirSync(output,{recursive:true});
 const slides=[{title:'最终活动方案',layout:'cover',bullets:['采用8万元预算'],notes:'依据最终修订资料。'},{title:'执行与风险',layout:'content',bullets:['不收集手机号','具体日期待确认'],notes:'保留未知信息。'}];
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const body=JSON.parse(raw);bodies.push(body);
  const receipts=body.messages.filter(m=>m.role==='tool').map(m=>{try{return JSON.parse(m.content).result;}catch{return null;}}).filter(Boolean);
  deckId=receipts.findLast(r=>r.artifactReview?.kind==='presentation'&&r.id)?.id||deckId;
  const deckRead=receipts.findLast(r=>r.total_pages===2);if(deckRead){const deck=JSON.parse(deckRead.text);firstSlide??=deck.slides[0];target??=deck.slides[1].shapes.find(s=>s.text==='不收集手机号');}
  let calls=[];const n=bodies.length;
  if(n===1)calls=[{name:'review_sources',args:{ids:[sourceId]}},{name:'inspect_document',args:{id:sourceId,required_sections:['风险']}}];
  if(n===2)calls=[{name:'read_items',args:{ids:[sourceId],max_chars:12000}},{name:'create_presentation',args:{title:'最终活动方案',slides}}];
  if(n===3)calls=[{name:'read_presentation',args:{id:deckId,max_pages:2}}];
  if(n===4)calls=[{name:'edit_presentation',args:{id:deckId,edits:[{action:'bounds',page:2,shape_id:target.id,x:20,y:target.y,w:target.w,h:target.h}]}}];
  if(n===5)calls=[{name:'inspect_presentation',args:{id:deckId}}];
  if(n===6)calls=[{name:'edit_presentation',args:{id:deckId,edits:[{action:'bounds',page:2,shape_id:target.id,x:target.x,y:target.y,w:target.w,h:target.h},{action:'font_size',page:2,shape_id:target.id,font_size:24}]}}];
  if(n===7)calls=[{name:'inspect_presentation',args:{id:deckId}},{name:'read_presentation',args:{id:deckId,max_pages:2}},{name:'review_sources',args:{ids:[sourceId]}}];
  const delta=calls.length?{role:'assistant',tool_calls:calls.map((call,index)=>({index,id:'review-'+randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}))}:{role:'assistant',content:'2页PPT已保存，第二页指定文本框已修复并回读，其他页面保持。结构检查通过；未做事实和视觉审核。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:randomUUID(),object:'chat.completion.chunk',model:'review-fixture',choices:[{index:0,delta,finish_reason:calls.length?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:60}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(30000);page.on('pageerror',error=>errors.push(error.message));await page.waitForFunction(()=>!$('#startup'));
  sourceId=await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'review-fixture',clearKey:true,automatic:false});const source=await native('createDocument',{name:'最终修订资料',format:'md',text:'# 活动方案\n最终预算8万元，不收集手机号，具体日期待确认。'});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'selected',context:[source.id],requestLimit:12});AgentUi.render();return source.id;},'http://127.0.0.1:'+model.address().port+'/v1');
  await page.locator('#agent-input').fill('根据所选资料制作2页PPT，检查资料文档和排版，调整第2页文本框位置与字号，读回全部页面。');await page.locator('.agent-send').click();
  for(let approval=0;approval<3;approval++){
   await page.waitForSelector('[data-agent-run]');
   if(approval===2){const task=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));const report=task.messages.findLast(m=>m.tool==='inspect_presentation'&&m.status==='done').result.artifactReview;assert(report.findings.some(row=>row.code==='outside_canvas'&&row.page===2&&row.shapeId===target.id));assert.match(await page.locator('[data-artifact-review]').last().textContent(),/结构检查不等于事实与视觉审核/);}
   await page.locator('[data-agent-run]').click();
  }
  await page.waitForFunction(()=>!AgentUi.state.job);const task=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));
  assert.equal(task.status,'completed',task.error);assert.equal(task.options.businessVersion,1);assert.equal(bodies.length,8);
  const reviews=task.messages.filter(m=>m.tool==='review_sources').map(m=>m.result.sourceReview[0]);assert.equal(reviews[0].status,'unread');assert.equal(reviews[0].parsed,true);assert.equal(reviews[0].processed,false);assert.equal(reviews.at(-1).status,'complete');assert.equal(reviews.at(-1).citationStatus,'not_semantically_verified');
  const reports=task.messages.filter(m=>m.tool==='inspect_presentation').map(m=>m.result.artifactReview);assert.equal(reports.at(-1).errors,0);assert.equal(reports.at(-1).visualReviewed,false);assert.equal(reports.at(-1).rendered,false);
  assert(task.messages.find(m=>m.tool==='inspect_document').result.artifactReview.findings.some(f=>f.code==='missing_section'));
  const saved=await page.evaluate(id=>native('readPresentation',{id}),deckId);assert.equal(saved.slides.length,2);const comparable=shape=>Object.fromEntries(Object.keys(firstSlide.shapes[0]).filter(key=>key!=='truncated').map(key=>[key,shape[key]]));assert.deepEqual(saved.slides[0].shapes.map(comparable),firstSlide.shapes.map(comparable));assert(saved.slides[1].shapes.some(s=>s.id===target.id&&s.text===target.text&&s.x===target.x&&s.fontSize===24));
  assert(fs.existsSync((await page.evaluate(id=>byId(id),deckId)).path));assert.equal(await page.evaluate(()=>state.items.filter(i=>i.ext==='PPTX').length),1);assert.deepEqual(errors,[]);
  await page.screenshot({path:path.join(output,'review-result.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:bodies.length,businessVersion:1,sourceStatuses:reviews.map(r=>r.status),repairedObject:target.id,originalPagePreserved:true,visualReviewed:false},null,2));
  console.log('Review desktop: TypeScript planning, parsed versus processed sources, native document checks, exact PPT defect repair and saved-page read-back passed.');
 }catch(error){if(page){await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});const task=await page.evaluate(()=>AgentUi.state.watch?native('agentTaskRead',{taskId:AgentUi.state.watch}):null).catch(()=>null);fs.writeFileSync(path.join(output,'failure.json'),JSON.stringify({bodies,task},null,2));}throw error;}finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(resolve=>model.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
