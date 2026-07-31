#!/usr/bin/env python3
"""Generate CustomRecipeSO wrappers for installed Overcooked! 2 DLC recipes.

The generated assets contain only lightweight bundle/path references. They do
not copy game models, textures, audio, prefabs, or AssetBundle contents.
"""

from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path

import UnityPy


SCRIPT_GUID = {
    "custom_recipe": "83fb008bcc8e793429b02c178c430815",
    "reference": "0cff7c13895ab9e47a5e02d4619cc3b9",
}

PSEUDO_RE = re.compile(
    r"bundleName:\s*(?P<bundle>[^\r\n]+).*?assetPath:\s*(?P<path>[^\r\n]+)",
    re.S,
)
GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")


def unity_guid(key: str) -> str:
    return hashlib.md5(("overcooked2-dlc-custom-recipe:" + key).encode("utf-8")).hexdigest()


def normalize_path(path: str) -> str:
    return path.strip().strip('"').replace("\\", "/").lower()


def safe_name(path: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", Path(path).stem)


def meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def pseudo_meta(guid: str) -> str:
    return f"fileFormatVersion: 2\nguid: {guid}\n"


def unity_ref(guid: str | None) -> str:
    if not guid:
        return "{fileID: 0}"
    return "{fileID: 11400000, guid: " + guid + ", type: 2}"


def yaml_list_refs(refs: list[str]) -> str:
    if not refs:
        return " []"
    return "\n" + "\n".join("  - " + unity_ref(guid) for guid in refs)


def read_existing_pseudo_refs(project_assets: Path) -> dict[tuple[str, str], tuple[str, Path]]:
    refs: dict[tuple[str, str], tuple[str, Path]] = {}
    for asset_path in project_assets.rglob("*.asset"):
        try:
            text = asset_path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        match = PSEUDO_RE.search(text)
        if not match:
            continue
        meta_path = asset_path.with_name(asset_path.name + ".meta")
        if not meta_path.exists():
            continue
        meta_match = GUID_RE.search(meta_path.read_text(encoding="utf-8", errors="ignore"))
        if not meta_match:
            continue
        key = (match.group("bundle").strip(), normalize_path(match.group("path")))
        refs.setdefault(key, (meta_match.group(1), asset_path))
    return refs


def classify_reference_folder(path: str) -> str:
    p = normalize_path(path)
    if "/data/orderdefinitions/ingredients/" in p or "/data/orderdefinitions/mixedingredients/" in p:
        return "Ingredients"
    if "/data/orderdefinitions/cookedingredients/" in p:
        return "Ingredients"
    if "/prefabs/ingredients/" in p:
        return "Ingredients"
    if "/data/recipes/cookingstepdata/" in p or "/data/orderdefinitions/cookingstepdata/" in p:
        return "CookingSteps"
    if "/data/recipes/platingstepdata/" in p:
        return "PlatingSteps"
    if "/prefabs/recipes/" in p:
        return "Products"
    if "/gui/icons/" in p:
        return "Icons"
    return "References"


def pseudo_ref_yaml(name: str, bundle: str, asset_path: str) -> str:
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
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID['reference']}, type: 3}}\n"
        f"  m_Name: {name}\n"
        "  m_EditorClassIdentifier: \n"
        f"  prefabName: {name}\n"
        f"  bundleName: {bundle}\n"
        f"  assetPath: {asset_path}\n"
    )


def ensure_pseudo_ref(
    refs: dict[tuple[str, str], tuple[str, Path]],
    output_root: Path,
    bundle: str,
    asset_path: str,
) -> str | None:
    if not asset_path:
        return None
    key = (bundle, normalize_path(asset_path))
    existing = refs.get(key)
    if existing:
        return existing[0]

    folder = classify_reference_folder(asset_path)
    name = safe_name(asset_path)
    target = output_root / "_refs" / folder / f"{name}.asset"
    guid = unity_guid("ref:" + bundle + ":" + normalize_path(asset_path))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(pseudo_ref_yaml(name, bundle, asset_path), encoding="utf-8")
    target.with_name(target.name + ".meta").write_text(pseudo_meta(guid), encoding="utf-8")
    refs[key] = (guid, target)
    return guid


