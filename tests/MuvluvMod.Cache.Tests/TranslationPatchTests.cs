using System;
using System.IO;
using System.Threading.Tasks;
using Il2CppAssets.Api.MemoryDB;
using Il2CppCysharp.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MuvluvMod.Patches;
using Xunit;

namespace MuvluvMod.Tests;

public sealed class TranslationPatchTests : IDisposable
{
    public TranslationPatchTests()
    {
        Config.TranslationEnabled.Value = true;
        Core.Instance = new();
        Core.Translations = new();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingMasterDataUsesTheSwitchAtApplicationTime(bool enabledAtCompletion)
    {
        var row = new Row { Text = "original" };
        var data = new Il2CppReferenceArray<IDataObject> { row };
        var result = new UniTask<Il2CppReferenceArray<IDataObject>>(Task.FromResult(data));
        TranslationPatch.TranslateMasterDataAfterLoad(ref result);
        try
        {
            Assert.True(Core.Instance.Step());
            Assert.False(result.GetAwaiter().IsCompleted);
            Assert.True(TranslationPatch.HasPendingLoads);

            Config.TranslationEnabled.Value = enabledAtCompletion;
        }
        finally
        {
            Core.Translations.Ready.TrySetResult();
            Core.Instance.Step();
        }

        Assert.Same(data, await result);
        Assert.False(TranslationPatch.HasPendingLoads);
        Assert.Equal(enabledAtCompletion ? 1 : 0, Core.Translations.Applications);
        Assert.Equal(enabledAtCompletion ? "translated" : "original", row.Text);
    }

    private sealed class Row : IDataObject
    {
        public string Text { get; set; }
    }

    public void Dispose()
    {
        TranslationPatch.Reset();
        Core.Instance = null;
        Core.Translations = null;
    }
}
