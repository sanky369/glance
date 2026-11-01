import {readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {root} from './unity.ts';
export const revisionPaths=['tasks/tasks.json','unity-hmi/Assets/Glance/Editor/GlanceTestCommands.cs','unity-hmi/Assets/Glance/StateBridge/GlanceShell.cs','unity-hmi/Assets/Modules/BodyControl/Scripts/InterestPoint.cs','unity-hmi/Assets/Glance/Editor/GlancePointerCommands.cs','unity-hmi/Assets/Glance/StateBridge/GlanceNativeLabels.cs'];
export function fingerprint(uiHash:string,components:any){if(!components||!Object.keys(components).length)return uiHash;return createHash('sha256').update(JSON.stringify({uiHash,components:Object.entries(components).sort(([a],[b])=>a.localeCompare(b))})).digest('hex')}
export async function currentRevision(uiHash:string){const componentHashes:any={};for(const name of revisionPaths)try{componentHashes[name]=createHash('sha256').update(await readFile(resolve(root,name))).digest('hex')}catch{}return {componentHashes,hmiRevision:fingerprint(uiHash,componentHashes)}}
