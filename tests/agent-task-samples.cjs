'use strict';
// 30-task measurement baseline for the kok Agent. Plain CommonJS module, no app, no network, no files.
// It defines realistic user requests derived from kok's actual uses (library curation, documents/PDFs,
// video subtitles, comparisons, notes/presentations, counting/filtering, tag/collection upkeep, follow-up edits).
// tests/agent-live-samples.cjs consumes the same list when a live run is explicitly authorized.
//
// Local samples must be answerable by the native zero-model router (agentLocalPlan): explicit Chinese verb
// + explicit constraint, and one of its supported intents (count/list/collections/tags/kinds).
// Everything here is synthetic; fixture notes never reference real files or personal data.
const METRICS=Object.freeze(['inputTokens','outputTokens','cachedTokens','cost','durationMs','requests','retries','duplicateReads','opened','citationIdsResolve','novice']);
const NOVICE_CHECKS=Object.freeze(['startsWithoutHelp','confirmsOnlyWhenNeeded','findsResult']);
const CATEGORIES=Object.freeze(['local','generation','qa','complex','needs-input']);
const ROUTES=Object.freeze(['local','model']);
const LOCAL_KINDS=Object.freeze(['count','list','collections','tags','kinds']);
const FIXTURE_KINDS=Object.freeze(['documents','library','video-subs','pdf']);
const ARTIFACT_EXTS=Object.freeze(['md','docx','txt','pptx']);
const EXPECTED_DISTRIBUTION=Object.freeze({local:8,generation:10,qa:7,complex:3,'needs-input':2});
// Explicit verb + explicit constraint per supported local intent. These lists mirror the vocabulary of
// src/AgentLocalTasks.cs (CountWords / ListWords / CollectionIntent / TagIntent / KindIntent) so the
// validator can prove that local samples are answerable by the native router without a model call.
// Keep them in sync when the router vocabulary changes; the live harness reports the actual route anyway.
const LOCAL_ROUTER_PATTERNS=Object.freeze({
 count:{verb:/(统计|数一下|数数|算一下|数量|多少|几份|几个|几篇|几首|几张|几段|几本|几部|一共|总共|合计|共计|计数)/,constraint:/(视频|影片|电影|录像|文档|文稿|图片|照片|相片|图像|音乐|音频|歌曲|电子书|图书|书籍|小说|PDF|pdf|PPTX|ppt|演示文稿|幻灯片|DOCX|doc|word|XLSX|excel|表格|CSV|csv|TXT|txt|EPUB|epub|Markdown|markdown|HTML|html|JSON|json|分类|标签|「[^」]+」|最近\s*[0-9一二两三四五六七八九十]+\s*[天日]|[0-9一二两三四五六七八九十]+\s*[天日]\s*(?:内|以内)|(?:最近|近|过去|这|本|上)\s*(?:一|1)?\s*个?\s*(?:周|星期|礼拜|月)|今天|昨天|大于\s*[0-9]+\s*(?:TB|GB|MB|KB|B)|收藏)/i},
 list:{verb:/(找出|列出|列举|罗列|显示|查看|筛选|过滤|找一下|列出来|清单|列表|有哪些|哪些|哪个|哪几|给我看|看看)/,constraint:/(视频|影片|电影|录像|文档|文稿|图片|照片|相片|图像|音乐|音频|歌曲|电子书|图书|书籍|小说|PDF|pdf|分类|标签|「[^」]+」|资料|最近\s*[0-9一二两三四五六七八九十]+\s*[天日]|今天|昨天|大于\s*[0-9]+\s*(?:GB|MB|KB|B)|收藏)/i},
 collections:{verb:/(分类列表|有哪些分类|分类有哪些|哪些分类|每个分类|各分类|分类分布|分类统计|分类数量|分类总数|多少个分类|几个分类|全部分类|所有分类)/,constraint:/(有哪些|每个|各自|分别|多少|列表|数量)/},
 tags:{verb:/(有哪些标签|标签列表|标签有哪些|哪些标签|每个标签|各标签|标签分布|标签统计|标签数量|多少个标签|几个标签|所有标签|全部标签|标签云)/,constraint:/(标签|#|＃|数量|多少|列表)/},
 kinds:{verb:/(各类|各类型|类型分布|文件类型|文件种类|类型统计|类型数量|按类型|每种|每一种|多少种|哪些类型|几种类型|资料库构成|库构成)/,constraint:/(数量|多少|文件|资料|构成|分布)/}
});
// The router refuses generative/write wording before parsing intents (RejectZh in src/AgentLocalTasks.cs).
// Local samples must stay outside this vocabulary or they intentionally fall back to the model path.
const ROUTER_REJECT_MARKERS=/(总结|概括|归纳|摘要|提炼|提取|分析|观点|看法|建议|推荐|规划|计划|整理|生成|制作|做一份|做一个|做成|写一份|写一个|撰写|编写|创建|新建|修改|改成|改为|重命名|删除|删掉|移除|移动|合并|拆分|翻译|润色|改写|续写|比较|对比|区别|差异|共同|异同|为什么|怎么|如何|讲什么|讲了什么|说了什么|主要内容|什么意思|主题|思路|教程|方案|步骤|分类建议|标签建议|帮我写|帮我做)/;

function sample(row){
 const acceptance=row.acceptance||{};
 // v0.12.1 #29: optional literal content rules. facts must appear in the answer/deliverable; forbidden
 // strings must not. They are bounded string checks, never a semantic judge of source support.
 const facts=Array.isArray(acceptance.facts)?Object.freeze(acceptance.facts.map(String)):undefined;
 const forbidden=Array.isArray(acceptance.forbidden)?Object.freeze(acceptance.forbidden.map(String)):undefined;
 const value={
  id:row.id,
  title:row.title,
  goal:row.goal,
  category:row.category,
  expectedRoute:row.expectedRoute,
  fixture:Object.freeze({kind:row.fixture.kind,notes:row.fixture.notes}),
  acceptance:Object.freeze({
   resultPresent:acceptance.resultPresent!==false,
   artifactOpenable:!!acceptance.artifactOpenable,
   citationsRequired:!!acceptance.citationsRequired,
   noUnsupportedClaims:acceptance.noUnsupportedClaims!==false,
   ...(acceptance.expectedArtifactExt?{expectedArtifactExt:acceptance.expectedArtifactExt}:{}),
   ...(facts?{facts}:{}),
   ...(forbidden?{forbidden}:{})
  }),
  metrics:METRICS,
  noviceChecks:NOVICE_CHECKS
 };
 if(row.expectedLocalKind)value.expectedLocalKind=row.expectedLocalKind;
 return Object.freeze(value);
}

const RAW=[
 // ---------------------------------------------------------------- local: 8 deterministic, zero model calls
 {id:'local-count-videos',title:'统计资料库视频数量',category:'local',expectedRoute:'local',expectedLocalKind:'count',
  goal:'统计一下资料库里一共有多少个视频文件，只回答数量即可。',
  fixture:{kind:'library',notes:'隔离资料库导入 kok 自带原创示例（含 1 个 webm 视频、同名 srt 字幕）和合成文档；只读统计，不修改任何资料。'},
  acceptance:{resultPresent:true}},
 {id:'local-count-pdf',title:'统计资料库 PDF 数量',category:'local',expectedRoute:'local',expectedLocalKind:'count',
  goal:'资料库里有多少份 PDF 文件？只统计数量并列出文件名。',
  fixture:{kind:'library',notes:'隔离资料库含 1 份原创示例 PDF 与若干合成文档；只读统计，不修改任何资料。'},
  acceptance:{resultPresent:true}},
 {id:'local-list-collection-travel',title:'列出旅行分类资料',category:'local',expectedRoute:'local',expectedLocalKind:'list',
  goal:'列出「评估·旅行」分类里的全部资料，给出名称和类型即可。',
  fixture:{kind:'library',notes:'合成分类「评估·旅行」含 3 份 Markdown 文档（旅行初稿、修订稿、摄影准备清单）；只读列表。'},
  acceptance:{resultPresent:true}},
 {id:'local-list-recent-three-days',title:'列出最近三天导入',category:'local',expectedRoute:'local',expectedLocalKind:'list',
  goal:'列出最近三天导入的资料，按导入时间说明名称即可。',
  fixture:{kind:'library',notes:'隔离资料库中的示例与合成文档均在本次会话导入；只读列表。'},
  acceptance:{resultPresent:true}},
 {id:'local-collections-overview',title:'查看分类及各分类数量',category:'local',expectedRoute:'local',expectedLocalKind:'collections',
  goal:'有哪些分类？各自有多少份资料？用列表说明。',
  fixture:{kind:'library',notes:'合成分类「评估·旅行」「评估·生活」「评估·学习」「评估·工作」等，外加示例资料默认分类；只读统计。'},
  acceptance:{resultPresent:true}},
 {id:'local-tags-design',title:'查找设计标签资料',category:'local',expectedRoute:'local',expectedLocalKind:'tags',
  goal:'有哪些标签？把标了 #设计 标签的资料列出来看看。',
  fixture:{kind:'library',notes:'隔离资料库中为 1 份合成文档预置 #设计 标签；只读筛选。'},
  acceptance:{resultPresent:true}},
 {id:'local-kinds-distribution',title:'统计各类文件数量',category:'local',expectedRoute:'local',expectedLocalKind:'kinds',
  goal:'统计资料库里各类文件的数量，比如视频、图片、文档、音乐各有多少，给出构成统计。',
  fixture:{kind:'library',notes:'隔离资料库含原创示例（视频、字幕、图片、音乐、PDF、DOCX、EPUB、Markdown）与合成文档，用于纯资料库构成统计。'},
  acceptance:{resultPresent:true}},
 {id:'local-count-collection-travel',title:'统计旅行分类数量',category:'local',expectedRoute:'local',expectedLocalKind:'count',
  goal:'统计「评估·旅行」分类里有多少份资料，只回答数量。',
  fixture:{kind:'library',notes:'合成分类「评估·旅行」含 3 份 Markdown 文档；只读统计。'},
  acceptance:{resultPresent:true}},

 // ---------------------------------------------------- generation: 10 summaries, notes, presentation, compare, proposals
 {id:'gen-summary-selected-docs',title:'总结所选文档要点',category:'generation',expectedRoute:'model',
  goal:'读取所选的两份旅行资料，用三条要点总结旅行计划，重点包含日期、目的地和预算，不要修改资料。',
  fixture:{kind:'documents',notes:'合成 Markdown：海边旅行初稿与修订稿，含日期、目的地、两版预算；仅用于总结。'},
  acceptance:{resultPresent:true,facts:['厦门','1200','1800']}},
 {id:'gen-pdf-notes',title:'PDF 阅读要点笔记',category:'generation',expectedRoute:'model',
  goal:'阅读这份 PDF，把要点整理成一篇新的 Markdown 笔记并保存到资料库。',
  fixture:{kind:'pdf',notes:'原创示例 PDF（kok 使用指南），页数少、可离线解析；另附合成文档作对照。'},
  acceptance:{resultPresent:true,artifactOpenable:true,expectedArtifactExt:'md'}},
 {id:'gen-video-subtitle-summary',title:'视频字幕摘要',category:'generation',expectedRoute:'model',
  goal:'根据这个视频的字幕整理 3 条摘要，并注明对应的时间点，不修改原文件。',
  fixture:{kind:'video-subs',notes:'原创示例 webm 视频与同名中文字幕 srt，导入后作为字幕来源；只读摘要。'},
  acceptance:{resultPresent:true}},
 {id:'gen-note-from-docs',title:'资料整理成笔记',category:'generation',expectedRoute:'model',
  goal:'把选中的两份资料整理成一篇 Markdown 笔记，包含日期、预算和准备清单，保存为资料库里的新文档。',
  fixture:{kind:'documents',notes:'合成 Markdown：旅行初稿与摄影准备清单，内容可合成笔记；原文件不得修改。'},
  acceptance:{resultPresent:true,artifactOpenable:true,citationsRequired:true,expectedArtifactExt:'md',facts:['厦门','相机']}},
 {id:'gen-presentation-five-pages',title:'生成五页演示文稿',category:'generation',expectedRoute:'model',
  goal:'用选中的资料做一份 5 页的演示文稿，主题是海边旅行计划，生成后保存到资料库。',
  fixture:{kind:'documents',notes:'合成 Markdown：旅行初稿、修订稿与摄影准备清单，可作为演示文稿素材。'},
  acceptance:{resultPresent:true,artifactOpenable:true,citationsRequired:true,expectedArtifactExt:'pptx',facts:['厦门']}},
 {id:'gen-compare-two-versions',title:'比较两版旅行计划',category:'generation',expectedRoute:'model',
  goal:'读取并比较初稿和修订稿两份资料，说明预算和日期的差异，引用两份来源，不修改文件。',
  fixture:{kind:'documents',notes:'合成 Markdown：修订稿预算 1800 元替代初稿 1200 元，日期一致；比较任务。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['1200','1800','600']}},
 {id:'gen-organize-recent-proposal',title:'建议整理最近资料',category:'generation',expectedRoute:'model',
  goal:'先读取最近导入的资料，告诉我可以怎么按主题分类和打标签；只提出建议，等我确认后再执行修改。',
  fixture:{kind:'library',notes:'隔离资料库中的原创示例与合成文档；允许提出修改建议，但确认前不得实际写入。'},
  acceptance:{resultPresent:true,citationsRequired:true}},
 {id:'gen-batch-tags-proposal',title:'批量添加标签建议',category:'generation',expectedRoute:'model',
  goal:'为选中的两份资料建议添加 #待读 和 #旅行 标签，先提出修改让我确认，不要直接改动。',
  fixture:{kind:'library',notes:'从资料库中选取 2 份合成文档作为批量元数据目标；先提方案，确认后才可写入并撤销。'},
  acceptance:{resultPresent:true}},
 {id:'gen-batch-collection-proposal',title:'批量移入分类建议',category:'generation',expectedRoute:'model',
  goal:'把选中的两份资料移入新分类「评估·归档」，先提出修改并说明影响，等待我确认。',
  fixture:{kind:'library',notes:'从资料库中选取 2 份合成文档作为批量分类目标；确认前分类不得改变。'},
  acceptance:{resultPresent:true}},
 {id:'gen-video-subtitle-note',title:'视频字幕时间点笔记',category:'generation',expectedRoute:'model',
  goal:'用这个视频的字幕生成一条带时间点的 Markdown 笔记并保存为资料库里的新文档。',
  fixture:{kind:'video-subs',notes:'原创示例 webm 视频与同名中文字幕 srt；生成新笔记，原视频与字幕文件不得修改。'},
  acceptance:{resultPresent:true,artifactOpenable:true,expectedArtifactExt:'md'}},

 // --------------------------------------------------------------------------- qa: 7 evidence questions with citations
 {id:'qa-travel-budget-unknown',title:'旅行预算与住宿问题',category:'qa',expectedRoute:'model',
  goal:'根据所选资料回答：这次旅行的总预算是多少？住宿费用确定了吗？请引用你读到的来源，不确定的地方直接说明。',
  fixture:{kind:'documents',notes:'合成 Markdown：旅行初稿写预算 1200 元、住宿费用未提供；回答必须引用已读来源。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['1200'],forbidden:['1800']}},
 {id:'qa-pdf-second-page',title:'PDF 内容与页码引用',category:'qa',expectedRoute:'model',
  goal:'这份 PDF 第 2 页讲了什么？只根据 PDF 内容回答，并指出信息在 PDF 中的页码位置。',
  fixture:{kind:'pdf',notes:'原创示例 PDF（kok 使用指南）页数少、文本可提取；回答需引用该 PDF 来源并给页码锚点。'},
  acceptance:{resultPresent:true,citationsRequired:true}},
 {id:'qa-compare-version-budget',title:'两版预算差异问答',category:'qa',expectedRoute:'model',
  goal:'初稿和修订稿的预算分别是多少？差额是多少？请分别引用两份来源，不要合并成一个数字。',
  fixture:{kind:'documents',notes:'合成初稿（1200 元）与修订稿（1800 元），差额 600 元；两份来源必须分别引用。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['1200','1800','600'],forbidden:['500元']}},
 {id:'qa-plant-watering',title:'植物记录问答',category:'qa',expectedRoute:'model',
  goal:'根据植物记录回答：阳台有几盆植物？多久浇一次水？只依据资料内容，引用来源。',
  fixture:{kind:'documents',notes:'合成 Markdown：植物记录写 2 盆植物、每 7 天浇水一次；不得补写资料外信息。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['2盆','7天'],forbidden:['3盆','8天']}},
 {id:'qa-release-checklist',title:'发布检查清单问答',category:'qa',expectedRoute:'model',
  goal:'根据所选资料，回答发布前需要完成的检查项有哪些，逐条列出并引用对应的资料。',
  fixture:{kind:'documents',notes:'原创示例 DOCX 与合成发布检查清单 Markdown，列出若干检查项；回答需引用来源。'},
  acceptance:{resultPresent:true,citationsRequired:true}},
 {id:'qa-reading-plan-dates',title:'阅读计划日期问答',category:'qa',expectedRoute:'model',
  goal:'从所选资料里找出阅读计划的关键日期和对应任务，逐条列出并引用来源。',
  fixture:{kind:'documents',notes:'合成阅读计划 Markdown，含日期与任务对应关系；回答需逐条引用来源。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['11月8日','11月15日']}},
 {id:'qa-travel-preparation',title:'旅行准备清单问答',category:'qa',expectedRoute:'model',
  goal:'根据摄影准备清单和旅行计划，回答需要带哪些摄影装备；资料没有提到的信息要说明。',
  fixture:{kind:'documents',notes:'合成摄影准备清单（相机、电池、存储卡等）与旅行计划；回答需引用已读来源并标出未知项。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['电池']}},

 // ------------------------------------------------------------------------ complex: 3 multi-step / delegated long tasks
 {id:'complex-delegation-two-docs',title:'委派子任务汇总两份资料',category:'complex',expectedRoute:'model',
  goal:'把读取并总结两份资料的工作委派给一个只读子任务，你根据子任务报告给出结论并核对来源，不修改资料。',
  fixture:{kind:'documents',notes:'合成旅行初稿与修订稿；委派只读子任务，父任务需核对子任务来源。'},
  acceptance:{resultPresent:true,citationsRequired:true,facts:['厦门']}},
 {id:'complex-long-task-note-followup',title:'长任务：总结后写笔记并读回核对',category:'complex',expectedRoute:'model',
  goal:'分步完成：先阅读所选两份资料并总结，再新建一篇 Markdown 笔记写入总结，最后读回笔记核对内容完整后结束。不要修改原始资料。',
  fixture:{kind:'documents',notes:'合成旅行初稿与修订稿；需要计划、写入、读回核对的多步长任务，原文件保持不变。'},
  acceptance:{resultPresent:true,artifactOpenable:true,citationsRequired:true,expectedArtifactExt:'md',facts:['厦门']}},
 {id:'complex-pdf-video-presentation',title:'PDF 与视频字幕综合演示',category:'complex',expectedRoute:'model',
  goal:'综合这份 PDF 和示例视频的字幕，先列出计划，再制作一份 3 页的演示文稿并保存；引用两类来源。',
  fixture:{kind:'pdf',notes:'原创示例 PDF 与同目录示例 webm 视频及中文字幕 srt；跨来源制作演示文稿，长任务。'},
  acceptance:{resultPresent:true,artifactOpenable:true,citationsRequired:true,expectedArtifactExt:'pptx'}},

 // ------------------------------------------------------------------ needs-input: 2 honest missing-evidence cases
 {id:'needs-hotel-price',title:'询问未提供的酒店价格',category:'needs-input',expectedRoute:'model',
  goal:'资料里写了酒店名称和每晚价格吗？如果资料没有提供，请明确说没有，不要联网猜测，并以需要补充信息结束。',
  fixture:{kind:'documents',notes:'合成 Markdown 明确写“酒店名称和住宿价格未提供”；模型不得编造酒店名或价格。'},
  acceptance:{resultPresent:true,forbidden:['每晚价格为','元/晚']}},
 {id:'needs-pdf-contact-info',title:'询问 PDF 未包含的联系方式',category:'needs-input',expectedRoute:'model',
  goal:'这份 PDF 里有没有作者的联系方式或补充数据表格？如果没有，请直接说明缺少信息并以需要补充信息结束，不要编造。',
  fixture:{kind:'pdf',notes:'原创示例 PDF 不含作者联系方式或数据表格；模型应如实说明缺失并结束为需要补充信息。'},
  acceptance:{resultPresent:true,forbidden:['联系方式：','邮箱：']}}
];

const SAMPLES=Object.freeze(RAW.map(sample));

// Coverage requirements from the task request. Derived from titles/goals/fixture metadata so the table
// stays the single source of truth; the validator fails if a category coverage requirement is missing.
const COVERAGE_RULES=Object.freeze([
 {name:'PDF 任务',min:2,test:s=>/PDF/i.test(s.title+' '+s.goal)||s.fixture.kind==='pdf'},
 {name:'视频 / 字幕任务',min:2,test:s=>/视频|字幕/.test(s.title+' '+s.goal)||s.fixture.kind==='video-subs'},
 {name:'多文件对比',min:2,test:s=>/对比|比较|差异/.test(s.title+' '+s.goal)},
 {name:'批量元数据',min:2,test:s=>/批量/.test(s.title)&&/标签|分类/.test(s.title+' '+s.goal)},
 {name:'纯资料库统计',min:1,test:s=>s.expectedLocalKind==='kinds'}
]);

module.exports=Object.freeze({version:1,samples:SAMPLES});

// ---------------------------------------------------------------------------------------------- offline validator
if(require.main===module){
 const assert=require('node:assert/strict');
 const pad=(value,width)=>String(value).padEnd(width);
 const fail=message=>{throw new Error('Sample set invalid: '+message);};
 const count=(list,predicate)=>list.filter(predicate).length;

 assert.equal(module.exports.version,1,'version must be 1');
 assert.ok(Array.isArray(SAMPLES),'samples must be an array');
 assert.equal(SAMPLES.length,30,'exactly 30 samples are required');

 const ids=new Set();
 const byCategory={};
 const byRoute={local:0,model:0};
 for(const s of SAMPLES){
  assert.ok(/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(s.id),'id must be kebab-case: '+s.id);
  if(ids.has(s.id))fail('duplicate id '+s.id);ids.add(s.id);
  assert.ok(typeof s.title==='string'&&s.title.trim().length>0&&s.title.length<=80,'title is required and <=80 chars: '+s.id);
  assert.ok(typeof s.goal==='string'&&s.goal.trim().length>0&&s.goal.length<=2000,'goal is required and <=2000 chars: '+s.id);
  assert.ok(CATEGORIES.includes(s.category),'invalid category on '+s.id);
  assert.ok(ROUTES.includes(s.expectedRoute),'invalid expectedRoute on '+s.id);
  byCategory[s.category]=(byCategory[s.category]||0)+1;byRoute[s.expectedRoute]++;
  assert.ok(FIXTURE_KINDS.includes(s.fixture?.kind),'invalid fixture kind on '+s.id);
  assert.ok(typeof s.fixture.notes==='string'&&s.fixture.notes.trim().length>0,'fixture notes are required on '+s.id);
  const a=s.acceptance||{};
  assert.equal(a.resultPresent,true,'resultPresent must be true: '+s.id);
  assert.equal(a.noUnsupportedClaims,true,'noUnsupportedClaims must be true: '+s.id);
  assert.equal(typeof a.artifactOpenable,'boolean','artifactOpenable must be boolean: '+s.id);
  if(a.artifactOpenable){assert.ok(ARTIFACT_EXTS.includes(a.expectedArtifactExt),'openable artifact needs expectedArtifactExt: '+s.id);}
  else assert.equal(a.expectedArtifactExt,undefined,'expectedArtifactExt only when artifactOpenable: '+s.id);
  // v0.12.1 #29: optional literal content rules; validate their shape when a sample defines them.
  for(const key of ['facts','forbidden']){
   const list=a[key];if(list===undefined)continue;
   assert.ok(Array.isArray(list)&&list.length>0,key+' must be a non-empty array when present: '+s.id);
   assert.ok(list.every(item=>typeof item==='string'&&item.trim().length>0),key+' entries must be non-empty strings: '+s.id);
  }
  assert.deepEqual(s.metrics,[...METRICS],'metrics list mismatch on '+s.id);
  assert.deepEqual(s.noviceChecks,[...NOVICE_CHECKS],'noviceChecks list mismatch on '+s.id);
  if(s.category==='local'){
   assert.equal(s.expectedRoute,'local','local category must expect the local route: '+s.id);
   assert.ok(LOCAL_KINDS.includes(s.expectedLocalKind),'local sample needs a router-supported expectedLocalKind: '+s.id);
   assert.ok(s.goal.length<=300,'local goals must fit the agentLocalPlan 300-character contract: '+s.id);
   assert.ok(!ROUTER_REJECT_MARKERS.test(s.goal),'local goal contains generative/write wording that the router intentionally routes to the model: '+s.id);
   const pattern=LOCAL_ROUTER_PATTERNS[s.expectedLocalKind];
   assert.ok(pattern.verb.test(s.goal),'local goal must use an explicit verb for '+s.expectedLocalKind+': '+s.id);
   assert.ok(pattern.constraint.test(s.goal),'local goal must contain an explicit constraint for '+s.expectedLocalKind+': '+s.id);
  }else{
   assert.equal(s.expectedRoute,'model','non-local category must expect the model route: '+s.id);
   assert.equal(s.expectedLocalKind,undefined,'expectedLocalKind is only for local samples: '+s.id);
  }
  if(s.category==='qa')assert.equal(a.citationsRequired,true,'qa samples must require citations: '+s.id);
  if(s.category==='needs-input')assert.equal(a.citationsRequired,false,'needs-input samples do not require citations: '+s.id);
 }

 assert.deepEqual(byCategory,{...EXPECTED_DISTRIBUTION},'category distribution must be '+'local 8 / generation 10 / qa 7 / complex 3 / needs-input 2');
 assert.equal(byRoute.local,8,'exactly 8 samples must expect the local route');
 assert.equal(byRoute.model,22,'exactly 22 samples must expect the model route');

 // v0.12.1 #29: the live harness checks acceptance.facts / acceptance.forbidden literally. Require enough
 // samples to define them so the content checks cannot silently become vacuous.
 const factsCoverage=count(SAMPLES,s=>Array.isArray(s.acceptance.facts)&&s.acceptance.facts.length>0);
 const forbiddenCoverage=count(SAMPLES,s=>Array.isArray(s.acceptance.forbidden)&&s.acceptance.forbidden.length>0);
 assert.ok(factsCoverage>=8,'at least 8 samples must define literal acceptance facts, found '+factsCoverage);
 assert.ok(forbiddenCoverage>=4,'at least 4 samples must define forbidden strings, found '+forbiddenCoverage);

 const coverage=COVERAGE_RULES.map(rule=>({name:rule.name,count:count(SAMPLES,rule.test),min:rule.min}));
 for(const row of coverage)if(row.count<row.min)fail('coverage "'+row.name+'" requires at least '+row.min+' samples, found '+row.count);
 const kindCoverage={};for(const s of SAMPLES)kindCoverage[s.fixture.kind]=(kindCoverage[s.fixture.kind]||0)+1;
 for(const kind of FIXTURE_KINDS)if(!kindCoverage[kind])fail('no sample uses the fixture kind "'+kind+'"');

 // Compact table: one row per sample, grouped by category.
 const order=['local','generation','qa','complex','needs-input'];
 const rows=[];
 for(const category of order)for(const s of SAMPLES.filter(s=>s.category===category)){
  rows.push([s.id,s.category,s.expectedRoute,s.expectedLocalKind||'-',s.acceptance.expectedArtifactExt||'-',s.acceptance.citationsRequired?'yes':'no',s.title]);
 }
 console.log('Agent task samples v'+module.exports.version+': 30 samples — '+order.map(category=>category+' '+byCategory[category]).join(' / '));
 console.log('Routes: local '+byRoute.local+' / model '+byRoute.model+' · fixture kinds: '+Object.entries(kindCoverage).map(([kind,n])=>kind+' '+n).join(', '));
 console.log('Local router vocabulary: '+byRoute.local+' goals use a supported intent verb + explicit constraint and avoid generative/write wording (300-char contract).');
 console.log('Coverage: '+coverage.map(row=>row.name+' '+row.count+' (min '+row.min+')').join(' · '));
 console.log('Literal content rules: facts on '+factsCoverage+' samples · forbidden on '+forbiddenCoverage+' samples (bounded string checks, not semantic verification).');
 console.log('');
 console.log(pad('id',36)+pad('category',12)+pad('route',7)+pad('kind',12)+pad('artifact',9)+pad('citations',10)+'title');
 for(const row of rows)console.log(pad(row[0],36)+pad(row[1],12)+pad(row[2],7)+pad(row[3],12)+pad(row[4],9)+pad(row[5],10)+row[6]);
 console.log('');
 console.log('OK: 30-task baseline sample set validated (offline; no app, model or network was used).');
}
