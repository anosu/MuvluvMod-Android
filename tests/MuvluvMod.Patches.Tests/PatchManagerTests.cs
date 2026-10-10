using HarmonyLib;
using MuvluvMod.Patches;
using Xunit;

namespace MuvluvMod.Tests;

public sealed class PatchManagerTests : IDisposable
{
    [Fact]
    public void FailedRollbackRetainsHandleForLaterCleanup()
    {
        Harmony.FailPatch = true;
        Harmony.FailUnpatch = true;
        Assert.Throws<InvalidOperationException>(PatchManager.Initialize);
        Assert.True(Harmony.Installed);
        int attempts = Harmony.UnpatchAttempts;
        Harmony.FailUnpatch = false;
        Assert.True(PatchManager.Shutdown());
        Assert.False(Harmony.Installed);
        Assert.Equal(attempts + 1, Harmony.UnpatchAttempts);
    }

    [Fact]
    public void FailedShutdownCanBeRetried()
    {
        PatchManager.Initialize();
        Harmony.FailUnpatch = true;
        Assert.False(PatchManager.Shutdown());
        Assert.True(Harmony.Installed);
        Harmony.FailUnpatch = false;
        Assert.True(PatchManager.Shutdown());
        Assert.False(Harmony.Installed);
    }

    public void Dispose()
    {
        TranslationPatch.HasPendingLoads = false;
        Harmony.FailPatch = Harmony.FailUnpatch = false;
        PatchManager.Shutdown();
    }

    [Fact]
    public void PendingLoadDefersShutdownWithoutLosingHandle()
    {
        PatchManager.Initialize();
        TranslationPatch.HasPendingLoads = true;
        int attempts = Harmony.UnpatchAttempts;
        Assert.False(PatchManager.Shutdown());
        Assert.Equal(attempts, Harmony.UnpatchAttempts);
        Assert.True(Harmony.Installed);
        TranslationPatch.HasPendingLoads = false;
        Assert.True(PatchManager.Shutdown());
        Assert.False(Harmony.Installed);
    }
}
