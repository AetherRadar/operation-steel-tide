"""Keep disconnected forearm UV surfaces together through shared skin paint.

The inspected source has submillimetre seams which are separate topology.
Consensus weights close their animated gaps without welding armor to cloth or
changing the original shape and UVs.
"""
from __future__ import annotations

import numpy as np
from mathutils.kdtree import KDTree


def repair_forearm_seams(rig, mesh, positions, weights):
    parents = np.arange(len(positions))

    def root(index):
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    def forearm_region(points, side):
        bone = rig.data.bones[side + 'ForeArm']
        start = np.asarray(rig.matrix_world @ bone.head_local)
        axis = np.asarray(rig.matrix_world @ bone.tail_local) - start
        fraction = (points - start) @ axis / (axis @ axis)
        distance = np.linalg.norm(points - (start + fraction[:, None] * axis), axis=1)
        return (fraction > -0.20) & (fraction < 1.18) & (distance < 0.09)

    for side in ('Left', 'Right'):
        indices = np.flatnonzero(forearm_region(positions, side))
        tree = KDTree(len(indices))
        for index in indices:
            tree.insert(positions[index], int(index))
        tree.balance()
        for index in indices:
            for _, other, _ in tree.find_range(positions[index], 0.001):
                if other > index:
                    first, second = root(index), root(other)
                    if first != second:
                        parents[second] = first
    _, inverse = np.unique([root(index) for index in range(len(positions))], return_inverse=True)
    count = np.bincount(inverse)
    points = np.zeros((len(count), 3))
    paint = np.zeros((len(count), weights.shape[1]))
    np.add.at(points, inverse, positions)
    np.add.at(paint, inverse, weights)
    points /= count[:, None]
    paint /= count[:, None]
    edges = np.array([edge.vertices[:] for edge in mesh.data.edges])
    edges = np.unique(np.sort(inverse[edges], axis=1), axis=0)
    edges = edges[edges[:, 0] != edges[:, 1]]
    active = count > 1
    for side in ('Left', 'Right'):
        active |= forearm_region(points, side)
    destination = np.concatenate((edges[:, 0], edges[:, 1]))
    source = np.concatenate((edges[:, 1], edges[:, 0]))
    keep = active[destination]
    destination, source = destination[keep], source[keep]
    order = np.argsort(destination)
    destination, source = destination[order], source[order]
    starts = np.r_[0, np.flatnonzero(np.diff(destination)) + 1]
    targets = destination[starts]
    factors = 1.0 / np.maximum(np.linalg.norm(points[destination] - points[source], axis=1), 0.004)
    degree = np.add.reduceat(factors, starts)
    for _ in range(24):
        average = np.add.reduceat(paint[source] * factors[:, None], starts, axis=0) / degree[:, None]
        paint[targets] = 0.28 * paint[targets] + 0.72 * average
    print(f'FOREARM_SEAM_CHECK mesh={mesh.name} shared_groups={int((count > 1).sum())} '
          f'paint_vertices={int(active.sum())} valid=true', flush=True)
    return paint[inverse]
