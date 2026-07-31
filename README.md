# Overcooked! 2 Level Editor

这是一个用于制作《胡闹厨房 2 / Overcooked! 2》自定义关卡的 Unity 工程。它适合想做关卡但不熟悉编程的玩家：大多数操作都在 Unity 编辑器里完成，例如摆放桌台、添加食材箱、选择菜单、测试关卡和打包关卡。

English: This is a Unity-based custom level editor for Overcooked! 2. The main workflow is designed for creators who know the game better than programming.

## 你需要准备什么

1. Steam 版《Overcooked! 2》。
2. Unity `2017.4.8f1`。脚本会检测它是否存在；如果没有，仍建议通过 Unity Hub 安装这个版本。
3. 本项目代码。

Python 3 和 AssetRipper 不需要你一开始就手动准备。推荐使用下面的 PowerShell 脚本，它会检测 Python、安装 `UnityPy`、下载/打开 AssetRipper，并把 AssetRipper 导出的 `Assembly-CSharp` 自动复制和打补丁。

本仓库不会提交正版游戏的模型、贴图、音频或 AssetBundle。项目只保存代码和“资源路径引用”。真正的游戏资源仍然从你自己电脑上的正版游戏目录读取。

## 推荐：先运行 Windows 自动准备脚本

在项目根目录打开 PowerShell，运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup_windows.ps1 -InstallMissing
```

脚本会尽量自动完成：

1. 查找 Steam 版《Overcooked! 2》。
2. 检测 Unity `2017.4.8f1`。
3. 检测 Python 3；如果加了 `-InstallMissing`，会尝试用 `winget` 安装。
4. 安装 Python 包 `UnityPy`。
5. 下载 AssetRipper 到工具目录。默认优先放在 `F:/OC2LevelEditorTools`；没有 F 盘时放在项目 `.oc2-tools`。
6. 创建 `Assets/StreamingAssets/Windows` 本地目录链接。
7. 生成 `Assets/dlc` 轻量 DLC 引用。

如果脚本找不到游戏目录，可以手动指定：

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup_windows.ps1 -InstallMissing -GameDir "F:\SteamLibrary\steamapps\common\Overcooked! 2"
```

如果要让脚本打开 AssetRipper：

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup_windows.ps1 -InstallMissing -LaunchAssetRipper
```

AssetRipper 导出完成后，再把导出的 Unity Project 路径交给脚本，它会复制 `Assembly-CSharp` 并覆盖 `Assembly-CSharp-Patch`：

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup_windows.ps1 -AssetRipperExport "F:\AssetRipperExport\ExportedProject"
```

## 第一次打开项目

1. 下载或 clone 本项目。
2. 用 Unity Hub 或 Unity `2017.4.8f1` 打开项目根目录。
3. 如果 Unity 提示导入资源，等待它导入完成。
4. 先运行上面的 `tools/setup_windows.ps1`。如果你没有运行脚本，也可以在 Unity 里做下面的自动配置。

## 一键自动配置

在 Unity 顶部菜单点击：

```text
Tools > OC2 Setup > Auto Setup
```

它会自动做这些事：

1. 查找 Steam 安装的《Overcooked! 2》。
2. 创建本地目录链接：

```text
Assets/StreamingAssets/Windows
```

这个链接指向游戏目录里的：

```text
Overcooked2_Data/StreamingAssets/Windows
```

3. 运行 `tools/generate_dlc_assets.py`。
4. 在 `Assets/dlc` 生成 DLC 菜谱、食材、厨具、图标、RecipeMatchList 等轻量引用。
5. 刷新 Unity 资源数据库。

如果自动配置失败，点击：

```text
Tools > OC2 Setup > Check Environment
```

它会显示当前项目路径、Unity 版本、找到的游戏资源路径和 Python 状态。

## 如果自动配置找不到游戏

请确认 Steam 版游戏已经安装。常见目录类似：

