using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Modules.Menus.Scripts;

namespace Glance.Editor
{
    public static class GlanceTestCommands
    {
        private static Vector2 Size()
        {
            var t=typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            return (Vector2)t.GetMethod("GetMainPlayModeViewTargetSize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        }
        [CliGroupCommand("glance.test","Reset the fixed test state; never an agent action.")]
        public static object Reset()
        {
            GlanceState.Reset();
            foreach(var car in Resources.FindObjectsOfTypeAll<Modules.CarData>()) { car.LeftTemperature=22;car.RightTemperature=22; }
            UnityEngine.Object.FindFirstObjectByType<GlanceRadioUI>().Show("Home");
            UnityEngine.Object.FindFirstObjectByType<SceneSwitcherUI>().LoadScreenInMainViewport(Scenes.BodyControl);
            return State();
        }
        [CliGroupCommand("glance.test","Read verifier-only state, never included in the Decisions observation.")]
        public static object State()
        {
            var car=Resources.FindObjectsOfTypeAll<Modules.CarData>().Single();
            var ui=UnityEngine.Object.FindFirstObjectByType<GlanceRadioUI>();
            return new { stationId=GlanceState.StationId,driverTemperature=car.LeftTemperature,passengerTemperature=car.RightTemperature,screenId=ui.Page+"/"+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                ready=UnityEngine.Object.FindObjectsByType<SceneSwitcherButton>(FindObjectsSortMode.None).All(b=>b.GetComponent<Button>().interactable) };
        }
        [CliGroupCommand("glance.test","Enumerate only visible hit-testable controls and complete input gestures.")]
        public static object Observe()
        {
            var size=Size();var candidates=new List<object>();
            var buttons=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.isActiveAndEnabled&&b.IsInteractable()).OrderByDescending(b=>b.transform.position.y).ThenBy(b=>b.transform.position.x);
            foreach(var b in buttons)
            {
                var rt=(RectTransform)b.transform;var corners=new Vector3[4];rt.GetWorldCorners(corners);
                var p=RectTransformUtility.WorldToScreenPoint(null,rt.position);
                if(p.x<0||p.y<0||p.x>=size.x||p.y>=size.y) continue;
                var ev=new PointerEventData(EventSystem.current){position=p};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
                if(hits.Count==0||!(hits[0].gameObject.transform==b.transform||hits[0].gameObject.transform.IsChildOf(b.transform)))continue;
                var label=b.GetComponentInChildren<Text>()?.text;
                if(string.IsNullOrWhiteSpace(label)) label=b.GetComponentInChildren<TMPro.TMP_Text>()?.text;
                if(string.IsNullOrWhiteSpace(label)) label="Unlabelled control";
                var x=p.x/size.x*1280;var y=(1-p.y/size.y)*720;
                candidates.Add(new { id="a"+candidates.Count,label,x,y,endX=x,endY=y,duration=.12f,width=(corners[2].x-corners[0].x)/size.x*1280,height=(corners[2].y-corners[0].y)/size.y*720 });
            }
            foreach(var w in UnityEngine.Object.FindObjectsByType<TemperatureSelectorWidget>(FindObjectsSortMode.None).OrderBy(w=>w.transform.position.x))
            {
                var handle=w.GetComponentInChildren<SelectorHandle>();var p=RectTransformUtility.WorldToScreenPoint(null,handle.transform.position);
                var left=w.transform.position.x<size.x/2;var x=p.x/size.x*1280;var y=(1-p.y/size.y)*720;
                foreach(var direction in new[]{-1,1})candidates.Add(new { id="a"+candidates.Count,label=(left?"Driver":"Passenger")+" temperature: drag "+(direction>0?"right":"left"),x,y,endX=x+direction*28,endY=y,duration=.5f,width=28f,height=28f });
            }
            return new { width=1280,height=720,controls=candidates };
        }
    }
}
