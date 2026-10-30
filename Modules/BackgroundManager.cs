using System;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TONE;

/// <summary>
/// 主菜单背景图管理。
///
/// 玩家可以把任意图片丢进 <see cref="FolderPath"/> 目录，模组会从中**随机**挑一张
/// 作为主菜单背景；目录为空或全部读取失败时，退回内嵌的默认背景。
///
/// 目录位置：
///   · 电脑端  &lt;游戏目录&gt;/BrushUpRoles-DATA/Backgrounds/
///   · 手机端  $STAR_DATA_PATH/BrushUpRoles-DATA/Backgrounds/
///
/// 目录首次创建时会把内嵌的默认背景复制进去，方便玩家直接替换。
/// </summary>
public static class BackgroundManager
{
    private const string DATA_FOLDER = "BrushUpRoles-DATA";
    private const string BACKGROUND_FOLDER = "Backgrounds";

    /// <summary>支持的扩展名。PNG/JPG 走 Unity 原生解码，BMP/TGA 由本类自行解码。</summary>
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".tga"];

    // 参照尺寸：1600x900 的图在 ReferencePpu 下正好铺满屏幕。
    // 自定义图片按「等比缩放到恰好覆盖屏幕」计算 PPU，避免被拉伸或留黑边。
    private const float RefWidth = 1600f;
    private const float RefHeight = 900f;

    /// <summary>
    /// 背景目录的**绝对路径**。
    /// 电脑端 Main.Path 是 "."，必须转绝对路径，否则 explorer 打不开。
    /// </summary>
    public static string FolderPath
    {
        get
        {
            try
            {
                return System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Main.Path, DATA_FOLDER, BACKGROUND_FOLDER));
            }
            catch
            {
                return System.IO.Path.Combine(Main.Path, DATA_FOLDER, BACKGROUND_FOLDER);
            }
        }
    }

    /// <summary>电脑端 150、手机端 119 —— 与主菜单原有的取值保持一致。</summary>
    public static float ReferencePpu => OperatingSystem.IsAndroid() ? 119f : 150f;

    /// <summary>
    /// 确保背景目录存在。
    /// 由 Main.Load() 调用，失败只记日志，不影响模组加载。
    ///
    /// ⚠️ 这里**刻意不把内嵌的默认背景复制进去**：
    ///    那个目录是明文可读的，放进去等于把美术资源白送出去（有被盗用风险）。
    ///    默认背景只留在 DLL 内嵌资源里，玩家目录为空时运行期自动回退。
    /// </summary>
    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
        }
        catch (Exception ex)
        {
            Logger.Warn($"背景目录初始化失败（已忽略）：{ex.Message}", "BackgroundManager");
        }
    }

    /// <summary>玩家目录里所有受支持的图片（完整路径）。</summary>
    public static string[] GetUserImagePaths()
    {
        try
        {
            var folder = FolderPath;
            if (!Directory.Exists(folder)) return [];

            return Directory.GetFiles(folder)
                .Where(f => SupportedExtensions.Contains(
                    System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Logger.Warn($"枚举背景目录失败：{ex.Message}", "BackgroundManager");
            return [];
        }
    }

    /// <summary>
    /// 随机取一张背景。优先用玩家目录里的图片；目录为空或读取失败时退回内嵌背景。
    /// 返回 null 表示彻底失败（调用方应跳过设置背景）。
    /// </summary>
    public static Sprite LoadRandomSprite()
    {
        var files = GetUserImagePaths();

        if (files.Length > 0)
        {
            // 打乱顺序后逐个尝试，避免某张坏图导致背景随机丢失
            var order = Enumerable.Range(0, files.Length).OrderBy(_ => Guid.NewGuid()).ToArray();

            foreach (var i in order)
            {
                var sprite = LoadSpriteFromFile(files[i]);
                if (sprite != null)
                {
                    Logger.Info($"使用自定义背景：{System.IO.Path.GetFileName(files[i])}", "BackgroundManager");
                    return sprite;
                }
            }

            Logger.Warn("自定义背景全部读取失败，改用内嵌默认背景", "BackgroundManager");
        }

        return LoadRandomEmbeddedSprite();
    }

    /// <summary>
    /// 从内嵌资源里随机取一张（玩家目录为空时的兜底）。
    ///
    /// ⚠️ 这里刻意**不走 Utils.LoadSprite**，而是读成字节后交给
    ///    <see cref="CreateSpriteAutoCover"/> —— 与自定义图片完全同一套解码与
    ///    PPU 计算，避免两条路径出现缩放/朝向不一致。
    /// </summary>
    private static Sprite LoadRandomEmbeddedSprite()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string folder = "TONE.Resources.Background.CurrentArtWinner";
            var names = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(folder, StringComparison.Ordinal)
                            && n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (names.Length == 0) return null;

            var choice = names[IRandom.Instance.Next(0, names.Length)];

            using var stream = Utils.GetResourceStream(choice);
            if (stream == null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            var texture = DecodeViaUnity(bytes);
            if (texture == null) return null;

            Logger.Info($"使用内嵌默认背景：{choice}", "BackgroundManager");
            return CreateSpriteAutoCover(texture);
        }
        catch (Exception ex)
        {
            Logger.Warn($"读取内嵌背景失败：{ex.Message}", "BackgroundManager");
            return null;
        }
    }

    private static Sprite LoadSpriteFromFile(string path)
    {
        try
        {
            var texture = LoadTextureFromFile(path);
            if (texture == null) return null;

            return CreateSpriteAutoCover(texture);
        }
        catch (Exception ex)
        {
            Logger.Warn($"背景读取失败 {System.IO.Path.GetFileName(path)}：{ex.Message}", "BackgroundManager");
            return null;
        }
    }

    /// <summary>
    /// 按「恰好覆盖屏幕」的 PPU 建 Sprite：任一方向都不小于屏幕，不会露黑边。
    /// 自定义图片与内嵌兜底都走这里，保证表现一致。
    /// </summary>
    private static Sprite CreateSpriteAutoCover(Texture2D texture)
    {
        float w = texture.width, h = texture.height;
        if (w <= 0 || h <= 0) return null;

        float ppu = Math.Min(ReferencePpu * w / RefWidth, ReferencePpu * h / RefHeight);
        if (ppu <= 0) ppu = ReferencePpu;

        var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
        return sprite;
    }

    private static Texture2D LoadTextureFromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0) return null;

        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".bmp" => DecodeBmp(bytes),
            ".tga" => DecodeTga(bytes),
            _ => DecodeViaUnity(bytes),   // .png / .jpg / .jpeg
        };
    }

    /// <summary>PNG / JPG 交给 Unity 原生解码。</summary>
    private static Texture2D DecodeViaUnity(byte[] bytes)
    {
        var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        var buf = new Il2CppStructArray<byte>(bytes.Length);
        for (int i = 0; i < bytes.Length; i++) buf[i] = bytes[i];

        if (!ImageConversion.LoadImage(texture, buf, false))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        return texture;
    }

    /// <summary>
    /// 最小 BMP 解码：支持未压缩的 8/24/32 位。
    /// 其它情况（RLE 压缩等）返回 null，由调用方退回默认背景。
    /// </summary>
    private static Texture2D DecodeBmp(byte[] d)
    {
        if (d.Length < 54 || d[0] != (byte)'B' || d[1] != (byte)'M') return null;

        int dataOffset = BitConverter.ToInt32(d, 10);
        int infoSize = BitConverter.ToInt32(d, 14);
        int w = BitConverter.ToInt32(d, 18);
        int hRaw = BitConverter.ToInt32(d, 22);
        int bpp = BitConverter.ToInt16(d, 28);
        int compression = BitConverter.ToInt32(d, 30);

        if (compression != 0) return null;          // 只处理 BI_RGB（未压缩）
        if (w <= 0 || hRaw == 0) return null;
        if (bpp != 8 && bpp != 24 && bpp != 32) return null;

        bool topDown = hRaw < 0;
        int h = Math.Abs(hRaw);

        // 8 位需要调色板
        int paletteCount = 0, paletteOffset = 14 + infoSize;
        if (bpp == 8) paletteCount = Math.Max(0, (dataOffset - paletteOffset) / 4);

        int rowSize = ((bpp * w + 31) / 32) * 4;
        long need = (long)dataOffset + (long)rowSize * h;
        if (need > d.Length) return null;

        var px = new Il2CppStructArray<Color32>((long)(w * h));

        for (int y = 0; y < h; y++)
        {
            int srcY = topDown ? y : (h - 1 - y);
            int row = dataOffset + srcY * rowSize;
            int dstRow = y * w;

            for (int x = 0; x < w; x++)
            {
                byte r, g, b, a = 255;

                if (bpp == 8)
                {
                    int idx = d[row + x];
                    if (idx >= paletteCount) { r = g = b = 0; }
                    else
                    {
                        int po = paletteOffset + idx * 4;
                        b = d[po]; g = d[po + 1]; r = d[po + 2];
                    }
                }
                else if (bpp == 24)
                {
                    int o = row + x * 3;
                    b = d[o]; g = d[o + 1]; r = d[o + 2];
                }
                else
                {
                    int o = row + x * 4;
                    b = d[o]; g = d[o + 1]; r = d[o + 2];
                    a = d[o + 3];
                    if (a == 0) a = 255;       // 很多 BMP 把 A 当填充字节写 0
                }

                px[dstRow + x] = new Color32(r, g, b, a);
            }
        }

        return BuildTexture(px, w, h);
    }

    /// <summary>
    /// 最小 TGA 解码：支持类型 2/3（未压缩）与 10/11（RLE），8/24/32 位。
    /// </summary>
    private static Texture2D DecodeTga(byte[] d)
    {
        if (d.Length < 18) return null;

        int idLength = d[0];
        int cmapType = d[1];
        int imageType = d[2];
        int w = d[12] | (d[13] << 8);
        int h = d[14] | (d[15] << 8);
        int bpp = d[16];
        int desc = d[17];

        if (cmapType != 0) return null;                       // 不支持调色板
        if (imageType != 2 && imageType != 3 && imageType != 10 && imageType != 11) return null;
        if (w <= 0 || h <= 0) return null;

        bool rle = imageType >= 10;
        bool gray = imageType is 3 or 11;
        if (gray && bpp != 8) return null;
        if (!gray && bpp != 24 && bpp != 32) return null;

        int bytesPerPixel = bpp / 8;
        int offset = 18 + idLength;
        bool topOrigin = (desc & 0x20) != 0;

        var px = new Il2CppStructArray<Color32>((long)(w * h));

        int total = w * h;
        int i = 0;
        int p = offset;

        while (i < total)
        {
            int count;
            bool isRun;

            if (rle)
            {
                if (p >= d.Length) break;
                int packet = d[p++];
                count = (packet & 0x7F) + 1;
                isRun = (packet & 0x80) != 0;
            }
            else
            {
                count = total - i;
                isRun = false;
            }

            if (isRun)
            {
                if (p + bytesPerPixel > d.Length) break;
                byte r, g, b, a = 255;
                if (gray) { r = g = b = d[p]; }
                else
                {
                    b = d[p]; g = d[p + 1]; r = d[p + 2];
                    if (bpp == 32) a = d[p + 3];
                }
                p += bytesPerPixel;

                for (int k = 0; k < count && i < total; k++, i++)
                    px[IndexTga(i, w, h, topOrigin)] = new Color32(r, g, b, a);
            }
            else
            {
                for (int k = 0; k < count && i < total; k++, i++)
                {
                    if (p + bytesPerPixel > d.Length) break;
                    byte r, g, b, a = 255;
                    if (gray) { r = g = b = d[p]; }
                    else
                    {
                        b = d[p]; g = d[p + 1]; r = d[p + 2];
                        if (bpp == 32) a = d[p + 3];
                    }
                    p += bytesPerPixel;
                    px[IndexTga(i, w, h, topOrigin)] = new Color32(r, g, b, a);
                }
            }
        }

        return BuildTexture(px, w, h);
    }

    /// <summary>TGA 默认原点在左下角（与 Unity 纹理一致），置位 0x20 时是左上角。</summary>
    private static int IndexTga(int i, int w, int h, bool topOrigin)
    {
        if (topOrigin) return i;
        int x = i % w;
        int y = i / w;
        return (h - 1 - y) * w + x;
    }

    private static Texture2D BuildTexture(
        Il2CppStructArray<Color32> px, int w, int h)
    {
        try
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(px);
            texture.Apply();
            return texture;
        }
        catch (Exception ex)
        {
            Logger.Warn($"构建纹理失败：{ex.Message}", "BackgroundManager");
            return null;
        }
    }

    /// <summary>
    /// 打开背景目录。
    /// 电脑端用资源管理器；手机端没有通用的目录浏览器，改为把路径写进日志并尝试系统打开。
    /// </summary>
    public static void OpenFolder()
    {
        try
        {
            var folder = FolderPath;
            Directory.CreateDirectory(folder);

            Logger.Info($"背景目录：{folder}", "BackgroundManager");

            if (OperatingSystem.IsAndroid())
            {
                // 安卓无法可靠打开任意目录，退化为提示路径
                try { Application.OpenURL("file://" + folder); } catch { }
                return;
            }

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe")
                {
                    Arguments = $"\"{folder.Replace("/", "\\")}\"",
                });
        }
        catch (Exception ex)
        {
            Logger.Warn($"打开背景目录失败：{ex.Message}", "BackgroundManager");
        }
    }
}
