"""Retire Jianghai's obsolete photographed tenement overlay planes."""

from __future__ import annotations

import bpy


RETIRED_FACADE_OVERLAY_VERSION = 1
RETIRED_FACADE_OVERLAY_COUNT = 35
RETIRED_FACADE_OVERLAY_PREFIXES = (
    "JianghaiExpansion_Facade_EastPhoto_",
    "JianghaiExpansion_Facade_WestClock_",
)
RETIRED_FACADE_OVERLAY_PARENT_NAME = "JianghaiExpansion_UrbanFacades"
RETIRED_FACADE_OVERLAY_MARKER = "jianghai_facade_overlay_retirement_version"
RETIRED_FACADE_OVERLAY_PREVIOUSLY_REMOVED_NAME = (
    "JianghaiExpansion_Facade_EastPhoto_F0_C1_Insert"
)
RETIRED_FACADE_OVERLAY_NAMES = tuple(
    sorted(
        name
        for prefix in RETIRED_FACADE_OVERLAY_PREFIXES
        for floor in range(3)
        for column in range(3)
        for role in ("Insert", "Wall")
        for name in (f"{prefix}F{floor}_C{column}_{role}",)
        if name != RETIRED_FACADE_OVERLAY_PREVIOUSLY_REMOVED_NAME
    )
)


def _expected_name_table_ready() -> bool:
    return (
        len(RETIRED_FACADE_OVERLAY_NAMES) == RETIRED_FACADE_OVERLAY_COUNT
        and sum("_EastPhoto_" in name for name in RETIRED_FACADE_OVERLAY_NAMES) == 17
        and sum("_WestClock_" in name for name in RETIRED_FACADE_OVERLAY_NAMES) == 18
        and RETIRED_FACADE_OVERLAY_PREVIOUSLY_REMOVED_NAME
        not in RETIRED_FACADE_OVERLAY_NAMES
    )


def remove_retired_facade_overlays() -> int:
    """Remove exactly EastPhoto 17 + WestClock 18, never adjacent authored art."""

    if not _expected_name_table_ready():
        raise RuntimeError("Jianghai retired-facade exact-name table is invalid")

    parent = bpy.data.objects.get(RETIRED_FACADE_OVERLAY_PARENT_NAME)
    if parent is None:
        raise RuntimeError(
            f"Facade expansion root is missing: {RETIRED_FACADE_OVERLAY_PARENT_NAME}"
        )
    overlays = sorted(
        (
            obj
            for obj in bpy.data.objects
            if obj.name.startswith(RETIRED_FACADE_OVERLAY_PREFIXES)
        ),
        key=lambda obj: obj.name,
    )
    if overlays:
        actual_names = tuple(obj.name for obj in overlays)
        if actual_names != RETIRED_FACADE_OVERLAY_NAMES:
            missing = sorted(set(RETIRED_FACADE_OVERLAY_NAMES) - set(actual_names))
            unexpected = sorted(set(actual_names) - set(RETIRED_FACADE_OVERLAY_NAMES))
            raise RuntimeError(
                "Jianghai photographed facade exact-name contract drifted: "
                f"missing={missing} unexpected={unexpected}"
            )
        invalid_types = [obj.name for obj in overlays if obj.type != "MESH"]
        invalid_parents = [
            obj.name
            for obj in overlays
            if obj.parent is not parent
        ]
        if invalid_types or invalid_parents:
            raise RuntimeError(
                "Jianghai photographed facade object contract drifted: "
                f"non_mesh={invalid_types} wrong_parent={invalid_parents}"
            )
        east_count = sum("_EastPhoto_" in obj.name for obj in overlays)
        west_count = sum("_WestClock_" in obj.name for obj in overlays)
        if (
            len(overlays) != RETIRED_FACADE_OVERLAY_COUNT
            or east_count != 17
            or west_count != 18
        ):
            raise RuntimeError(
                "Jianghai photographed facade retirement set drifted: "
                f"total={len(overlays)} east={east_count} west={west_count}"
            )
        for obj in overlays:
            bpy.data.objects.remove(obj, do_unlink=True)
        removed = len(overlays)
    else:
        if (
            parent.get(RETIRED_FACADE_OVERLAY_MARKER)
            != RETIRED_FACADE_OVERLAY_VERSION
            or parent.get("jianghai_retired_facade_overlay_count")
            != RETIRED_FACADE_OVERLAY_COUNT
        ):
            raise RuntimeError(
                "Jianghai facade overlays disappeared without retirement provenance"
            )
        removed = 0

    for stale_key in (
        "jianghai_enterable_replaced_insert_version",
        "jianghai_enterable_replaced_insert_name",
        "jianghai_enterable_replacement_role",
    ):
        if stale_key in parent:
            del parent[stale_key]
    parent[RETIRED_FACADE_OVERLAY_MARKER] = RETIRED_FACADE_OVERLAY_VERSION
    parent["jianghai_retired_facade_overlay_count"] = RETIRED_FACADE_OVERLAY_COUNT
    parent["jianghai_retired_facade_overlay_prefixes"] = ";".join(
        RETIRED_FACADE_OVERLAY_PREFIXES
    )
    parent["jianghai_retired_facade_overlay_reason"] = (
        "Retired 17 EastPhoto and 18 WestClock photographed facade planes; "
        "real authored building shells and EastPhotoHouse aperture remain"
    )
    bpy.context.view_layer.update()
    return removed


def validate_retired_facade_overlays() -> int:
    parent = bpy.data.objects.get(RETIRED_FACADE_OVERLAY_PARENT_NAME)
    residuals = sorted(
        obj.name
        for obj in bpy.data.objects
        if obj.name.startswith(RETIRED_FACADE_OVERLAY_PREFIXES)
    )
    expected_prefix_record = ";".join(RETIRED_FACADE_OVERLAY_PREFIXES)
    valid = (
        _expected_name_table_ready()
        and parent is not None
        and not residuals
        and parent.get(RETIRED_FACADE_OVERLAY_MARKER)
        == RETIRED_FACADE_OVERLAY_VERSION
        and parent.get("jianghai_retired_facade_overlay_count")
        == RETIRED_FACADE_OVERLAY_COUNT
        and parent.get("jianghai_retired_facade_overlay_prefixes")
        == expected_prefix_record
    )
    if not valid:
        raise RuntimeError(
            "Retired Jianghai facade overlay contract is invalid: "
            f"parent={parent is not None} residuals={residuals}"
        )
    return len(residuals)
