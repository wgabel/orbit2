"""Read-only validation of current-format native root leaf documents."""
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
    def __init__(self, catalog):
        self.catalog = catalog
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

    def node(self, node, path):
        if not self.obj(node, path, ("id", "typeKey", "parameters", "ports", "composite")):
            return
        node_id = self.guid(node.get("id"), path + ".id")
        key = node.get("typeKey")
        definition = self.definitions.get(key) if isinstance(key, str) else None
        if node.get("composite") is not None:
            self.error("unsupported-composite", path + ".composite", "Nested graphs require S9")
            return
        if definition is None:
            self.error("node-type", path + ".typeKey", "Unknown or unsupported leaf type", actual=key)
            return
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
        if node_id is not None:
            if node_id in self.nodes:
                self.error("identity", path + ".id", "Duplicate node ID", actual=node_id)
            else:
                self.nodes[node_id] = ports

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

    def connections(self, graph):
        rules = self.catalog._rules
        pairs = {(p["source"], p["destination"]) for p in rules["compatibleSignalPairs"]}
        amounts = {p["destination"]: p["rule"] for p in rules["amounts"]}
        fan_in = {p["policy"]: p["allowsMultiple"] for p in rules["fanIn"]}
        seen, accepted = set(), []
        for i, edge in enumerate(self.items(graph.get("connections"), "$.rootGraph.connections")):
            path = f"$.rootGraph.connections[{i}]"
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

    def layouts(self, value, graph_id):
        seen = set()
        preferences = self.catalog._contract["canvasPreferences"]
        for i, layout in enumerate(self.items(value, "$.canvasLayouts")):
            path = f"$.canvasLayouts[{i}]"
            if not self.obj(layout, path, ("graphId", "panX", "panY", "zoom", "positions")):
                continue
            identity = self.guid(layout.get("graphId"), path + ".graphId")
            self.unique(identity, seen, path + ".graphId")
            if identity != graph_id:
                self.error("layout-graph", path + ".graphId", "Only the root canvas is supported")
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
                if node is not None and node not in self.nodes:
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
        graph = document.get("rootGraph")
        if not self.obj(graph, "$.rootGraph", ("id", "nodes", "connections")):
            return
        graph_id = self.guid(graph.get("id"), "$.rootGraph.id")
        active = self.guid(document.get("activeGraphId"), "$.activeGraphId", "active-graph")
        if active is not None and active != graph_id:
            self.error("active-graph", "$.activeGraphId", "Active graph must be the root in S5")
        for i, node in enumerate(self.items(graph.get("nodes"), "$.rootGraph.nodes")):
            self.node(node, f"$.rootGraph.nodes[{i}]")
        self.connections(graph)
        self.layouts(document.get("canvasLayouts"), graph_id)


def validate_patch(document, catalog):
    """Return structured errors without modifying a native JSON-shaped dictionary.

    This validates the current complete leaf contract, not legacy migrations,
    nested graphs, annotations, playback or musical quality.
    """
    if not isinstance(catalog, Catalog):
        raise TypeError("catalog must be a Catalog")
    if not all(key in catalog._contract for key in ("overview", "canvasPreferences")):
        return ValidationResult((Diagnostic("catalog-contract", "$", "Regenerate native S5 metadata"),))
    validator = _Validator(catalog)
    validator.validate(document)
    return ValidationResult(tuple(validator.errors))
