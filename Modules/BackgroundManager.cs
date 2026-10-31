using System;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TONE;

public static class BackgroundManager
{
    private const string DATA_FOLDER = "BrushUpRoles-DATA";
    private const string BACKGROUND_FOLDER = "Backgrounds";

    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".tga"];

    private const float RefWidth = 1600f;
    private const float RefHeight = 900f;

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

    public static float ReferencePpu => OperatingSystem.IsAndroid() ? 119f : 150f;

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

    public static Sprite LoadRandomSprite()
    {
        var files = GetUserImagePaths();

        if (files.Length > 0)
        {

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
            _ => DecodeViaUnity(bytes),
        };
    }

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

    private static Texture2D DecodeBmp(byte[] d)
    {
        if (d.Length < 54 || d[0] != (byte)'B' || d[1] != (byte)'M') return null;

        int dataOffset = BitConverter.ToInt32(d, 10);
        int infoSize = BitConverter.ToInt32(d, 14);
        int w = BitConverter.ToInt32(d, 18);
        int hRaw = BitConverter.ToInt32(d, 22);
        int bpp = BitConverter.ToInt16(d, 28);
        int compression = BitConverter.ToInt32(d, 30);

        if (compression != 0) return null;
        if (w <= 0 || hRaw == 0) return null;
        if (bpp != 8 && bpp != 24 && bpp != 32) return null;

        bool topDown = hRaw < 0;
        int h = Math.Abs(hRaw);

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
                    if (a == 0) a = 255;
                }

                px[dstRow + x] = new Color32(r, g, b, a);
            }
        }

        return BuildTexture(px, w, h);
    }

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

        if (cmapType != 0) return null;
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

    public static void OpenFolder()
    {
        try
        {
            var folder = FolderPath;
            Directory.CreateDirectory(folder);

            Logger.Info($"背景目录：{folder}", "BackgroundManager");

            if (OperatingSystem.IsAndroid())
            {

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
