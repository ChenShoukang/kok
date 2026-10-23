// Every automated media test requires a silent native window, not just muted DOM controls.
const playwright=require('playwright-core');
function silencePage(){
 if(window.kokTestAudioSilenced)return;window.kokTestAudioSilenced=true;
 const play=HTMLMediaElement.prototype.play;
 HTMLMediaElement.prototype.play=function(...args){this.muted=true;return Reflect.apply(play,this,args);};
 document.querySelectorAll('audio,video').forEach(media=>{media.muted=true;});
 document.addEventListener('play',event=>{if(event.target instanceof HTMLMediaElement)event.target.muted=true;},true);
}
module.exports={chromium:{async connectOverCDP(...args){
 const browser=await playwright.chromium.connectOverCDP(...args);
 try{
   for(const context of browser.contexts()){
     await context.addInitScript(silencePage);
     for(const page of context.pages().filter(page=>page.url().startsWith('https://app.kok.local'))){
       await page.evaluate(silencePage);await page.waitForFunction(()=>typeof native==='function');
       const diagnostics=await page.evaluate(()=>native('diagnostics'));
       if(!diagnostics.silentTest)throw Error('Start the isolated test window with --debug --test-silent. Media tests will not run with audible output.');
     }
   }
   return browser;
 }catch(error){await browser.close();throw error;}
}}};
