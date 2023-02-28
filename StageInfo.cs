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
using SpaceWarp.API.AssetBundles;
using SpaceWarp.API.Toolbar;
using SpaceWarp.API.Managers;
using SpaceWarp.API;

namespace StageInfo {

    [MainMod]
    public class StageInfo : Mod {

        private GUISkin _spaceWarpUISkin;
        private bool showGUI = false;
        private int windowWidth = 300;
        private Rect guiRect;
        private int windowHeight = 700;
        private GUIStyle horizontalDivider = new GUIStyle();
        private bool refreshing = false;
        private bool inVAB = false;
        private bool hideExtra = false;
        private Dictionary<int, SituationData> situData = new Dictionary<int, SituationData>();
        private Dictionary<int, bool> inAtmo = new Dictionary<int, bool>();

        public void Awake() => guiRect = new Rect((Screen.width * 0.8632f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);

        public override void OnInitialized() {
            ResourceManager.TryGetAsset($"space_warp/swconsoleui/swconsoleUI/spacewarpConsole.guiskin", out _spaceWarpUISkin);
            GameManager.Instance.Game.Messages.Subscribe<VesselDeltaVCalculationMessage>(GetSituationData);
            GameManager.Instance.Game.Messages.Subscribe<GameStateChangedMessage>(UpdateGameState);
            GameManager.Instance.Game.Messages.Subscribe<OpenEngineersReportWindowMessage>(ShowGUI);
            GameManager.Instance.Game.Messages.Subscribe<CloseEngineersReportWindowMessage>(HideGUI);
            SpaceWarpManager.RegisterAppButton(
                "Stage Info",
                "BTN-StageInfoButton",
                SpaceWarpManager.LoadIcon(),
                delegate { showGUI = !showGUI; });
        }

        private void UpdateGameState(MessageCenterMessage msg) {
            inVAB = GameManager.Instance.Game.GlobalGameState.GetState() == GameState.VehicleAssemblyBuilder;
            if (!inVAB && GameManager.Instance.Game.GlobalGameState.GetState() != GameState.FlightView) showGUI = false;
        }

        private void ShowGUI(MessageCenterMessage msg) => showGUI = true;
        private void HideGUI(MessageCenterMessage msg) => showGUI = false;

        void OnGUI() {
            if (!showGUI) return;
            GUI.skin = _spaceWarpUISkin;
            guiRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                guiRect,
                FillGUI,
                "<color=#696DFF>// STAGE INFO</color>",
                GUILayout.Height(0),
                GUILayout.Width(windowWidth));
        }

        private void FillGUI(int windowID) {
            if (refreshing || situData.Count == 0) GUILayout.Label("<i>Calculating Stage Info...</i>");
            else {
                int cols = inVAB ? 4 : 3;
                horizontalDivider.fixedHeight = 2;
                horizontalDivider.margin = new RectOffset(0, 0, 4, 4);
                GUILayout.Box("", horizontalDivider);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b> <color=#C0C7D5>S.</color> <color=#0A0B0E>|</color></b>", GUILayout.Width(windowWidth / 6));
                GUILayout.Label("<b><color=#C0C7D5>T/W</color>  <color=#0A0B0E>|</color></b>", GUILayout.Width(windowWidth / 6));
                GUILayout.Label(" <b><color=#C0C7D5>∆<i>v</i></color></b>", GUILayout.Width(windowWidth / 4));
                GUILayout.Label($"<b>{(inVAB ? "<color=#C0C7D5>SITUATION" : "<color=#0A0B0E>|</color> <color=#C0C7D5>BURN")}</color></b>", GUILayout.Width(windowWidth / 4));
                GUILayout.EndHorizontal();
                GUILayout.Box("", horizontalDivider);
                for (int i = 0; i < situData.Count; i++) {
                    if (inVAB && hideExtra && situData[i].dV[DeltaVSituationOptions.Altitude] == 0) continue;
                    if (!inVAB && hideExtra && i < situData.Count - 1) continue;
                    DeltaVSituationOptions situ = inVAB ? (inAtmo[i] ? DeltaVSituationOptions.SeaLevel : DeltaVSituationOptions.Vaccum) : DeltaVSituationOptions.Altitude;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($" <color=#C0C7D5>{situData.Count - i:00}</color> <color=#0A0B0E><b>|</b></color>", GUILayout.Width(windowWidth / 6));
                    float twr = situData[i].twr[situ];
                    GUILayout.Label($"<color={(twr < 1 ? "#E04949" : (twr > 1.25 ? "#0DBE2A" : "#E0A400"))}>{twr:N2}</color> <color=#0A0B0E><b>|</b></color>", GUILayout.Width(windowWidth / 6));
                    GUILayout.Label($"<color=#C0C7D5>{situData[i].dV[situ]:N0} m/s</color>", GUILayout.Width(windowWidth / 4));
                    if (inVAB) inAtmo[i] = GUILayout.Toggle(inAtmo[i], $"<color=#C0C7D5> {(inAtmo[i] ? "1 atm" : "Vac.")}</color>", GUILayout.Width(windowWidth / 4));
                    else GUILayout.Label($"<color=#0A0B0E><b>|</b></color> <color=#C0C7D5>{FormatBurnTime(situData[i].burn)}</color>", GUILayout.Width(windowWidth / 4));
                    GUILayout.EndHorizontal(); }
                GUILayout.BeginHorizontal();
                hideExtra = GUILayout.Toggle(hideExtra, $"<color=#C0C7D5>{(inVAB ? "Hide Empty" : "Hide Future")}</color>");
                GUILayout.FlexibleSpace();
                showGUI = !GUILayout.Button("<color=#C0C7D5>Close</color>", GUILayout.Width(windowWidth / 6));
                GUILayout.EndHorizontal(); }
            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        private void GetSituationData(MessageCenterMessage msg) {
            if (inVAB) Debug.Log("[StageInfo]: Received DeltaV message");
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

        private string FormatBurnTime(double seconds) {
            int s = (int)Math.Round(seconds);
            if (s < 60) return $"{s}s";
            return $"{s / 60}m {s % 60}s";
        }

        public class SituationData {

            public double burn;
            public Dictionary<DeltaVSituationOptions, float> twr;
            public Dictionary<DeltaVSituationOptions, double> dV;

            public SituationData(DeltaVStageInfo stageInfo) {
                burn = stageInfo.StageBurnTime;
                twr = new Dictionary<DeltaVSituationOptions, float>() {
                    [DeltaVSituationOptions.SeaLevel] = stageInfo.GetSituationTWR(DeltaVSituationOptions.SeaLevel),
                    [DeltaVSituationOptions.Altitude] = stageInfo.GetSituationTWR(DeltaVSituationOptions.Altitude),
                    [DeltaVSituationOptions.Vaccum] = stageInfo.GetSituationTWR(DeltaVSituationOptions.Vaccum)};
                dV = new Dictionary<DeltaVSituationOptions, double>() {
                    [DeltaVSituationOptions.SeaLevel] = stageInfo.GetSituationDeltaV(DeltaVSituationOptions.SeaLevel),
                    [DeltaVSituationOptions.Altitude] = stageInfo.GetSituationDeltaV(DeltaVSituationOptions.Altitude),
                    [DeltaVSituationOptions.Vaccum] = stageInfo.GetSituationDeltaV(DeltaVSituationOptions.Vaccum)};
            }

        }

    }
}
