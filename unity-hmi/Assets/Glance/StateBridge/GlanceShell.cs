using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Modules.Menus.Scripts;

namespace Glance
{
    public sealed class GlanceShell : MonoBehaviour
    {
        public static GlanceShell Instance { get; private set; }
        public bool RadioVisible => panel != null && panel.gameObject.activeSelf;
        private Transform panel;
        private Text toggleText;
        private Text hint;
        private SceneSwitcherUI switcher;
        private bool pendingReset;
        private bool resetRadio;
        private void Awake()
        {
            Instance=this;panel=transform.Find("Radio workspace");
            var go=new GameObject("Radio workspace toggle",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(transform,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=new Vector2(1,0);rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(-220,48);rt.sizeDelta=new Vector2(150,38);
            var image=go.GetComponent<Image>();image.color=new Color(.25f,.32f,.2f,.98f);if(panel!=null){image.sprite=panel.GetComponent<Image>().sprite;image.type=Image.Type.Sliced;}
            go.GetComponent<Button>().onClick.AddListener(()=>{pendingReset=false;SetRadioVisible(!RadioVisible);});
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            toggleText=label.GetComponent<Text>();toggleText.font=Resources.Load<Font>("Glance/Inter")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");toggleText.fontSize=14;toggleText.color=new Color(.88f,.96f,.75f);toggleText.alignment=TextAnchor.MiddleCenter;toggleText.raycastTarget=false;
            var help=new GameObject("Vehicle interaction hint",typeof(RectTransform),typeof(Text));help.transform.SetParent(transform,false);var hr=(RectTransform)help.transform;hr.anchorMin=hr.anchorMax=new Vector2(.5f,0);hr.anchoredPosition=new Vector2(0,115);hr.sizeDelta=new Vector2(520,22);hint=help.GetComponent<Text>();hint.font=toggleText.font;hint.fontSize=11;hint.color=new Color(.75f,.83f,.66f);hint.alignment=TextAnchor.MiddleCenter;hint.raycastTarget=false;
            ApplyLabel();
        }
        private void Start()
        {
            switcher=Object.FindAnyObjectByType<SceneSwitcherUI>();if(switcher!=null)switcher.SceneChanged+=OnNativeScene;GlanceNativeLabels.Enhance();
        }
        private void OnDestroy(){if(switcher!=null)switcher.SceneChanged-=OnNativeScene;if(Instance==this)Instance=null;}
        private void OnNativeScene(Scenes scene)
        {
            GlanceNativeLabels.Enhance();
            if(pendingReset){pendingReset=false;SetRadioVisible(resetRadio);}else SetRadioVisible(false);
        }
        public static void PrepareTask(bool radio)
        {
            if(Instance==null)return;
            Instance.resetRadio=radio;
            Instance.pendingReset=Object.FindObjectsByType<SceneSwitcherButton>(FindObjectsSortMode.None).Any(b=>!b.GetComponent<Button>().interactable);
            SetRadioVisible(radio);
        }
        public static void SetRadioVisible(bool visible)
        {
            if(Instance?.panel==null)return;Instance.panel.gameObject.SetActive(visible);Instance.ApplyLabel();
        }
        private void ApplyLabel(){if(toggleText!=null)toggleText.text=RadioVisible?"Full HMI":"Radio";if(hint!=null)hint.text=!RadioVisible&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="BodyControl"?"Drag vehicle to rotate · tap a visible body control":"";}
    }
}
