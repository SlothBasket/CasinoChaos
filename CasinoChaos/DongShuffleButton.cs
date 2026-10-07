using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GWYF_CasinoChaos
{
    internal static class DongShuffleButton
    {
        private static GameObject _object;
        private static NetworkIdentity _owner;
        private static Button _button;
        internal static void Tick()
        {
            if(_owner!=NetworkClient.localPlayer)Shutdown();
            if(!_object&&NetworkClient.localPlayer)Create(NetworkClient.localPlayer);
            if(!_button)return;
            var profile=_owner.GetComponent<PlayerProfile>();
            var state=profile?BodyPartState.Get(profile.steamId):BodyState.Complete;
            _button.interactable=NetworkClient.ready&&state.Revision!=0&&state.Has(CustomBodyPart.Dong);
        }
        private static void Create(NetworkIdentity owner)
        {
            var quit=owner.transform.Find("PlayerUI/EscapeMenu/Panel/Inner/QuitButton") as RectTransform;
            var image=quit?quit.GetComponent<Image>():null;
            var text=quit?quit.GetComponentInChildren<TextMeshProUGUI>(true):null;
            if(!quit||!image||!text)return;
            _owner=owner;
            _object=new GameObject("CasinoChaosShuffleDong",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(LayoutElement));
            var rect=_object.GetComponent<RectTransform>();rect.SetParent(quit.parent,false);
            rect.anchorMin=quit.anchorMin;rect.anchorMax=quit.anchorMax;rect.pivot=quit.pivot;
            rect.sizeDelta=new Vector2(quit.sizeDelta.x,56);rect.anchoredPosition=quit.anchoredPosition+new Vector2(0,-80);
            _object.GetComponent<LayoutElement>().ignoreLayout=true;
            var background=_object.GetComponent<Image>();background.sprite=image.sprite;background.material=image.material;background.color=image.color;background.type=image.type;
            _button=_object.GetComponent<Button>();_button.targetGraphic=background;
            var original=quit.GetComponent<Button>();if(original)_button.colors=original.colors;
            _button.onClick.AddListener(DongAppearanceNetwork.RequestShuffle);
            var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
            var labelRect=label.GetComponent<RectTransform>();labelRect.SetParent(rect,false);labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;
            labelRect.offsetMin=new Vector2(12,4);labelRect.offsetMax=new Vector2(-12,-4);
            var tmp=label.GetComponent<TextMeshProUGUI>();tmp.font=text.font;tmp.fontSharedMaterial=text.fontSharedMaterial;tmp.color=text.color;
            tmp.alignment=TextAlignmentOptions.Center;tmp.enableAutoSizing=true;tmp.fontSizeMin=18;tmp.fontSizeMax=26;tmp.raycastTarget=false;
            tmp.text="Shuffle Dong Size  [F7]";
        }
        internal static void Shutdown()
        {if(_object){_object.SetActive(false);Object.Destroy(_object);}_object=null;_button=null;_owner=null;}
    }
}
