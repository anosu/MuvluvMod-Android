using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using Utility.Notifications;

namespace MuvluvMod;

public static class Config
{
    public static readonly string FilePath = Path.Combine(
        MelonEnvironment.UserDataDirectory,
        $"{ModInfo.Name}.cfg"
    );

    public static MelonPreferences_Entry<bool> DynamicMosaic { get; private set; }
    public static MelonPreferences_Entry<bool> EnableSkipButton { get; private set; }
    public static MelonPreferences_Entry<bool> VoiceInterruption { get; private set; }
    public static MelonPreferences_Entry<bool> AutoSkipBattle { get; private set; }

    public static MelonPreferences_Entry<bool> TranslationEnabled { get; private set; }
    public static MelonPreferences_Entry<string> TranslationCdnUrl { get; private set; }
    public static MelonPreferences_Entry<string> TranslationLanguage { get; private set; }
    public static MelonPreferences_Entry<string> TranslationCacheDirectory { get; private set; }
    public static MelonPreferences_Entry<bool> TranslationPreferLocalFiles { get; private set; }
    public static MelonPreferences_Entry<string> FontBundlePath { get; private set; }
    public static MelonPreferences_Entry<bool> SubmitMissingScenes { get; private set; }

    private static bool _initializing;
    private static bool _entriesBound;
    private static MelonPreferences_Category _preferenceCategory;
    private static readonly List<MelonPreferences_Category> PreferenceCategories = new();

    public static void Initialize()
    {
        _initializing = true;
        try
        {
            if (!_entriesBound)
            {
                BindAllEntries();
                _entriesBound = true;
            }

            _preferenceCategory.LoadFromFile(false);
            foreach (var category in PreferenceCategories)
                category.SaveToFile(false);
        }
        finally
        {
            _initializing = false;
        }
    }

    private static void BindAllEntries()
    {
        var general = CreateCategory("General");
        DynamicMosaic = CreateEntry(
            general,
            "DynamicMosaic",
            false,
            "是否开启游戏内动态马赛克（默认关闭）"
        );
        EnableSkipButton = CreateEntry(
            general,
            "EnableSkipButton",
            true,
            "是否总是开启跳过按钮（默认开启）"
        );
        VoiceInterruption = CreateEntry(
            general,
            "VoiceInterruption",
            false,
            "剧情中播放下一句话时是否中断当前语音（默认关闭）"
        );
        AutoSkipBattle = CreateEntry(
            general,
            "AutoSkipBattle",
            false,
            "自动跳过战斗（自动触发跳过按钮，不受跳过按钮开关影响，默认关闭）"
        );

        var translation = CreateCategory("Translation");
        TranslationEnabled = CreateEntry(
            translation,
            "Enable",
            true,
            "是否开启翻译；修改后重启生效"
        );
        TranslationCdnUrl = CreateEntry(
            translation,
            "CdnURL",
            "https://raw.githubusercontent.com/anosu/muvluvgg-translation/refs/heads/main",
            "翻译加载的CDN；修改后重启生效"
        );
        TranslationLanguage = CreateEntry(
            translation,
            "Language",
            "zh_Hans",
            "翻译语言，目前支持：zh_Hans；修改后重启生效"
        );

        var cache = CreateCategory("Translation.Cache");
        TranslationCacheDirectory = CreateEntry(
            cache,
            "Directory",
            $"{ModInfo.Name}/translation",
            "翻译缓存目录，默认相对于用户数据目录，也可使用绝对路径；修改后重启生效"
        );
        TranslationPreferLocalFiles = CreateEntry(
            cache,
            "PreferLocalFiles",
            false,
            "本地翻译文件存在时是否忽略清单哈希并优先使用本地文件（manifest除外）；修改后重启生效"
        );

        var font = CreateCategory("Translation.Font");
        FontBundlePath = CreateEntry(
            font,
            "AssetBundlePath",
            $"{ModInfo.Name}/sarasagothicsc-bold",
            $"TMP字体AssetBundle路径，默认取 MelonLoader/UserData/{ModInfo.Name}/sarasagothicsc-bold；修改后重启生效"
        );

        var debug = CreateCategory("Translation.Debug");
        SubmitMissingScenes = CreateEntry(
            debug,
            "SubmitMissingScenes",
            true,
            "是否向翻译调试服务提交缺失剧本"
        );
    }

    private static MelonPreferences_Category CreateCategory(string name)
    {
        var category = MelonPreferences.CreateCategory(name);
        category.SetFilePath(FilePath, false, false);
        _preferenceCategory ??= category;
        PreferenceCategories.Add(category);
        return category;
    }

    private static MelonPreferences_Entry<T> CreateEntry<T>(
        MelonPreferences_Category category,
        string key,
        T defaultValue,
        string description
    )
    {
        var entry = category.CreateEntry(key, defaultValue, description, description);
        entry.OnEntryValueChanged.Subscribe(
            (_, newValue) =>
            {
                if (_initializing)
                    return;

                category.SaveToFile(false);
                Logger.Info($"[{category.Identifier}] {key} => {newValue}");
                Toast.Info($"[{category.Identifier}]", $"{key} => {newValue}");
            }
        );
        return entry;
    }
}
