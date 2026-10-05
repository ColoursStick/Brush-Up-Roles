# Brush Up Roles (BUR)

一个基于 [Town of Next Edited (TONE)](https://github.com/qin-qwq/TownofNext-Edited) 二次开发的
Among Us 模组。

**房主限定（Host Only）** —— 只有房主需要安装，其他玩家用原版客户端即可加入。

---

## 关于本模组

本模组是 TONE 的分支版本，**基于 TONE v19 官方源码重做**。在原项目基础上：

- **重制了职业表** —— 移除 TONE 原有的绝大部分职业，仅保留并重新打磨一部分
- **适配 Among Us v19.0**（2026.9.29，64 位）
- **移除巫师阵营**
- 界面文案、日志、数据目录统一为 BUR

保留了 TONE 的核心基础设施：原版设置菜单注入、职业系统、选项系统、RPC、
击杀链、会议/投票/胜负判定。

## 职业表

| 阵营 | 数量 | 职业 |
|---|---|---|
| 船员 | 15 | 告密者 · 换票师 · 市长 · 验尸官 · 警长 · 大神官 · 变色龙 · 殉道者 · 天才 · 时间之主 · 老兵 · 侠客 · 大明星 · 正义赌怪 · 网红 |
| 内鬼 | 7 | 赏金猎人 · 吸血鬼 · 邪恶赌怪 · 自爆兵 · 快枪手 · 抹除者 · 易怒者 |
| 中立 | 11 | 末日预言家 · 小丑 · 纵火犯 · 投机者 · 豺狼 · 神 · 丘比特 · 薛定谔的猫 · 审判官 · 幻影 · 秃鹫 |

**附加职业全部保留**，可与上述职业自由组合。

**网红**使用官方 TONE v19 的实现（`InfluencerTONE`，基于原版 `SpiritGuide`）：
船员死亡后可成为网红，用图片卡片向存活玩家传递信息。

### 职业下架机制（重要）

原 TONE 有 385 个职业，彼此交叉引用极密（例如 `Executioner` 一个文件就调用另外 29 个职业的
静态方法），硬删文件会产生大量编译错误。

因此采用**白名单下架**：

```csharp
// Modules/CustomRolesHelper.cs —— 列出保留的职业
public static readonly HashSet<CustomRoles> KeepRoles = [ ... ];

// Roles/Core/CustomRoleManager.cs —— 其余职业判定为「下架」
public static bool IsOptBlackListed(this Type role) { ... }
```

**关键实现细节**：被下架职业的选项**照常创建**（避免 `OptionItem` 为 null），
只通过 `SetHidden(true)` 从设置菜单隐藏。

这一点非常重要 —— 全工程有 **600+ 处**从全局路径读取这些选项
（职业分配、每帧更新、名单颜色等）。若选项为 null 会抛 `NullReferenceException`，
表现为**开始游戏黑屏**或**每帧刷错**。

隐藏后其刷新率读不到值（自动回退 0），因此这些职业也永远不会被分配 ——
功能上等同于删除。相关代码：

```
Modules/CustomRolesHelper.cs         KeepRoles 白名单
Roles/Core/CustomRoleManager.cs      IsOptBlackListed / HideBlackListedOptions
Modules/OptionHolder.cs              HideIfBlackListed（29 个选项创建点）
```

## 支持的平台与版本

| 平台 | 游戏版本 | 说明 |
|---|---|---|
| Windows x64 | Among Us v19（2026.9.29） | 需 64 位 BepInEx |
| Android arm64 | Among Us v19（2026.9.29） | 星光（Starlight）启动器 |

平台差异全部由运行时的 `OperatingSystem.IsAndroid()` 判断处理，
**同一个 DLL 在 PC 与 Android 上通用**，不需要分别编译。

## 构建

### 前置

- .NET 6 SDK
- 对应版本的游戏已安装并**至少运行过一次**
  （BepInEx 会在 `<游戏目录>/BepInEx/interop` 生成 IL2CPP interop 程序集）

> ⚠️ 官方 NuGet 上的 `AmongUs.GameLibs.Steam` 只有占位包（`0.0.0-placeholder.0`），
> 真实的 `2026.9.29` 未公开发布。因此本模组改为**直接引用游戏目录里的 interop**
> （与 Aeterna-End 项目同一做法）。

### 命令

```powershell
dotnet build -c Release -p:Platform=Windows `
  -p:GameLibsInteropPath="<游戏目录>\BepInEx\interop"
```

产物：`bin/Windows/Release/net6.0/BUR.dll`

> 注意：`GitInfo` 已移除。本仓库无 `.git` 历史时它会算出非法的版本号
> `0.0.0+main.` 导致 NETSDK1018 编译失败；`ThisAssembly.Git.*` 改由根目录的
> `ThisAssemblyStub.cs` 提供。

## 目录结构

```
Roles/          职业实现
  Core/         职业基类与管理器（含下架机制）
  Vanilla/      原版职业的模组化实现
  AddOns/       附加职业
  (Ghosts)/     幽灵职业
Patches/        Harmony 补丁
Modules/        基础设施（选项、翻译、RPC、日志…）
GameModes/      自定义游戏模式
Resources/      内嵌资源（图片、语言、音效、公告）
assets/         仓库用图（模组图标）
```

## 许可

GPLv3 —— 见 [LICENSE](LICENSE)。

本模组基于 TONE 修改，TONE 同样以 GPLv3 发布。
**依据 GPLv3，分发二进制时必须提供完整对应源码**，本仓库即为该源码。

## 鸣谢

- [Town of Next Edited (TONE)](https://github.com/qin-qwq/TownofNext-Edited) —— 本模组的直接上游
- [Endless Host Roles (EHR)](https://github.com/Gurge44/EndlessHostRoles) —— 参考 `ReallyBegin` 接管、
  `StateMachineWrapper` 用法、公告排版范式
- [Aeterna-End](https://github.com/waffle-ful/Aeterna-End-K-not) —— 参考 `GameLibsInteropPath` 构建方式
  与 Android 适配策略
- [Reactor](https://github.com/XtraCube/Reactor) —— `CompilerGeneratedObjectWrapper` / `StateMachineWrapper` 的来源

## 免责声明

本模组与 Among Us 或 Innersloth LLC 无关联。Among Us 及相关素材版权归 Innersloth LLC 所有。