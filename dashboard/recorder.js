export function initializeRecorder(button,status){
 let recorder,tracks=[],chunks=[],timer;
 async function stop(){if(recorder?.state==='recording')recorder.stop()}
 button.onclick=async()=>{
  if(recorder?.state==='recording'){await stop();return}
  try{
   status.textContent='Select only the Glance browser tab and enable tab audio.';
   const stream=await navigator.mediaDevices.getDisplayMedia({video:{displaySurface:'browser',frameRate:24},audio:true,preferCurrentTab:true});tracks=stream.getTracks();chunks=[];
   const supported=['video/webm;codecs=vp9,opus','video/webm;codecs=vp8,opus','video/webm'].find(t=>MediaRecorder.isTypeSupported(t));
   recorder=new MediaRecorder(stream,{mimeType:supported,videoBitsPerSecond:3500000});
   recorder.ondataavailable=e=>{if(e.data.size)chunks.push(e.data)};
   recorder.onstop=async()=>{clearTimeout(timer);tracks.forEach(t=>t.stop());button.textContent='Record live demo';status.textContent='Saving live capture…';try{const blob=new Blob(chunks,{type:recorder.mimeType});const r=await fetch('/api/recording',{method:'POST',headers:{'Content-Type':'video/webm'},body:blob});if(!r.ok)throw Error('Save failed');status.textContent='Live demo saved · maximum 85 seconds';}catch(e){status.textContent=e.message}};
   stream.getVideoTracks()[0].onended=stop;recorder.start(1000);timer=setTimeout(stop,85000);button.textContent='Stop recording';status.textContent='RECORDING · live Glance tab';
  }catch(e){tracks.forEach(t=>t.stop());status.textContent='Recording unavailable: '+e.message}
 };
 return {stop};
}
