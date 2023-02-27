using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KSP.Api;
using KSP.Game;
using KSP.Input;
using KSP.Messages;
using KSP.Messages.PropertyWatchers;
using KSP.OAB;
using KSP.Sim.DeltaV;
using KSP.Sim.impl;
using UnityEngine;
using SpaceWarp.API.Mods;

namespace StageInfo {

    [MainMod]
    public class StageInfo : Mod {

        static bool loaded = false;
        private bool showGUI = false;
        private int windowWidth = 300;
        private Rect guiRect;
        private int windowHeight = 700;
        private GUIStyle horizontalDivider = new GUIStyle();
        private bool refreshing = false;
        private bool subscribed = false;
        private Dictionary<int, float> twrReadouts = new Dictionary<int, float>();
        private Dictionary<int, double> dVReadouts = new Dictionary<int, double>();

        private bool inAtmo = false;

        public override void OnInitialized() {
            Logger.Info("StageInfo Loaded");
            if (loaded) {
                Destroy(this);
            }
            loaded = true;
        }

        void Awake() {
            guiRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        }

        private void Update() {
            if (!subscribed && ShouldSubscribeToMessages()) SubscribeToMessages();
            
            if (GameManager.Instance.Game.GlobalGameState.GetState() == GameState.VehicleAssemblyBuilder && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.E)) {
                showGUI = !showGUI;
            }
        }

        private bool ShouldSubscribeToMessages() {
            if (subscribed) return false;
            if (GameManager.Instance == null) return false;
            if (GameManager.Instance.Game == null) return false;
            if (GameManager.Instance.Game.Messages == null) return false;
            return true;
        }

        void OnGUI() {
            if (!showGUI) return;
            guiRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                guiRect,
                FillGUI,
                "Stage Info",
                GUILayout.Height(0),
                GUILayout.Width(windowWidth));

        }

        private void FillGUI(int windowID) {
            if (refreshing || twrReadouts.Count == 0) {
                GUILayout.Label("Gathering Stage Info...");
            } else {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Stage", GUILayout.Width(windowWidth / 3));
                GUILayout.Label("TWR", GUILayout.Width(windowWidth / 3));
                GUILayout.Label("deltaV", GUILayout.Width(windowWidth / 3));
                GUILayout.EndHorizontal();
                horizontalDivider.fixedHeight = 2;
                horizontalDivider.margin = new RectOffset(0, 0, 4, 4);
                GUILayout.Box("", horizontalDivider);
                for (int i = 0; i < twrReadouts.Count; i++) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{twrReadouts.Count - i:00}", GUILayout.Width(windowWidth / 3));
                    GUILayout.Label($"{twrReadouts[i]:N2}", GUILayout.Width(windowWidth / 3));
                    GUILayout.Label($"{dVReadouts[i]:N0} m/s", GUILayout.Width(windowWidth / 3));
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                inAtmo = GUILayout.Toggle(inAtmo, "Sea Level");
                GUILayout.EndHorizontal();
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        private void PopulateReadouts(MessageCenterMessage msg) {
            Debug.Log("[StageInfo]: Received DeltaV message");
            VesselDeltaVCalculationMessage dvm = msg as VesselDeltaVCalculationMessage;
            if (dvm == null) return;
            refreshing = true;
            twrReadouts.Clear();
            dVReadouts.Clear();
            foreach (DeltaVStageInfo stageInfo in dvm.DeltaVComponent.StageInfo) {
                
                twrReadouts.Add(stageInfo.Stage, stageInfo.GetSituationTWR(inAtmo ? DeltaVSituationOptions.SeaLevel : DeltaVSituationOptions.Vaccum));
                dVReadouts.Add(stageInfo.Stage, stageInfo.GetSituationDeltaV(inAtmo ? DeltaVSituationOptions.SeaLevel : DeltaVSituationOptions.Vaccum));
            }

            refreshing = false;
        }

        private void SubscribeToMessages() {
            GameManager.Instance.Game.Messages.Subscribe<VesselDeltaVCalculationMessage>(PopulateReadouts);
            subscribed = true;
        }


    }
}
