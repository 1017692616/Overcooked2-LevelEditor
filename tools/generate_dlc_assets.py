#!/usr/bin/env python3
"""Generate lightweight Unity reference assets for installed Overcooked! 2 DLC data.

The generated files contain only bundle names and asset paths. They never copy or
serialize proprietary game assets into this repository.
"""

from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path

import UnityPy


DLC_BUNDLES = {
    "dlc02": ("bundle162", "bundle167"),
    "dlc03": ("bundle208", "bundle210"),
    "dlc04": ("bundle225", "bundle226", "bundle417"),
    "dlc05": ("bundle247", "bundle250"),
    "dlc07": ("bundle293", "bundle297"),
    "dlc08": ("bundle351", "bundle354", "bundle359"),
    "dlc09": ("bundle404", "bundle405"),
    "dlc10": ("bundle419", "bundle421"),
    "dlc11": ("bundle427", "bundle428"),
    "dlc13": ("bundle448", "bundle449"),
}

SCRIPT_GUID = {
    "reference": "0cff7c13895ab9e47a5e02d4619cc3b9",
    "recipe": "753d9e70603f6a140b05f30f176ec2dd",
}


def unity_guid(key: str) -> str:
    return hashlib.md5(("overcooked2-level-editor:" + key).encode("utf-8")).hexdigest()


def meta(guid: str) -> str:
    return f"fileFormatVersion: 2\nguid: {guid}\n"


def yaml_asset(script_guid: str, name: str, bundle: str, asset_path: str, score: int | None = None) -> str:
    extra = f"\n  score: {score}" if score is not None else ""
    return (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
        f"  m_Name: {name}\n"
        "  m_EditorClassIdentifier: \n"
        f"  prefabName: {name}\n"
        f"  bundleName: {bundle}\n"
        f"  assetPath: {asset_path}{extra}\n"
    )


def safe_name(path: str) -> str:
    name = Path(path).stem
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", name)


def classify(path: str) -> tuple[str, bool] | None:
    p = path.lower()
    if "/data/orderdefinitions/recipeitems/" in p:
        return "Recipes", True
    if "/data/orderdefinitions/ingredients/" in p or "/data/orderdefinitions/mixedingredients/" in p or "/data/orderdefinitions/cookedingredients/" in p:
        return "Ingredients", False
    if "/data/orderdefinitions/cookingstepdata/" in p or "/data/recipes/cookingstepdata/" in p:
        return "CookingSteps", False
    if "/data/recipes/" in p and "recipematchlist" in p:
        return "RecipeMatchLists", False
    if "/prefabs/recipes/" in p:
        return "Products", False
    if "/prefabs/ingredients/" in p:
        return "Ingredients", False
    if (
        "/prefabs/sharedkitchen/" in p
        or "/prefabs/shared kitchen/" in p
        or "/prefabs/mechanics/" in p
    ):
        return "Kitchen", False
    return None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--game-streaming-assets",
        default=r"F:\SteamLibrary\steamapps\common\Overcooked! 2\Overcooked2_Data\StreamingAssets\Windows",
    )
    parser.add_argument("--output", default="Assets/dlc")
    parser.add_argument("--default-score", type=int, default=60)
    args = parser.parse_args()

    game_root = Path(args.game_streaming_assets)
    output_root = Path(args.output)
    if not game_root.is_dir():
        raise SystemExit(f"StreamingAssets directory not found: {game_root}")

    generated = 0
    counts: dict[str, int] = {}
    for dlc, bundles in DLC_BUNDLES.items():
        entries: dict[tuple[str, str, str], str] = {}
        used_targets: set[Path] = set()
        for bundle in bundles:
            bundle_path = game_root / bundle
            if not bundle_path.is_file():
                print(f"warning: missing {bundle_path}")
                continue
            env = UnityPy.load(str(bundle_path))
            for asset_path, obj in env.container.items():
                category = classify(asset_path)
                if category is None:
                    continue
                folder, is_recipe = category
                key = (folder, bundle, asset_path)
                entries[key] = asset_path

        for (folder, bundle, asset_path), _ in sorted(entries.items()):
            name = safe_name(asset_path)
            target_dir = output_root / dlc / folder
            target_dir.mkdir(parents=True, exist_ok=True)
            target = target_dir / f"{name}.asset"
            if target in used_targets:
                name = f"{name}_{unity_guid(bundle + asset_path)[:8]}"
                target = target_dir / f"{name}.asset"
            used_targets.add(target)
            script_guid = SCRIPT_GUID["recipe"] if folder == "Recipes" else SCRIPT_GUID["reference"]
            score = args.default_score if folder == "Recipes" else None
            target.write_text(
                yaml_asset(script_guid, name, bundle, asset_path, score),
                encoding="utf-8",
            )
            target.with_name(target.name + ".meta").write_text(
                meta(unity_guid(dlc + "/" + folder + "/" + name)),
                encoding="utf-8",
            )
            generated += 1
            counts[f"{dlc}/{folder}"] = counts.get(f"{dlc}/{folder}", 0) + 1

    print(f"generated {generated} reference assets")
    for key in sorted(counts):
        print(f"{key}: {counts[key]}")


if __name__ == "__main__":
    main()
