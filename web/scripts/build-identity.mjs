import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const version=readFileSync(path.join(root,'VERSION'),'utf8').trim();
const packageVersion=JSON.parse(readFileSync(path.join(root,'web/package.json'),'utf8')).version;
if(version.split(/[-+]/)[0]!==packageVersion)throw Error('Package version differs from VERSION.');
let identity={version,revision:'unknown',official:false,dirty:true};
if(existsSync(path.join(root,'.build-identity.json'))){
  identity=JSON.parse(readFileSync(path.join(root,'.build-identity.json'),'utf8'));
  if(identity.version!==version||!/^[a-f0-9]{40}$/.test(identity.revision)||typeof identity.official!=='boolean')throw Error('Invalid prepared build identity.');
}else{
  try{identity.revision=execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8',stdio:['ignore','pipe','ignore']}).trim();}catch{}
}
mkdirSync(path.join(root,'web/generated'),{recursive:true});
writeFileSync(path.join(root,'web/generated/build-identity.json'),JSON.stringify(identity)+'\n');
