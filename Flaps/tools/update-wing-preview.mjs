import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import {fileURLToPath} from 'node:url';

// Build-time only. The downloaded viewer never loads a separate catalog.
function validateWing(wing){
 if(!wing || typeof wing.id!=='string' || !wing.id || typeof wing.name!=='string')throw Error('Wing needs a stable id and name');
 if(wing.format==='pending')return;
 if(wing.format!=='raw' || !Array.isArray(wing.meshes) || !wing.meshes.length)throw Error(wing.id+': missing actual meshes');
 let animatedFlap=false;
 for(const mesh of wing.meshes){
  if(!Array.isArray(mesh.positions)||mesh.positions.length%3||!mesh.positions.length||!mesh.positions.every(Number.isFinite))throw Error(wing.id+': invalid positions');
  const length=mesh.positions.length;
  if(!Array.isArray(mesh.normals)||mesh.normals.length!==length||!mesh.normals.every(Number.isFinite))throw Error(wing.id+': invalid normals');
  if(!Array.isArray(mesh.submeshes)||!mesh.submeshes.length)throw Error(wing.id+': missing triangles');
  for(const sub of mesh.submeshes)if(!Array.isArray(sub.triangles)||sub.triangles.length%3||!sub.triangles.every(i=>Number.isInteger(i)&&i>=0&&i<length/3))throw Error(wing.id+': invalid triangles');
  if(mesh.frames){
   if(!Array.isArray(mesh.frames)||mesh.frames.length<2)throw Error(wing.id+': incomplete frames');
   let previous=-1;
   for(const frame of mesh.frames){
    if(!Number.isFinite(frame.angle)||frame.angle<0||frame.angle>30||frame.angle<=previous)throw Error(wing.id+': frame angles must increase from 0 to 30');
    for(const name of ['positions','normals'])if(!Array.isArray(frame[name])||frame[name].length!==length||!frame[name].every(Number.isFinite))throw Error(wing.id+': invalid frame '+name);
    previous=frame.angle;
   }
   if(mesh.frames[0].angle!==0||previous!==30)throw Error(wing.id+': poses must cover all selector settings');
   if(mesh.role==='flap')animatedFlap=true;
  }
 }
 if(wing.hasFlaps&&!animatedFlap)throw Error(wing.id+': flaps enabled without actual moving flap poses');
}
function mergeWings(current,incoming){
 const ids=new Set();
 for(const wing of incoming){if(ids.has(wing.id))throw Error('Duplicate wing id '+wing.id);ids.add(wing.id);validateWing(wing)}
 const models=new Map(current.map(wing=>[wing.id,wing]));
 for(const wing of incoming)models.set(wing.id,wing);
 return [...models.values()];
}
function crc32(bytes){
 let crc=0xffffffff;
 for(const byte of bytes){crc^=byte;for(let k=0;k<8;k++)crc=(crc>>>1)^((crc&1)?0xedb88320:0)}
 return(crc^0xffffffff)>>>0;
}
function viewerZip(html){
 const data=Buffer.from(html),name=Buffer.from('aviassembly-wing-preview.html'),packed=zlib.deflateRawSync(data,{level:9}),crc=crc32(data);
 const l=Buffer.alloc(30),c=Buffer.alloc(46),e=Buffer.alloc(22);
 l.writeUInt32LE(0x04034b50);l.writeUInt16LE(20,4);l.writeUInt16LE(0x800,6);l.writeUInt16LE(8,8);l.writeUInt32LE(crc,14);l.writeUInt32LE(packed.length,18);l.writeUInt32LE(data.length,22);l.writeUInt16LE(name.length,26);
 c.writeUInt32LE(0x02014b50);c.writeUInt16LE(20,4);c.writeUInt16LE(20,6);c.writeUInt16LE(0x800,8);c.writeUInt16LE(8,10);c.writeUInt32LE(crc,16);c.writeUInt32LE(packed.length,20);c.writeUInt32LE(data.length,24);c.writeUInt16LE(name.length,28);
 e.writeUInt32LE(0x06054b50);e.writeUInt16LE(1,8);e.writeUInt16LE(1,10);e.writeUInt32LE(c.length+name.length,12);e.writeUInt32LE(l.length+name.length+packed.length,16);
 return Buffer.concat([l,name,packed,c,name,e]);
}
const source=process.argv[2];
if(!source)throw Error('Usage: node Flaps/tools/update-wing-preview.mjs exported-models.json');
const directory=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),viewer=path.join(directory,'aviassembly-wing-preview.html');
const updates=JSON.parse(fs.readFileSync(source,'utf8'));
if(updates.schemaVersion!==1||!Array.isArray(updates.wings))throw Error('Unsupported export schema');
const html=fs.readFileSync(viewer,'utf8'),pattern=/(<script type="application\/json" data-wing-models>)([\s\S]*?)(<\/script>)/;
const match=html.match(pattern);if(!match)throw Error('Embedded model registry is missing');
const catalog=JSON.parse(match[2]);catalog.wings=mergeWings(catalog.wings,updates.wings);
const json=JSON.stringify(catalog).replace(/</g,'\\u003c'),next=html.replace(pattern,(_,a,b,c)=>a+json+c),zip=viewerZip(next);
fs.writeFileSync(viewer+'.tmp',next);fs.writeFileSync(path.join(directory,'aviassembly-wing-preview.zip.tmp'),zip);
fs.renameSync(viewer+'.tmp',viewer);fs.renameSync(path.join(directory,'aviassembly-wing-preview.zip.tmp'),path.join(directory,'aviassembly-wing-preview.zip'));
console.log('Embedded '+updates.wings.length+' updated wing models into '+viewer);
