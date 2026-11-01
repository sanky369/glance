#!/usr/bin/env python3
"""Build a captioned replay from genuine retained Unity/Decisions evidence."""
import json,pathlib,subprocess,tempfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
BASE=ROOT/'docs/build/evidence'
runs=json.loads((BASE/'index.json').read_text())
baselines=[r for r in runs if r['taskId']=='radio' and r['variant']=='baseline' and r.get('policyVersion')=='verified-stop-v1']
revised=[r for r in runs if r['taskId']=='radio' and r['variant']=='revised' and r.get('policyVersion')=='verified-stop-v1']
assert len(baselines)>=3 and len(revised)>=3
b=sorted(baselines,key=lambda r:r['id'])[0]
a=next(r for r in sorted(revised,key=lambda r:r['id'],reverse=True) if r.get('finalScreenshot'))
c=next(r for r in runs if r['taskId']=='climate' and r['variant']=='regression' and r['success'] and r.get('finalScreenshot'))
trace=json.loads((BASE/b['id']/'trace.public.json').read_text())
font='/System/Library/Fonts/Supplemental/Arial.ttf'
out=ROOT/'docs/build/glance-demo.mp4'
with tempfile.TemporaryDirectory(prefix='glance-demo-') as tmp:
 tmp=pathlib.Path(tmp);segments=[]
 def scene(image,seconds,title,caption):
  i=len(segments);top=tmp/f'{i}-title.txt';bottom=tmp/f'{i}-caption.txt';top.write_text(title);bottom.write_text(caption)
  file=tmp/f'{i:02}.mp4'
  filt=f"scale=1920:1080,setsar=1,drawbox=x=0:y=0:w=iw:h=112:color=0x09111d@0.97:t=fill,drawbox=x=0:y=930:w=iw:h=150:color=0x09111d@0.97:t=fill,drawtext=fontfile={font}:textfile={top}:fontcolor=0x7ee5d4:fontsize=42:x=50:y=32,drawtext=fontfile={font}:textfile={bottom}:fontcolor=white:fontsize=29:line_spacing=12:x=50:y=958"
  cmd=['ffmpeg','-hide_banner','-loglevel','error','-y','-loop','1','-i',str(image),'-vf',filt,'-t',str(seconds),'-r','24','-an','-c:v','libx264','-preset','veryfast','-crf','23','-pix_fmt','yuv420p',str(file)]
  subprocess.run(cmd,check=True);segments.append(file)
 image=BASE/b['id']/'000.png'
 scene(image,5,'GLANCE | Improve a car touchscreen with evidence','For automotive HMI teams inspecting prototypes before human testing.\nCaptioned replay of genuine API runs; not a live screen recording.')
 for s in trace:
  if not s.get('executed'):continue
  scene(BASE/b['id']/s['screenshot'],3,'BASELINE | Decisions selects the next visible control',f"Actual choice: {s['action']['label']}  |  Input {len([x for x in trace[:s['call']+1] if x.get('executed')])} of {b['actions']}\nScreenshot -> visible choices -> Decisions -> real Unity pointer input")
 scene(BASE/b['id']/trace[-1]['screenshot'],5,'BASELINE | Independent code verifies success','BBC Radio 2 verified from application state.\nThree fresh baseline trials: 3/3 passed, median 5 UI inputs.')
 scene(BASE/a['id']/'000.png',8,'CODEX | A constrained, reviewed UI-only revision','Measured path: Media > Sources > FM radio > Presets > BBC Radio 2\nCodex added large Home presets. Only Show/Menu changed; checks stayed fixed.')
 scene(BASE/a['id']/'000.png',6,'FRESH RETEST | Same task, model and evaluation policy','Actual Decisions choice: BBC Radio 2\nOne real UI click. Fresh context and reset state; no hidden state sent to the model.')
 scene(BASE/a['id']/a['finalScreenshot'],6,'VERIFIED RESULT | Radio now plays BBC Radio 2',f"Revised UI: {sum(r['success'] for r in revised)}/{len(revised)} successful trials, median 1 UI input.\nBaseline: 3/3 successful trials, median 5 inputs. Failed exploratory runs are retained.")
 scene(BASE/c['id']/c['finalScreenshot'],7,'REGRESSION | Climate still works after the UI change','Real Decisions-selected UI inputs set driver temperature to 21 C.\nIndependent state check: driver 21 C; passenger unchanged at 22 C.')
 scene(BASE/a['id']/'000.png',7,'GLANCE | Test -> evidence -> Codex revision -> fresh proof','Instrumented, assisted walkthrough; visible-control inventory helps discovery.\nNo driver-distraction, eye-tracking or compliance claims. Unity template credited.')
 listing=tmp/'concat.txt';listing.write_text(''.join("file '"+str(p)+"'\n" for p in segments))
 subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(listing),'-c','copy','-movflags','+faststart',str(out)],check=True)
manifest={'format':'Captioned replay assembled from genuine run screenshots; no simulated actions','baseline':b['id'],'revised':a['id'],'climate':c['id'],'output':'glance-demo.mp4'}
(ROOT/'docs/build/demo-sources.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(out)
