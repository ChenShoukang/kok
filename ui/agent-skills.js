'use strict';
// Pi-inspired progressive disclosure: advertise metadata, send only the chosen
// instruction. Importing a skill never loads scripts or registers OS tools.
const AgentSkills=(()=>{
 const key='kok.visual-agent.skills.v1';
 const builtins=[{name:'polish',title:'润色',description:'保留原意，改善表达',task:'polish',instructions:''},{name:'shorten',title:'精简',description:'保留重点，减少冗余',task:'shorten',instructions:''},{name:'expand',title:'扩写',description:'只基于已有事实适度展开',task:'expand',instructions:''},{name:'translate',title:'翻译',description:'翻译选中的文字',task:'translate',instructions:''},{name:'custom',title:'自定义修改',description:'输入这次修改要求',task:'custom',instructions:''}];
 function validate(value){if(!value||typeof value!=='object'||!/^([a-z0-9]+-)*[a-z0-9]+$/.test(value.name||'')||value.name.length>64||builtins.some(s=>s.name===value.name)||typeof value.description!=='string'||!value.description.trim()||value.description.length>200||typeof value.instructions!=='string'||!value.instructions.trim()||value.instructions.length>800)throw Error('技能需要名称、简介及不超过 800 字符的指令。');return{name:value.name,title:value.name,description:value.description.trim(),instructions:value.instructions.trim(),task:'custom'};}
 function parse(raw){if(typeof raw!=='string'||raw.length>16000)throw Error('技能文件过大。');const text=raw.replace(/^\uFEFF/,'').replace(/\r\n/g,'\n').trim();if(text.startsWith('{'))return validate(JSON.parse(text));const match=/^---\n([\s\S]*?)\n---\n([\s\S]+)$/.exec(text);if(!match)throw Error('请导入带 name 和 description 的 SKILL.md。');const field=name=>{const row=new RegExp('^'+name+':\\s*(.+)$','m').exec(match[1]);return row?row[1].trim().replace(/^['"]|['"]$/g,''):'';};return validate({name:field('name'),description:field('description'),instructions:match[2]});}
 function custom(){try{return JSON.parse(localStorage.getItem(key)||'[]').map(validate).slice(0,20);}catch{return[];}}
 function all(){return[...builtins,...custom()];}
 function add(skill){const value=validate(skill),rows=custom();if(rows.some(s=>s.name===value.name))throw Error('已存在同名技能。');if(rows.length>=20)throw Error('最多导入 20 个指令技能。');localStorage.setItem(key,JSON.stringify([...rows,value]));return value;}
 function remove(name){localStorage.setItem(key,JSON.stringify(custom().filter(s=>s.name!==name)));}
 function markdown(skill){return'---\nname: '+skill.name+'\ndescription: '+JSON.stringify(skill.description)+'\n---\n\n'+skill.instructions+'\n';}
 return{builtins,all,custom,parse,add,remove,markdown};
})();
if(typeof module!=='undefined')module.exports=AgentSkills;
