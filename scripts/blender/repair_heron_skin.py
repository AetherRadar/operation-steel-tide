"""Paint smooth garment and wrist transitions on Heron's existing surface.

Run after the shared finger consolidation and initial garment pass. The
source's vest/arm and hip/leg transitions span only one centimetre despite
large joint travel. Extend those transitions along the existing topology,
preserving the actual glove surface as a fixed boundary.
"""

import numpy as np

from repair_operator_skin import _read_skin, _smooth_garments, _write_skin


def refine_heron_skin(rig, meshes):
    """Refine Heron's authored weights without moving or welding geometry."""
    count = 0
    for mesh in meshes:
        if mesh.name != "OperatorBody":
            continue
        positions, weights = _read_skin(mesh)
        authoring_positions = np.asarray([
            list(rig.matrix_world.inverted() @ mesh.matrix_world @ vertex.co)
            for vertex in mesh.data.vertices])
        hand_groups = [group.index for group in mesh.vertex_groups if "Hand" in group.name]
        glove = weights[:, hand_groups].sum(axis=1) > .5
        garment = (authoring_positions[:, 2] > .80) & (authoring_positions[:, 2] < 1.70)
        weights = _smooth_garments(mesh, positions, weights, glove, garment, iterations=168)

        # Keep the distal palm/fingers fixed while painting a continuous
        # forearm-to-hand transition through the wrist and glove cuff.
        wrist_region = np.zeros(len(positions), dtype=bool)
        palm = np.zeros(len(positions), dtype=bool)
        for side in ("Left", "Right"):
            wrist = np.asarray(rig.data.bones[side + "Hand"].head_local)
            elbow = np.asarray(rig.data.bones[side + "ForeArm"].head_local)
            axis = elbow - wrist
            axis /= np.linalg.norm(axis)
            distance = (authoring_positions - wrist) @ axis
            radius = np.linalg.norm(authoring_positions - wrist, axis=1)
            wrist_region |= (radius < .115) & (distance > -.055) & (distance < .105)
            palm |= (radius < .24) & (distance < -.055)
        weights = _smooth_garments(mesh, positions, weights, palm, wrist_region, iterations=48)
        _write_skin(mesh, weights)
        mesh["steel_tide_heron_garment_transition_revision"] = 1
        count += 1
        print(f"HERON_SKIN_REFINEMENT_CHECK vertices={len(positions)} "
              f"garment={int(garment.sum())} wrist={int(wrist_region.sum())}", flush=True)
    if count != 1:
        raise RuntimeError(f"Heron requires exactly one OperatorBody surface, found {count}")
