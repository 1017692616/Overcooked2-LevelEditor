# Overcooked2 Level Editor

《胡闹厨房 2》自定义关卡编辑器。

## 项目规则

开始修改前请阅读 [PROJECT_CONSTITUTION.md](PROJECT_CONSTITUTION.md)。

每次修改完成后必须：

1. 在对应任务分支上完成修改。
2. 同步更新相关 README 和文档。
3. 创建包含中文和英文说明的 commit。
4. 将 commit 成功推送到对应远端分支。

## 本地游戏资源

编辑器运行时会加载游戏原始 AssetBundle。请将游戏目录中的：

```text
Overcooked2_Data/StreamingAssets/Windows
```

复制到项目：

```text
Assets/StreamingAssets/Windows
```

推荐打开 Unity 后直接点击 `Tools > OC2 Setup > Auto Setup`。它会自动查找 Steam 游戏目录，并创建指向游戏 `Overcooked2_Data/StreamingAssets/Windows` 的本地目录链接，不会复制正版 bundle。点击 `Tools > OC2 Setup > Check Environment` 可以查看检测到的路径和 Python 状态。

不要将游戏专有的模型、贴图、音频或二进制 bundle 提交到 Git 仓库。

## 文档

- 中文教程：[Docs/zh/tutorial.md](Docs/zh/tutorial.md)
- 中文参考：[Docs/zh/reference.md](Docs/zh/reference.md)
- English tutorial: [Docs/en/tutorial.md](Docs/en/tutorial.md)
- English reference: [Docs/en/reference.md](Docs/en/reference.md)

## DLC 资源引用

在安装了 Steam 游戏的电脑上运行：

`Tools > OC2 Setup > Auto Setup` 也会自动生成 DLC 轻量引用。如果希望使用命令行，可以运行：

```powershell
python tools/generate_dlc_assets.py --game-streaming-assets "F:\SteamLibrary\steamapps\common\Overcooked! 2\Overcooked2_Data\StreamingAssets\Windows"
```

脚本会扫描所有 `bundle*` 文件，并按 `downloadablecontent/dlcXX` 自动分组，在 `Assets/dlc` 生成 DLC 菜单、食材、烹饪步骤、装盘步骤、图标、成品、厨具 Prefab 和 `RecipeMatchList` 的轻量级引用。正版游戏 bundle 仍保留在本机，不得提交到 GitHub。

关卡配置会自动包含官方 DLC 菜谱匹配表和 DLC 烹饪步骤。使用 DLC 菜单时，只需要把生成的菜单引用加入 `LevelInfoSO.recipes`；旧的 `dlcRecipeMatchListSOs` 和 `dlcCookingStepSOs` 字段只用于额外或非标准引用。

搅拌器、果汁机、烤盘等厨具可使用 `PseudoPrefabCookingUtensil`，其 `allowedIngredientSOs` 同时支持 `CookableContainer` 和 `MixableContainer`。DLC 调味料机或饮料机可使用 `PseudoPrefabPlacementDispenser`，并在 `ingredientSOs` 中配置食材。
