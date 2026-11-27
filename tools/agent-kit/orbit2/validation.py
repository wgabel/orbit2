"""Read-only native graph validation; opt-in composite structural preview."""
from copy import deepcopy
from dataclasses import dataclass
import re
from uuid import UUID

from .catalog import Catalog
from .patch import _number, _bounded, _check_route


@dataclass(frozen=True)
class Diagnostic:
    code: str
    path: str
    message: str
    expected: object = None
    actual: object = None


class PatchValidationError(ValueError):
    def __init__(self, diagnostics):
        self.diagnostics = diagnostics
        super().__init__("; ".join(f"{d.code} at {d.path}: {d.message}" for d in diagnostics))


@dataclass(frozen=True)
class ValidationResult:
    errors: tuple[Diagnostic, ...]

    @property
    def is_valid(self):
        return not self.errors

    def raise_for_errors(self):
        if self.errors:
            raise PatchValidationError(self.errors)


_GUID = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")


class _Validator:
    def __init__(self, catalog, allow_composites=False):
        self.catalog = catalog
        self.allow_composites = allow_composites
        self.graphs = {}
        self.errors = []
        self.nodes = {}
        self.definitions = {n["persistedTypeKey"]: n for n in catalog._nodes.values()
                            if n["metadataStatus"] == "leaf-definition"}

    def error(self, code, path, message, expected=None, actual=None):
        self.errors.append(Diagnostic(code, path, message, deepcopy(expected), deepcopy(actual)))

    def obj(self, value, path, required=()):
        if not isinstance(value, dict):
            self.error("shape", path, "Expected an object", "object", type(value).__name__)
            return False
        for key in required:
            if key not in value:
                self.error("shape", f"{path}.{key}", "Missing required field")
        return True

    def items(self, value, path):
        if not isinstance(value, list):
            self.error("shape", path, "Expected an array", "array", type(value).__name__)
            return []
        return value

    def guid(self, value, path, code="identity"):
        if isinstance(value, str) and _GUID.fullmatch(value):
            parsed = UUID(value)
            if parsed.int:
                return str(parsed)
        self.error(code, path, "Expected a nonempty native GUID", "GUID", value)
        return None

    def unique(self, value, seen, path, code="identity"):
        if value is None:
            return False
        if value in seen:
            self.error(code, path, "Duplicate identity or key", "unique", value)
            return False
        seen.add(value)
        return True

    def numeric(self, value, path, code, limits=None):
        try:
            return _number(value, path) if limits is None else _bounded(value, limits, path)
        except ValueError as exc:
            self.error(code, path, str(exc), limits, value)
            return None

    def overview(self, overview, path):
        limits = self.catalog._contract["overview"]
        if not self.obj(overview, path, ("title", "description", "instructions", "tags")):
            return
        def length(value):
            return len(value.encode("utf-16-le", errors="surrogatepass")) // 2
        for key, limit in (("title", limits["maxTitleLength"]),
                           ("description", limits["maxTextLength"]),
                           ("instructions", limits["maxTextLength"])):
            value = overview.get(key)
            if not isinstance(value, str) or length(value) > limit:
                self.error("overview", f"{path}.{key}", "Invalid text or length", limit, value)
        tags = self.items(overview.get("tags"), path + ".tags")
        if len(tags) > limits["maxTags"]:
            self.error("overview", path + ".tags", "Too many tags", limits["maxTags"], len(tags))
        seen = set()
        for i, tag in enumerate(tags):
            where = f"{path}.tags[{i}]"
            if (not isinstance(tag, str) or not 1 <= length(tag) <= limits["maxTagLength"]
                    or tag != tag.strip() or "," in tag
                    or any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in tag)):
                self.error("overview", where, "Invalid tag", actual=tag)
            else:
                self.unique(tag.lower(), seen, where, "overview")

    def node(self, node, path, depth=0):
        if not self.obj(node, path, ("id", "typeKey", "parameters", "ports", "composite")):
            return
        node_id = self.guid(node.get("id"), path + ".id")
        key = node.get("typeKey")
        definition = self.definitions.get(key) if isinstance(key, str) else None
        if node.get("composite") is not None:
            if not self.allow_composites:
                self.error("unsupported-composite", path + ".composite", "Nested graphs require S9")
            else:
                self.composite(node, node_id, path, depth)
            return
        if definition is None:
            self.error("node-type", path + ".typeKey", "Unknown or unsupported leaf type", actual=key)
            return
        if depth and key == "output.master":
            self.error("node-type", path + ".typeKey", "Child graphs use their output rack, not master output")
        for field, supported in (("isMuted", definition["supportsMute"]),
                                 ("isBypassed", definition["bypassInputKey"] is not None)):
            value = node.get(field, False)
            if type(value) is not bool or (value and not supported):
                self.error("node-state", f"{path}.{field}", "Invalid or unsupported node state", actual=value)
        ports = {}
        for collection in ("parameters", "ports"):
            entries = {p["key"]: p for p in definition[collection]}
            keys, ids = set(), set()
            for i, item in enumerate(self.items(node.get(collection), f"{path}.{collection}")):
                where = f"{path}.{collection}[{i}]"
                required = ("key", "id", "baseValue") if collection == "parameters" else ("key", "id")
                if not self.obj(item, where, required):
                    continue
                identity = self.guid(item.get("id"), where + ".id")
                unique_id = self.unique(identity, ids, where + ".id")
                name = item.get("key")
                metadata = entries.get(name) if isinstance(name, str) else None
                if metadata is None:
                    self.error("definition-key", where + ".key", "Unknown definition key", actual=name)
                    continue
                unique_key = self.unique(name, keys, where + ".key", "definition-key")
                if collection == "parameters":
                    value = self.numeric(item.get("baseValue"), where + ".baseValue", "parameter-value", metadata)
                    choices = metadata["choices"]
                    if value is not None and choices is not None and not any(c["value"] == value for c in choices):
                        self.error("parameter-value", where + ".baseValue", "Value must match a native choice",
                                   [c["value"] for c in choices], value)
                elif unique_id and unique_key:
                    ports[identity] = metadata
            for missing in sorted(entries.keys() - keys):
                self.error("definition-key", f"{path}.{collection}", "Missing current definition key", missing)
        self.register_node(node_id, ports, path)

    def register_node(self, node_id, ports, path):
        if node_id is not None:
            if node_id in self.nodes:
                self.error("identity", path + ".id", "Duplicate node ID", actual=node_id)
            else:
                self.nodes[node_id] = ports

    def composite(self, node, node_id, path, depth):
        where = path + ".composite"
        payload = node["composite"]
        if node.get("typeKey") != "composite.graph":
            self.error("node-type", path + ".typeKey", "Expected the native composite type")
            return
        if not self.obj(payload, where, ("name", "inputRackId", "outputRackId", "inputs", "outputs", "graph")):
            return
        if not isinstance(payload.get("name"), str) or not payload["name"].strip():
            self.error("shape", where + ".name", "Composite requires a nonempty name")
        if node.get("parameters") != []:
            self.error("definition-key", path + ".parameters", "Composite has no leaf parameters")
        if type(node.get("isMuted", False)) is not bool or node.get("isBypassed", False) is not False:
            self.error("node-state", path, "Invalid composite mute/bypass state")
        if payload.get("macros", []) != []:
            self.error("unsupported-composite-macro", where + ".macros",
                       "Macro validation is a subsequent slice")
        enums = self.catalog._rules["boundaryEnumValues"]
        event = self.catalog._rules["eventPortConstraints"]
        event_types = {item["destination"] for item in self.catalog._rules["amounts"] if item["eventPort"]}
        parent_ports, rack_inputs, rack_outputs = {}, {}, {}
        ids = set()
        for field, parent_direction, rack_direction, rack_ports in (
                ("inputs", "Input", "Output", rack_inputs),
                ("outputs", "Output", "Input", rack_outputs)):
            names = set()
            for i, port in enumerate(self.items(payload.get(field), where + "." + field)):
                loc = f"{where}.{field}[{i}]"
                if not self.obj(port, loc, ("id", "name", "signalType", "channelCount", "fanIn")):
                    continue
                identity = self.guid(port.get("id"), loc + ".id")
                unique = self.unique(identity, ids, loc + ".id")
                name = port.get("name")
                if not isinstance(name, str) or not name.strip():
                    self.error("boundary", loc + ".name", "Boundary requires a nonempty name")
                else:
                    self.unique(name.lower(), names, loc + ".name", "boundary")
                signal_value, policy_value = port.get("signalType"), port.get("fanIn")
                signal = enums["signalType"].get(str(signal_value)) if type(signal_value) is int else None
                policy = enums["fanIn"].get(str(policy_value)) if type(policy_value) is int else None
                channels = port.get("channelCount")
                if (signal is None or policy is None or type(channels) is not int
                        or not 1 <= channels <= 2147483647):
                    self.error("boundary", loc, "Invalid native boundary signal, fan-in or channels")
                    continue
                if signal in event_types and (channels != event["channelCount"] or policy != event["fanIn"]):
                    self.error("boundary", loc, "Invalid event boundary constraints")
                    continue
                if not unique:
                    continue
                def project(direction):
                    return {"direction": direction, "signalType": signal, "channelCount": channels,
                            "fanIn": policy if direction == "Input" else "Single"}
                parent_ports[identity] = project(parent_direction)
                rack_ports[identity] = project(rack_direction)

        # The native serializer reconstructs the mirrors from the exposed
        # ports. Require the complete canonical saved mirror for this preview.
        mirrored = set()
        for i, port in enumerate(self.items(node.get("ports"), path + ".ports")):
            loc = f"{path}.ports[{i}]"
            if not self.obj(port, loc, ("id", "key")):
                continue
            identity = self.guid(port.get("id"), loc + ".id")
            self.unique(identity, mirrored, loc + ".id")
            if identity not in parent_ports or port.get("key") != "boundary-" + (identity or "").replace("-", ""):
                self.error("boundary-mirror", loc, "Saved port must mirror the exposed boundary identity")
        if mirrored != set(parent_ports):
            self.error("boundary-mirror", path + ".ports", "Saved boundary mirror is incomplete")
        racks = []
        for field, ports in (("inputRackId", rack_inputs), ("outputRackId", rack_outputs)):
            identity = self.guid(payload.get(field), where + "." + field)
            racks.append((identity, ports, where + "." + field))
        self.graph(payload.get("graph"), where + ".graph", depth + 1, racks)
        self.register_node(node_id, parent_ports, path)

    def graph(self, graph, path, depth=0, racks=()):
        if depth > 64:
            self.error("graph-depth", path, "Composite preview supports at most 64 nested levels")
            return
        if not self.obj(graph, path, ("id", "nodes", "connections")):
            return
        graph_id = self.guid(graph.get("id"), path + ".id")
        if graph_id is None:
            return
        if graph_id in self.graphs:
            self.error("identity", path + ".id", "Duplicate graph ID", actual=graph_id)
            return
        parent = self.nodes
        self.nodes = {}
        self.graphs[graph_id] = self.nodes
        try:
            for identity, ports, rack_path in racks:
                self.register_node(identity, ports, rack_path)
            for i, node in enumerate(self.items(graph.get("nodes"), path + ".nodes")):
                self.node(node, f"{path}.nodes[{i}]", depth)
            self.connections(graph, path)
        finally:
            self.nodes = parent

    def endpoint(self, data, path, direction):
        if not self.obj(data, path, ("nodeId", "portId")):
            return None, None
        node = self.guid(data.get("nodeId"), path + ".nodeId", "endpoint")
        port = self.guid(data.get("portId"), path + ".portId", "endpoint")
        if node is None or port is None:
            return None, None
        metadata = self.nodes.get(node, {}).get(port)
        if metadata is None or metadata["direction"] != direction:
            self.error("endpoint", path, f"Expected an existing {direction.lower()} in this graph", actual=data)
            return None, None
        return {"nodeId": node, "portId": port}, metadata

    def connections(self, graph, graph_path):
        rules = self.catalog._rules
        pairs = {(p["source"], p["destination"]) for p in rules["compatibleSignalPairs"]}
        amounts = {p["destination"]: p["rule"] for p in rules["amounts"]}
        fan_in = {p["policy"]: p["allowsMultiple"] for p in rules["fanIn"]}
        seen, accepted = set(), []
        for i, edge in enumerate(self.items(graph.get("connections"), graph_path + ".connections")):
            path = f"{graph_path}.connections[{i}]"
            if not self.obj(edge, path, ("id", "source", "destination", "amount")):
                continue
            identity = self.guid(edge.get("id"), path + ".id")
            self.unique(identity, seen, path + ".id")
            src, output = self.endpoint(edge.get("source"), path + ".source", "Output")
            dst, input_port = self.endpoint(edge.get("destination"), path + ".destination", "Input")
            amount = self.numeric(edge.get("amount"), path + ".amount", "connection-amount")
            if src is None or dst is None or amount is None:
                continue
            try:
                _check_route(src, output, dst, input_port, amount, accepted, pairs, amounts, fan_in)
            except ValueError as exc:
                self.error("routing", path, str(exc))
            else:
                accepted.append({"source": src, "destination": dst})

    def layouts(self, value):
        seen = set()
        preferences = self.catalog._contract["canvasPreferences"]
        for i, layout in enumerate(self.items(value, "$.canvasLayouts")):
            path = f"$.canvasLayouts[{i}]"
            if not self.obj(layout, path, ("graphId", "panX", "panY", "zoom", "positions")):
                continue
            identity = self.guid(layout.get("graphId"), path + ".graphId")
            self.unique(identity, seen, path + ".graphId")
            if identity not in self.graphs:
                self.error("layout-graph", path + ".graphId", "Unknown or unsupported canvas graph")
            graph_nodes = self.graphs.get(identity, {})
            for key in ("panX", "panY", "zoom"):
                self.numeric(layout.get(key), f"{path}.{key}", "viewport",
                             self.catalog._contract["zoom"] if key == "zoom" else None)
            positioned = set()
            for j, position in enumerate(self.items(layout.get("positions"), path + ".positions")):
                where = f"{path}.positions[{j}]"
                if not self.obj(position, where, ("nodeId", "x", "y")):
                    continue
                node = self.guid(position.get("nodeId"), where + ".nodeId")
                self.unique(node, positioned, where + ".nodeId")
                if node is not None and node not in graph_nodes:
                    self.error("layout-node", where + ".nodeId", "Unknown position owner")
                for key in ("x", "y"):
                    self.numeric(position.get(key), f"{where}.{key}", "viewport")
            for key, allowed in preferences.items():
                if key in layout and (type(layout[key]) is not int or layout[key] not in allowed):
                    self.error("layout-preference", f"{path}.{key}", "Unknown preference", allowed, layout[key])
            for key in ("notes", "nodeDescriptions", "connectionDescriptions", "visualGroups", "visualizations"):
                if key in layout and layout[key] != []:
                    self.error("unsupported-layout", f"{path}.{key}", "Only empty presentation collections are supported in S5")
            if layout.get("expandedVisualizationNodeId") is not None:
                self.error("unsupported-layout", path + ".expandedVisualizationNodeId", "Expanded visualization is not supported in S5")

    def validate(self, document):
        if not self.obj(document, "$", ("schemaVersion", "patchId", "rootGraph", "beatsPerMinute",
                                        "activeGraphId", "canvasLayouts")):
            return
        version = document.get("schemaVersion")
        expected = self.catalog._contract["patchSchemaVersion"]
        if type(version) is not int or version != expected:
            self.error("schema-version", "$.schemaVersion", "Unsupported native version", expected, version)
            return
        self.guid(document.get("patchId"), "$.patchId")
        self.numeric(document.get("beatsPerMinute"), "$.beatsPerMinute", "tempo", self.catalog._contract["tempo"])
        if "overview" in document:
            self.overview(document["overview"], "$.overview")
        self.graph(document.get("rootGraph"), "$.rootGraph")
        active = self.guid(document.get("activeGraphId"), "$.activeGraphId", "active-graph")
        if active is not None and active not in self.graphs:
            self.error("active-graph", "$.activeGraphId", "Active graph must be supported and exist")
        self.layouts(document.get("canvasLayouts"))


def validate_patch(document, catalog, *, allow_composites=False):
    """Return structured errors without modifying a native JSON-shaped dictionary.

    By default validate the established leaf contract. Opt in to the S9b.1
    macro-free composite structural preview; this does not enable Patch.load,
    authoring, rich annotations, legacy migrations or audio validation.
    """
    if not isinstance(catalog, Catalog):
        raise TypeError("catalog must be a Catalog")
    if not all(key in catalog._contract for key in ("overview", "canvasPreferences")):
        return ValidationResult((Diagnostic("catalog-contract", "$", "Regenerate native S5 metadata"),))
    if allow_composites and "boundaryEnumValues" not in catalog._rules:
        return ValidationResult((Diagnostic("catalog-contract", "$", "Regenerate native boundary metadata"),))
    validator = _Validator(catalog, allow_composites)
    validator.validate(document)
    return ValidationResult(tuple(validator.errors))