```text
F:/SteamLibrary/steamapps/common/Overcooked! 2
C:/Program Files (x86)/Steam/steamapps/common/Overcooked! 2
```

如果你不想用自动配置，也可以手动处理：

1. 找到游戏目录中的 `Overcooked2_Data/StreamingAssets/Windows`。
2. 把它复制到项目的 `Assets/StreamingAssets/Windows`，或自己创建目录链接。
3. 在项目根目录运行：

```powershell
python tools/generate_dlc_assets.py --game-streaming-assets "F:\SteamLibrary\steamapps\common\Overcooked! 2\Overcooked2_Data\StreamingAssets\Windows"
```

## 准备 Assembly-CSharp

这个项目需要游戏脚本类型才能在 Unity 里正常编译。你需要从自己本机游戏导出脚本：

推荐做法是运行脚本打开 AssetRipper，导出后再让脚本复制：

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup_windows.ps1 -InstallMissing -LaunchAssetRipper
powershell -ExecutionPolicy Bypass -File tools/setup_windows.ps1 -AssetRipperExport "你的导出目录\ExportedProject"
```

手动流程如下：

1. 下载并打开 AssetRipper。
2. 在 AssetRipper 设置中勾选 `Skip StreamingAssets Folder`。
3. 打开游戏目录里的 `Overcooked2_Data`。
4. 导出 Unity Project。
5. 将导出目录里的：

```text
ExportedProject/Assets/Scripts/Assembly-CSharp
```

复制到本项目：

```text
Assets/Scripts/Assembly-CSharp
```

6. 将本项目 `Assembly-CSharp-Patch` 目录里的文件复制到：

```text
Assets/Scripts/Assembly-CSharp
```

并覆盖同名文件。

`Assets/Scripts/Assembly-CSharp` 是本机生成内容，不应该提交到公开仓库。

## 打开示例关卡

项目里有一个可直接编辑的起始关卡：

```text
Assets/LevelSets/codex_demo/scenes/s_codex_demo_1.unity
```

关卡配置文件在：

```text
Assets/LevelSets/codex_demo/data/Level_Codex_1/LevelInfo_Codex_1.asset
```

在 Unity 的 Project 面板双击场景文件打开。场景打开后，Hierarchy 里会有 `PseudoPrefabManager`，它负责加载游戏资源和关卡配置。

## 怎么创建自己的关卡

推荐先复制示例关卡，不要从空场景开始。

1. 在 `Assets/LevelSets` 下复制 `codex_demo` 文件夹。
2. 改成自己的名字，例如：

```text
Assets/LevelSets/my_first_level
```

3. 修改里面的场景名，例如：

```text
s_my_first_level_1.unity
```

4. 修改 `data` 里的 `LevelSetInfo` 和 `LevelInfo` 名字。
5. 打开新场景。
6. 选中 Hierarchy 里的 `PseudoPrefabManager`。
7. 在 Inspector 里找到 `PseudoPrefabManagerStub > levelInfo`。
8. 把你的 `LevelInfo` asset 拖进去。
9. 点击：

```text
Tools > Reload Pseudo Assets
```

这样 Unity 会按你的配置重新加载关卡资源。

## 关卡配置里最常改的字段

打开你的 `LevelInfo_*.asset`，常用字段是：

- `levelName`：英文关卡名。
- `levelNameZH`：中文关卡名。
- `sceneName`：场景名，必须和打包后的场景文件名一致，建议不要用太短的名字。
- `recipes`：这一关会出现的菜单。
- `debugRecipeCount`：通常填 `0`。
- `disableDynamicParenting`：普通静态关卡一般勾选；有移动平台、升降平台时通常取消勾选。
- `config_1p` / `config_2p` / `config_3p` / `config_4p`：不同玩家人数的时间、分数、订单参数。
- `dependencies`：额外需要加载的游戏 bundle。普通关卡通常保留已有配置即可；如果用了特别的 BGM 或 DLC 资源，可能需要加对应 bundle。

## 怎么添加菜单

菜单都放在 `LevelInfoSO.recipes` 里。

原版菜单一般在：

```text
Assets/common01/food/Recipes
```

DLC 菜单由自动配置生成，一般在：

```text
Assets/dlc/dlcXX/Recipes
```

操作方法：

1. 选中你的 `LevelInfo_*.asset`。
2. 在 Inspector 找到 `recipes`。
3. 增加数组大小。
4. 从 Project 面板把菜谱 asset 拖进去。
5. 点击 `Tools > Reload Pseudo Assets`。
6. 点 Play 测试。

现在官方 DLC 的 `RecipeMatchList` 和烹饪步骤会自动合并。也就是说，正常添加 DLC 菜单时，不需要再手动拖 `dlcRecipeMatchListSOs` 或 `dlcCookingStepSOs`。

## 怎么添加 DLC 厨具和资源

自动配置会在 `Assets/dlc` 里生成轻量引用。常见目录：

- `Recipes`：菜单。
- `Ingredients`：食材。
- `Kitchen`：厨具、桌台、机关、厨房 prefab。
- `CookingSteps`：烹饪步骤。
- `PlatingSteps`：装盘步骤。
- `Icons`：图标。
- `Products`：成品模型。

如果要放 DLC 厨具，例如搅拌机、果汁机、火锅、烤盘：

1. 在场景里放一个对应的占位物体或复制已有厨具。
2. 在 Inspector 找到 `PseudoPrefab...Stub` 组件。
3. 把 `Assets/dlc/.../Kitchen` 里的厨具引用拖到对应的 `pseudoPrefabSO` 字段。
4. 如果厨具限制食材，在 `allowedIngredientSOs` 里加入对应食材引用。
5. 点击 `Tools > Reload Pseudo Assets` 查看效果。

DLC 调味料机、饮料机等通常使用 `PseudoPrefabPlacementDispenser`，在 `ingredientSOs` 中配置可生成的食材。

## 摆放物体的基本方法

对 Unity 新手来说，先记这几个就够：

- 左键选中物体。
- `W` 移动物体。
- `E` 旋转物体。
- `R` 缩放物体。
- 按住 `Ctrl` 可以辅助吸附。
- 场景视图右上角可以切换视角。
- 修改资源引用后点 `Tools > Reload Pseudo Assets`。

不要直接编辑加载出来的临时真实物体。多数真实物体是运行时从游戏 bundle 加载的，应该改它旁边或父级上的 `PseudoPrefab...Stub` 配置。


Clean starter map:

```text
Assets/LevelSets/clean_kitchen/scenes/s_clean_kitchen_1.unity
```

`Clean Kitchen` / `干净厨房` is a small clean template level with the default floor, base manager setup, and the burger recipe. Use it as the safe starting point for a new custom level.

## 测试关卡

1. 打开你的关卡场景。
2. 确认 `PseudoPrefabManagerStub.levelInfo` 指向你的 `LevelInfo`。
3. 点击：

```text
Tools > Reload Pseudo Assets
```

4. 点击 Unity 顶部的 Play。
5. 如果菜单、食材或厨具不对，退出 Play，修改配置，再 Reload。

如果刚点 Play 时出现很多 warning，不一定是你的关卡错了。这个项目依赖反编译脚本，Unity 可能会显示一些原游戏脚本 warning。真正需要重点看的是红色 Error。

## 打包关卡

保存和构建前，先点击：

```text
Tools > Toggle Prepare For Building
```

它会清理临时加载出来的物体，避免把不该保存的运行时物体写进场景。

然后点击：

```text
Tools > Build Current Level AssetBundles
```

构建成功后会自动把本次关卡包同步到：

```text
Assets/AssetBundles/你的关卡包名
```

如果目标文件被游戏占用，工具会先关闭游戏、替换文件，再重新启动游戏。目标目录例如：

```text
Overcooked! 2/BepInEx/plugins/OC2DIYLevel/levels/你的关卡名
```

`Tools > Build AssetBundles` 会构建项目里所有带 AssetBundle 标记的包，通常只有维护者整理全项目资源时才需要。日常导出单个地图请用 `Tools > Build Current Level AssetBundles`，它会在构建前把本地 DLC 引用换成游戏内引用，跳过 `dlc_assets` 本地包，并检查当前场景是否混入了 `Chef_`、`player`、`VoiceChat` 这类游戏运行时对象；如果检测到污染场景，构建会停止，避免自定义地图包影响原版关卡。构建成功后会自动安装到游戏目录，并且只复制真正的 bundle 文件，不会把 `.manifest` 或 `.meta` 一起装进去。

## 常见问题

### 看不到物体，但地图上有

通常是资源没有加载或引用没配好。先点：

```text
Tools > Reload Pseudo Assets
```

再检查 `Assets/StreamingAssets/Windows` 是否存在，以及 `PseudoPrefab...Stub` 里的资源引用是否为空。

### 只有汉堡，DLC 菜单没出现

检查你的 `LevelInfoSO.recipes`。菜单必须加在 `recipes` 数组里。DLC 菜单在 `Assets/dlc/dlcXX/Recipes`。

不需要手动加 DLC `RecipeMatchList`，项目会自动合并已知官方 DLC 的匹配表和烹饪步骤。

### Inspector 一点别的物体就切走，没法拖拽

在 Inspector 右上角点小锁图标，锁住当前 Inspector。锁住后再去 Project 面板拖资源。

### 提示找不到 Windows bundle

运行：

```text
Tools > OC2 Setup > Check Environment
```

如果 `Assets/StreamingAssets/Windows` 不存在，运行：

```text
Tools > OC2 Setup > Auto Setup
```

### Auto Setup 没有生成 DLC 引用

确认 Python 3 可以运行。也可以手动在项目根目录执行：

```powershell
python tools/generate_dlc_assets.py --game-streaming-assets "你的游戏目录\Overcooked2_Data\StreamingAssets\Windows"
```

### Play Mode 后一堆报错

先看 Console 里最上面的红色 Error。大量 warning 可能来自反编译的游戏脚本，不一定影响关卡。常见处理顺序：

1. 确认 Assembly-CSharp 已放到正确目录。
2. 确认 `Assembly-CSharp-Patch` 已覆盖进去。
3. 确认 `Assets/StreamingAssets/Windows` 可访问。
4. 点击 `Tools > Reload Pseudo Assets`。
5. 重启 Unity 再试。

## DLC 菜单引用方式

菜单、厨具、食材这些 DLC 资源本身就在游戏的 `StreamingAssets/Windows` 里。默认不要把它们重新打进关卡包，而是使用轻量引用，例如 DLC09 会指向游戏自带的 `bundle404`。

如果只是想给关卡添加 DLC 顶部订单，推荐直接使用：

```text
Assets/dlc_custom_recipes/dlcXX/Recipes
```

这里的每个 `.asset` 都是项目生成好的 `CustomRecipeSO` 包装资产。它只保存菜谱名、分数、UID、bundle 名、游戏内资源路径和轻量引用，不包含 DLC 模型、贴图、音频或原始 AssetBundle。把这些资产拖到当前关卡 `LevelInfoSO.recipes` 数组里，构建后旧版 `OC2DIYLevel` 也能识别并显示顶部菜单。

1. 打开目标关卡场景。
2. 在 Project 面板打开 `Assets/dlc_custom_recipes/dlcXX/Recipes`。
3. 选中当前关卡的 `LevelInfoSO`，把需要的 DLC 菜谱拖进 `recipes`。
4. 使用 `Tools > Build Current Level AssetBundles` 构建当前关卡。

这个默认流程不会生成 `dlc_assets` 本地包，也不会在关卡包里新增 DLC 资源副本。只有当你真的改了游戏原始资源，才使用 `Tools > OC2 DLC > Import DLC09 Assets For Current Level (Legacy Local Bundle)` 这种旧流程。

## 不要提交这些内容

请不要把下面这些提交到公开仓库：

- `Assets/StreamingAssets/Windows`
- 原游戏 AssetBundle
- 原游戏模型、贴图、音频
- `Assets/Scripts/Assembly-CSharp`
- 你本机生成的 `Assets/dlc` 资源引用，除非项目维护者明确决定要提交这些轻量引用
- Unity 自动生成的 `Library`、`Temp`

## 给贡献者

贡献前请阅读：

```text
PROJECT_CONSTITUTION.md
```

本项目约定：

1. 在对应分支上修改。
2. 修改功能时同步更新 README 或文档。
3. commit message 包含中文和英文说明。
4. 修改完成后推送到对应远程分支。

当前主要开发分支：

```text
release
```

## 依赖自动处理 / Dependency note

如果关卡引用了 DLC 菜谱或其他游戏内 pseudo asset，构建步骤会自动把这些资源所在的 bundle 名加入 `LevelInfoSO.dependencies`。

If a level references DLC recipes or other game-bundled pseudo assets, the build step auto-adds each referenced bundle name to `LevelInfoSO.dependencies`.

这意味着作者可以继续使用 `Assets/dlc_custom_recipes`、`Assets/dlc/...` 和 `Assets/common...` 里的轻量引用，不需要手动记住每个资源在哪个 `bundleXXX` 里。

That means creators can keep using lightweight references from `Assets/dlc_custom_recipes`, `Assets/dlc/...`, and `Assets/common...` without manually remembering the source bundle for each resource.

## DLC CustomRecipe 转换 / DLC CustomRecipe conversion

`Tools > Build Current Level AssetBundles` 会在构建前运行 `tools/generate_dlc_custom_recipes.py`。脚本会扫描本机已安装游戏的 bundle，并在 `Assets/dlc_custom_recipes` 下生成轻量 `CustomRecipeSO` 包装资产。

`Tools > Build Current Level AssetBundles` runs `tools/generate_dlc_custom_recipes.py` before building. The script scans the locally installed game bundles and generates lightweight `CustomRecipeSO` wrappers under `Assets/dlc_custom_recipes`.

构建时，如果 `LevelInfoSO.recipes` 里仍然放着 DLC `PseudoPrefabSORecipe`，工具会自动替换成对应的生成版 `CustomRecipeSO`。这样构建出的关卡可以兼容原始 `OC2DIYLevel` 运行时，因为它认识 `CustomRecipeSO` 和 `optionalRecipeMatchListItems`，但不认识项目后来新增的 `dlcRecipeMatchListSOs` 辅助字段。

During the same build step, DLC `PseudoPrefabSORecipe` entries in `LevelInfoSO.recipes` are replaced with the generated `CustomRecipeSO` assets. This keeps built levels compatible with the original OC2DIYLevel runtime, which already understands `CustomRecipeSO` and `optionalRecipeMatchListItems`, but does not understand the newer `dlcRecipeMatchListSOs` helper field.

当前已生成所有带固定配方组成、可以作为顶部订单显示的 DLC 菜谱。少数 `optional...` 和 `permutation...` 资源是匹配辅助模板，不是完整顶部订单，因此不会直接出现在 `recipes` 里。

All DLC recipes with fixed compositions and real top-order UI are generated. A small number of `optional...` and `permutation...` assets are match-list helper templates rather than complete top-order recipes, so they are not added directly to `recipes`.

生成器会把 DLC 复合菜谱里的子菜谱转成生成版 `CustomRecipeSO` 引用，并把普通食材优先映射到游戏内 ingredient prefab。这样热巧克力、煎饼、套餐等嵌套菜单在 Play Mode 和构建包里都能拿到正确的订单节点与图标。

The generator maps nested DLC recipe components to generated `CustomRecipeSO` assets and maps normal ingredients to in-game ingredient prefabs where possible. This lets nested orders such as hot chocolate, pancakes, and combo meals resolve their order nodes and icons in Play Mode and built level packages.
