using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using MuvluvMod.Patches;
using MuvluvMod.Services;
using Utility.Assets;
using Utility.Diagnostics;
using Utility.Notifications;

[assembly: MelonInfo(
    typeof(MuvluvMod.Core),
    MuvluvMod.ModInfo.Name,
    MuvluvMod.ModInfo.Version,
    MuvluvMod.ModInfo.Author
)]
[assembly: HarmonyDontPatchAll]

namespace MuvluvMod;

public sealed class Core : MelonMod
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;

    private static HttpClient _httpClient;

    public static MelonLogger.Instance Log { get; private set; }
    public static TranslationManager Translations { get; private set; }
    public static MissingSceneReporter MissingSceneReporter { get; private set; }

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;

        try
        {
            InitializeUtility();
            Config.Initialize();
            InitializeServices();
            MissingSceneReporter.Initialize();
            Translations.Initialize();
            PatchManager.Initialize();

            Logger.Info($"{ModInfo.Name} loaded successfully");
            Toast.Success(
                ModInfo.Name,
                $"Mod 加载成功，版本: {ModInfo.Version}",
                duration: 7f
            );
        }
        catch (Exception e)
        {
            Log.Error($"Initialization failed: {e}");
            Shutdown();
            throw;
        }
    }

    public override void OnDeinitializeMelon() => Shutdown();

    private static void InitializeUtility()
    {
        Logging.SetSink(entry =>
        {
            string text = entry.Exception == null
                ? $"[{entry.Category}] {entry.Message}"
                : $"[{entry.Category}] {entry.Message}\n{entry.Exception}";

            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Log.Warning(text);
                    break;
                case LogLevel.Error:
                    Log.Error(text);
                    break;
                default:
                    Log.Msg(text);
                    break;
            }
        });
        Toast.Initialize($"{ModInfo.Name}.ToastManager");
    }

    private static void InitializeServices()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(PooledConnectionIdleTimeoutMinutes),
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{ModInfo.Name}/{ModInfo.Version}"
        );

        string cacheDirectory = ResolveUserDataPath(Config.TranslationCacheDirectory.Value);
        Logger.Info($"Translation cache directory: {cacheDirectory}");
        var translationCache = new TranslationCache(
            Config.TranslationCdnUrl.Value,
            cacheDirectory,
            Config.TranslationLanguage.Value,
            Config.TranslationPreferLocalFiles.Value,
            _httpClient
        );

        Translations = new TranslationManager(
            translationCache,
            new AssetBundleLoader<TMP_FontAsset>(
                ResolveUserDataPath(Config.FontBundlePath.Value)
            )
        );
        MissingSceneReporter = new MissingSceneReporter(_httpClient);
    }

    private static string ResolveUserDataPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Configured path cannot be empty", nameof(path));

        return Path.GetFullPath(
            Path.IsPathRooted(path) ? path : Path.Combine(MelonEnvironment.UserDataDirectory, path)
        );
    }

    private static void Shutdown()
    {
        try
        {
            PatchManager.Shutdown();
        }
        catch (Exception e)
        {
            Log?.Error($"Patch shutdown failed: {e}");
        }

        try
        {
            Translations?.Shutdown();
        }
        catch (Exception e)
        {
            Log?.Error($"Translation shutdown failed: {e}");
        }

        Translations = null;
        MissingSceneReporter = null;
        _httpClient?.Dispose();
        _httpClient = null;

        try
        {
            Toast.Shutdown();
        }
        catch (Exception e)
        {
            Log?.Error($"Toast shutdown failed: {e}");
        }

        Logging.SetSink(null);
    }
}
