const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const page=fs.readFileSync(require('path').join(__dirname,'../aviassembly-wing-preview.html'),'utf8');
const executable=page.match(/<script type="module">([\s\S]*?)<\/script>/)[1];
assert(!/\b(?:import\s*\(|fetch\s*\(|https?:\/\/)/.test(executable));
const ids=JSON.parse(page.match(/data-wing-models>([\s\S]*?)<\/script>/)[1]).wings;
const el=()=>({options:[],on:{},add(o){this.options.push(o)},addEventListener(type,f){this.on[type]=f},setAttribute(){}});
const selectors={},node=s=>selectors[s]??=el();
const registry={textContent:JSON.stringify({wings:ids})};
node('[data-model]').textContent=page.match(/data-model>([\s\S]*?)<\/script>/)[1];
const picker=node('[data-wing]'),view=node('[data-view]'),canvas=node('[data-canvas]'),status=node('[data-status]');
view.options=['wing','flaps','slats','aileron','le'].map(value=>({value}));
let points=0,draws=0,frames=[],now=0,lastGeometry=0,signature=0;
const point=(x,y)=>{assert(Number.isFinite(x+y));points++;signature+=Math.sin(x*.01+y*.003);};
const ctx={setTransform(){},clearRect(){draws=points=signature=0},beginPath(){},moveTo:point,lineTo:point,closePath(){},fill(){draws++}};
Object.assign(canvas,{clientWidth:900,clientHeight:440,getContext:()=>ctx,setPointerCapture(){}});
const buttons=[0,10,20,30].map(angle=>Object.assign(el(),{dataset:{angle}}));const root={querySelector:s=>s==='[data-wing-models]'?null:node(s),querySelectorAll:()=>buttons};
const context={document:{getElementById:()=>root,querySelector:s=>s==='[data-wing-models]'?registry:null},Option:class{constructor(text,value){Object.assign(this,{text,value})}},ResizeObserver:class{observe(){}},requestAnimationFrame:f=>frames.push(f),matchMedia:()=>({matches:true}),devicePixelRatio:1,atob,Blob,Response,DecompressionStream};
vm.runInNewContext(executable,context);
function render(){while(frames.length)frames.shift()(now+=16);assert(draws>0 && points===draws*3);return signature;}
(async()=>{
 assert.equal(picker.options.length,16);assert.equal(ids.filter(w=>w.hasFlaps).length,13);for(const wing of ids){
  picker.value=wing.id;await picker.on.change();assert(status.textContent.includes(wing.name));render();
  if(wing.hasFlaps){buttons[0].on.click();lastGeometry=render();buttons[3].on.click();assert.notEqual(render(),lastGeometry);}
  for(const opt of view.options.filter(o=>!o.disabled)){view.value=opt.value;view.on.change();render();}
  canvas.on.wheel({deltaY:30,preventDefault(){}});render();
 }
 console.log('PASS all 16 wings draw offline, 13 animate, and device views and zoom work.');
})().catch(e=>{console.error(e);process.exitCode=1});
