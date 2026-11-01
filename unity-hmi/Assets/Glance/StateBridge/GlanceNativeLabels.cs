using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Modules.HVAC.Scripts;
using HMI_Hub_Template;

namespace Glance
{
    public static class GlanceNativeLabels
    {
        private const string CaptionName="Glance control caption";

        // Called after a native scene has loaded. Captions are real rendered UI, never agent metadata.
        public static void Enhance()
        {
            foreach(var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                for(var index=0;index<button.onClick.GetPersistentEventCount();index++)
                {
                    if(!(button.onClick.GetPersistentTarget(index) is MusicPlayerWidget))continue;
                    switch(button.onClick.GetPersistentMethodName(index))
                    {
                        case "TogglePlayPause":Caption(button,"Play / Pause",false,96);break;
                        case "NextTrack":Caption(button,"Next",true);break;
                        case "PreviousTrack":Caption(button,"Previous",true);break;
                    }
                }
            if(SceneManager.GetActiveScene().name!="HVAC")return;
            foreach(var control in Object.FindObjectsByType<OnOffButton>(FindObjectsSortMode.None))
                Caption(control.GetComponent<Button>(),"A/C power",false,96);
            foreach(var control in Object.FindObjectsByType<AutoButton>(FindObjectsSortMode.None))
                Caption(control.GetComponent<Button>(),"Auto climate",false,96);
            foreach(var control in Object.FindObjectsByType<AirflowPowerControl>(FindObjectsSortMode.None))
            {
                Caption(Read<Button>(control,"_leftButton"),"Fan −",true);
                Caption(Read<Button>(control,"_rightButton"),"Fan +",true);
            }
            foreach(var control in Object.FindObjectsByType<HeatedSeat>(FindObjectsSortMode.None))
            {
                var side=typeof(HeatedSeat).GetField("_side",BindingFlags.Instance|BindingFlags.NonPublic);
                if(side==null)continue;
                var driver=(AirflowVisualsController.Sides)side.GetValue(control)==AirflowVisualsController.Sides.Left;
                Caption(control.GetComponent<Button>(),driver?"Driver seat heat":"Passenger seat heat");
            }
            foreach(var control in Object.FindObjectsByType<ToggleButton>(FindObjectsSortMode.None))
            {
                var button=control.GetComponent<Button>();
                if(button!=null&&button.transform.Find(CaptionName)==null)Caption(button,"Airflow",true);
            }
        }

        private static T Read<T>(object source,string field) where T:class =>
            source.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(source) as T;

        private static void Caption(Button button,string caption,bool compact=false,float width=140)
        {
            if(button==null)return;
            var existing=button.transform.Find(CaptionName);
            Text text;
            if(existing!=null)text=existing.GetComponent<Text>();
            else
            {
                var go=new GameObject(CaptionName,typeof(RectTransform),typeof(LayoutElement),typeof(Text),typeof(Shadow));
                go.layer=button.gameObject.layer;
                go.transform.SetParent(button.transform,false);
                go.GetComponent<LayoutElement>().ignoreLayout=true;
                var rect=(RectTransform)go.transform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);
                rect.pivot=new Vector2(.5f,1);
                rect.anchoredPosition=new Vector2(0,-5);
                rect.sizeDelta=new Vector2(compact?64:width,20);
                text=go.GetComponent<Text>();
                text.font=Resources.Load<Font>("Glance/Inter")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize=12;
                text.fontStyle=FontStyle.Normal;
                text.alignment=TextAnchor.UpperCenter;
                text.color=new Color(.88f,.92f,.96f,1f);
                text.horizontalOverflow=HorizontalWrapMode.Overflow;
                text.verticalOverflow=VerticalWrapMode.Overflow;
                text.raycastTarget=false;
                text.supportRichText=false;
                var shadow=go.GetComponent<Shadow>();
                shadow.effectColor=new Color(.015f,.02f,.03f,.9f);
                shadow.effectDistance=new Vector2(0,-1);
            }
            if(text!=null)text.text=caption;
        }
    }
}
