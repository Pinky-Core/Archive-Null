using UnityEngine;
using UnityEngine.Rendering;

public static class GraphicsSettingsManager
{
    public enum Preset { Low, Medium, High, Epic, Custom }

    private const string Prefix = "archive.graphics.";
    private static readonly int[] AntiAliasingValues = { 0, 2, 4, 8 };
    private static readonly int[] FpsValues = { 30, 60, 120, -1 };
    private static readonly float[] RenderScales = { 0.6f, 0.75f, 0.9f, 1f, 1.15f };

    public static Preset CurrentPreset => (Preset)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "preset", (int)Preset.Medium), 0, 4);
    public static int ShadowIndex => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "shadows", 2), 0, 2);
    public static int TextureIndex => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "textures", 1), 0, 2);
    public static int AntiAliasingIndex => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "aa", 1), 0, AntiAliasingValues.Length - 1);
    public static int ShadowDistanceIndex => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "shadow_distance", 1), 0, 4);
    public static int RenderScaleIndex => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "render_scale", 2), 0, RenderScales.Length - 1);
    public static bool VSync => PlayerPrefs.GetInt(Prefix + "vsync", 0) == 1;
    public static int FpsIndex => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "fps", 1), 0, FpsValues.Length - 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() => ApplySaved();

    public static void CyclePreset(int direction = 1)
    {
        int value = ((int)CurrentPreset + direction + 5) % 5;
        ApplyPreset((Preset)value);
    }

    public static void ApplyPreset(Preset preset)
    {
        PlayerPrefs.SetInt(Prefix + "preset", (int)preset);
        switch (preset)
        {
            case Preset.Low: SaveValues(0, 0, 0, 0, 0, false, 1); break;
            case Preset.Medium: SaveValues(1, 1, 1, 1, 1, false, 1); break;
            case Preset.High: SaveValues(2, 2, 2, 2, 3, true, 1); break;
            case Preset.Epic: SaveValues(2, 2, 3, 4, 4, true, 2); break;
        }
        PlayerPrefs.Save();
        ApplySaved();
    }

    public static void CycleShadows() => SetCustomInt("shadows", (ShadowIndex + 1) % 3);
    public static void CycleTextures() => SetCustomInt("textures", (TextureIndex + 1) % 3);
    public static void CycleAntiAliasing() => SetCustomInt("aa", (AntiAliasingIndex + 1) % AntiAliasingValues.Length);
    public static void CycleShadowDistance() => SetCustomInt("shadow_distance", (ShadowDistanceIndex + 1) % 5);
    public static void CycleRenderScale() => SetCustomInt("render_scale", (RenderScaleIndex + 1) % RenderScales.Length);
    public static void ToggleVSync() => SetCustomInt("vsync", VSync ? 0 : 1);
    public static void CycleFps() => SetCustomInt("fps", (FpsIndex + 1) % FpsValues.Length);

    public static void ApplySaved()
    {
        QualitySettings.shadows = ShadowIndex switch
        {
            0 => ShadowQuality.Disable,
            1 => ShadowQuality.HardOnly,
            _ => ShadowQuality.All
        };
        QualitySettings.globalTextureMipmapLimit = 2 - TextureIndex;
        QualitySettings.antiAliasing = AntiAliasingValues[AntiAliasingIndex];
        QualitySettings.shadowDistance = new[] { 15f, 30f, 50f, 75f, 100f }[ShadowDistanceIndex];
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync ? -1 : FpsValues[FpsIndex];
        ApplyRenderScale(RenderScales[RenderScaleIndex]);
    }

    public static string PresetLabel(bool spanish) => CurrentPreset switch
    {
        Preset.Low => spanish ? "BAJO" : "LOW",
        Preset.Medium => spanish ? "MEDIO" : "MEDIUM",
        Preset.High => spanish ? "ALTO" : "HIGH",
        Preset.Epic => spanish ? "EPICO" : "EPIC",
        _ => spanish ? "PERSONALIZADO" : "CUSTOM"
    };
    public static string ShadowLabel(bool spanish) => ShadowIndex switch { 0 => spanish ? "NO" : "OFF", 1 => spanish ? "DURAS" : "HARD", _ => spanish ? "SUAVES" : "SOFT" };
    public static string TextureLabel(bool spanish) => TextureIndex switch { 0 => spanish ? "BAJA" : "LOW", 1 => spanish ? "MEDIA" : "MEDIUM", _ => spanish ? "ALTA" : "HIGH" };
    public static string AntiAliasingLabel() => AntiAliasingValues[AntiAliasingIndex] == 0 ? "OFF" : AntiAliasingValues[AntiAliasingIndex] + "X";
    public static string ShadowDistanceLabel() => new[] { "15 M", "30 M", "50 M", "75 M", "100 M" }[ShadowDistanceIndex];
    public static string RenderScaleLabel() => Mathf.RoundToInt(RenderScales[RenderScaleIndex] * 100f) + "%";
    public static string VSyncLabel() => VSync ? "ON" : "OFF";
    public static string FpsLabel() => FpsValues[FpsIndex] < 0 ? "SIN LIMITE" : FpsValues[FpsIndex].ToString();

    private static void SetCustomInt(string key, int value)
    {
        PlayerPrefs.SetInt(Prefix + key, value);
        PlayerPrefs.SetInt(Prefix + "preset", (int)Preset.Custom);
        PlayerPrefs.Save();
        ApplySaved();
    }

    private static void SaveValues(int shadows, int textures, int aa, int shadowDistance, int renderScale, bool vsync, int fps)
    {
        PlayerPrefs.SetInt(Prefix + "shadows", shadows);
        PlayerPrefs.SetInt(Prefix + "textures", textures);
        PlayerPrefs.SetInt(Prefix + "aa", aa);
        PlayerPrefs.SetInt(Prefix + "shadow_distance", shadowDistance);
        PlayerPrefs.SetInt(Prefix + "render_scale", renderScale);
        PlayerPrefs.SetInt(Prefix + "vsync", vsync ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "fps", fps);
    }

    private static void ApplyRenderScale(float scale)
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null) return;
        var property = asset.GetType().GetProperty("renderScale");
        if (property != null && property.CanWrite) property.SetValue(asset, scale);
    }
}
