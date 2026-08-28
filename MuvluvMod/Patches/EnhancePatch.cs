using Il2CppAssets.Battle.Overseers;
using Il2CppAssets.CustomRendererFeatures;
using Il2CppAssets.GameUi.Externals;
using Il2CppAssets.GameUi.Scenario;
using Il2CppAssets.VisualEffectData.VisualEffects;
using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Contains gameplay enhancement patches unrelated to translation rendering.
/// </summary>
[HarmonyPatch]
public static class EnhancePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MosaicRendererFeature), nameof(MosaicRendererFeature.Create))]
    public static void ConfigureMosaic(MosaicRendererFeature __instance)
    {
        if (!Config.DynamicMosaic.Value && __instance?.passSettings != null)
            __instance.passSettings.Keyword = "114514";
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudOverseer), nameof(HudOverseer.SetSkipAvaiability))]
    public static void ConfigureSkip(HudOverseer __instance, ref bool available)
    {
        if (Config.EnableSkipButton.Value)
            available = true;

        var skipButton = __instance?.skipButton;
        if (Config.AutoSkipBattle.Value && skipButton?.onClick != null)
            skipButton.onClick.Invoke();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.StopVoice))]
    public static bool ConfigureVoiceInterruption() =>
        Config.VoiceInterruption.Value || !PatchManager.IsPlayingScenario;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.Refresh), [])]
    public static void BeginScenario() => PatchManager.SetScenarioPlaying(true);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.Leave))]
    public static void EndScenario() => PatchManager.SetScenarioPlaying(false);
}
