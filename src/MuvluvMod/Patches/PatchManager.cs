using HarmonyLib;

namespace MuvluvMod.Patches;

/// <summary>
/// Registers Harmony patches and owns shared scenario state.
/// </summary>
public static class PatchManager
{
    private static HarmonyLib.Harmony _harmony;

    public static bool IsPlayingScenario { get; private set; }

    public static void Initialize()
    {
        if (_harmony != null)
            return;

        ResetState();
        var harmony = new HarmonyLib.Harmony(ModInfo.Name);
        _harmony = harmony;
        try
        {
            harmony.PatchAll(typeof(PatchManager).Assembly);
            Logger.Info("Harmony patches applied");
        }
        catch
        {
            try
            {
                harmony.UnpatchSelf();
                _harmony = null;
                ResetState();
            }
            catch (System.Exception e)
            {
                Logger.Error($"Harmony rollback failed: {e}");
            }

            throw;
        }
    }

    public static bool Shutdown()
    {
        if (TranslationPatch.HasPendingLoads)
        {
            Logger.Warn("Shutdown deferred: scenario or master data load is active");
            return false;
        }
        var harmony = _harmony;
        try
        {
            harmony?.UnpatchSelf();
            if (harmony != null)
                Logger.Info("Harmony patches removed");
        }
        catch (System.Exception e)
        {
            Logger.Error($"Harmony shutdown failed: {e}");
            return false;
        }
        _harmony = null;
        TranslationPatch.Reset();
        ResetState();
        return true;
    }

    public static void SetScenarioPlaying(bool playing) => IsPlayingScenario = playing;

    private static void ResetState()
    {
        IsPlayingScenario = false;
    }
}
