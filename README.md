# MuvluvMod Android

本版本针对 [LemonLoader v0.7.4-android.3](https://github.com/LemonLoaderX/LemonLoader/releases/tag/v0.7.4-android.3) 构建，Mod 目标为 `net10.0`，需要 .NET 10 SDK；加载器运行时为 .NET 11，历史 `net6` 目录仍作为引用和安装路径。Loader 编译 DLL 来自正式发行包，不随 Mod ZIP 部署。使用 Patcher 安装该加载器时需 2.1.0 或更新版；从原始游戏 APK 准备新 payload，保留包名、签名及用户数据，勿混用旧运行时。游戏 Interop 保持各项目现有导出，运行验证需使用对应游戏版本。

MuvluvMod 的 LemonLoader Android 移植版，为 Unity IL2CPP 客户端提供中文本地化与游玩体验优化。

## 功能

- 剧情、界面和游戏数据翻译
- 翻译下载、本地缓存和离线回退
- 自定义中文 TextMeshPro 字体
- 动态马赛克、战斗跳过和自动跳过
- 剧情语音播放优化
- Mod 状态与配置变更 Toast 通知

## 环境

- Android ARM64
- Unity IL2CPP
- LemonLoader 或兼容的 MelonLoader Android 环境

## 安装

从 Releases 下载 `MuvluvMod-Android.zip`，解压到游戏的 `MelonLoader` base 目录并保留归档中的 `Mods` 和 `UserData` 路径。配置文件首次启动后生成在 `MelonLoader/UserData/MuvluvMod.cfg`。

## 构建

Utility 通过固定提交的 Git submodule 与 `ProjectReference` 从源码构建，不再维护公共库 DLL 副本。

```powershell
git submodule update --init --recursive
python shared/ModEngineering/scripts/project.py package
```

输出位于 `artifacts/release/v<version>/`。本地共享开发目录、依赖升级和 CI 配置见 [shared/ModEngineering/docs/CONVENTIONS.md](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。游戏和加载器编译引用见 [dependencies/README.md](dependencies/README.md)。

## 自动发布

推送任意分支或创建 Pull Request 会执行 Release 构建验证，但不会上传占用 Actions 存储配额的 artifact。推送与项目版本一致的 `v*` 标签时，工作流会把 ZIP 和校验文件直接发布到对应 GitHub Release；标签和项目版本不一致会直接失败。

## 开发

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

Android 使用 MelonPreferences，保留配置键和 UserData 路径，修改后重启生效。资源地址及路径统一修剪空格，无效 HTTP/HTTPS 地址及空路径回退默认值；文本和数值编辑只写日志，布尔开关保留 Toast。字体归入口生命周期，后台翻译和缺失剧本请求接收卸载取消信号；同一资源的旧请求不能删除后来新建的请求。协程尚在交回游戏加载结果时保留运行依赖，卸载成功后解除配置事件、字体注册和 HTTP 资源。主数据实际写入前重新检查翻译开关，关闭翻译仍正常完成游戏加载。

测试覆盖缓存、取消、缺失剧本开关和等待期间关闭翻译；字体和剧情调度需游戏内验证。PC 的 ConfigurationManager 标签和快捷键不参与 Android 构建。
