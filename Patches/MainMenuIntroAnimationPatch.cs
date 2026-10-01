using System;
using HarmonyLib;
using UnityEngine;

namespace TONE.Patches;

/// <summary>
/// 主菜单进场动画：主菜单面板（含按钮与 Logo）从下往上滑入 —— 「按钮从消失到出现」。
///
/// 动的是 <c>MainMenuManager.mainMenuUI</c>（由 MainMenuManagerPatch 记录到 MenuPanel），
/// 背景大图 SplashArt 保持不动。
///
/// 缓动：ease-out cubic + 5% 过冲，让面板落地时有一点分量。
/// </summary>
[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
internal static class MainMenuIntroAnimationPatch
{
    private static bool _running;
    private static bool _played;
    private static float _t;

    private const float RiseDur = 1.05f;
    private const float RiseDistance = 6.5f;
    private const float Overshoot = 0.05f;

    /// <summary>刚启动游戏那一次先等一会儿再升起来</summary>
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
            _played = false;   // 面板还没建好，下一帧再试
            return;
        }

        _to = MainMenuManagerPatch.MenuPanelHome;
        _from = _to + new Vector3(0f, -RiseDistance, 0f);

        // 刚进游戏先等 1 秒（这段时间面板停在屏幕下方）；之后每次回主菜单立刻播
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

        // 等待期间保持在起点
        if (_delay > 0f)
        {
            _delay -= Time.deltaTime;
            _panel.transform.position = _from;
            return;
        }

        _everPlayed = true;

        float k = Mathf.Clamp01(_t / RiseDur);
        float eased = 1f - Mathf.Pow(1f - k, 3f);           // ease-out cubic
        float over = Mathf.Sin(k * Mathf.PI) * Overshoot;   // 中段最大、两端归零
        float y = Mathf.Lerp(_from.y, _to.y, Mathf.Clamp01(eased + over));

        _panel.transform.position = new Vector3(_from.x, y, _from.z);

        if (k >= 1f)
        {
            _panel.transform.position = _to;   // 精确落位
            _running = false;
            Logger.Info("主菜单进场动画结束", "MainMenuIntro");
        }
    }
}
