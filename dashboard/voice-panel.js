export function initializeVoice(panel){
 if(!panel)return;
 panel.innerHTML=`<div class="voice-heading"><h2>Talk to Glance</h2><span class="voice-status" role="status">Checking voice availability…</span></div><p class="voice-disclosure">AI-generated voice · Microphone audio goes to OpenAI only while you hold the speak button. Typed questions also go to OpenAI. API usage is billed to the configured account.</p><p>Ask about HMI results, compare runs, or request an available body, driving, climate, media, or appearance evaluation.</p><div class="voice-controls"><button type="button" class="voice-start" disabled>Start voice</button><button type="button" class="voice-stop" disabled>End voice</button><button type="button" class="voice-talk" disabled aria-pressed="false">Hold to speak</button><button type="button" class="voice-mute" disabled aria-pressed="false">Lock microphone</button></div><form class="voice-question"><label for="voice-question-input">Or type a question</label><input id="voice-question-input" name="question" maxlength="2000" autocomplete="off" placeholder="What do the latest results show?" disabled><button type="submit" disabled>Ask Glance</button></form><p class="voice-error" role="alert" hidden></p><audio class="voice-audio" controls hidden></audio><div class="voice-transcript" role="log" aria-label="Voice transcript" aria-live="polite"></div>`;
 const find=s=>panel.querySelector(s),start=find('.voice-start'),stop=find('.voice-stop'),mute=find('.voice-mute'),talk=find('.voice-talk'),form=find('.voice-question'),question=find('input'),ask=form.querySelector('button'),status=find('.voice-status'),error=find('.voice-error'),audio=find('audio'),log=find('.voice-transcript');
 let pc=null,dc=null,stream=null,controller=null,connectionTimer=null,sessionTimer=null,generation=0,active=false,disposed=false,speaking=false,muted=false,responding=false;
 const seen=new Set();
 const line=(speaker,text)=>{if(typeof text!=='string'||!text.trim())return;const p=document.createElement('p');p.textContent=`${speaker}: ${text}`;log.append(p);while(log.children.length>40)log.firstChild.remove();log.scrollTop=log.scrollHeight;};
 const showError=message=>{error.textContent=message;error.hidden=false;};
 const send=event=>{if(!active||dc?.readyState!=='open')return;try{dc.send(JSON.stringify(event));}catch{end('Voice disconnected');showError('The voice connection closed. Start voice to reconnect.');}};
 function end(message='Voice ended'){
  ++generation;active=false;controller?.abort();controller=null;clearTimeout(connectionTimer);clearTimeout(sessionTimer);stream?.getTracks().forEach(t=>t.stop());stream=null;dc?.close();dc=null;const old=pc;pc=null;old?.close();audio.pause();audio.srcObject=null;audio.hidden=true;start.disabled=false;stop.disabled=true;mute.disabled=true;mute.textContent='Lock microphone';mute.setAttribute('aria-pressed','false');talk.disabled=true;question.disabled=true;ask.disabled=true;speaking=false;muted=false;responding=false;talk.setAttribute('aria-pressed','false');status.textContent=message;
 }
 async function api(path,options={}){const r=await fetch(path,{...options,signal:AbortSignal.timeout(15000)});const data=await r.json();if(!r.ok)throw new Error(data.error||`Request failed (${r.status})`);return data;}
 async function tool(event){
  if(seen.has(event.call_id))return;seen.add(event.call_id);const current=generation;let result;
  try{
   const args=JSON.parse(event.arguments||'{}');
   if(event.name==='get_task_catalog'){
    result=await api('/api/catalog');
   }else if(event.name==='get_run_results'){
    const data=await api('/api/runs');
    if(args.task){const catalog=await api('/api/catalog');if(!Array.isArray(catalog.tasks)||!catalog.tasks.some(task=>task.id===args.task))throw new Error('That HMI task is not in the current supported catalog');}
    result={benchmark:data.benchmark,job:data.job?{running:data.job.running,task:data.job.task,variant:data.job.variant,exitCode:data.job.exitCode,error:data.job.error}:null,currentUiHash:data.currentUiHash,currentHmiRevision:data.currentHmiRevision,liveAvailable:data.liveAvailable,runs:(data.runs||[]).filter(run=>!args.task||run.taskId===args.task).slice(0,args.task?12:40).map(r=>({id:r.id,taskId:r.taskId,variant:r.variant,uiHash:r.uiHash,hmiRevision:r.hmiRevision,success:r.success,failure:r.failure,final:r.final,task:r.task,actions:r.actions,screensVisited:r.screensVisited,backtracks:r.backtracks,smallTargets:r.smallTargets,elapsedMs:r.elapsedMs,threshold:r.threshold})),caution:'These are recorded verifier results; Compare hmiRevision with currentHmiRevision when present (otherwise uiHash with currentUiHash) before calling evidence current. A started or running job is not a passing result.'};
   }else if(event.name==='request_run'){
    const catalog=await api('/api/catalog');
    const selected=Array.isArray(catalog.tasks)?catalog.tasks.find(task=>task.id===args.task):null;
    if(!selected)throw new Error('That HMI task is not in the current supported catalog. No evaluation was started.');
    const variant=args.variant??'current';
    if(!['current','baseline','revised','regression'].includes(variant))throw new Error('Unsupported evaluation variant. No evaluation was started.');
    if(current!==generation||!active)return;
    line('Glance',`Requesting ${selected.title||args.task} / ${variant} evaluation…`);
    result=await api(variant==='current'?'/api/instruction':'/api/start',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(variant==='current'?{task:args.task}:{task:args.task,variant})});
    result={...result,caution:'Only started. Fetch actual verifier results before claiming completion or success.'};
   }else throw new Error('Unknown voice tool');
  }catch(e){result={error:e.message};if(current===generation)showError(e.message);}
  if(current!==generation||!active)return;
  send({type:'conversation.item.create',item:{type:'function_call_output',call_id:event.call_id,output:JSON.stringify(result)}});send({type:'response.create'});
 }
 function eventMessage(message){
  let event;try{event=JSON.parse(message.data);}catch{return;}
  if(event.type==='response.function_call_arguments.done')void tool(event);
  if(event.type==='conversation.item.input_audio_transcription.completed')line('You',event.transcript);
  if(event.type==='response.output_audio_transcript.done')line('Glance',event.transcript);
  if(event.type==='conversation.item.input_audio_transcription.failed')showError('Your speech could not be transcribed. Try again.');
  if(event.type==='error')showError(event.error?.message||'The voice service reported an error.');
  if(event.type==='input_audio_buffer.speech_started')status.textContent=speaking?'Listening · Release to send':'Processing speech · Microphone off';
  if(event.type==='response.created'){responding=true;status.textContent=speaking?'Listening · Release to send':'Glance is responding · Microphone off';}
  if(event.type==='response.done'){responding=false;status.textContent=speaking?'Listening · Release to send':'Connected · Microphone off · Hold to speak or type';if(event.response?.status==='failed'){status.textContent='Voice response failed · Microphone off';showError(event.response.status_details?.error?.message||'Voice response failed.');}if(event.response?.status==='incomplete'){status.textContent='Response incomplete · Microphone off';showError('Glance’s response was cut short'+(event.response.status_details?.reason?` (${event.response.status_details.reason})`:'')+'. Ask to continue or ask a shorter question.');}}
 }
 async function begin(){
  const current=++generation;active=true;start.disabled=true;stop.disabled=false;error.hidden=true;seen.clear();status.textContent='Requesting microphone…';
  try{
   if(!navigator.mediaDevices?.getUserMedia||!window.RTCPeerConnection)throw new Error('Voice needs a browser with microphone and WebRTC support on localhost or HTTPS.');
   const mic=await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:true,noiseSuppression:true}});mic.getAudioTracks().forEach(t=>t.enabled=false);
   if(current!==generation){mic.getTracks().forEach(t=>t.stop());return;}stream=mic;pc=new RTCPeerConnection();const peer=pc;
   peer.ontrack=e=>{if(current!==generation||!active)return;audio.srcObject=e.streams[0]||new MediaStream([e.track]);audio.hidden=false;audio.play().catch(()=>{if(current===generation&&active)showError('Press Play in the audio control to hear Glance.');});};
   peer.onconnectionstatechange=()=>{if(current!==generation)return;if(['failed','closed','disconnected'].includes(peer.connectionState))end('Voice disconnected');};
   stream.getTracks().forEach(t=>peer.addTrack(t,stream));dc=peer.createDataChannel('oai-events');dc.onmessage=message=>{if(current===generation&&active)eventMessage(message);};
   dc.onopen=()=>{if(current!==generation)return;clearTimeout(connectionTimer);status.textContent='Connected · Microphone off · Hold to speak or type';mute.disabled=false;talk.disabled=false;question.disabled=false;ask.disabled=false;sessionTimer=setTimeout(()=>end('Voice ended after 10 minutes. Start again to continue.'),600000);send({type:'response.create',response:{instructions:'In at most three short sentences, introduce yourself as Glance, an AI voice companion for HMI evaluations. Explain that the microphone is off until they hold the speak button, or they can type. Ask which Glance HMI evaluation or result they want to discuss. Do not discuss run results until requested and fetched.'}});};
   dc.onclose=()=>{if(current===generation&&active)end('Voice disconnected');};
   connectionTimer=setTimeout(()=>{if(current===generation){end('Connection timed out');showError('Voice did not connect. Check your network and retry.');}},35000);
   controller=new AbortController();status.textContent='Connecting to OpenAI…';const offer=await peer.createOffer();await peer.setLocalDescription(offer);
   const response=await fetch('/api/voice/session',{method:'POST',headers:{'Content-Type':'application/sdp'},body:offer.sdp,signal:controller.signal});
   if(!response.ok){const details=await response.json().catch(()=>({}));throw new Error(details.error||`Voice connection failed (${response.status})`);}
   const sdp=await response.text();if(current!==generation)return;await peer.setRemoteDescription({type:'answer',sdp});
  }catch(e){if(current!==generation)return;end('Voice unavailable');showError(e.name==='NotAllowedError'?'Microphone permission was denied. Allow microphone access and try again.':e.message);}
 }
 function release(){speaking=false;stream?.getAudioTracks().forEach(t=>t.enabled=false);talk.setAttribute('aria-pressed','false');if(active&&dc?.readyState==='open')status.textContent='Connected · Microphone off · Hold to speak or type';}
 function press(){if(!active||dc?.readyState!=='open'||muted||speaking)return;if(responding){send({type:'response.cancel'});responding=false;}speaking=true;stream?.getAudioTracks().forEach(t=>t.enabled=true);talk.setAttribute('aria-pressed','true');status.textContent='Listening · Release to send';}
 talk.addEventListener('pointerdown',event=>{if(event.button!==0)return;event.preventDefault();talk.setPointerCapture(event.pointerId);press();});
 for(const type of ['pointerup','pointercancel','lostpointercapture','blur'])talk.addEventListener(type,release);
 talk.addEventListener('keydown',event=>{if(event.code==='Space'||event.code==='Enter'){event.preventDefault();if(!event.repeat)press();}});
 talk.addEventListener('keyup',event=>{if(event.code==='Space'||event.code==='Enter'){event.preventDefault();release();}});
 const loseFocus=()=>release();window.addEventListener('blur',loseFocus);
 const hide=()=>{if(document.hidden)release();};document.addEventListener('visibilitychange',hide);
 form.addEventListener('submit',event=>{event.preventDefault();const text=question.value.trim();if(!text||!active||dc?.readyState!=='open')return;release();if(responding){send({type:'response.cancel'});responding=false;}send({type:'conversation.item.create',item:{type:'message',role:'user',content:[{type:'input_text',text}]}});line('You',text);question.value='';send({type:'response.create'});});
 start.addEventListener('click',begin);stop.addEventListener('click',()=>end());mute.addEventListener('click',()=>{release();muted=!muted;talk.disabled=muted;mute.setAttribute('aria-pressed',String(muted));mute.textContent=muted?'Unlock microphone':'Lock microphone';});
 const unload=()=>end();window.addEventListener('pagehide',unload);
 api('/api/voice/status').then(data=>{if(disposed)return;start.disabled=!data.available;status.textContent=data.available?'Ready · Microphone off':data.reason;}).catch(()=>{if(!disposed){status.textContent='Voice unavailable';showError('Could not reach the local voice service.');}});
 return {stop:end,destroy(){disposed=true;end();window.removeEventListener('pagehide',unload);window.removeEventListener('blur',loseFocus);document.removeEventListener('visibilitychange',hide);panel.replaceChildren();}};
}