def scan_bundle_containers(game_root: Path) -> tuple[dict[int, tuple[str, str]], dict[str, object]]:
    path_id_index: dict[int, tuple[str, str]] = {}
    recipe_objects: dict[str, object] = {}
    for bundle_path in sorted(game_root.glob("bundle*")):
        if bundle_path.suffix == ".meta":
            continue
        try:
            env = UnityPy.load(str(bundle_path))
        except Exception as error:
            print(f"warning: unable to scan {bundle_path.name}: {error}")
            continue
        for asset_path, pptr in env.container.items():
            path_id = getattr(pptr, "m_PathID", None)
            if path_id is not None:
                path_id_index.setdefault(path_id, (bundle_path.name, asset_path))
            if "/downloadablecontent/" in normalize_path(asset_path) and "/data/orderdefinitions/recipeitems/" in normalize_path(asset_path):
                recipe_objects[asset_path] = pptr
    return path_id_index, recipe_objects


def resolve_pptr(pptr: object, path_id_index: dict[int, tuple[str, str]]) -> tuple[str, str] | None:
    if pptr is None:
        return None
    path_id = getattr(pptr, "m_PathID", None)
    if not path_id:
        return None
    return path_id_index.get(path_id)


def first_picture_path(tile: dict, path_id_index: dict[int, tuple[str, str]]) -> tuple[str, str] | None:
    pictures = tile.get("m_tileDefinition", {}).get("m_mainPictures", [])
    if not pictures:
        return None
    path_id = pictures[0].get("m_PathID")
    return path_id_index.get(path_id) if path_id else None


def first_modifier_path(tile: dict, path_id_index: dict[int, tuple[str, str]]) -> tuple[str, str] | None:
    pictures = tile.get("m_tileDefinition", {}).get("m_modifierPictures", [])
    if not pictures:
        return None
    path_id = pictures[0].get("m_PathID")
    return path_id_index.get(path_id) if path_id else None


def score_index(project_assets: Path) -> dict[tuple[str, str], int]:
    result: dict[tuple[str, str], int] = {}
    score_re = re.compile(r"score:\s*(-?\d+)")
    for asset_path in (project_assets / "dlc").rglob("*.asset") if (project_assets / "dlc").exists() else []:
        try:
            text = asset_path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        ref = PSEUDO_RE.search(text)
        score = score_re.search(text)
        if ref and score:
            result[(ref.group("bundle").strip(), normalize_path(ref.group("path")))] = int(score.group(1))
    return result


