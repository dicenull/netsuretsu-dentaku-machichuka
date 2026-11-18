const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');let now=1000,events=[],created;
const body={appendChild(input){input.parentNode=this;}};
const canvas={getBoundingClientRect(){return {left:0,top:0,width:360,height:800};},focus(){},addEventListener(){},removeEventListener(){},};
const ctx={LibraryManager:{library:{}},mergeInto(a,b){Object.assign(a,b);},performance:{now:()=>now},
 Module:{canvas},window:{matchMedia:()=>({matches:true}),visualViewport:{offsetTop:0,height:800}},
 document:{body,fullscreenElement:null,activeElement:null,createElement(){created={style:{},hidden:true,value:'',events:{},setAttribute(){},addEventListener(n,f){this.events[n]=f;},focus(){ctx.document.activeElement=this;},remove(){this.removed=true;}};return created;}},
 lengthBytesUTF8:s=>s.length,_malloc:()=>1,_free(){},stringToUTF8(){},UTF8ToString:x=>x,callbackShim:(action)=>events.push(action)};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../Assets/Plugins/WebGL/FeastWebInput.jslib'),'utf8').replace(/\{\{\{ makeDynCall\('vii', 'callback'\) \}\}\}/g,'callbackShim'),ctx);const api=ctx.LibraryManager.library;
api.FeastWebInput_Create(1);api.FeastWebInput_PlaceResponsive(.05,.7,.7,.08,22/800);
assert.equal(created.style.fontSize,'22px');assert.equal(ctx.document.activeElement,null,'mobile keyboard must not auto-open');
function key(repeat=false){created.events.keydown({key:'Enter',keyCode:13,isComposing:false,repeat,stopPropagation(){},preventDefault(){}});}
created.events.compositionstart();key();assert.deepEqual(events,[]);
created.events.compositionend();assert.deepEqual(events,[0]);key();assert.deepEqual(events,[0]);assert.equal(api.FeastWebInput_IsComposing(),1);
now+=81;key();assert.deepEqual(events,[0,1]);key(true);assert.deepEqual(events,[0,1]);
ctx.window.visualViewport.height=480;assert.equal(api.FeastWebInput_VisibleBottom(),.6);
api.FeastWebInput_Hide();key();assert.deepEqual(events,[0,1]);assert.equal(created.hidden,true);
api.FeastWebInput_Destroy();assert.equal(ctx.Module.feastWebInput,undefined);
console.log('PASS: JS syntax, IME composition/80ms/repeat, responsive font, mobile non-autofocus, viewport and hide/destroy contracts. Not browser runtime validation.');
