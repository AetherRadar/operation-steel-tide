"""Validate evaluated Blender skin before exporting the operator asset.

The short-edge tear criterion matches OperatorPresentationDiagnostics. Bone
lengths and normalized weights alone do not establish a correctly bound mesh.
"""
from __future__ import annotations

import bpy
import numpy as np


POSES = (
    'idle', 'walk', 'run', 'sprint', 'crouch_idle', 'crouch_walk',
    'ready_idle', 'ready_walk', 'ready_run', 'ready_sprint',
    'ready_crouch_idle', 'ready_crouch_walk', 'aim_idle', 'aim_walk',
    'aim_run', 'aim_sprint', 'aim_crouch_idle', 'aim_crouch_walk',
    'prone_idle', 'prone_crawl', 'hit', 'death', 'downed', 'revive_kneel', 'revived',
    'shoot', 'reload', 'melee', 'throw', 'interact', 'pickup', 'heal',
    'jump_start', 'jump_loop', 'jump_land', 'slide_start', 'slide_loop', 'slide_exit',
    'pistol_ready_idle', 'pistol_ready_walk', 'pistol_ready_run', 'pistol_ready_sprint',
    'pistol_ready_crouch_idle', 'pistol_ready_crouch_walk',
    'pistol_aim_idle', 'pistol_aim_walk', 'pistol_aim_run', 'pistol_aim_sprint',
    'pistol_aim_crouch_idle', 'pistol_aim_crouch_walk', 'pistol_shoot',
    'pistol_prone_idle', 'pistol_prone_crawl', 'pistol_reload',
    'rifle_prone_idle', 'rifle_prone_crawl', 'preview_stand',
)
PHASES = (0.0, 0.25, 0.5, 0.75, 0.99)


def world_vertices(mesh):
    points = np.empty(len(mesh.data.vertices) * 3, dtype=np.float64)
    mesh.data.vertices.foreach_get('co', points)
    matrix = np.array(mesh.matrix_world)
    return points.reshape((-1, 3)) @ matrix[:3, :3].T + matrix[:3, 3]


def validate_operator_geometry(rig, meshes, sample_action):
    surfaces = []
    for mesh in meshes:
        points = world_vertices(mesh)
        mesh.data.calc_loop_triangles()
        edges = np.asarray(sorted({tuple(sorted((triangle.vertices[index], triangle.vertices[(index + 1) % 3])))
                                   for triangle in mesh.data.loop_triangles for index in range(3)}))
        lengths = np.linalg.norm(points[edges[:, 0]] - points[edges[:, 1]], axis=1)
        # HY-3D carries many authored UV/fabric fold edges below 2 cm. They
        # are not exposed surface spans; validate the larger connected spans
        # where a skin tear is visually meaningful.
        short = (lengths >= 0.020) & (lengths < 0.035)
        surfaces.append((mesh, edges[short], lengths[short]))
    if not surfaces:
        raise RuntimeError(f'{rig.name}: no authored skinned geometry')
    failures = []
    count = 0
    for name in POSES:
        action = bpy.data.actions.get(name)
        if action is None:
            raise RuntimeError(f'{rig.name}: missing required geometry sample action {name}')
        start, end = action.frame_range
        for phase in PHASES:
            sample_action(rig, action, start + (end - start) * phase)
            depsgraph = bpy.context.evaluated_depsgraph_get()
            torn = 0
            maximum = 0.0
            floor = float('inf')
            ceiling = float('-inf')
            for mesh, edges, rest in surfaces:
                points = world_vertices(mesh.evaluated_get(depsgraph))
                if not np.isfinite(points).all():
                    raise RuntimeError(f'{mesh.name}:{name}: nonfinite evaluated skin')
                floor = min(floor, float(points[:, 2].min()))
                ceiling = max(ceiling, float(points[:, 2].max()))
                lengths = np.linalg.norm(points[edges[:, 0]] - points[edges[:, 1]], axis=1)
                excess = lengths - rest
                mask = (excess > 0.035) & (lengths > rest * 4)
                torn += int(mask.sum())
                maximum = max(maximum, float(np.max(excess)))
                if mask.any():
                    worst = int(np.argmax(np.where(mask, excess, -1)))
                    failures.append(f'{mesh.name}:{name}:{phase:.2f}:edge='
                                    f'{edges[worst].tolist()}:excess={excess[worst]:.4f}')
            height = ceiling - floor
            settled = 'prone' in name or name == 'downed' or name == 'death' and phase >= 0.99
            if settled and (height > 0.70 or floor < -0.06 or floor > 0.13):
                failures.append(f'{rig.name}:{name}:{phase:.2f}:height={height:.4f}:floor={floor:.4f}')
            count += 1
            print(f'OPERATOR_GEOMETRY_CHECK action={name} phase={phase:.2f} '
                  f'torn={torn} max_excess={maximum:.4f} height={height:.4f} floor={floor:.4f}', flush=True)
    print(f'OPERATOR_GEOMETRY_PASS samples={count} valid={str(not failures).lower()}', flush=True)
    if failures:
        raise RuntimeError('Authored operator geometry failed: ' + '; '.join(failures))
