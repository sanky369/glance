using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Modules.Menus.Scripts;

namespace Glance
{
    public class GlanceRadioUI : MonoBehaviour
    {
        public string Page { get; private set; } = "Home";
        private Transform content;
        private Text station;
        private Font font;
        private readonly Color ink = new Color(.92f,.95f,1f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Mount()
        {
            if (Object.FindFirstObjectByType<GlanceRadioUI>() != null) return;
            var root = new GameObject("Glance Radio UI");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<GlanceRadioUI>();
        }
        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var panel=Box(transform,"Radio workspace",new Vector2(0,20),new Vector2(800,370),new Color(.025f,.045f,.08f,.98f));
            Label(panel,"GLANCE  /  INFOTAINMENT",new Vector2(0,145),new Vector2(730,42),25);
            station=Label(panel,"",new Vector2(0,105),new Vector2(730,32),18);
            content=Box(panel,"Controls",new Vector2(0,-15),new Vector2(740,200),new Color(.04f,.065f,.105f));
            Button(panel,"Home",new Vector2(-285,-145),new Vector2(120,38),()=>Show("Home"));
            Button(panel,"Climate",new Vector2(285,-145),new Vector2(120,38),()=> { Show("Climate"); Object.FindFirstObjectByType<SceneSwitcherUI>().LoadScreenInMainViewport(Scenes.HVAC); });
            Show("Home");
        }
        private void Update() { if(station!=null) station.text="Now playing: "+Name(GlanceState.StationId); }
        public void Show(string page)
        {
            Page=page;
            foreach(Transform t in content) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
            if(page=="Climate") { Label(content,"Use the driver temperature handle below",Vector2.zero,new Vector2(700,50),22); return; }
            if(page=="Home")
            {
                Button(content,"BBC Radio 1",new Vector2(-230,40),new Vector2(210,72),()=>Tune("radio_1"));
                Button(content,"BBC Radio 2",new Vector2(0,40),new Vector2(210,72),()=>Tune("radio_2"));
                Button(content,"BBC Radio 3",new Vector2(230,40),new Vector2(210,72),()=>Tune("radio_3"));
                Button(content,"Media",new Vector2(-230,-48),new Vector2(210,64),()=>Show("Media"));
                Button(content,"Vehicle",new Vector2(0,-48),new Vector2(210,64),()=>Show("Vehicle"));
                Button(content,"Settings",new Vector2(230,-48),new Vector2(210,64),()=>Show("Settings"));
            }
            else if(page=="Media") { Menu("Sources",-180,()=>Show("Sources"));Menu("Library",0,()=>Show("Library"));Menu("Audio",180,()=>Show("Audio")); }
            else if(page=="Sources") { Menu("FM radio",-180,()=>Show("FM"));Menu("Bluetooth",0,()=>Show("Bluetooth"));Menu("USB",180,()=>Show("USB")); }
            else if(page=="FM") { Menu("Presets",-140,()=>Show("Presets"));Menu("Scan",140,()=>Show("Scan")); }
            else if(page=="Presets")
            {
                Label(content,"FM presets",new Vector2(0,72),new Vector2(650,32),21);
                Button(content,"BBC Radio 1",new Vector2(-230,10),new Vector2(210,72),()=>Tune("radio_1"));
                Button(content,"BBC Radio 2",new Vector2(0,10),new Vector2(210,72),()=>Tune("radio_2"));
                Button(content,"BBC Radio 3",new Vector2(230,10),new Vector2(210,72),()=>Tune("radio_3"));
            }
            else Label(content,page,new Vector2(0,25),new Vector2(650,45),24);
            if(page!="Home") Button(content,"Back",new Vector2(0,-65),new Vector2(140,64),()=>Show(Parent(page)));
        }
        private void Tune(string id) { GlanceState.SelectStation(id); Show("Playing"); }
        private static string Name(string id)=>id=="radio_2"?"BBC Radio 2":id=="radio_3"?"BBC Radio 3":"BBC Radio 1";
        private static string Parent(string page)=>page=="Presets"?"FM":page=="FM"?"Sources":page=="Sources"?"Media":"Home";
        private void Menu(string label,float x,UnityAction action)=>Button(content,label,new Vector2(x,10),new Vector2(164,64),action);
        private Transform Box(Transform parent,string name,Vector2 at,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=at;rt.sizeDelta=size;
            go.GetComponent<Image>().color=color;return go.transform;
        }
        private Text Label(Transform parent,string text,Vector2 at,Vector2 size,int points)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchoredPosition=at;rt.sizeDelta=size;
            var label=go.GetComponent<Text>();label.font=font;label.text=text;label.fontSize=points;label.color=ink;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
        }
        private void Button(Transform parent,string label,Vector2 at,Vector2 size,UnityAction action)
        {
            var t=Box(parent,label,at,size,new Color(.13f,.20f,.29f));var b=t.gameObject.AddComponent<Button>();b.targetGraphic=t.GetComponent<Image>();b.onClick.AddListener(action);Label(t,label,Vector2.zero,size,17);
        }
    }
}
