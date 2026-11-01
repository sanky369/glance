# Glance voice companion

The dashboard uses OpenAI Realtime over browser WebRTC for bidirectional speech. The standard project API key remains on the Node server. The browser sends an SDP offer to the local server, which exchanges it with `POST https://api.openai.com/v1/realtime/calls`; only the SDP answer comes back. No browser speech-recognition substitute is used.

The default model is `gpt-realtime-2.1`, voice `marin`, and input transcript model `gpt-4o-mini-transcribe`. Set server environment variable `OPENAI_REALTIME_MODEL` to use another compatible Realtime model. `OPENAI_API_KEY` comes from the existing ignored `.env.local`.

## Integration

In `dashboard/server.ts`, import `handleVoice` from `./voice.ts`. Within the request handler, before its final 404, call:

```ts
if(await handleVoice(req,res,url,{enabled:!replayOnly}))return;
```

Serve `dashboard/voice-panel.js` at `/voice-panel.js` with JavaScript content type. Add an element such as `<section id="voice-panel"></section>` and load:

```html
<script type="module">
import {initializeVoice} from '/voice-panel.js';
initializeVoice(document.querySelector('#voice-panel'));
</script>
```

`initializeVoice` returns `{stop, destroy}` for host cleanup. Its markup exposes `.voice-heading`, `.voice-status`, `.voice-disclosure`, `.voice-controls`, `.voice-start`, `.voice-stop`, `.voice-mute`, `.voice-talk`, `.voice-question`, `.voice-error`, `.voice-audio`, and `.voice-transcript` for styling. Transcript text uses textContent, not HTML.

Routes:

- `GET /api/voice/status`: `{available, model, reason}`. Replay mode disables voice.
- `POST /api/voice/session`: `Content-Type: application/sdp`, raw audio SDP offer, maximum 64 KiB. Returns an SDP answer. Requires an exact localhost:3210 or 127.0.0.1:3210 Origin; errors are JSON. Provider calls time out after 25 seconds, and concurrent connection attempts return 409.
- Existing `GET /api/runs`: supplies verifier results and current job for voice tools.
- Existing `POST /api/start`: receives only allowlisted radio/climate and baseline/revised/regression arguments. Existing server busy/replay checks remain authoritative.

## Behavior and limits

Start voice explicitly requests microphone permission. The interface discloses AI-generated speech, OpenAI audio processing, and account billing before connection. End voice stops all microphone tracks, closes the peer and data channel, clears remote audio, and aborts a pending local handshake. Leaving the page also cleans up. Sessions close locally after ten minutes. The microphone track starts disabled and remains off until Hold to speak is held. Pointer release/cancel, lost pointer capture, key release, blur, hidden tabs, and End voice turn it off again. Space and Enter provide the same hold/release behavior from the keyboard. Lock microphone disables push-to-talk until unlocked; unlocking never opens the microphone. There is no hands-free mode. The typed question form sends a real Realtime input_text message and requests an actual model response, allowing conversation with the microphone off. Beginning speech or a typed question cancels an in-progress response. Blank transcript events are omitted. If browser autoplay is blocked, a visible audio control and error explain how to resume playback.

The model can read a compact selection of the latest twelve run results and request existing evaluations through two fixed tools. It cannot issue arbitrary URLs, code, file edits, or Unity commands. Instructions require fresh evidence before claims of success and distinguish job-start acknowledgement from verifier completion. Hashes are supplied for distinguishing recorded evidence from the current UI. Spoken claims are model output: authoritative pass/fail remains the dashboard verifier evidence. Run status is fetched on demand, not automatically polled by voice. Original-versus-current comparisons use the canonical `benchmark.baseline` and `benchmark.revised` entries supplied by the server, rather than recent rows’ variant labels. Default answers are limited by instruction to three short sentences/about sixty spoken words. The audio response allowance is 1,800 output tokens; incomplete responses display a visible warning and the API reason when provided. Failed responses preserve the API error message.

Voice transcripts are held in the page only, capped at forty displayed entries. While push-to-talk is held, microphone audio travels directly between the browser and OpenAI through WebRTC. The browser may continue showing microphone permission/capture while connected even though the transmitted track is disabled; End voice fully stops capture. This feature requires a browser with microphone/WebRTC support and a secure context such as localhost or HTTPS. External hosted access is intentionally unsupported by the fixed loopback Origin allowlist.

## Verification record

On 2026-11-01, both files passed `node --check`. A real OpenAI session-configuration smoke check sent the complete exported `voiceSession` to `/v1/realtime/client_secrets`, without capturing microphone audio or generating speech. Safe output:

```json
{"checkedAt":"2026-11-01T14:03:23.370Z","endpoint":"/v1/realtime/client_secrets","status":200,"model":"gpt-realtime-2.1","sessionAccepted":true,"credentialReturned":true}
```

The ephemeral credential was neither printed nor saved. This verifies API credentials, model access, and session schema. Subsequent real Chrome verification by the lead confirmed SDP connection, AI speech/transcripts, and get_run_results. Ambient speech triggered interruptions during that check; the client was then changed to default push-to-talk and a real typed-question path. The lead subsequently verified a typed question reaching real benchmark tools and a spoken answer with the microphone off. That answer was cut short, so the output allowance was raised from 700 to 1,800 tokens and concise-answer instructions were added. The final response-limit and incomplete-warning changes were syntax checked; another live acceptance check remains with the lead.

Repeat the smoke without exposing the ephemeral credential:

```sh
node --env-file=.env.local --input-type=module <<'JS'
import {voiceSession} from './dashboard/voice.ts';
const response=await fetch('https://api.openai.com/v1/realtime/client_secrets',{
  method:'POST',
  headers:{Authorization:`Bearer ${process.env.OPENAI_API_KEY}`,'Content-Type':'application/json'},
  body:JSON.stringify({session:voiceSession}),signal:AbortSignal.timeout(25000)
});
const data=await response.json();
console.log(JSON.stringify({checkedAt:new Date().toISOString(),status:response.status,
  model:voiceSession.model,sessionAccepted:response.ok,credentialReturned:!!data.value,
  error:response.ok?undefined:data.error?.message}));
JS
```

For manual end-to-end verification: start the dashboard, click Start voice, allow the microphone, ask for latest radio/climate results, compare the spoken run IDs and success with visible verifier evidence, hold/release the speak button, verify keyboard Space/Enter and microphone lock/unlock, ask a typed question with the microphone off, and End voice. Confirm the browser microphone indicator turns off and playback ends. Ask to start a specific known evaluation only when its API/Unity cost is intended; confirm the job is merely reported as started until new verifier results exist. Test denied microphone permission and a second run request during an active job. Save a recording or browser report when performing that acceptance check.

## Primary documentation

- [OpenAI browser WebRTC and server-side unified SDP exchange](https://developers.openai.com/api/docs/guides/voice-webrtc)
- [Realtime conversations, transcription, and function calling](https://developers.openai.com/api/docs/guides/realtime-conversations)
- [Realtime session credential API](https://developers.openai.com/api/reference/resources/realtime/subresources/client_secrets/methods/create)

Documentation and session schema were verified on 2026-11-01.
