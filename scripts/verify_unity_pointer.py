#!/usr/bin/env python3
"""E2E pointer check. Requires the isolated Unity Editor playing Main."""
import datetime, json, pathlib, subprocess, time
ROOT = pathlib.Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'unity-hmi'
OUT = ROOT / 'docs/unity-hmi/evidence/pointer' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
OUT.mkdir(parents=True, exist_ok=False)
trace = []
def cmd(*args):
    proc = subprocess.run(['unity','command','--caller','plugin','--skill','unity-cli','--project-path',str(PROJECT),'--format','json',*map(str,args)], capture_output=True, text=True, timeout=60)
    if not proc.stdout.strip(): raise RuntimeError(f'Unity CLI exited {proc.returncode}: {proc.stderr[-1000:]}')
    data = json.loads(proc.stdout)
    trace.append(data)
    if not data['success']: raise RuntimeError(data.get('errors'))
    result = data['data']['result']
    if isinstance(result,dict) and result.get('success') is False: raise RuntimeError(result)
    return result

def read(code): return cmd('script','eval','--','--code',code)['result']
def state(): return read('return new { ready=UnityEngine.Object.FindObjectsByType<Modules.Menus.Scripts.SceneSwitcherButton>(UnityEngine.FindObjectsSortMode.None).All(b=>b.GetComponent<UnityEngine.UI.Button>().interactable), scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, driver=UnityEngine.Resources.FindObjectsOfTypeAll<Modules.CarData>().Single().LeftTemperature, passenger=UnityEngine.Resources.FindObjectsOfTypeAll<Modules.CarData>().Single().RightTemperature };')
def settled():
    time.sleep(.35)
    return state()
def gesture(x,y,ex=None,ey=None,duration=.12):
    cmd('glance.pointer','gesture','--','--x',x,'--y',y,'--end_x',x if ex is None else ex,'--end_y',y if ey is None else ey,'--duration',duration,'--width',1280,'--height',720)
    deadline=time.monotonic()+5
    while cmd('glance.pointer','status')['busy']:
        if time.monotonic()>deadline: raise TimeoutError('Pointer gesture did not finish')
        time.sleep(.05)
def view_point(expr):
    return read('var t=System.Type.GetType("UnityEditor.PlayModeView,UnityEditor"); var size=(UnityEngine.Vector2)t.GetMethod("GetMainPlayModeViewTargetSize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(null,null); var p='+expr+'; return new { x=p.x/size.x*1280f, y=(1f-p.y/size.y)*720f };')
def button(scene):
    return view_point('UnityEngine.RectTransformUtility.WorldToScreenPoint(null,UnityEngine.Object.FindObjectsByType<Modules.Menus.Scripts.SceneSwitcherButton>(UnityEngine.FindObjectsSortMode.None).Single(b=>b.Scene.ToString()=="'+scene+'").transform.position)')
def navigate(scene):
    p=button(scene);gesture(p['x'],p['y'])
    deadline=time.monotonic()+6
    while True:
        s=state()
        if s['scene']==scene and s['ready']: return
        if time.monotonic()>deadline: raise AssertionError(s)
        time.sleep(.1)

def capture(name):
    r=cmd('capture','game_view','--','--source','screen','--save_path','Screenshots/pointer-'+name+'.png')
    src=PROJECT/r['savedPath'];(OUT/(name+'.png')).write_bytes(src.read_bytes())
try:
    initial=state()
    cmd('glance.pointer','begin')
    navigate('HVAC' if initial['scene'] != 'HVAC' else 'BodyControl')
    navigate('BodyControl')
    capture('before')
    navigate('HVAC')
    capture('climate')
    climate=state(); passenger=climate['passenger']
    # Read geometry only; all temperature changes go through the visible drag handle.
    p=view_point('UnityEngine.RectTransformUtility.WorldToScreenPoint(null,UnityEngine.Object.FindObjectsByType<Modules.Menus.Scripts.TemperatureSelectorWidget>(UnityEngine.FindObjectsSortMode.None).OrderBy(w=>w.transform.position.x).First().GetComponentInChildren<Modules.Menus.Scripts.SelectorHandle>().transform.position)')
    observations=[climate]
    target=22 if climate['driver']==21 else 21
    for _ in range(30):
        s=state()
        if s['driver']==target:
            if target==22: target=21
            else: break
        # Short held drags in the direction of the desired temperature.
        direction=1 if s['driver']<target else -1
        gesture(p['x'],p['y'],p['x']+direction*28,p['y'],.5)
        observations.append(settled())
    final=settled()
    assert final['scene']=='HVAC' and final['driver']==21,final
    assert any(o['driver']!=21 for o in observations),'No actual temperature change demonstrated'
    assert final['passenger']==passenger,final
    capture('driver-21')
    console=cmd('console','get')['groundTruth']
    assert not console['compilationFailed'] and console['consoleErrors']==0,console
    cmd('glance.pointer','end')
    cleanup=read('return new { valid=UnityEngine.InputSystem.InputSystem.settings!=null, routing=UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode.ToString(), background=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior.ToString(), mice=UnityEngine.InputSystem.InputSystem.devices.Count(d=>d.name=="GlancePointerMouse") };')
    assert cleanup['valid'] and cleanup['mice']==0,cleanup
    result={'cleanup':cleanup,'success':True,'initial':initial,'observations':observations,'final':final,'console':console,'method':'Virtual mouse Input System events; no navigation API, temperature setters, or direct UI event invocation'}
    (OUT/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
    print('Evidence:',OUT)
except Exception as exc:
    (OUT/'failure.json').write_text(json.dumps({'error':str(exc)},indent=2)+'\n')
    raise
finally:
    try: cmd('glance.pointer','end')
    except Exception: pass
    (OUT/'trace.json').write_text(json.dumps(trace,indent=2)+'\n')
    (OUT/'trace.public.json').write_text(json.dumps(trace,indent=2).replace(str(ROOT), '<GLANCE_ROOT>')+'\n')
