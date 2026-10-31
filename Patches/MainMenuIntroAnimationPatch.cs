using System;
using HarmonyLib;
using UnityEngine;

namespace TONE.Patches;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
internal static class MainMenuIntroAnimationPatch
{
    private static bool _running;
    private static bool _played;
    private static float _t;

    private const float RiseDur = 1.05f;
    private const float RiseDistance = 6.5f;
    private const float Overshoot = 0.05f;

    private const float FirstLaunchDelay = 1.0f;

    private static float _delay;
    private static bool _everPlayed;

    private static GameObject _panel;
    private static Vector3 _from;
    private static Vector3 _to;

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    public static void Postfix(MainMenuManager __instance)
    {
        try
        {
            if (__instance == null) return;

            if (__instance.mainMenuUI == null || !__instance.mainMenuUI.activeInHierarchy)
            {
                _running = false;
                _played = false;
                _t = 0f;
                _delay = 0f;
                _panel = null;
                return;
            }

            if (!_played && !_running) Start();
            if (!_running) return;

            _t += Time.deltaTime;
            Animate();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "MainMenuIntro");
            _running = false;
            _played = true;
        }
    }

    private static void Start()
    {
        _panel = MainMenuManagerPatch.MenuPanel;
        if (_panel == null)
        {
            _played = false;
            return;
        }

        _to = MainMenuManagerPatch.MenuPanelHome;
        _from = _to + new Vector3(0f, -RiseDistance, 0f);

        _delay = _everPlayed ? 0f : FirstLaunchDelay;

        _t = 0f;
        _played = true;
        _running = true;

        _panel.transform.position = _from;

        Logger.Info($"主菜单进场动画开始：面板 {_panel.name} 从 y={_from.y:0.##} 升到 y={_to.y:0.##}", "MainMenuIntro");
    }

    private static void Animate()
    {
        if (_panel == null) { _running = false; return; }

        if (_delay > 0f)
        {
            _delay -= Time.deltaTime;
            _panel.transform.position = _from;
            return;
        }

        _everPlayed = true;

        float k = Mathf.Clamp01(_t / RiseDur);
        float eased = 1f - Mathf.Pow(1f - k, 3f);
        float over = Mathf.Sin(k * Mathf.PI) * Overshoot;
        float y = Mathf.Lerp(_from.y, _to.y, Mathf.Clamp01(eased + over));

        _panel.transform.position = new Vector3(_from.x, y, _from.z);

        if (k >= 1f)
        {
            _panel.transform.position = _to;
            _running = false;
            Logger.Info("主菜单进场动画结束", "MainMenuIntro");
        }
    }
}
