using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Extensions;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal sealed class EarMachineController : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<BodyShreddingMachine,bool> Buying=AccessTools.FieldRefAccess<BodyShreddingMachine,bool>("_isLeverBuy");
        private static readonly AccessTools.FieldRef<BodyShreddingMachine,bool> LidOpen=AccessTools.FieldRefAccess<BodyShreddingMachine,bool>("_isLidOpen");
        private static readonly AccessTools.FieldRef<BodyShreddingMachine,bool> Processing=AccessTools.FieldRefAccess<BodyShreddingMachine,bool>("_isProcessing");
        private static readonly AccessTools.FieldRef<BodyShreddingMachine,MeshRenderer[]> Meshes=AccessTools.FieldRefAccess<BodyShreddingMachine,MeshRenderer[]>("buttonMeshes");
        private static readonly AccessTools.FieldRef<BodyShreddingMachine,TextMeshPro> PriceTag=AccessTools.FieldRefAccess<BodyShreddingMachine,TextMeshPro>("priceTag");
        private static readonly AccessTools.FieldRef<BodyShreddingMachine,SFXComponent> BuySound=AccessTools.FieldRefAccess<BodyShreddingMachine,SFXComponent>("buyBackSfx");
        private static readonly MethodInfo Occupant=AccessTools.Method(typeof(BodyShreddingMachine),"GetPlayersOrgans");
        private static readonly MethodInfo Process=AccessTools.Method(typeof(BodyShreddingMachine),"ProcessRoutine");
        private static readonly MethodInfo SellSound=AccessTools.Method(typeof(BodyShreddingMachine),"RpcOnEyeShredded");
        private static readonly MethodInfo SetPrices=AccessTools.Method(typeof(BodyShreddingMachine),"ServerSetPrices");
        private readonly Dictionary<MachinePart,EarMachineButton> _buttons=new Dictionary<MachinePart,EarMachineButton>();
        private readonly Dictionary<MachinePart,IInteractable> _prompts=new Dictionary<MachinePart,IInteractable>();
        private readonly Dictionary<InteractableEventTrigger,(string Name,string Tooltip)> _originalPrompts=new Dictionary<InteractableEventTrigger,(string,string)>();
        private readonly Dictionary<Transform,Vector3> _positions=new Dictionary<Transform,Vector3>();
        private readonly Dictionary<Transform,Vector3> _scales=new Dictionary<Transform,Vector3>();
        private readonly List<GameObject> _clones=new List<GameObject>();
        private MeshRenderer[] _originalMeshes;
        private TextMeshPro _tag, _rightTag;
        private Vector2 _tagSize, _tagPosition;
        private TextAlignmentOptions _alignment;
        private TextWrappingModes _wrapping;
        private float _font,_fontMin,_fontMax,_lineSpacing;
        private TextOverflowModes _overflow;
        private bool _autoSize,_viewKnown,_buying,_restored;
        private string _basePriceText;
        internal BodyShreddingMachine Machine {get;private set;}
        internal bool Initialized {get;private set;}
        internal static EarMachineController Ensure(BodyShreddingMachine machine)
        {
            if(!machine||machine.netId==0)return null;
            var c=machine.GetComponent<EarMachineController>();
            if(c)return c.Initialized?c:null;
            c=machine.gameObject.AddComponent<EarMachineController>();
            try{c.Configure(machine);return c;}
            catch(Exception e){CasinoChaosPlugin.Log("Body machine setup failed: "+e);c.Restore();Destroy(c);return null;}
        }
        private void Configure(BodyShreddingMachine machine)
        {
            Machine=machine;
            var eye=machine.transform.Find("Model/Base/EyeButton");
            if(!eye)throw new InvalidOperationException("Confirmed Eye hierarchy missing");
            Vector3 templateScale=eye.localScale, templatePosition=eye.localPosition;
            if(BodyEconomy.Entries.Length>BodyMachineLayout.Capacity)throw new InvalidOperationException("Body button grid capacity exceeded; review machine geometry before adding entries");
            _originalMeshes=Meshes(machine);var meshes=new List<MeshRenderer>(_originalMeshes);
            _tag=PriceTag(machine);
            if(_tag){_basePriceText=_tag.text;_font=_tag.fontSize;_fontMin=_tag.fontSizeMin;_fontMax=_tag.fontSizeMax;_autoSize=_tag.enableAutoSizing;_lineSpacing=_tag.lineSpacing;_overflow=_tag.overflowMode;_tagSize=_tag.rectTransform.sizeDelta;_tagPosition=_tag.rectTransform.anchoredPosition;_alignment=_tag.alignment;_wrapping=_tag.textWrappingMode;}
            var staging=new GameObject("CasinoChaosBodyButtonStaging");staging.SetActive(false);
            try{
                foreach(var entry in BodyEconomy.Entries){
                    Transform button;
                    if(entry.VanillaButton!=null){
                        button=eye.parent.Find(entry.VanillaButton);
                        if(!button)throw new InvalidOperationException("Missing "+entry.VanillaButton);
                        _positions.Add(button,button.localPosition);_scales.Add(button,button.localScale);
                        var interaction=button.GetComponent<InteractableEventTrigger>();
                        if(!interaction)throw new InvalidOperationException("Missing vanilla button interaction: "+entry.VanillaButton);
                        _originalPrompts.Add(interaction,(interaction.InteractableName,interaction.TooltipMessage));
                        _prompts.Add(entry.Id,interaction);
                    }else{
                        var go=Instantiate(eye.gameObject,staging.transform,false);_clones.Add(go);
                        go.SetActive(false);go.name="CasinoChaos"+entry.DisplayName+"Button";
                        if(go.GetComponentsInChildren<NetworkIdentity>(true).Length!=0)throw new InvalidOperationException("Unexpected clone NetworkIdentity");
                        var template=go.GetComponent<InteractableEventTrigger>();
                        var adapter=go.AddComponent<EarMachineButton>();adapter.Configure(this,template,entry.Id);_buttons.Add(entry.Id,adapter);_prompts.Add(entry.Id,adapter);
                        foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true))
                            if(component&&(component is NetworkBehaviour||component.GetType().Name=="GameObjectLocalizer"||component.GetType().Name=="ButtonTextSetter"))DestroyImmediate(component);
                        foreach(var label in go.GetComponentsInChildren<TextMeshPro>(true))label.text=entry.DisplayName;
                        button=go.transform;button.SetParent(eye.parent,false);button.localRotation=eye.localRotation;button.localScale=templateScale;
                        foreach(string path in new[]{"Model/SM_button_1","Model/SM_button_1 (1)"}){
                            var mesh=button.Find(path)?.GetComponent<MeshRenderer>();if(!mesh)throw new InvalidOperationException("Missing button model");meshes.Add(mesh);
                        }
                    }
                    // One shared definition order controls both originals and clones.
                    button.localScale=(_scales.TryGetValue(button,out var originalScale)?originalScale:templateScale)*BodyMachineLayout.Scale;
                    button.localPosition=new Vector3(BodyMachineLayout.X(entry.MachineOrder,templatePosition.x),BodyMachineLayout.Y(entry.MachineOrder),templatePosition.z);

                }
                Meshes(machine)=meshes.ToArray();Initialized=true;
                foreach(var go in _clones)go.SetActive(true);
            }finally{Destroy(staging);}
            if(NetworkServer.active){SetPrices.Invoke(machine,null);ApplyView(ServerView());EarMachineNetwork.Publish(this);}
            EarMachineNetwork.ApplyPending(this);RefreshPrice();
            CasinoChaosPlugin.Log("Body machine entries installed: EYE/EAR/MOUTH/DONG/LEG/BODY/BUTT; machine="+machine.netId);
        }
        internal EarMachineView ServerView()=>new EarMachineView{Machine=Machine.netId,Buying=Buying(Machine),EyePrice=BodyEconomy.Get(MachinePart.Eye).SellValue};
        internal void ApplyView(EarMachineView view)
        {
            if(!Initialized||view.Machine!=Machine.netId)return;
            bool first=!_viewKnown;_viewKnown=true;_buying=view.Buying;
            RefreshPrompts();
            var color=(Color)AccessTools.Field(typeof(BodyShreddingMachine),_buying?"buttonAddColor":"buttonRemoveColor").GetValue(Machine);
            foreach(var mesh in Meshes(Machine))if(mesh)mesh.material.SetColor("_Color",color);
            if(first&&!NetworkServer.active){
                foreach(var lever in (Transform[])AccessTools.Field(typeof(BodyShreddingMachine),"leverTransforms").GetValue(Machine))if(lever)lever.localRotation=Quaternion.Euler(_buying?Vector3.forward*75:Vector3.zero);
                string mode=(string)AccessTools.Field(typeof(BodyShreddingMachine),_buying?"localizedBuyString":"localizedSellString").GetValue(Machine);
                foreach(var text in (TextMeshPro[])AccessTools.Field(typeof(BodyShreddingMachine),"modeTexts").GetValue(Machine))if(text){text.text=mode;text.color=_buying?Color.forestGreen:Color.crimson;}
            }
            RefreshPrice();
        }
        private void RefreshPrompts()
        {
            if(!Initialized||!_viewKnown)return;
            foreach(var entry in BodyEconomy.Entries)
            {
                if(!_prompts.TryGetValue(entry.Id,out var interaction))continue;
                string title=entry.DisplayName;
                string tooltip="[E] "+(_buying?"Buy ":"Sell ")+title;
                if(interaction.InteractableName!=title)interaction.InteractableName=title;
                if(interaction.TooltipMessage!=tooltip)interaction.TooltipMessage=tooltip;
            }
        }
        // Vanilla localization can arrive after initial mode setup. Keep the
        // same mode-aware strings, notifying hovered UI only when they differ.
        private void LateUpdate()=>RefreshPrompts();
        internal void PricesDisplayed(int eyePrice,string vanillaText){if(vanillaText!=null)_basePriceText=vanillaText;RefreshPrice();}
        internal void ModeDisplayed(bool buying)=>ApplyView(new EarMachineView{Machine=Machine.netId,Buying=buying});
        private void RefreshPrice()
        {
            if(!_tag)return;
            if(!_rightTag){
                // PriceTag is a standalone world TMP object. Copy only its text
                // style, stripping localization so neither column gets overwritten.
                var go=Instantiate(_tag.gameObject,_tag.transform.parent,false);go.name="CasinoChaosPriceColumn";_clones.Add(go);
                foreach(var component in go.GetComponents<MonoBehaviour>())if(!(component is TextMeshPro))DestroyImmediate(component);
                _rightTag=go.GetComponent<TextMeshPro>();
                float width=(_tagSize.x-.04f)/2;
                _tag.rectTransform.sizeDelta=_rightTag.rectTransform.sizeDelta=new Vector2(width,_tagSize.y);
                // PriceTag faces backward (Y=180), so screen-right is local -X.
                _tag.rectTransform.anchoredPosition=_tagPosition+Vector2.right*(_tagSize.x-width)/2;
                _rightTag.rectTransform.anchoredPosition=_tagPosition-Vector2.right*(_tagSize.x-width)/2;
            }
            var columns=new[]{new StringBuilder(),new StringBuilder()};
            foreach(var entry in BodyEconomy.Entries){var text=columns[entry.MachineOrder%2];if(text.Length>0)text.Append('\n');text.Append(entry.DisplayName).Append(" <color=yellow>").Append(entry.Price(_buying)).Append("</color>");}
            for(int i=0;i<2;i++){
                var tag=i==0?_tag:_rightTag;tag.text=columns[i].ToString();tag.enableAutoSizing=true;
                tag.fontSizeMax=_font;tag.fontSizeMin=_font*.50f;tag.lineSpacing=0;
                tag.textWrappingMode=TextWrappingModes.NoWrap;tag.alignment=TextAlignmentOptions.TopLeft;tag.overflowMode=TextOverflowModes.Truncate;
            }
        }
        internal bool CanPress(PlayerInteract player,MachinePart entry)
        {
            if(!_buttons.TryGetValue(entry,out var button)||!Initialized||!Machine.isActiveAndEnabled||!player||player.connectionToClient==null||!button.IsInteractable||!button.MeetRequirements)return false;
            var inventory=player.GetComponent<PlayerInventory>();var controller=player.GetComponent<PlayerController>();
            if(!inventory||!controller||inventory.NetworkholdingItem||(controller.State==PlayerController.PlayerState.Ragdoll&&controller.hasBody))return false;
            var head=player.GetComponentInChildren<PlayerHead>();Vector3 origin=head?head.transform.position:player.transform.position;
            return Vector3.Distance(origin,button.transform.position)<=player.raycastDistance+player.raycastRadius+player.raycastBlockThreshold;
        }
        internal void PlayFeedback(PlayerInteract player,MachinePart entry){if(_buttons.TryGetValue(entry,out var button))button.RpcOnInteract(player);}
        internal void ServerTrade(PlayerInteract player,MachinePart entry)
        {
            if(!NetworkServer.active||!Initialized||!_buttons.ContainsKey(entry)||LidOpen(Machine)||Processing(Machine))return;
            var organs=(PlayerOrgans)Occupant.Invoke(Machine,null);var profile=organs?organs.GetComponent<PlayerProfile>():null;
            if(!profile||organs.connectionToClient==null||!BodyPartNetwork.TryGetServerState(profile.steamId,out var state))return;
            bool buying=Buying(Machine);
            if(!BodyEconomy.Choose(state,entry,buying,UnityEngine.Random.value>=.5f,out var part)){CasinoChaosPlugin.Log(entry+" rejected: no eligible part");return;}
            var money=NetworkSingleton<MoneyManager>.Instance;int price=BodyEconomy.Get(entry).Price(buying);
            if(!money)return;
            bool chooseRight=BodyEconomy.CustomPair(entry,out _,out var right) && part==right;
            if(!BodyEconomy.TryTransact(state,entry,buying,chooseRight,money.TryChangeTicketBalance,
                (chosen,present)=>BodyPartNetwork.ServerSet(profile.steamId,chosen,present),out part))return;
            if(buying&&entry==MachinePart.Dong)DongAppearanceNetwork.ServerReroll(profile.steamId);
            Machine.StartCoroutine((IEnumerator)Process.Invoke(Machine,null));
            if(buying)BuySound(Machine).RpcPlayOneShotWith3DPos();else SellSound.Invoke(Machine,null);
            CasinoChaosPlugin.Log($"{entry} {(buying?"buy":"sell")}: occupant={profile.playerName}; part={part}; tickets={price}; server=true");
        }
        internal void Restore(bool restorePrices=false)
        {
            if(_restored)return;_restored=true;Initialized=false;
            foreach(var button in _buttons.Values)if(button)button.IsInteractable=false;
            foreach(var go in _clones)if(go){go.SetActive(false);Destroy(go);}
            foreach(var pair in _originalPrompts)if(pair.Key){pair.Key.InteractableName=pair.Value.Name;pair.Key.TooltipMessage=pair.Value.Tooltip;}
            foreach(var pair in _positions)if(pair.Key)pair.Key.localPosition=pair.Value;
            foreach(var pair in _scales)if(pair.Key)pair.Key.localScale=pair.Value;
            if(Machine&&_originalMeshes!=null)Meshes(Machine)=_originalMeshes;
            if(_tag){_tag.text=_basePriceText;_tag.fontSize=_font;_tag.fontSizeMin=_fontMin;_tag.fontSizeMax=_fontMax;_tag.enableAutoSizing=_autoSize;_tag.lineSpacing=_lineSpacing;_tag.overflowMode=_overflow;_tag.rectTransform.sizeDelta=_tagSize;_tag.rectTransform.anchoredPosition=_tagPosition;_tag.alignment=_alignment;_tag.textWrappingMode=_wrapping;}
            if(restorePrices&&NetworkServer.active&&Machine)SetPrices.Invoke(Machine,null); // Unpatched on mod unload.
        }
        private void OnDestroy()=>Restore();
    }
    [HarmonyPatch(typeof(BodyShreddingMachine),nameof(BodyShreddingMachine.OnStartClient))]
    internal static class EarMachineClientSpawn{private static void Postfix(BodyShreddingMachine __instance)=>EarMachineController.Ensure(__instance);}
    [HarmonyPatch(typeof(BodyShreddingMachine),nameof(BodyShreddingMachine.ServerToggleLever))]
    internal static class EarMachineServerMode{private static void Postfix(BodyShreddingMachine __instance)=>EarMachineNetwork.Publish(EarMachineController.Ensure(__instance));}
    [HarmonyPatch(typeof(BodyShreddingMachine),"ServerSetPrices")]
    internal static class EarMachineServerPrices
    {
        private static bool Prefix(BodyShreddingMachine __instance)
        {
            if(!NetworkServer.active)return false;
            int eye=BodyEconomy.Get(MachinePart.Eye).SellValue,mouth=BodyEconomy.Get(MachinePart.Mouth).SellValue,body=BodyEconomy.Get(MachinePart.Body).SellValue;
            AccessTools.Field(typeof(BodyShreddingMachine),"_eyePrice").SetValue(__instance,eye);
            AccessTools.Field(typeof(BodyShreddingMachine),"_mouthPrice").SetValue(__instance,mouth);
            AccessTools.Field(typeof(BodyShreddingMachine),"_bodyPrice").SetValue(__instance,body);
            AccessTools.Method(typeof(BodyShreddingMachine),"RpcSetPrices").Invoke(__instance,new object[]{body,eye,mouth});return false;
        }
        private static void Postfix(BodyShreddingMachine __instance)=>EarMachineNetwork.Publish(EarMachineController.Ensure(__instance));
    }
    [HarmonyPatch(typeof(BodyShreddingMachine),"UserCode_RpcSetPrices__Int32__Int32__Int32")]
    internal static class EarMachinePriceDisplay
    {private static void Postfix(BodyShreddingMachine __instance,int eyePrice){var tag=(TextMeshPro)AccessTools.Field(typeof(BodyShreddingMachine),"priceTag").GetValue(__instance);string vanilla=tag?tag.text:null;EarMachineController.Ensure(__instance)?.PricesDisplayed(eyePrice,vanilla);}}
    [HarmonyPatch(typeof(BodyShreddingMachine),"UserCode_RpcToggleLever__Boolean")]
    internal static class EarMachineClientMode{private static void Postfix(BodyShreddingMachine __instance,bool isBuy)=>EarMachineController.Ensure(__instance)?.ModeDisplayed(isBuy);}
}
