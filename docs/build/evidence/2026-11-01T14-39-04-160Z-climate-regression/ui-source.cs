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
        private Sprite surface;
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
            font = Resources.Load<Font>("Glance/Inter") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            surface = RoundedSurface();
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var panel=Box(transform,"Radio workspace",new Vector2(0,20),new Vector2(920,400),new Color(.035f,.043f,.045f,.98f));
            var art = new GameObject("Studio artwork",typeof(RectTransform),typeof(RawImage)); art.transform.SetParent(panel,false);
            var artRect=(RectTransform)art.transform;artRect.anchorMin=Vector2.zero;artRect.anchorMax=Vector2.one;artRect.offsetMin=artRect.offsetMax=Vector2.zero;
            var image=art.GetComponent<RawImage>();image.texture=Resources.Load<Texture2D>("Glance/Studio");image.color=new Color(1,1,1,.35f);image.raycastTarget=false;
            Label(panel,"G L A N C E   /   RADIO",new Vector2(-195,161),new Vector2(460,34),22);
            Label(panel,"CONNECTED DRIVE",new Vector2(300,161),new Vector2(260,34),11);
            station=Label(panel,"",new Vector2(-160,118),new Vector2(540,32),16);
            content=Box(panel,"Controls",new Vector2(0,-15),new Vector2(840,200),new Color(.055f,.066f,.068f,.88f));
            Button(panel,"Home",new Vector2(-335,-156),new Vector2(160,46),()=>Show("Home"));
            Button(panel,"Climate",new Vector2(335,-156),new Vector2(160,46),()=> { Show("Climate"); Object.FindFirstObjectByType<SceneSwitcherUI>().LoadScreenInMainViewport(Scenes.HVAC); });
            Show("Home");
        }
        private void Update() { if(station!=null) station.text="Now playing: "+Name(GlanceState.StationId); }
        public void Show(string page)
        {
            Page=page;
            foreach(Transform t in content) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
            if(page=="Climate") { Label(content,"Use the driver temperature handle below",Vector2.zero,new Vector2(700,50),22); return; }
            if(page=="Home") {
                Menu("Media",-180,()=>Show("Media")); Menu("Vehicle",0,()=>Show("Vehicle"));Menu("Settings",180,()=>Show("Settings"));
                Button(content,"BBC Radio 1",new Vector2(-220,-60),new Vector2(200,64),()=>Tune("radio_1"));
                Button(content,"BBC Radio 2",new Vector2(0,-60),new Vector2(200,64),()=>Tune("radio_2"));
                Button(content,"BBC Radio 3",new Vector2(220,-60),new Vector2(200,64),()=>Tune("radio_3"));
            }
            else if(page=="Media") { Menu("Sources",-180,()=>Show("Sources"));Menu("Library",0,()=>Show("Library"));Menu("Audio",180,()=>Show("Audio")); }
            else if(page=="Sources") { Menu("FM radio",-180,()=>Show("FM"));Menu("Bluetooth",0,()=>Show("Bluetooth"));Menu("USB",180,()=>Show("USB")); }
            else if(page=="FM") { Menu("Presets",-140,()=>Show("Presets"));Menu("Scan",140,()=>Show("Scan")); }
            else if(page=="Presets")
            {
                Label(content,"FM presets",new Vector2(0,60),new Vector2(650,40),21);
                Button(content,"BBC Radio 1",new Vector2(-220,0),new Vector2(200,64),()=>Tune("radio_1"));
                Button(content,"BBC Radio 2",new Vector2(0,0),new Vector2(200,64),()=>Tune("radio_2"));
                Button(content,"BBC Radio 3",new Vector2(220,0),new Vector2(200,64),()=>Tune("radio_3"));
            }
            else Label(content,page,new Vector2(0,25),new Vector2(650,45),24);
            if(page!="Home") Button(content,"Back",new Vector2(0,-65),new Vector2(95,64),()=>Show(Parent(page)));
        }
        private void Tune(string id) { GlanceState.SelectStation(id); Show("Playing"); }
        private static string Name(string id)=>id=="radio_2"?"BBC Radio 2":id=="radio_3"?"BBC Radio 3":"BBC Radio 1";
        private static string Parent(string page)=>page=="Presets"?"FM":page=="FM"?"Sources":page=="Sources"?"Media":"Home";
        private void Menu(string label,float x,UnityAction action)=>Button(content,label,new Vector2(x,35),new Vector2(140,64),action);
        private Transform Box(Transform parent,string name,Vector2 at,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=at;rt.sizeDelta=size;
            var image=go.GetComponent<Image>();image.color=color;image.sprite=surface;image.type=Image.Type.Sliced;
            var edge=go.AddComponent<Outline>();edge.effectColor=new Color(.65f,.75f,.65f,.12f);edge.effectDistance=new Vector2(1,-1);return go.transform;
        }
        private Text Label(Transform parent,string text,Vector2 at,Vector2 size,int points)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchoredPosition=at;rt.sizeDelta=size;
            var label=go.GetComponent<Text>();label.font=font;label.text=text;label.fontSize=points;label.color=ink;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
        }
        private static Sprite RoundedSurface()
        {
            const int size=48;const float radius=12f;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="Glance rounded surface";
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var nearest=new Vector2(Mathf.Clamp(x,radius,size-radius-1),Mathf.Clamp(y,radius,size-radius-1));
                var alpha=Mathf.Clamp01(radius-Vector2.Distance(new Vector2(x,y),nearest)+.5f);
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));
        }
        private void Button(Transform parent,string label,Vector2 at,Vector2 size,UnityAction action)
        {
            var t=Box(parent,label,at,size,new Color(.13f,.16f,.155f,.98f));var b=t.gameObject.AddComponent<Button>();b.targetGraphic=t.GetComponent<Image>();var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.78f,.92f,.64f);colors.pressedColor=new Color(.6f,.8f,.4f);colors.selectedColor=Color.white;colors.fadeDuration=.12f;b.colors=colors;
            b.onClick.AddListener(action);var text=Label(t,label,Vector2.zero,size,17);text.fontStyle=FontStyle.Bold;
        }
    }
}
