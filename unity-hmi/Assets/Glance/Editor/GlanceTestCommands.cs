using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.Pipeline.Commands;
using UnityEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Modules;
using Modules.Menus.Scripts;
using Modules.BodyControl.Scripts;
using BodyPart = Modules.BodyControl.Scripts.BodyPart;
using HMI_Hub_Template;
using Newtonsoft.Json.Linq;

namespace Glance.Editor
{
    public static class GlanceTestCommands
    {
        private static bool resetPending;
        private static float resetViewReadyAt;
        private static bool resetPartsOpen;
        private static string resetScene;
        private static bool PartOpen(InterestPoint point) => (bool)ReadPrivate(point,"_isOpened");
        private static bool PartOpen(InterestPoint[] points,BodyPart part) => points.Any(p=>(BodyPart)ReadPrivate(p,"_boundPart")==part&&PartOpen(p));
        private static object ReadPrivate(object value,string field,Type declaringType=null) => value==null?null:(declaringType??value.GetType()).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(value);
        private static Vector2 Size()
        {
            var t=typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            return (Vector2)t.GetMethod("GetMainPlayModeViewTargetSize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        }
        [CliGroupCommand("glance.test","Reset a registered task's deterministic fixture; never an agent action.")]
        public static object Reset(string task="radio")
        {
            var registry=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../tasks/tasks.json"))));
            var definition=registry[task] as JObject;
            if(definition==null)throw new ArgumentException("Unknown registered Glance task: "+task);
            var fixture=definition["reset"] as JObject;
            var sceneName=fixture?.Value<string>("startScene")??"BodyControl";
            if(!Enum.TryParse<Scenes>(sceneName,out var initialScene)||initialScene==Scenes.Main)throw new ArgumentException("Invalid task reset scene");
            GlanceState.Reset();
            foreach(var car in Resources.FindObjectsOfTypeAll<CarData>())
            {
                car.LeftTemperature=22;car.RightTemperature=22;
                car.AirConditioningOn=true;car.AirflowPowerLevel=5;
                car.LeftHeatedSeatLevel=0;car.RightHeatedSeatLevel=0;car.AutoOn=false;
                car.TrunkOpen=false;car.DriverDoorOpen=false;car.PassengerDoorOpen=false;
                car.CurrentGear=CarData.Gears.Parking;
            }
            foreach(var point in UnityEngine.Object.FindObjectsByType<InterestPoint>(FindObjectsSortMode.None))point.ForceClose();
            resetPending=true;resetPartsOpen=fixture?.Value<bool?>("doorsOpen")??false;resetScene=sceneName;
            EditorApplication.update-=CompleteReset;EditorApplication.update+=CompleteReset;
            foreach(var theme in Resources.FindObjectsOfTypeAll<ThemeData>())theme.SetSelectedSchemeIndex(0);
            foreach(var selector in UnityEngine.Object.FindObjectsByType<ColorSelectorWidget>(FindObjectsSortMode.None))
                typeof(SelectorWidget).GetMethod("SelectChoice",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(selector,new object[]{0,false});
            foreach(var music in UnityEngine.Object.FindObjectsByType<MusicPlayerWidget>(FindObjectsSortMode.None))
            {
                if((int)ReadPrivate(music,"_trackIndex")!=0)music.NextTrack();
                typeof(MusicPlayerWidget).GetField("_elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(music,0f);
                var playing=fixture?.Value<bool?>("musicPlaying")??true;
                if(music.IsPlaying!=playing)music.TogglePlayPause();
            }
            UnityEngine.Object.FindFirstObjectByType<GlanceRadioUI>().Show("Home");
            UnityEngine.Object.FindFirstObjectByType<SceneSwitcherUI>().LoadScreenInMainViewport(initialScene);
            GlanceShell.PrepareTask(task=="radio");
            return State();
        }
        private static void CompleteReset()
        {
            if(!EditorApplication.isPlaying){resetPending=false;EditorApplication.update-=CompleteReset;return;}
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=resetScene)return;
            if(UnityEngine.Object.FindObjectsByType<SceneSwitcherButton>(FindObjectsSortMode.None).Any(b=>!b.GetComponent<Button>().interactable))return;
            var points=UnityEngine.Object.FindObjectsByType<InterestPoint>(FindObjectsSortMode.None);
            if(points.Any(p=>ReadPrivate(p,"_partAnimation")!=null))return;
            if(resetScene=="BodyControl")
                foreach(var orbit in UnityEngine.Object.FindObjectsByType<CinemachineOrbitalFollow>(FindObjectsSortMode.None))
                {
                    orbit.HorizontalAxis.Value=orbit.HorizontalAxis.Center;
                    orbit.VerticalAxis.Value=orbit.VerticalAxis.Center;
                }
            var gear=UnityEngine.Object.FindFirstObjectByType<GearsSwitcher>();
            if(gear!=null&&(resetScene=="ADAS"||resetScene=="BodyControl"))
                typeof(GearsSwitcher).GetMethod("SelectGear",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(gear,new object[]{resetScene=="ADAS"?CarData.Gears.First_Driving:CarData.Gears.Parking});
            resetViewReadyAt=Time.realtimeSinceStartup+.5f;
            foreach(var point in points)if(PartOpen(point)!=resetPartsOpen)point.OnButtonPressed();
            resetPending=false;EditorApplication.update-=CompleteReset;
        }
        [CliGroupCommand("glance.test","Read verifier-only state, never included in the Decisions observation.")]
        public static object State()
        {
            var car=Resources.FindObjectsOfTypeAll<CarData>().Single();
            var ui=UnityEngine.Object.FindFirstObjectByType<GlanceRadioUI>();
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var music=UnityEngine.Object.FindFirstObjectByType<MusicPlayerWidget>();
            var gear=UnityEngine.Object.FindFirstObjectByType<GearsSwitcher>();
            var gearIndex=gear==null?-1:(int)ReadPrivate(gear,"_currentlySelectedGearIndex");
            var gearNames=new[]{"P","R","N","D"};
            var theme=Resources.FindObjectsOfTypeAll<ThemeData>().FirstOrDefault();
            var simulation=UnityEngine.Object.FindFirstObjectByType<SimulationSystem>();
            var selectors=UnityEngine.Object.FindObjectsByType<SelectorWidget>(FindObjectsSortMode.None);
            var body=UnityEngine.Object.FindObjectsByType<InterestPoint>(FindObjectsSortMode.None);
            var ready=!resetPending&&Time.realtimeSinceStartup>=resetViewReadyAt&&UnityEngine.Object.FindObjectsByType<SceneSwitcherButton>(FindObjectsSortMode.None).All(b=>b.GetComponent<Button>().interactable)
                && selectors.All(s=>ReadPrivate(s,"_containerSnapCoroutine",typeof(SelectorWidget))==null&&ReadPrivate(s,"_snapBackCoroutine",typeof(SelectorWidget))==null)
                && body.All(p=>ReadPrivate(p,"_partAnimation")==null)
                && (gear==null||ReadPrivate(gear,"_underlineTween")==null)
                && (scene!="ADAS"||!SimulationSystem.IsPaused);
            return new {
                stationId=GlanceState.StationId,driverTemperature=car.LeftTemperature,passengerTemperature=car.RightTemperature,
                screenId=(ui==null||GlanceShell.Instance==null||!GlanceShell.Instance.RadioVisible?"Native":ui.Page)+"/"+scene,activeScene=scene,radioVisible=GlanceShell.Instance!=null&&GlanceShell.Instance.RadioVisible,ready,
                trunkOpen=car.TrunkOpen,driverDoorOpen=car.DriverDoorOpen,passengerDoorOpen=car.PassengerDoorOpen,
                hoodOpen=PartOpen(body,BodyPart.Hood),chargingPortOpen=PartOpen(body,BodyPart.ChargingPort),
                rearLeftDoorOpen=PartOpen(body,BodyPart.RearLeftDoor),rearRightDoorOpen=PartOpen(body,BodyPart.RearRightDoor),
                allBodyPartsClosed=scene=="BodyControl"&&body.Length>0&&body.All(p=>!PartOpen(p))&&!car.TrunkOpen&&!car.DriverDoorOpen&&!car.PassengerDoorOpen,
                resetPending,
                airConditioningOn=car.AirConditioningOn,fanLevel=car.AirflowPowerLevel,autoOn=car.AutoOn,
                driverSeatHeat=car.LeftHeatedSeatLevel,passengerSeatHeat=car.RightHeatedSeatLevel,
                gearSelection=gearIndex>=0&&gearIndex<gearNames.Length?gearNames[gearIndex]:"unavailable",
                currentGear=car.CurrentGear.ToString(),themeIndex=theme==null?-1:theme.SelectedSchemeIndex,
                musicPlaying=music==null?(bool?)null:music.IsPlaying,
                musicTrackTitle=(ReadPrivate(music,"_titleText") as TMPro.TMP_Text)?.text,
                simulationPaused=SimulationSystem.IsPaused,
                simulationRunning=scene=="ADAS"&&simulation!=null&&simulation.isActiveAndEnabled&&!SimulationSystem.IsPaused,
                speedKph=car.Speed
            };
        }
        private static bool Visible(Transform target,Vector2 point,Vector2 size)
        {
            if(!target.gameObject.activeInHierarchy||point.x<0||point.y<0||point.x>=size.x||point.y>=size.y||EventSystem.current==null)return false;
            if(target.GetComponentsInParent<CanvasGroup>().Any(g=>g.alpha<.05f||!g.interactable||!g.blocksRaycasts))return false;
            var ev=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            return hits.Count>0&&(hits[0].gameObject.transform==target||hits[0].gameObject.transform.IsChildOf(target));
        }
        private static string VisibleLabel(Transform target)
        {
            var texts=target.GetComponentsInChildren<Text>().Where(t=>t.isActiveAndEnabled&&t.color.a*t.GetComponentsInParent<CanvasGroup>().Aggregate(1f,(alpha,g)=>alpha*g.alpha)>.05f).Select(t=>t.text)
                .Concat(target.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.isActiveAndEnabled&&t.color.a*t.GetComponentsInParent<CanvasGroup>().Aggregate(1f,(alpha,g)=>alpha*g.alpha)>.05f).Select(t=>t.text));
            return string.Join(" ",texts.Where(t=>!string.IsNullOrWhiteSpace(t)).Distinct()).Trim();
        }
        [CliGroupCommand("glance.test","Enumerate only visible hit-testable controls and complete input gestures.")]
        public static object Observe()
        {
            var size=Size();var candidates=new List<object>();
            foreach(var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.isActiveAndEnabled&&b.IsInteractable()).OrderByDescending(b=>b.transform.position.y).ThenBy(b=>b.transform.position.x))
            {
                var rt=(RectTransform)b.transform;var corners=new Vector3[4];rt.GetWorldCorners(corners);
                var canvas=b.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                var p=RectTransformUtility.WorldToScreenPoint(camera,rt.position);
                if(!Visible(b.transform,p,size))continue;
                var label=VisibleLabel(b.transform);
                if(string.IsNullOrWhiteSpace(label)){var point=b.GetComponentInParent<InterestPoint>();if(point!=null)label=VisibleLabel(point.transform);}
                if(string.IsNullOrWhiteSpace(label))label="Unlabelled control";
                var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var z=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                var x=p.x/size.x*1280;var y=(1-p.y/size.y)*720;
                candidates.Add(new { id="a"+candidates.Count,label,x,y,endX=x,endY=y,duration=.12f,width=Mathf.Abs(z.x-a.x)/size.x*1280,height=Mathf.Abs(z.y-a.y)/size.y*720 });
            }
            foreach(var widget in UnityEngine.Object.FindObjectsByType<SelectorWidget>(FindObjectsSortMode.None).OrderBy(w=>w.transform.position.x))
            {
                var handle=widget.GetComponentInChildren<SelectorHandle>();if(handle==null)continue;
                var canvas=handle.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                var p=RectTransformUtility.WorldToScreenPoint(camera,handle.transform.position);if(!Visible(handle.transform,p,size))continue;
                var x=p.x/size.x*1280;var y=(1-p.y/size.y)*720;
                var label=widget is TemperatureSelectorWidget?(p.x<size.x/2?"Driver":"Passenger")+" temperature":"Visible color selector";
                foreach(var direction in new[]{-1,1})candidates.Add(new { id="a"+candidates.Count,label=label+": drag "+(direction>0?"right":"left"),x,y,endX=x+direction*28,endY=y,duration=.5f,width=28f,height=28f });
            }
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="BodyControl"&&GlanceShell.Instance!=null&&!GlanceShell.Instance.RadioVisible)
            {
                Vector2? origin=null;
                foreach(var yFraction in new[]{.5f,.4f,.6f})
                {
                    foreach(var xFraction in new[]{.5f,.4f,.6f})
                    {
                        var point=new Vector2(size.x*xFraction,size.y*yFraction);var clear=true;
                        foreach(var offset in new[]{-120f,-60f,0f,60f,120f})
                        {
                            var ev=new PointerEventData(EventSystem.current){position=point+new Vector2(offset/1280*size.x,0)};
                            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
                            if(hits.Any(h=>h.module is GraphicRaycaster)){clear=false;break;}
                        }
                        if(clear){origin=point;break;}
                    }
                    if(origin.HasValue)break;
                }
                if(origin.HasValue)
                {
                    var x=origin.Value.x/size.x*1280;var y=(1-origin.Value.y/size.y)*720;
                    foreach(var direction in new[]{-1,1})candidates.Add(new { id="a"+candidates.Count,label="Rotate vehicle view "+(direction>0?"right":"left"),x,y,endX=x+direction*120,endY=y,duration=.6f,width=240f,height=80f });
                }
            }
            return new { width=1280,height=720,controls=candidates };
        }
    }
}
