using System.Collections.Generic;
using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Registers Harmony patches and owns shared scenario state.
/// </summary>
public static class PatchManager
{
    private static HarmonyLib.Harmony _harmony;

    public static long CurrentSceneId { get; private set; }
    public static bool IsPlayingScenario { get; private set; }

    public static void Initialize()
    {
        if (_harmony != null)
            return;

        ResetState();
        var harmony = new HarmonyLib.Harmony(ModInfo.Name);
        try
        {
            harmony.PatchAll(typeof(PatchManager).Assembly);
            _harmony = harmony;
            Logger.Info("Harmony patches applied");
        }
        catch
        {
            try
            {
                harmony.UnpatchSelf();
            }
            catch (System.Exception e)
            {
                Logger.Error($"Harmony rollback failed: {e}");
            }

            ResetState();
            throw;
        }
    }

    public static void Shutdown()
    {
        var harmony = _harmony;
        _harmony = null;
        try
        {
            harmony?.UnpatchSelf();
            if (harmony != null)
                Logger.Info("Harmony patches removed");
        }
        finally
        {
            ResetState();
        }
    }

    public static void SetCurrentScene(long sceneId) => CurrentSceneId = sceneId;

    public static void SetScenarioPlaying(bool playing) => IsPlayingScenario = playing;

    private static void ResetState()
    {
        CurrentSceneId = 0;
        IsPlayingScenario = false;
    }

    public static bool TryGetCurrentSceneTranslation(out Dictionary<string, string> translation)
    {
        translation = null;
        return Config.TranslationEnabled.Value
            && Core.Translations != null
            && Core.Translations.TryGetSceneTranslation(CurrentSceneId, out translation);
    }
}
