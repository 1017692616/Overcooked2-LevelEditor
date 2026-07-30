# Project Constitution / 项目宪法

## 1. Scope / 适用范围

These rules apply to every change made in this repository, including code, Unity assets, documentation, generated reference assets, and build scripts.

本规则适用于本仓库的所有修改，包括代码、Unity 资源、文档、生成的引用资源和构建脚本。

## 2. Branches / 分支

- Work must be completed on the corresponding task branch.
- Do not commit task changes directly to `main`.
- The branch must track the correct remote branch before delivery.

- 所有修改必须在对应任务分支上完成。
- 不得直接向 `main` 提交任务修改。
- 交付前，当前分支必须跟踪正确的远端分支。

## 3. Commit And Push / 提交与推送

- Every completed modification must be committed and pushed to its corresponding remote branch.
- A change is not considered complete until the push succeeds.
- Each commit must include a Chinese and an English explanation.
- Commit subjects should be concise; use the commit body for the bilingual details.

- 每次修改完成后都必须提交，并推送到对应的远端分支。
- 远端推送成功前，不得视为修改完成。
- 每个 commit 必须包含中文和英文说明。
- commit 标题应简洁，双语详细说明写入 commit body。

Recommended format:

推荐格式：

```text
Add DLC asset references / 添加 DLC 资源引用

中文：说明修改内容、影响范围和验证结果。
English: Describe the changes, impact, and verification results.
```

## 4. Documentation / 文档

- User-visible behavior changes must update the relevant README and reference documentation.
- If both Chinese and English documentation exist, update both versions in the same change.
- README instructions must match the actual branch, build, asset, and installation workflow.

- 用户可见行为发生变化时，必须同步更新对应 README 和参考文档。
- 如果存在中英文文档，必须在同一次修改中同步更新两种语言。
- README 中的分支、构建、资源和安装说明必须与实际流程一致。

## 5. Verification / 验证

Before committing:

提交前必须：

- Check `git status`.
- Run the narrowest relevant validation available.
- Confirm generated references point to existing local game assets when applicable.
- Confirm no proprietary game binaries, models, textures, or audio were added unintentionally.

- 检查 `git status`。
- 执行当前修改对应的最小必要验证。
- 如涉及资源引用，确认生成的引用指向本机存在的游戏资源。
- 确认没有误加入游戏专有二进制、模型、贴图或音频。

## 6. Delivery Checklist / 交付清单

```text
[ ] Change implemented / 修改已完成
[ ] Relevant tests or checks passed / 相关测试或检查通过
[ ] README updated / README 已更新
[ ] Chinese and English docs updated / 中英文文档已同步
[ ] Bilingual commit created / 已创建双语 commit
[ ] Commit pushed to the corresponding branch / 已推送到对应分支
[ ] Final branch and commit reported / 已报告最终分支和 commit
```
