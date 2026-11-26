"""Explicit reference-cleanup scope for current root leaf documents.

This is an allowlist of understood deletion semantics, not a second schema
validator. New persisted fields require a deliberate cleanup-policy review.
"""
from .validation import Diagnostic, PatchValidationError


def require_known_deletion_scope(document):
    errors = []

    def known(obj, path, keys):
        for key in sorted(obj.keys() - set(keys.split())):
            errors.append(Diagnostic("unsupported-deletion-field", f"{path}.{key}",
                                     "Cannot determine reference ownership of unknown field"))

    known(document, "$", "schemaVersion patchId rootGraph beatsPerMinute activeGraphId canvasLayouts overview")
    if "overview" in document:
        known(document["overview"], "$.overview", "title description instructions tags")
    graph = document["rootGraph"]
    known(graph, "$.rootGraph", "id nodes connections")
    for i, node in enumerate(graph["nodes"]):
        path = f"$.rootGraph.nodes[{i}]"
        known(node, path, "id typeKey parameters ports composite isMuted isBypassed")
        for j, parameter in enumerate(node["parameters"]):
            known(parameter, f"{path}.parameters[{j}]", "key id baseValue")
        for j, port in enumerate(node["ports"]):
            known(port, f"{path}.ports[{j}]", "key id")
    for i, edge in enumerate(graph["connections"]):
        path = f"$.rootGraph.connections[{i}]"
        known(edge, path, "id source destination amount")
        for endpoint in ("source", "destination"):
            known(edge[endpoint], f"{path}.{endpoint}", "nodeId portId")
    for i, layout in enumerate(document["canvasLayouts"]):
        path = f"$.canvasLayouts[{i}]"
        # S5 already rejects nonempty presentation collections and expanded views.
        known(layout, path, "graphId panX panY zoom positions visualizations connectionLabels "
                            "annotationVisibility expandedVisualizationNodeId notes nodeDescriptions "
                            "connectionDescriptions visualGroups")
        for j, position in enumerate(layout["positions"]):
            known(position, f"{path}.positions[{j}]", "nodeId x y")
    if errors:
        raise PatchValidationError(tuple(errors))
