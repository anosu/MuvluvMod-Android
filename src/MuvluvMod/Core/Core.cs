using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
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
    private static CancellationTokenSource _shutdown;
    private static AssetBundleLoader<TMP_FontAsset> _font;
    private static IEnumerator _fontLoad;
    private static object _fontCoroutine;

    public static MelonLogger.Instance Log { get; private set; }
    public static TranslationManager Translations { get; private set; }
    internal static MissingSceneReporter MissingSceneReporter { get; private set; }

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
            LoadFont();
            PatchManager.Initialize();

            Logger.Info($"{ModInfo.Name} loaded successfully");
            Toast.Success(ModInfo.Name, $"Mod 加载成功，版本: {ModInfo.Version}", duration: 7f);
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
            string text =
                entry.Exception == null
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
        _shutdown = new CancellationTokenSource();
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
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{ModInfo.Name}/{ModInfo.Version}");

        string cacheDirectory = ResolveUserDataPath(Config.TranslationCacheDirectory.Value);
        Logger.Info($"Translation cache directory: {cacheDirectory}");
        var translationCache = new TranslationCache(
            Config.TranslationCdnUrl.Value,
            cacheDirectory,
            Config.TranslationLanguage.Value,
            Config.TranslationPreferLocalFiles.Value,
            _httpClient,
            _shutdown.Token
        );

        Translations = new TranslationManager(translationCache);
        _font = new AssetBundleLoader<TMP_FontAsset>(
            ResolveUserDataPath(Config.FontBundlePath.Value)
        );
        MissingSceneReporter = new MissingSceneReporter(
            _httpClient,
            () => Config.SubmitMissingScenes.Value,
            _shutdown.Token
        );
    }

    private static void LoadFont()
    {
        if (!Config.TranslationEnabled.Value || _font == null || _font.IsLoaded || _font.IsLoading)
            return;
        _fontLoad = _font.Load(
            () =>
            {
                if (!TMP_Settings.fallbackFontAssets.Contains(_font.Asset))
                    TMP_Settings.fallbackFontAssets.Add(_font.Asset);
                Logger.Info($"Fallback font registered: {_font.Asset.name}");
            },
            error => Toast.Error("字体加载失败", error.Message)
        );
        _fontCoroutine = MelonCoroutines.Start(_fontLoad);
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
            if (!PatchManager.Shutdown())
                return;
        }
        catch (Exception e)
        {
            Log?.Error($"Patch shutdown failed: {e}");
            return;
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
        Config.Shutdown();
        MissingSceneReporter = null;
        _shutdown?.Cancel();
        _shutdown?.Dispose();
        _shutdown = null;
        if (_fontCoroutine != null)
            MelonCoroutines.Stop(_fontCoroutine);
        (_fontLoad as IDisposable)?.Dispose();
        if (_font?.IsLoaded == true)
            TMP_Settings.fallbackFontAssets.Remove(_font.Asset);
        _font = null;
        _fontLoad = null;
        _fontCoroutine = null;
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
