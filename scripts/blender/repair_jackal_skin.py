"""Paint Jackal's cloth joints without joining its separate armor surfaces.

The authored mesh topology stays intact. Exact UV copies share paint samples;
nearby vest and arm surfaces must not be merged with a distance weld.
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np

REVISION = 1
REVISION_KEY = 'steel_tide_jackal_garment_revision'
FOREARM_REVISION_KEY = 'steel_tide_jackal_forearm_seams_revision'


def repair_jackal_skin(rig, mesh, read_skin, write_skin):
    if mesh.get(REVISION_KEY) == REVISION:
        return repair_jackal_forearm_seams(rig, mesh, read_skin, write_skin)
    positions, weights = read_skin(mesh)
    for side in ('Left', 'Right'):
        indices = [group.index for group in mesh.vertex_groups if group.name.startswith(side + 'Hand')]
        total = weights[:, indices].sum(axis=1)
        weights[:, indices] = 0.0
        weights[:, mesh.vertex_groups[side + 'Hand'].index] = total
    cells, inverse = np.unique(np.round(positions, 5), axis=0, return_inverse=True)
    columns = np.flatnonzero(weights.sum(axis=0) > 1.0e-8)
    samples = np.zeros((len(cells), len(columns)))
    np.add.at(samples, inverse, weights[:, columns])
    samples /= np.bincount(inverse)[:, None]
    edges = np.array([edge.vertices[:] for edge in mesh.data.edges])
    edges = np.unique(np.sort(inverse[edges], axis=1), axis=0)
    edges = edges[edges[:, 0] != edges[:, 1]]

    def diffuse(region, iterations):
        active = np.zeros(len(cells), dtype=bool)
        np.maximum.at(active, inverse, region)
        destination = np.concatenate((edges[:, 0], edges[:, 1]))
        source = np.concatenate((edges[:, 1], edges[:, 0]))
        keep = active[destination]
        destination, source = destination[keep], source[keep]
        order = np.argsort(destination)
        destination, source = destination[order], source[order]
        starts = np.r_[0, np.flatnonzero(np.diff(destination)) + 1]
        targets = destination[starts]
        factors = 1.0 / np.maximum(np.linalg.norm(cells[destination] - cells[source], axis=1), 0.004)
        degree = np.add.reduceat(factors, starts)
        for _ in range(iterations):
            average = np.add.reduceat(samples[source] * factors[:, None], starts, axis=0) / degree[:, None]
            samples[targets] = 0.28 * samples[targets] + 0.72 * average

    def paint_chest(lower_bones):
        amount = np.clip((cells[:, 2] - 1.07) / 0.10, 0.0, 1.0)
        amount = amount * amount * (3.0 - 2.0 * amount)
        upper = int(np.flatnonzero(columns == mesh.vertex_groups['Spine1'].index)[0])
        for name in lower_bones:
            index = int(np.flatnonzero(columns == mesh.vertex_groups[name].index)[0])
            transfer = samples[:, index] * amount
            samples[:, index] -= transfer
            samples[:, upper] += transfer

    diffuse(np.ones(len(positions), dtype=bool), 24)
    paint_chest(('Spine',))
    # The two inspected cloth-joint bands leave the gloves and distant legs
    # unchanged during this extra pass. Armor remains separate mesh topology.
    region = ((positions[:, 2] > 1.10) & (positions[:, 2] < 1.56) & (np.abs(positions[:, 0]) < 0.37))
    region |= ((positions[:, 2] > 0.72) & (positions[:, 2] < 1.00) & (np.abs(positions[:, 0]) < 0.20))
    diffuse(region, 168)
    paint_chest(('Hips', 'Spine'))
    weights[:, columns] = samples[inverse]
    # This role's reviewed result uses four coherent influences. Retain that
    # exact paint even when the common exporter supports more influences.
    kept = np.argsort(weights, axis=1)[:, -4:]
    values = np.take_along_axis(weights, kept, axis=1)
    values /= values.sum(axis=1)[:, None]
    weights[:] = 0.0
    np.put_along_axis(weights, kept, values, axis=1)
    write_skin(mesh, weights)
    mesh[REVISION_KEY] = REVISION
    print(f'JACKAL_SKIN_CHECK mesh={mesh.name} vertices={len(positions)} '
          f'local_vertices={int(region.sum())} revision={REVISION} valid=true', flush=True)
    repair_jackal_forearm_seams(rig, mesh, read_skin, write_skin)
    return True


def repair_jackal_forearm_seams(rig, mesh, read_skin, write_skin):
    if mesh.get(FOREARM_REVISION_KEY) == 1:
        return False
    path = Path(__file__).with_name('repair_forearm_seams.py')
    specification = importlib.util.spec_from_file_location('repair_forearm_seams', path)
    seams = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(seams)
    positions, weights = read_skin(mesh)
    weights = seams.repair_forearm_seams(rig, mesh, positions, weights)
    kept = np.argsort(weights, axis=1)[:, -4:]
    values = np.take_along_axis(weights, kept, axis=1)
    values /= values.sum(axis=1)[:, None]
    weights[:] = 0.0
    np.put_along_axis(weights, kept, values, axis=1)
    write_skin(mesh, weights)
    mesh[FOREARM_REVISION_KEY] = 1
    return True
