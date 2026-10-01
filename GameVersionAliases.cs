// v19 适配：游戏选项类的版本号随游戏版本递增（v18 = V11，v19 = V12）。
// 用类型别名把「具体版本」收敛到这一处，其余 20 多处代码统一用别名，
// 换版本时只需要改这个文件。
//
//   BUR_GAME_V19 由 TONE.csproj 定义（对应 GameLibs 19.0.0）
#if BUR_GAME_V19
global using NormalOptionsType = AmongUs.GameOptions.NormalGameOptionsV12;
global using HideNSeekOptionsType = AmongUs.GameOptions.HideNSeekGameOptionsV12;
global using RoleOptionsCollectionType = AmongUs.GameOptions.RoleOptionsCollectionV12;
#else
global using NormalOptionsType = AmongUs.GameOptions.NormalGameOptionsV11;
global using HideNSeekOptionsType = AmongUs.GameOptions.HideNSeekGameOptionsV11;
global using RoleOptionsCollectionType = AmongUs.GameOptions.RoleOptionsCollectionV11;
#endif
