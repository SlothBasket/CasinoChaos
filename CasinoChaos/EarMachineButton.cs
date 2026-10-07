using System;
using UnityEngine;
using UnityEngine.Events;

namespace GWYF_CasinoChaos
{
    // A local IInteractable, not a NetworkBehaviour. The cloned visual hierarchy
    // never changes the vanilla machine's Mirror behaviour indices.
    public sealed class EarMachineButton : MonoBehaviour, IInteractable
    {
        private EarMachineController _machine;
        internal MachinePart Entry { get; private set; }
        private Outline _outline;
        private string _name, _tooltip;
        private UnityEvent<PlayerInteract> _hover, _hoverExit, _hold, _holdExit, _feedback;
        public float HoldDuration { get; set; }
        public bool HoldInteract { get; set; }
        public bool IsInteractable { get; set; }
        public bool MeetRequirements { get; set; }
        public bool IsBeingHovered { get; set; }
        public bool IsBeingHold { get; set; }
        public float HoldProgress { get; set; }
        public string TooltipMessage { get => _tooltip; set { _tooltip = value; OnInteractableChanged?.Invoke(this); } }
        public string InteractableName { get => _name; set { _name = value; OnInteractableChanged?.Invoke(this); } }
        public CursorManager.CursorType CursorType { get; set; }
        public event Action<IInteractable> OnInteractableChanged;
        internal void Configure(EarMachineController machine, InteractableEventTrigger template, MachinePart entry)
        {
            _machine = machine; Entry = entry; HoldDuration = template.HoldDuration; HoldInteract = template.HoldInteract;
            IsInteractable = template.IsInteractable; MeetRequirements = template.MeetRequirements; CursorType = template.CursorType;
            _hover = template.onHoverEvent; _hoverExit = template.onHoverExitEvent;
            _hold = template.onHoldEvent; _holdExit = template.onHoldExitEvent; _feedback = template.rpcOnInteractEvent;
            _outline = GetComponent<Outline>();
            if (!_outline) _outline = gameObject.AddComponent<Outline>();
            _outline.OutlineMode = Outline.Mode.OutlineAll; _outline.OutlineColor = Color.yellow;
            _outline.OutlineWidth = 5; _outline.enabled = false;
            InteractableName = BodyEconomy.Get(entry).DisplayName; TooltipMessage = "[E] Sell " + InteractableName;
        }
        public void OnHover(PlayerInteract player)
        {
            if (IsBeingHovered || !IsInteractable || !MeetRequirements) return;
            IsBeingHovered = true; _outline.enabled = true; IsBeingHold = false; HoldProgress = 0; _hover?.Invoke(player);
        }
        public void OnHoverExit(PlayerInteract player)
        { IsBeingHovered = false; if (_outline) _outline.enabled = false; IsBeingHold = false; HoldProgress = 0; _hoverExit?.Invoke(player); }
        public void OnHold(PlayerInteract player) { IsBeingHold = true; _hold?.Invoke(player); }
        public void OnHoldExit(PlayerInteract player) { IsBeingHold = false; HoldProgress = 0; _holdExit?.Invoke(player); }
        public void OnInteract(PlayerInteract player)
        {
            if (player && player.isLocalPlayer && IsInteractable && MeetRequirements) EarMachineNetwork.SendPress(_machine, Entry);
        }
        public void ServerOnHover(PlayerInteract player) { }
        public void ServerOnHoverExit(PlayerInteract player) { }
        public void ServerOnHold(PlayerInteract player) { }
        public void ServerOnHoldExit(PlayerInteract player) { }
        public void ServerOnInteract(PlayerInteract player) { }
        public void RpcOnInteract(PlayerInteract player) => _feedback?.Invoke(player);
        private void OnDisable() { if (_outline) _outline.enabled = false; IsBeingHovered = IsBeingHold = false; }
        private void OnDestroy() { OnInteractableChanged = null; }
    }
}
