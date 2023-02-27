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

        private Dictionary<int, SituationData> situData = new Dictionary<int, SituationData>();
        private Dictionary<int, bool> inAtmo = new Dictionary<int, bool>();
        private bool inVAB = false;

        public override void OnInitialized() {
            Logger.Info("StageInfo Loaded");
            if (loaded) Destroy(this);
            loaded = true;
        }

        void Awake() => guiRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        
        private void Update() {
            if (!subscribed && ShouldSubscribeToMessages()) SubscribeToMessages();
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.E)) showGUI = !showGUI;
        }

        private bool ShouldSubscribeToMessages() {
            if (subscribed) return false;
            if (GameManager.Instance == null) return false;
            if (GameManager.Instance.Game == null) return false;
            if (GameManager.Instance.Game.Messages == null) return false;
            return true;
        }

        private void UpdateGameState(MessageCenterMessage msg) => inVAB = GameManager.Instance.Game.GlobalGameState.GetState() == GameState.VehicleAssemblyBuilder;
        
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
            if (refreshing || situData.Count == 0) {
                GUILayout.Label("Calculating Stage Info...");
            } else {
                int cols = inVAB ? 4 : 3;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Stage", GUILayout.Width(windowWidth / cols));
                GUILayout.Label("TWR", GUILayout.Width(windowWidth / cols));
                GUILayout.Label("∆v", GUILayout.Width(windowWidth / cols));
                if (inVAB) GUILayout.Label("Situation", GUILayout.Width(windowWidth / 4));
                // GUILayout.Label("Thrust", GUILayout.Width(windowWidth / 6));
                // GUILayout.Label("ISP", GUILayout.Width(windowWidth / 6));
                GUILayout.EndHorizontal();
                horizontalDivider.fixedHeight = 2;
                horizontalDivider.margin = new RectOffset(0, 0, 4, 4);
                GUILayout.Box("", horizontalDivider);
                for (int i = 0; i < situData.Count; i++) {
                    DeltaVSituationOptions situ = inVAB ? (inAtmo[i] ? DeltaVSituationOptions.SeaLevel : DeltaVSituationOptions.Vaccum) : DeltaVSituationOptions.Altitude;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{situData.Count - i:00}", GUILayout.Width(windowWidth / cols));
                    GUILayout.Label($"{situData[i].twr[situ]:N2}", GUILayout.Width(windowWidth / cols));
                    GUILayout.Label($"{situData[i].dV[situ]:N0} m/s", GUILayout.Width(windowWidth / cols));
                    if (inVAB) inAtmo[i] = GUILayout.Toggle(inAtmo[i], "Sea Level", GUILayout.Width(windowWidth / 4));
                    // GUILayout.Label($"{situData[i].thrust[DeltaVSituationOptions.Altitude]:N1} kN", GUILayout.Width(windowWidth / 6));
                    // GUILayout.Label($"{situData[i].isp[DeltaVSituationOptions.Altitude]:N0} s", GUILayout.Width(windowWidth / 6));
                    GUILayout.EndHorizontal();
                }
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        private void PopulateReadouts(MessageCenterMessage msg) {
            Debug.Log("[StageInfo]: Received DeltaV message");
            VesselDeltaVCalculationMessage dvm = msg as VesselDeltaVCalculationMessage;
            if (dvm == null) return;
            refreshing = true;
            situData.Clear();
            inAtmo.Clear();
            foreach (DeltaVStageInfo stageInfo in dvm.DeltaVComponent.StageInfo) {
                situData.Add(stageInfo.Stage, new SituationData(stageInfo));
                if (inVAB) inAtmo.Add(stageInfo.Stage, stageInfo.Stage == dvm.DeltaVComponent.StageInfo.Count - 1); }
            refreshing = false;
        }

        private void SubscribeToMessages() {
            GameManager.Instance.Game.Messages.Subscribe<VesselDeltaVCalculationMessage>(PopulateReadouts);
            GameManager.Instance.Game.Messages.Subscribe<GameStateChangedMessage>(UpdateGameState);
            subscribed = true;
        }

        public class SituationData {

            public Dictionary<DeltaVSituationOptions, float> twr;
            public Dictionary<DeltaVSituationOptions, double> dV;
            // public Dictionary<DeltaVSituationOptions, float> thrust;
            // public Dictionary<DeltaVSituationOptions, double> isp;

            public SituationData(DeltaVStageInfo stageInfo) {
                twr = new Dictionary<DeltaVSituationOptions, float>() {
                    [DeltaVSituationOptions.SeaLevel] = stageInfo.GetSituationTWR(DeltaVSituationOptions.SeaLevel),
                    [DeltaVSituationOptions.Altitude] = stageInfo.GetSituationTWR(DeltaVSituationOptions.Altitude),
                    [DeltaVSituationOptions.Vaccum] = stageInfo.GetSituationTWR(DeltaVSituationOptions.Vaccum)};
                dV = new Dictionary<DeltaVSituationOptions, double>() {
                    [DeltaVSituationOptions.SeaLevel] = stageInfo.GetSituationDeltaV(DeltaVSituationOptions.SeaLevel),
                    [DeltaVSituationOptions.Altitude] = stageInfo.GetSituationDeltaV(DeltaVSituationOptions.Altitude),
                    [DeltaVSituationOptions.Vaccum] = stageInfo.GetSituationDeltaV(DeltaVSituationOptions.Vaccum)};
                //thrust = new Dictionary<DeltaVSituationOptions, float>() {
                //    [DeltaVSituationOptions.SeaLevel] = stageInfo.GetSituationThrust(DeltaVSituationOptions.SeaLevel),
                //    [DeltaVSituationOptions.Altitude] = stageInfo.GetSituationThrust(DeltaVSituationOptions.Altitude),
                //    [DeltaVSituationOptions.Vaccum] = stageInfo.GetSituationThrust(DeltaVSituationOptions.Vaccum)};
                //isp = new Dictionary<DeltaVSituationOptions, double>() {
                //    [DeltaVSituationOptions.SeaLevel] = stageInfo.GetSituationISP(DeltaVSituationOptions.SeaLevel),
                //    [DeltaVSituationOptions.Altitude] = stageInfo.GetSituationISP(DeltaVSituationOptions.Altitude),
                //    [DeltaVSituationOptions.Vaccum] = stageInfo.GetSituationISP(DeltaVSituationOptions.Vaccum)};
            }

        }

    }
}
