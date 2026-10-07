using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using Smooth;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal struct LegMovementCorrection : NetworkMessage { internal uint Player; internal Vector3 Position; }
    internal static class LegMovement
    {
        private sealed class Local {internal HopGate Gate=new HopGate();internal bool Jump;internal int Count=-1;}
        private sealed class Remote {internal Vector3 Position;internal float OwnerTime,Receipt,AirUntil,ExemptUntil,LogAt,CorrectionAt;internal bool Grounded, ExtrapolationOverridden;internal SmoothSyncMirror.ExtrapolationMode OriginalExtrapolation;internal float TravelBudget=.25f;}
        private static readonly RaycastHit[] GroundHits=new RaycastHit[8];
        private static readonly Dictionary<PlayerController,Local> Locals=new Dictionary<PlayerController,Local>();
        private static readonly Dictionary<SmoothSyncMirror,Remote> Remotes=new Dictionary<SmoothSyncMirror,Remote>();
        private static readonly AccessTools.FieldRef<PlayerController,Vector2> Input=AccessTools.FieldRefAccess<PlayerController,Vector2>("_moveInput");
        private static readonly AccessTools.FieldRef<PlayerController,Vector3> Direction=AccessTools.FieldRefAccess<PlayerController,Vector3>("_horizontalMoveDirection");
        private static readonly AccessTools.FieldRef<PlayerController,Vector3> Ground=AccessTools.FieldRefAccess<PlayerController,Vector3>("_groundVector");
        private static readonly AccessTools.FieldRef<PlayerController,PlayerSettings> Settings=AccessTools.FieldRefAccess<PlayerController,PlayerSettings>("_ps");
        private static readonly System.Reflection.MethodInfo Feedback=AccessTools.Method(typeof(PlayerController),"OnJumpFeedback");
        private static bool _installed;
        internal static void Install()
        {
            if(_installed)return;_installed=true;
            Writer<LegMovementCorrection>.write=(w,v)=>{w.WriteUInt(v.Player);w.WriteVector3(v.Position);};
            Reader<LegMovementCorrection>.read=r=>new LegMovementCorrection{Player=r.ReadUInt(),Position=r.ReadVector3()};
            RegisterReceiver();BodyPartState.Changed+=PartChanged;
        }
        internal static void RegisterReceiver()
        {if(_installed)NetworkClient.RegisterHandler<LegMovementCorrection>(Correct,requireAuthentication:true);}
        private static void Correct(LegMovementCorrection message)
        {
            if(!NetworkClient.localPlayer||NetworkClient.localPlayer.netId!=message.Player)return;
            var player=NetworkClient.localPlayer.GetComponent<PlayerController>();
            if(!player||player.State!=PlayerController.PlayerState.Free||player.IsLocked)return;
            player.LocalTeleport(message.Position);Locals.Remove(player);
        }
        private static void PartChanged(ulong id,CustomBodyPart part,bool present)
        {
            if(!NetworkServer.active||(part!=CustomBodyPart.LeftLeg&&part!=CustomBodyPart.RightLeg))return;
            foreach(var conn in NetworkServer.connections.Values){
                var profile=conn.identity?conn.identity.GetComponent<PlayerProfile>():null;
                if(!profile||profile.steamId!=id)continue;
                var player=profile.GetComponent<PlayerController>();var ps=player?Settings(player):null;
                if(player&&ps)Exempt(player,.5f+2*ps.jumpForce/Mathf.Max(1,ps.gravity));
            }
        }
        internal static int Count(PlayerController player)
        {
            var profile=player.GetComponent<PlayerProfile>();
            return profile?LegTuning.Count(BodyPartState.Get(profile.steamId)):2;
        }
        private static bool Active(PlayerController player)=>player.isLocalPlayer&&player.State==PlayerController.PlayerState.Free&&player.hasBody&&!player.IsLocked;
        private static Local Get(PlayerController player,int legs)
        {
            if(!Locals.TryGetValue(player,out var local)){local=new Local();Locals.Add(player,local);}
            if(local.Count!=legs){local.Count=legs;local.Gate=new HopGate();local.Jump=false;}
            return local;
        }
        internal static bool Move(PlayerController player)
        {
            int legs=Count(player);
            if(!Active(player)||legs==2){Locals.Remove(player);return true;}
            var rb=player.GetComponent<Rigidbody>();var ps=Settings(player);
            if(!rb||rb.isKinematic||!ps)return true;
            var local=Get(player,legs);var input=Input(player);
            Vector3 direction=(Vector3.ProjectOnPlane(player.head.transform.forward,Vector3.up)*input.y+Vector3.ProjectOnPlane(player.head.transform.right,Vector3.up)*input.x).normalized;
            Direction(player)=direction;
            var inventory=player.GetComponent<PlayerInventory>();
            float item=inventory&&inventory.NetworkholdingItem?1-Mathf.Clamp01(inventory.NetworkholdingItem.slowPercent):1;
            Vector3 velocity=rb.linearVelocity,horizontal=new Vector3(velocity.x,0,velocity.z);
            if(legs==1){
                float speed=ps.sprintMaxSpeed*LegTuning.HopRunFraction*item;
                local.Gate.Observe(Time.time,player.isGrounded,velocity.y);
                if(local.Gate.TryStart(Time.time,player.isGrounded,local.Jump||direction.sqrMagnitude>.01f)){
                    local.Jump=false;
                    rb.linearVelocity=new Vector3(direction.x*speed,ps.jumpForce,direction.z*speed);
                    Feedback.Invoke(player,new object[]{true});
                }else if(!player.isGrounded||local.Gate.InFlight){
                    if(direction.sqrMagnitude>.01f)rb.AddForce((direction*speed-horizontal)*ps.acceleration*LegTuning.AirSteering,ForceMode.Acceleration);
                    var v=rb.linearVelocity;var h=Vector3.ClampMagnitude(new Vector3(v.x,0,v.z),speed);rb.linearVelocity=new Vector3(h.x,v.y,h.z);
                }else rb.AddForce(-horizontal*ps.acceleration,ForceMode.Acceleration);
            }else{
                local.Jump=false;
                if(player.isGrounded){
                    float speed=ps.maxSpeed*LegTuning.SlideWalkFraction*item;
                    Vector3 desired=Vector3.ProjectOnPlane(direction,Ground(player)).normalized*speed;
                    rb.AddForce((desired-horizontal)*ps.acceleration,ForceMode.Acceleration);
                    var h=Vector3.ClampMagnitude(horizontal,speed);rb.linearVelocity=new Vector3(h.x,velocity.y,h.z);
                }
            }
            return false;
        }
        internal static bool Jump(PlayerController player,bool pressed)
        {
            int legs=Count(player);
            if(!Active(player)||legs==2)return true;
            if(legs==1&&pressed)Get(player,legs).Jump=true;
            return false;
        }
        internal static void Exempt(PlayerController player,float seconds)
        {
            foreach(var sync in player.GetComponents<SmoothSyncMirror>())if(!sync.isSyncingChild){
                if(!Remotes.TryGetValue(sync,out var remote)){remote=new Remote{Position=sync.getPosition(),Receipt=Time.time};Remotes.Add(sync,remote);}
                remote.ExemptUntil=Time.time+seconds;
                RestoreExtrapolation(sync,remote);
            }
        }
        private static bool Grounded(PlayerController player,Vector3 position,PlayerSettings ps)
        {
            Vector3 foot=position+Vector3.up*(ps.playerRadius-ps.playerHeadRadius);
            Vector3 center=foot+Vector3.up*.04f;
            int count=Physics.SphereCastNonAlloc(center,ps.playerRadius-.01f,Vector3.down,GroundHits,ps.groundCheckDistance+.05f,ps.groundMask,QueryTriggerInteraction.Ignore);
            var rb=player.GetComponent<Rigidbody>();
            for(int i=0;i<count;i++){
                var hit=GroundHits[i];
                if(hit.collider&&hit.collider.attachedRigidbody!=rb&&Vector3.Angle(hit.point-foot,Vector3.down)<=ps.maxSlopeAngle)return true;
            }
            return false;
        }
        internal static bool Validate(NetworkConnectionToClient conn,NetworkStateMirror message)
        {
            var sync=message.smoothSync;
            if(!NetworkServer.active||!sync||sync.isSyncingChild||sync.netIdentity.connectionToClient!=conn||message.state==null)return true;
            var player=sync.GetComponent<PlayerController>();var profile=player?player.GetComponent<PlayerProfile>():null;
            if(!player||!profile)return true;
            var state=message.state;var ps=Settings(player);if(!ps)return true;
            if(!Remotes.TryGetValue(sync,out var previous)){
                previous=new Remote{Position=sync.getPosition(),Receipt=Time.time,OwnerTime=state.ownerTimestamp};Remotes.Add(sync,previous);
            }
            int legs=BodyPartNetwork.TryGetServerState(profile.steamId,out var body)?LegTuning.Count(body):2;
            bool grounded=false;
            bool restricted=legs<2&&player.hasBody&&player.State==PlayerController.PlayerState.Free&&!player.IsLocked&&Time.time>=previous.ExemptUntil;
            if(restricted){
                if(float.IsNaN(state.ownerTimestamp)||float.IsInfinity(state.ownerTimestamp)||float.IsNaN(state.position.x)||float.IsNaN(state.position.y)||float.IsNaN(state.position.z)||float.IsInfinity(state.position.x)||float.IsInfinity(state.position.y)||float.IsInfinity(state.position.z))return false;
                grounded=Grounded(player,state.position,ps);
                if(!previous.ExtrapolationOverridden){previous.OriginalExtrapolation=sync.extrapolationMode;previous.ExtrapolationOverridden=true;}
                sync.extrapolationMode=SmoothSyncMirror.ExtrapolationMode.None;
                Vector3 delta=state.position-previous.Position;
                float receiptDelta=Mathf.Max(0,Time.time-previous.Receipt);
                float dt=Mathf.Clamp(Mathf.Max(receiptDelta,Mathf.Min(state.ownerTimestamp-previous.OwnerTime,receiptDelta+.05f)),.02f,.25f);
                float horizontal=new Vector2(delta.x,delta.z).magnitude;
                float maxSpeed=legs==1?ps.sprintMaxSpeed*LegTuning.HopRunFraction:ps.maxSpeed*LegTuning.SlideWalkFraction;
                bool ascending=delta.y>.025f;
                bool air=Time.time<previous.AirUntil||!grounded||ascending;
                // Tolerance covers packet jitter and the spherecast's takeoff/landing overlap.
                bool valid=LegTravelEnvelope.Plan(previous.TravelBudget,legs,maxSpeed,receiptDelta,dt,horizontal,
                    legs==1 && grounded && previous.Grounded && !air,out float budget);
                if(legs==0&&!grounded&&delta.y>.08f)valid=false;
                if(legs==1&&delta.y>ps.jumpForce*dt*1.4f+.15f)valid=false;
                if(!valid){
                    if(Time.time>=previous.LogAt){previous.LogAt=Time.time+2;CasinoChaosPlugin.Log("Rejected restricted movement: player="+player.netId+" legs="+legs);}
                    if(Time.time>=previous.CorrectionAt){
                        previous.CorrectionAt=Time.time+.5f;
                        conn.Send(new LegMovementCorrection{Player=player.netId,Position=previous.Position});
                    }
                    return false;
                }
                previous.TravelBudget=budget;
                if(legs==1&&ascending)previous.AirUntil=Time.time+2*ps.jumpForce/Mathf.Max(1,ps.gravity)+.20f;
            }
            if (!restricted) {previous.TravelBudget=.25f;RestoreExtrapolation(sync,previous);}
            previous.Position=state.position;previous.Receipt=Time.time;previous.OwnerTime=state.ownerTimestamp;previous.Grounded=grounded;
            return true;
        }
        internal static bool AllowOwnerTeleport(SmoothSyncMirror sync)
        {
            if (!NetworkServer.active || !sync || sync.isSyncingChild) return true;
            var player=sync.GetComponent<PlayerController>();var profile=player?player.GetComponent<PlayerProfile>():null;
            if (!player || !profile || !BodyPartNetwork.TryGetServerState(profile.steamId,out var body) || LegTuning.Count(body)==2 || !player.hasBody || player.State!=PlayerController.PlayerState.Free) return true;
            return Remotes.TryGetValue(sync,out var remote) && Time.time<remote.ExemptUntil;
        }
        private static void RestoreExtrapolation(SmoothSyncMirror sync, Remote remote)
        { if(remote.ExtrapolationOverridden && sync){sync.extrapolationMode=remote.OriginalExtrapolation;remote.ExtrapolationOverridden=false;} }
        internal static void Clear(){foreach(var pair in Remotes)RestoreExtrapolation(pair.Key,pair.Value);Locals.Clear();Remotes.Clear();}
        internal static void Shutdown(){_installed=false;BodyPartState.Changed-=PartChanged;NetworkClient.UnregisterHandler<LegMovementCorrection>();Clear();}
    }
    [HarmonyPatch(typeof(PlayerController),"MoveFree")]
    internal static class RestrictedLegLocomotion{private static bool Prefix(PlayerController __instance)=>LegMovement.Move(__instance);}
    [HarmonyPatch(typeof(PlayerController),"OnJump")]
    internal static class RestrictedLegJump{private static bool Prefix(PlayerController __instance,bool isPressed)=>LegMovement.Jump(__instance,isPressed);}
    [HarmonyPatch(typeof(PlayerController),"StepClimb")]
    internal static class RestrictedLegSteps{private static bool Prefix(PlayerController __instance)=>!__instance.isLocalPlayer||LegMovement.Count(__instance)==2||__instance.State!=PlayerController.PlayerState.Free;}
    [HarmonyPatch(typeof(SmoothSyncMirror),nameof(SmoothSyncMirror.HandleSyncServer))]
    internal static class ValidateLegMovement{private static bool Prefix(NetworkConnectionToClient conn,NetworkStateMirror networkState)=>LegMovement.Validate(conn,networkState);}
    [HarmonyPatch(typeof(SmoothSyncMirror), "UserCode_CmdTeleport__Vector3__Vector3__Vector3__Single")]
    internal static class ValidateLegOwnerTeleport { private static bool Prefix(SmoothSyncMirror __instance)=>LegMovement.AllowOwnerTeleport(__instance); }
    [HarmonyPatch(typeof(PlayerController),nameof(PlayerController.ServerKnockback))]
    internal static class LegKnockbackExemption{private static void Prefix(PlayerController __instance)=>LegMovement.Exempt(__instance,Resources.Load<PlayerSettings>("PlayerSettings").ragdollDuration+1);}
    [HarmonyPatch(typeof(PlayerController),nameof(PlayerController.ServerTeleport))]
    internal static class LegTeleportExemption{private static void Prefix(PlayerController __instance)=>LegMovement.Exempt(__instance,2);}
}
