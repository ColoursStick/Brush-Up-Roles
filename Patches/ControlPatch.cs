using System;
using TONE.Modules;
using TONE.Patches;
using UnityEngine;
using static TONE.Translator;

namespace TONE;

[HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
internal class ControllerManagerUpdatePatch
{
    private static readonly (int, int)[] resolutions = [(480, 270), (640, 360), (800, 450), (1280, 720), (1600, 900), (1920, 1080)];
    private static int resolutionIndex = 0;
    public static List<byte> CompletedRepairingPlayer = [];

    public static void Postfix()
    {

        try
        {
            var clientControlGUI = ClientControlGUI.Instance;
            var hudManagerExists = HudManager.InstanceExists;

            if (clientControlGUI)
            {
                if ((!hudManagerExists || !HudManager.Instance.Chat || !HudManager.Instance.Chat.IsOpenOrOpening) && Input.GetKeyDown(KeyCode.Delete))
                    clientControlGUI.TogglePanel();

                if (clientControlGUI.IsOpen) return;
            }

            if (!RehostManager.IsAutoRehostDone && GetKeysDown(KeyCode.LeftShift, KeyCode.C))
            {
                Logger.Info("User canceled Auto Rehost!", "ControllerManager");
                RehostManager.IsAutoRehostDone = true;
            }

            if (EndGameManagerPatch.IsRestarting && GetKeysDown(KeyCode.LeftShift, KeyCode.C))
            {
                Logger.Info("User canceled Auto Play Again!", "ControllerManager");
                EndGameManagerPatch.IsRestarting = false;
            }

            if (GameStates.IsInGame && (GameStates.IsCanMove || GameStates.IsMeeting) && GameModeBase.GetGameMode() is CustomGameMode.Standard)
            {
                if (Input.GetKey(KeyCode.F1))
                {
                    if (!InGameRoleInfoMenu.Showing)
                    {
                        InGameRoleInfoMenu.SetRoleInfoRef(PlayerControl.LocalPlayer);
                        InGameRoleInfoMenu.Show();
                    }
                }
                else if (InGameRoleInfoMenu.Showing) InGameRoleInfoMenu.Hide();
            }
            else if (InGameRoleInfoMenu.Showing) InGameRoleInfoMenu.Show();

            if (GetKeysDown(KeyCode.F11, KeyCode.LeftAlt))
            {
                resolutionIndex++;
                if (resolutionIndex >= resolutions.Length) resolutionIndex = 0;
                ResolutionManager.SetResolution(resolutions[resolutionIndex].Item1, resolutions[resolutionIndex].Item2, false);

            }

            if (GetKeysDown(KeyCode.F5, KeyCode.T))
            {
                Logger.Info("Reloaded Custom Translation File Colors", "KeyCommand");
                LoadLangs();
                Logger.SendInGame("Reloaded Custom Translation File");
            }

            if (GetKeysDown(KeyCode.F5, KeyCode.X))
            {
                Logger.Info("Exported Custom Translation and Role File", "KeyCommand");
                ExportCustomTranslation();
                Main.ExportCustomRoleColors();
                Logger.SendInGame("Exported Custom Translation and Role File");
            }

            if (GetKeysDown(KeyCode.F5, KeyCode.F))
            {
                if (MeetingStates.FirstMeeting && !CompletedRepairingPlayer.Contains(PlayerControl.LocalPlayer.PlayerId) && GameStates.IsInGame && !GameStates.IsMeeting)
                {
                    Logger.Info("Attempted to fix Black Screen", "KeyCommand");
                    ExileController.Instance?.ReEnableGameplay();
                    CompletedRepairingPlayer.Add(PlayerControl.LocalPlayer.PlayerId);
                }
            }

            if (GetKeysDown(KeyCode.F1, KeyCode.LeftControl))
            {
                Logger.Info("Send logs", "KeyCommand");
                Utils.DumpLog();
            }

            if (GetKeysDown(KeyCode.LeftAlt, KeyCode.C) && !Input.GetKey(KeyCode.LeftShift) && !GameStates.IsNotJoined)
            {
                Utils.CopyCurrentSettings();
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.C, KeyCode.LeftShift))
            {
                HudManager.Instance.Chat.SetVisible(true);
            }

            if (DebugModeManager.IsDebugMode && GetKeysDown(KeyCode.F8) && HudManager.Instance && !GameStates.IsMeeting && (GameStates.IsInGame || GameStates.IsLobby))
            {
                HudManager.Instance.gameObject.SetActive(!HudManager.Instance.gameObject.active);
            }

            if (Input.GetKeyDown(KeyCode.P) && PlayerControl.LocalPlayer != null)
            {
                Logger.Info(PlayerControl.LocalPlayer.GetTruePosition().ToString(), "GetLocalPlayerPos GetTruePosition()");
                Logger.Info(PlayerControl.LocalPlayer.transform.position.ToString(), "GetLocalPlayerPos transform.position");
            }

            if (!AmongUsClient.Instance.AmHost) return;

            if (GetKeysDown(KeyCode.Return, KeyCode.L, KeyCode.LeftShift) && GameStates.IsInGame)
            {
                NameNotifyManager.Notice.Clear();

                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                GameEndCheckerForNormal.GameIsEnded = true;
                if (GameStates.IsHideNSeek)
                {
                    GameEndCheckerForNormal.StartEndGame(GameOverReason.ImpostorDisconnect);
                }
            }

            if (GetKeysDown(KeyCode.Return) && GameSettingMenuPatch.Instance != null && GameSettingMenuPatch.Instance.isActiveAndEnabled == true)
            {
                GameSettingMenuPatch._SearchForOptions?.Invoke();
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.M, KeyCode.LeftShift) && GameStates.IsInGame)
            {
                if (GameStates.IsHideNSeek) return;

                if (!Main.IntroDestroyed) return;

                if (GameStates.IsMeeting)
                {
                    foreach (var pva in MeetingHud.Instance.playerStates)
                    {
                        if (pva == null) continue;

                        if (pva.VotedForId < 253)
                            MeetingHud.Instance.RpcClearVote(pva.PlayerId);
                    }
                    List<MeetingHud.VoterState> statesList = [];
                    MeetingHud.Instance.RpcVotingComplete(statesList.ToArray(), null, true, false, 0);
                    MeetingHud.Instance.RpcClose();
                }
                else
                {
                    if (Utils.GetTimeStamp() - Main.LastMeetingEnded < 2) return;
                    PlayerControl.LocalPlayer.NoCheckStartMeeting(null, force: true);
                }
            }

            if (Input.GetKeyDown(KeyCode.LeftShift) && GameStates.IsCountDown && !HudManager.Instance.Chat.IsOpenOrOpening)
            {
                var invalidColor = Main.EnumeratePlayerControls().Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).ToArray();
                if (invalidColor.Any())
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(GetString("Error.InvalidColorPreventStart"));
                    Logger.Info("Invalid Color Detected on force start!", "KeyCommand");
                }
                else
                {
                    Logger.Info("Countdown timer changed to 0", "KeyCommand");
                    GameStartManager.Instance.countDownTimer = 0;
                }
            }

            if (Input.GetKeyDown(KeyCode.C) && GameStates.IsCountDown && GameStates.IsLobby)
            {
                Logger.Info("Reset Countdown", "KeyCommand");
                GameStartManager.Instance.ResetStartState();
                Logger.SendInGame(GetString("CancelStartCountDown"));
            }

            if (GetKeysDown(KeyCode.N, KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftShift))
            {
                Main.isChatCommand = true;
                Utils.ShowActiveSettings();
            }

            if (GameStates.IsLobby && GetKeysDown(KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.Return, KeyCode.Delete))
            {
                OptionItem.AllOptions.ToArray().Where(x => x.Id > 0).Do(x => x.SetValueNoRpc(x.DefaultValue));
                Logger.SendInGame(GetString("RestTONESetting"));
            }

            if (GetKeysDown(KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.E, KeyCode.Return) && GameStates.IsInGame)
            {
                PlayerControl.LocalPlayer.SetDeathReason(PlayerState.DeathReason.etc);
                PlayerControl.LocalPlayer.SetRealKiller(PlayerControl.LocalPlayer);
                PlayerControl.LocalPlayer.RpcExileV3();

                Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.G, KeyCode.LeftShift) && GameStates.IsInGame && PlayerControl.LocalPlayer.FriendCode.GetDevUser().DeBug)
            {
                HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black));
                HudManager.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
            }

            if (GetKeysDown(KeyCode.F2, KeyCode.LeftControl))
            {
                Logger.isAlsoInGame = !Logger.isAlsoInGame;
                Logger.SendInGame($"In-game output log：{Logger.isAlsoInGame}");
            }

            if (!DebugModeManager.IsDebugMode) return;

            if (GetKeysDown(KeyCode.E, KeyCode.F, KeyCode.LeftControl))
            {
                CriticalErrorManager.SetCriticalError("Test AntiBlackout", true);
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.F, KeyCode.LeftShift))
            {
                Utils.FlashColor(new(1f, 0f, 0f, 0.3f));
                if (Constants.ShouldPlaySfx()) RPC.PlaySound(PlayerControl.LocalPlayer.PlayerId, Sounds.KillSound);
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.V, KeyCode.LeftShift) && GameStates.IsMeeting && !GameStates.IsOnlineGame)
            {
                MeetingHud.Instance.RpcClearVote(PlayerControl.LocalPlayer.PlayerId);
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.D, KeyCode.LeftShift) && GameStates.IsInGame)
            {
                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 79);
                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 80);
                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 81);
                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 82);
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.K, KeyCode.LeftShift) && GameStates.IsInGame)
            {
                PlayerControl.LocalPlayer.SetKillTimer(0f);
            }

            if (GetKeysDown(KeyCode.Return, KeyCode.T, KeyCode.LeftShift) && GameStates.IsInGame)
            {
                foreach (var task in PlayerControl.LocalPlayer.myTasks.ToArray())
                    PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                RPC.SyncCustomSettingsRPC();
                Logger.SendInGame(GetString("SyncCustomSettingsRPC"));
            }

            if (Input.GetKeyDown(KeyCode.Equals))
            {
                Main.VisibleTasksCount = !Main.VisibleTasksCount;
                DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage($"VisibleTaskCount has been changed to {Main.VisibleTasksCount}");
            }

            if (Input.GetKeyDown(KeyCode.C) && !GameStates.IsLobby)
            {
                foreach (var pc in PlayerControl.AllPlayerControls)
                {
                    if (!pc.AmOwner) pc.MyPhysics.RpcEnterVent(2);
                }
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                foreach (var pc in PlayerControl.AllPlayerControls)
                {
                    if (!pc.AmOwner) pc.MyPhysics.RpcExitVent(2);
                }
            }

            if (GetKeysDown(KeyCode.LeftShift, KeyCode.V, KeyCode.Return) && !GameStates.IsLobby && PlayerControl.LocalPlayer.FriendCode.GetDevUser().DeBug)
            {
                Vector2 pos = PlayerControl.LocalPlayer.NetTransform.transform.position;
                foreach (var pc in PlayerControl.AllPlayerControls)
                {
                    if (!pc.AmOwner)
                    {
                        pc.RpcTeleport(pos);
                        pos.x += 0.5f;
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                VentilationSystem.Update(VentilationSystem.Operation.StartCleaning, 0);
            }
        }
        catch (Exception error)
        {
            Utils.ThrowException(error);
        }
    }

    private static bool GetKeysDown(params KeyCode[] keys)
    {
        if (keys.Any(Input.GetKeyDown) && keys.All(Input.GetKey))
        {
            Logger.Info($"Shortcut Key：{keys.First(Input.GetKeyDown)} in [{string.Join(",", keys)}]", "GetKeysDown");
            return true;
        }
        return false;
    }

    private static bool ORGetKeysDown(params KeyCode[] keys) => keys.Any(Input.GetKeyDown);
}

[HarmonyPatch(typeof(ConsoleJoystick), nameof(ConsoleJoystick.HandleHUD))]
internal class ConsoleJoystickHandleHUDPatch
{
    public static void Postfix()
    {
        HandleHUDPatch.Postfix(ConsoleJoystick.player);
    }
}
[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.HandleHud))]
internal class KeyboardJoystickHandleHUDPatch
{
    public static void Postfix()
    {
        HandleHUDPatch.Postfix(KeyboardJoystick.player);
    }
}

internal class HandleHUDPatch
{
    public static void Postfix(Rewired.Player player)
    {
        if (!GameStates.IsInGame) return;
        if (GameStates.IsHideNSeek) return;
        if (player.GetButtonDown(8) &&
        PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
        PlayerControl.LocalPlayer.CanUseKillButton())
        {
            DestroyableSingleton<HudManager>.Instance.KillButton.DoClick();
        }
        if (player.GetButtonDown(50) &&
        PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
        PlayerControl.LocalPlayer.CanUseImpostorVentButton())
        {
            DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
        }
    }
}