def custom_recipe_yaml(
    name: str,
    composition_guids: list[str],
    cooking_step_guid: str | None,
    cooking_step_icon_guid: str | None,
    plating_step_guid: str | None,
    model_guid: str | None,
    icon_guid: str | None,
    uid: int,
    score: int,
) -> str:
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
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID['custom_recipe']}, type: 3}}\n"
        f"  m_Name: {name}\n"
        "  m_EditorClassIdentifier: \n"
        "  compositionSOs:" + yaml_list_refs(composition_guids) + "\n"
        f"  cookingStepSO: {unity_ref(cooking_step_guid)}\n"
        f"  cookingStepIconSO: {unity_ref(cooking_step_icon_guid)}\n"
        "  cookingStepIcon: {fileID: 0}\n"
        f"  platingStepSO: {unity_ref(plating_step_guid)}\n"
        f"  modelSO: {unity_ref(model_guid)}\n"
        "  model: {fileID: 0}\n"
        f"  iconSO: {unity_ref(icon_guid)}\n"
        "  icon: {fileID: 0}\n"
        f"  recipeName: {name}\n"
        f"  uID: {uid}\n"
        f"  score: {score}\n"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--game-streaming-assets", required=True)
    parser.add_argument("--project-assets", default="Assets")
    parser.add_argument("--output", default="Assets/dlc_custom_recipes")
    parser.add_argument("--default-score", type=int, default=60)
    args = parser.parse_args()

    game_root = Path(args.game_streaming_assets)
    project_assets = Path(args.project_assets)
    output_root = Path(args.output)
    if not game_root.is_dir():
        raise SystemExit(f"StreamingAssets directory not found: {game_root}")

    refs = read_existing_pseudo_refs(project_assets)
    scores = score_index(project_assets)
    path_id_index, recipe_objects = scan_bundle_containers(game_root)

    generated = 0
    skipped = 0
    for recipe_path, pptr in sorted(recipe_objects.items()):
        recipe_key = (Path(pptr.assetsfile.name).name, normalize_path(recipe_path))
        try:
            data = pptr.read()
            tree = pptr.read_typetree()
        except Exception as error:
            print(f"warning: unable to read recipe {recipe_path}: {error}")
            skipped += 1
            continue

        dlc_match = re.search(r"/downloadablecontent/(dlc\d+)(?:/|$)", normalize_path(recipe_path))
        if not dlc_match:
            skipped += 1
            continue
        dlc = dlc_match.group(1)
        bundle_name = Path(pptr.assetsfile.name).name
        name = safe_name(recipe_path)
        composition_guids: list[str] = []
        for child in getattr(data, "m_composition", []) or []:
            resolved = resolve_pptr(child, path_id_index)
            if not resolved:
                continue
            guid = ensure_pseudo_ref(refs, output_root, resolved[0], resolved[1])
            if guid:
                composition_guids.append(guid)

        cooking_step = resolve_pptr(getattr(data, "m_cookingStep", None), path_id_index)
        plating_step = resolve_pptr(getattr(data, "m_platingStep", None), path_id_index)
        model = resolve_pptr(getattr(data, "m_platingPrefab", None), path_id_index)
        gui = tree.get("m_orderGuiDescription", []) if isinstance(tree, dict) else []
        icon = first_picture_path(gui[0], path_id_index) if len(gui) > 0 else None
        cooking_icon = first_modifier_path(gui[1], path_id_index) if len(gui) > 1 else None

        cooking_step_guid = ensure_pseudo_ref(refs, output_root, cooking_step[0], cooking_step[1]) if cooking_step else None
        plating_step_guid = ensure_pseudo_ref(refs, output_root, plating_step[0], plating_step[1]) if plating_step else None
        model_guid = ensure_pseudo_ref(refs, output_root, model[0], model[1]) if model else None
        icon_guid = ensure_pseudo_ref(refs, output_root, icon[0], icon[1]) if icon else None
        cooking_icon_guid = ensure_pseudo_ref(refs, output_root, cooking_icon[0], cooking_icon[1]) if cooking_icon else None

        if not composition_guids:
            print(f"warning: skipped {recipe_path}: no composition refs resolved")
            skipped += 1
            continue

        target = output_root / dlc / "Recipes" / f"{name}.asset"
        target.parent.mkdir(parents=True, exist_ok=True)
        score = scores.get((bundle_name, normalize_path(recipe_path)), args.default_score)
        uid = int(getattr(data, "m_uID", 0) or 0)
        target.write_text(
            custom_recipe_yaml(
                name,
                composition_guids,
                cooking_step_guid,
                cooking_icon_guid,
                plating_step_guid,
                model_guid,
                icon_guid,
                uid,
                score,
            ),
            encoding="utf-8",
        )
        target.with_name(target.name + ".meta").write_text(
            meta(unity_guid("recipe:" + normalize_path(recipe_path))),
            encoding="utf-8",
        )
        generated += 1

    print(f"generated {generated} DLC CustomRecipeSO assets under {output_root}")
    if skipped:
        print(f"skipped {skipped} recipes")


if __name__ == "__main__":
    main()
