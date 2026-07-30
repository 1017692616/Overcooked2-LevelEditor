# Overcooked2 Level Editor

《胡闹厨房 2》自定义关卡编辑器项目。

This repository contains a Unity-based level editor and reference assets for creating custom Overcooked! 2 levels.

## Project Rules

Please read [PROJECT_CONSTITUTION.md](PROJECT_CONSTITUTION.md) before contributing.

Every completed change must:

1. Be made on the corresponding task branch.
2. Update the relevant README and documentation.
3. Use a commit with both Chinese and English explanations.
4. Be pushed successfully to the corresponding remote branch.

每次完成修改后必须：

1. 在对应任务分支上完成。
2. 更新相关 README 和文档。
3. 使用包含中英文说明的 commit。
4. 成功推送到对应远端分支。

## Local Assets

The editor loads the original game AssetBundles at runtime. Copy the game's `Overcooked2_Data/StreamingAssets/Windows` directory to:

```text
Assets/StreamingAssets/Windows
```

Do not commit the original game's proprietary models, textures, audio, or binary bundles to this repository.

Runtime note: when Unity reloads scripts or re-enters Play Mode, already loaded AssetBundles are reused instead of loading duplicate bundles with the same name. If the editor reports that the `Windows` bundle cannot be loaded, verify that `Assets/StreamingAssets/Windows` points to the game's `Overcooked2_Data/StreamingAssets/Windows` folder and restart Unity before testing again.

运行说明：Unity 重新加载脚本或重新进入 Play Mode 时，会复用已经加载的 AssetBundle，避免重复加载同名 bundle。如果编辑器提示无法加载 `Windows` bundle，请确认 `Assets/StreamingAssets/Windows` 指向游戏目录里的 `Overcooked2_Data/StreamingAssets/Windows` 文件夹，然后重启 Unity 再测试。

If duplicate bundle errors continue after a script reload, exit Play Mode and reopen the project so Unity releases bundles that were loaded before the current scripts were compiled.

如果脚本重载后仍然提示重复加载 bundle，请先退出 Play Mode 并重新打开项目，让 Unity 释放旧脚本版本加载过的 bundle。

编辑器运行时会加载游戏原始 AssetBundle。请将游戏目录中的 `Overcooked2_Data/StreamingAssets/Windows` 复制到：

```text
Assets/StreamingAssets/Windows
```

不要将游戏专有的模型、贴图、音频或二进制 bundle 提交到本仓库。

## Documentation

- English tutorial: [Docs/en/tutorial.md](Docs/en/tutorial.md)
- English reference: [Docs/en/reference.md](Docs/en/reference.md)

## DLC References

On a machine with the Steam game installed, run:

```powershell
python tools/generate_dlc_assets.py --game-streaming-assets "F:\SteamLibrary\steamapps\common\Overcooked! 2\Overcooked2_Data\StreamingAssets\Windows"
```

The generator scans every `bundle*` file and automatically groups assets by `downloadablecontent/dlcXX`. It generates lightweight references under `Assets/dlc` for DLC recipes, ingredients, cooking steps, plating steps, icons, recipe products, kitchen prefabs, and `RecipeMatchList` assets. The original game bundles remain local and must not be committed.
- 中文教程：[Docs/zh/tutorial.md](Docs/zh/tutorial.md)
- 中文参考：[Docs/zh/reference.md](Docs/zh/reference.md)
