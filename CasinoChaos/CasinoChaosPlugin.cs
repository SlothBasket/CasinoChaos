using System;
using System.Collections.Generic;
using BepInEx;
using Extensions;
using GWYF_ModAPI;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
#if DEBUG
using UnityEngine.InputSystem;
#endif

namespace GWYF_CasinoChaos
{
    [BepInPlugin("com.gwyf.casinochaos", "CasinoChaos", "1.6.3")]
    public class CasinoChaosPlugin : BaseUnityPlugin, IMod
    {
        string IMod.Name => "CasinoChaos";
        string IMod.Version => "1.6.3";
        string IMod.Author => "YourName";
        string IMod.Description => "Civilian bat hits add $5 and shared heat; escalating mafia responses and body economy.";

        private static Harmony _harmony;
        private static Action<string> _log;
        private static int _lastTickFrame = -1;
        internal static void Log(string message) => _log?.Invoke(message);

        private void Awake()
        {
            AccessibilitySettings.Bind(Config);
            HeatSettings.Bind(Config);
            _log = message => Logger.LogInfo(message);
            InstallPatch();
        }

        public void OnLoad(IModContext ctx)
        {
            AccessibilitySettings.Bind(Config);
            HeatSettings.Bind(Config);
            _log = ctx.Log;
            InstallPatch();
        }

        private static void InstallPatch()
        {
            if (_harmony != null)
                return;

            BodyPartNetwork.Install();
            DongAppearanceNetwork.Install(); FartNetwork.Install();
            DongVisuals.Install();
            HeatNetwork.Install();
            EarMachineNetwork.Install();
            BodyPartEffects.Install();
            StaticEyeOverlay.Install();
            LegMovement.Install();
            _harmony = new Harmony("com.gwyf.casinochaos");
            _harmony.PatchAll(typeof(CasinoChaosPlugin).Assembly);
            SceneManager.activeSceneChanged += SceneChanged;
            Log("CasinoChaos loaded: $5 civilian reward; global heat tiers 0-5 with 0/1/2/3/5/7 gun guards.");
#if DEBUG
            Log("DEVELOPMENT ONLY, host: Ctrl+Shift+Insert adds one global heat point; Ctrl+Shift+Delete resets global heat and removes guards.");
#endif
        }

        public void OnUnload()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            SceneManager.activeSceneChanged -= SceneChanged;
            DongAppearanceNetwork.Shutdown(); FartNetwork.Shutdown();
            DongVisuals.Shutdown();
            EarMachineNetwork.Shutdown();
            BodyPartEffects.Shutdown();
            StaticEyeOverlay.Shutdown();
            LegMovement.Shutdown();
            HeatSystem.Clear("mod unload");
            HeatNetwork.Shutdown();
            BodyPartNetwork.Shutdown();
            CivilianBatReward.Clear();
            _log = null;
        }

        private void OnDestroy() => OnUnload();
        private void Update() => Tick();
        public void OnUpdate() => Tick();
        public void OnGUI() { }
        public void OnFixedUpdate() { }
        public void OnSceneChanged(string sceneName) { }
        private static void SceneChanged(Scene oldScene, Scene newScene)
        {
            DongShuffleButton.Shutdown(); FartButton.Shutdown();
            DongVisuals.Clear();
            LegMovement.Clear();
            HeatSystem.Clear("scene changed to " + newScene.name);
            CivilianBatReward.Clear();
        }

        private static void Tick()
        {
            // The manager and BepInEx may both invoke this in the same frame.
            if (_lastTickFrame == Time.frameCount) return;
            _lastTickFrame = Time.frameCount;
            BodyPartNetwork.Tick();
            DongAppearanceNetwork.Tick(); FartNetwork.Tick();
            DongVisuals.Tick();
            EarMachineNetwork.Tick();
            HeatNetwork.Tick();
            HeatSystem.Tick();
            HeatHud.Tick();
            AccessibilitySettings.Refresh();
            BodyPartEffects.Tick();
            StaticEyeOverlay.Tick();
            if (!NetworkServer.active)
            {
                CivilianBatReward.Clear();
                return;
            }
#if DEBUG
            var k = Keyboard.current;
            bool chord = k != null && (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed) && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
            if (chord && k.deleteKey.wasPressedThisFrame) HeatSystem.Clear("DEVELOPMENT reset");
            else if (chord && k.insertKey.wasPressedThisFrame && NetworkClient.localPlayer)
            {
                var player = NetworkClient.localPlayer.GetComponent<PlayerController>();
                if (player) HeatSystem.AddPoint(player, "DEVELOPMENT heat point");
            }
#endif
        }

        [HarmonyPatch(typeof(Bat), "UserCode_CmdHitNpc__NPC__Single", new Type[] { typeof(NPC), typeof(float) })]
        private static class CivilianBatReward
        {
            // Bat enables its hit collider for 0.15 seconds; OnTriggerEnter can
            // repeat within that window. Separate vanilla swings are >=0.5s apart.
            private const float HitWindow = 0.15f;
            private static readonly Dictionary<(uint Bat, uint Npc), float> LastReward =
                new Dictionary<(uint Bat, uint Npc), float>();

            // This list is populated by the vanilla floor/crowd spawner, unlike
            // Mod API NPC handles. It identifies the existing casino civilians.
            private static readonly AccessTools.FieldRef<NPCSpawner, SyncList<NPC>> SpawnedCivilians =
                AccessTools.FieldRefAccess<NPCSpawner, SyncList<NPC>>("NPCs");

            internal static void Clear() => LastReward.Clear();

            private static void Postfix(Bat __instance, NPC npc)
            {
                if (!NetworkServer.active || !npc)
                    return;

                var attacker = __instance.NetworkHolder;
                if (!attacker)
                    return;
                var offender = attacker.GetComponent<PlayerController>();
                if (!offender) return;

                var spawner = NetworkSingleton<NPCSpawner>.Instance;
                if (!spawner || !SpawnedCivilians(spawner).Contains(npc))
                    return;

                // The original command has returned. ServerKnockback only accepts
                // an NPC with its Rigidbody, and sets its state to Ragdoll.
                if (!npc.GetComponent<Rigidbody>() || npc.State != NPC.NPCState.Ragdoll)
                    return;

                var key = (__instance.netId, npc.netId);
                float now = Time.time;
                if (LastReward.TryGetValue(key, out float last) && now - last < HitWindow)
                    return;

                try
                {
                    var money = NetworkSingleton<MoneyManager>.Instance;
                    var profile = attacker.GetComponent<PlayerProfile>();
                    BigNumber before = money.balance;
                    LastReward[key] = now;
                    bool rewarded = money.TryChangeBalance(5, profile, ChangeType.Misc);
                    HeatSystem.AddCivilianHit(offender);

                    _log?.Invoke($"Civilian bat hit detected: npc='{npc.name}' netId={npc.netId}; " +
                        $"attacker='{(profile ? profile.playerName : attacker.name)}' netId={attacker.netId}; " +
                        $"server={NetworkServer.active} host={NetworkServer.active && NetworkClient.active}; " +
                        $"shared balance before={before.ToSaveString()} after={money.balance.ToSaveString()}; reward=$5 applied={rewarded}");
                }
                catch (Exception error)
                {
                    _log?.Invoke($"Civilian bat reward failed after vanilla knockback: {error}");
                }
            }
        }
    }
}
