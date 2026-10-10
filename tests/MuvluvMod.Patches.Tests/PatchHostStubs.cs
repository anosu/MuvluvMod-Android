namespace HarmonyLib
{
    internal sealed class Harmony
    {
        internal static bool FailPatch;
        internal static bool FailUnpatch;
        internal static bool Installed;
        internal static int UnpatchAttempts;

        internal Harmony(string id) { }

        internal void PatchAll(Type type)
        {
            Installed = true;
            if (FailPatch)
                throw new InvalidOperationException("Simulated patch failure");
        }

        internal void PatchAll(System.Reflection.Assembly assembly) => PatchAll(typeof(Harmony));

        internal void UnpatchSelf()
        {
            UnpatchAttempts++;
            if (FailUnpatch)
                throw new InvalidOperationException("Simulated unpatch failure");
            Installed = false;
        }
    }
}

namespace MuvluvMod
{
    internal static class Logger
    {
        internal static void Error(string text) { }

        internal static void Info(string text) { }

        internal static void Warn(string text) { }
    }

    internal static class ModInfo
    {
        internal const string Name = "MuvluvMod";
    }
}

namespace MuvluvMod.Patches
{
    internal static class TranslationPatch
    {
        internal static bool HasPendingLoads;

        internal static void Reset() { }
    }
}
