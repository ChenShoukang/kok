import {Warehouse,Inspector} from './warehouse-state.mjs';
import {ClipCapture,installClipInteractions} from './clip-interactions.mjs';
import {installViewerShell} from './viewer-shell.mjs';
import {RecipeEditor} from './recipe-editor.mjs';
import {installWorkspaceEvents} from './workspace-events.mjs';
import {CollectionPages} from './collection-pages.mjs';
// Compatibility accessors for existing library/editor adapters; implementation is module-scoped.
globalThis.Warehouse=Warehouse;globalThis.Inspector=Inspector;
Warehouse.capture=ClipCapture;CollectionPages.install();installClipInteractions();installViewerShell();installWorkspaceEvents();
document.addEventListener('kok-collection-page',()=>Warehouse.select(Warehouse.selected));
Warehouse.recipeEditor=RecipeEditor;
Kok.register({id:'workspace',save:()=>Inspector.flush(),flush:()=>Inspector.flush(),nativeActions:['workspaceState','workspaceSave','queryCollection','fullTextSearch','storageInfo','reviewTimeline','indexContent','prepareContext','summarizeContent','presentationFromText','recipeRun','recipePreview','relatedLinks','spaceLinks','indexLibrary','commandToday','commandSpaces','commandClips','commandQueries','commandRecipes','commandReview','commandClip','commandInspect','commandAddSpace','commandContextAi'],
 beforeNavigate(route){if(route!==state.route){Warehouse.select([]);libraryDrag?.selected.clear();}if(route!=='spaces'){Warehouse.graph?.destroy();Warehouse.graph=null;}},
 routes:{today:()=>Warehouse.today(),spaces:()=>Warehouse.spaces(),clips:()=>Warehouse.clips(),queries:()=>Warehouse.queries(),recipes:()=>Warehouse.recipes(),review:()=>Warehouse.review().catch(fail)},
 nav:()=>[{id:'today',route:'today',label:'今天',icon:'calendar',order:-120},{id:'spaces',route:'spaces',label:'空间',icon:'folder',order:20},{id:'clips',route:'clips',label:'摘录',icon:'edit',order:21},{id:'queries',route:'queries',label:'智能集合',icon:'search',order:22},{id:'recipes',route:'recipes',label:'配方',icon:'layers',order:23,when:()=>!state.isPrivate},{id:'review',route:'review',label:'回顾',icon:'clock',order:24},...Warehouse.data.spaces.slice(0,4).map((space,n)=>({id:'space:'+space.id,spaceId:space.id,label:space.name,userContent:true,icon:'folder',className:'space-nav-child',order:20.1+n/100,count:()=>space.members.length}))],
 handlers:{'workspace.today':()=>navigate('today'),'workspace.spaces':()=>navigate('spaces'),'workspace.clips':()=>navigate('clips'),'workspace.queries':()=>navigate('queries'),'workspace.recipes':()=>navigate('recipes'),'workspace.review':()=>navigate('review'),'workspace.ai':()=>{Inspector.tab='ai';Inspector.show();},'workspace.clip':()=>Warehouse.clipForm(),'workspace.inspect':()=>Inspector.show(),'workspace.addSpace':()=>Warehouse.addSpace()},
 async start({startup}){await Warehouse.load();if(startup&&!location.search.includes('preview=')&&!state.isPrivate&&(!state.settings.startPage||state.settings.startPage==='today')){state.route='today';render();}},dispose:()=>{CollectionPages.clear();Warehouse.graph?.destroy();Warehouse.graph=null;Inspector.hide();}
});
