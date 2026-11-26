"""Build root-level leaf patches with native identities and basic edit guards."""
from copy import deepcopy
import json
import math
from pathlib import Path
import tempfile
from uuid import uuid4

from .catalog import Catalog


def _id():
    return str(uuid4())


def _number(value, label):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} must be a finite number")
    try:
        value = float(value)
    except OverflowError as exc:
        raise ValueError(f"{label} must be a finite number") from exc
    if not math.isfinite(value):
        raise ValueError(f"{label} must be a finite number")
    return value


def _bounded(value, limits, label):
    value = _number(value, label)
    if not limits["minimum"] <= value <= limits["maximum"]:
        raise ValueError(f"{label} outside native bounds")
    return value


def _address_key(address):
    # GUID equality is case-insensitive; preserve original spelling on disk.
    return (address["nodeId"].lower(), address["portId"].lower())


def _check_route(src, output, dst, input_port, amount, connections, pairs, amount_rules, fan_in):
    pair = (output["signalType"], input_port["signalType"])
    if pair not in pairs or output["channelCount"] != input_port["channelCount"]:
        raise ValueError("Incompatible signal types or channels")
    amount = _number(amount, "connection amount")
    if amount_rules[pair[1]] == "unity-only" and amount != 1:
        raise ValueError("Event connections require unity amount")
    for edge in connections:
        if _address_key(edge["destination"]) == _address_key(dst):
            if _address_key(edge["source"]) == _address_key(src):
                raise ValueError("Duplicate connection")
            if not fan_in[input_port["fanIn"]]:
                raise ValueError("Input accepts only one connection")
    # Existing audio and modulation edges share one dependency graph.
    pending, visited = [dst["nodeId"].lower()], set()
    while pending:
        current = pending.pop()
        if current == src["nodeId"].lower():
            raise ValueError("Connection would create a dependency cycle")
        if current not in visited:
            visited.add(current)
            pending.extend(e["destination"]["nodeId"].lower() for e in connections
                           if e["source"]["nodeId"].lower() == current)
    return amount


class Node:
    def __init__(self, patch, definition, *, saved=None):
        self._patch = patch
        self._definitions = {p["key"]: p for p in definition["parameters"]}
        self._ports = {p["key"]: p for p in definition["ports"]}
        self._data = saved if saved is not None else {
            "id": _id(), "typeKey": definition["persistedTypeKey"],
            "parameters": [{"key": p["key"], "id": _id(),
                            "baseValue": p["factoryBaseValue"]}
                           for p in definition["parameters"]],
            "ports": [{"key": p["key"], "id": _id()}
                      for p in definition["ports"]],
            "composite": None, "isMuted": definition["factoryIsMuted"],
            "isBypassed": definition["factoryIsBypassed"],
        }

    @property
    def id(self):
        return self._data["id"]

    @property
    def type_key(self):
        return self._data["typeKey"]

    def get(self, key):
        if key not in self._definitions:
            raise ValueError(f"Unknown parameter: {key}")
        return next(p["baseValue"] for p in self._data["parameters"] if p["key"] == key)

    def _active(self, definition):
        clauses = definition["activeWhen"]
        return clauses is None or any(
            all(self.get(condition["selectorKey"]) == condition["equalsValue"]
                for condition in clause) for clause in clauses)

    def is_parameter_active(self, key):
        if key not in self._definitions:
            raise ValueError(f"Unknown parameter: {key}")
        return self._active(self._definitions[key])

    def is_port_active(self, key):
        if key not in self._ports:
            raise ValueError(f"Unknown port: {key}")
        port = self._ports[key]
        return self._active(port) and all(
            self._active(parameter) for parameter in self._definitions.values()
            if parameter["modulationPortKey"] == key)

    def describe_parameter(self, key):
        """Detached native definition plus current stored base and activation."""
        active = self.is_parameter_active(key)
        return {**deepcopy(self._definitions[key]), "baseValue": self.get(key),
                "isActive": active}

    def describe_port(self, key):
        """Detached native port definition plus current configuration activity."""
        active = self.is_port_active(key)
        return {**deepcopy(self._ports[key]), "isActive": active}

    def list_ports(self, *, active_only=False):
        return tuple(self.describe_port(key) for key in self._ports
                     if not active_only or self.is_port_active(key))

    def set(self, key, value):
        self.set_parameters({key: value})

    def set_parameters(self, values):
        if self._patch is None:
            raise ValueError("Node has been removed from its patch")
        # Check the entire batch before changing any native state.
        checked = {}
        for key, value in values.items():
            if key not in self._definitions:
                raise ValueError(f"Unknown parameter: {key}")
            definition = self._definitions[key]
            value = _bounded(value, definition, key)
            choices = definition["choices"]
            if choices is not None and not any(c["value"] == value for c in choices):
                raise ValueError(f"Unsupported choice for {key}")
            checked[key] = value
        for parameter in self._data["parameters"]:
            if parameter["key"] in checked:
                parameter["baseValue"] = checked[parameter["key"]]

    def _endpoint(self, key, direction):
        port = self._ports.get(key)
        if port is None or port["direction"] != direction:
            raise ValueError(f"Unknown {direction.lower()} port: {key}")
        saved = next(p for p in self._data["ports"] if p["key"] == key)
        return {"nodeId": self.id, "portId": saved["id"]}, port


class Patch:
    def __init__(self, title, catalog, tempo=None):
        if not isinstance(catalog, Catalog):
            raise TypeError("catalog must be a Catalog")
        if not isinstance(title, str) or len(title.encode("utf-16-le")) // 2 > 120:
            raise ValueError("Title must be a string of at most 120 UTF-16 units")
        self._catalog = catalog
        limits = catalog._contract["tempo"]
        self._tempo = _bounded(limits["defaultValue"] if tempo is None else tempo,
                               limits, "tempo")
        self._title = title
        self._patch_id, self._graph_id = _id(), _id()
        self._loaded_document = None
        self._nodes = []
        self._aliases = {}
        self._connections = []
        self._pairs = {(p["source"], p["destination"])
                       for p in catalog._rules["compatibleSignalPairs"]}
        self._amount_rules = {p["destination"]: p["rule"]
                              for p in catalog._rules["amounts"]}
        self._fan_in = {p["policy"]: p["allowsMultiple"]
                        for p in catalog._rules["fanIn"]}

    @classmethod
    def create(cls, title, *, catalog, tempo=None):
        return cls(title, catalog, tempo)

    @classmethod
    def load(cls, path, *, catalog):
        """Load a validated current-format leaf patch without regenerating identities."""
        def unique_object(pairs):
            obj = {}
            for key, value in pairs:
                if key in obj:
                    raise ValueError(f"Duplicate JSON property: {key}")
                obj[key] = value
            return obj

        def reject_constant(value):
            raise ValueError(f"Invalid JSON number: {value}")

        document = json.loads(Path(path).read_text(encoding="utf-8-sig"),
                              object_pairs_hook=unique_object, parse_constant=reject_constant)
        from .validation import validate_patch
        validate_patch(document, catalog).raise_for_errors()
        # Even unknown fields must be serializable without losing numeric data.
        json.dumps(document, allow_nan=False)
        patch = cls("", catalog, document["beatsPerMinute"])
        patch._loaded_document = document
        patch._patch_id = document["patchId"]
        graph = document["rootGraph"]
        patch._graph_id = graph["id"]
        definitions = {}
        for definition in catalog._nodes.values():
            if definition["metadataStatus"] == "leaf-definition":
                definitions[definition["persistedTypeKey"]] = definition
        patch._nodes = [Node(patch, definitions[node["typeKey"]], saved=node)
                        for node in graph["nodes"]]
        patch._connections = graph["connections"]
        return patch

    def list_nodes(self):
        return tuple(self._nodes)

    def get_node_by_id(self, node_id):
        if isinstance(node_id, str):
            for node in self._nodes:
                if node.id.lower() == node_id.lower():
                    return node
        raise KeyError(node_id)

    def add_node(self, creation_key, *, alias=None, **parameters):
        if alias is not None and (not isinstance(alias, str) or not alias
                                  or alias in self._aliases):
            raise ValueError("Alias must be a unique nonempty string")
        definition = self._catalog.describe(creation_key)
        if definition["metadataStatus"] != "leaf-definition":
            raise ValueError(f"Unsupported node creation: {creation_key}")
        node = Node(self, definition)
        node.set_parameters(parameters)
        self._nodes.append(node)
        if alias is not None:
            self._aliases[alias] = node
        return node

    def get_node(self, alias):
        return self._aliases[alias]

    def connect(self, source, source_port, destination, destination_port, *, amount=1):
        if source not in self._nodes or destination not in self._nodes:
            raise ValueError("Both nodes must belong to this patch")
        src, output = source._endpoint(source_port, "Output")
        dst, input_port = destination._endpoint(destination_port, "Input")
        amount = _check_route(src, output, dst, input_port, amount, self._connections,
                              self._pairs, self._amount_rules, self._fan_in)
        edge = {"id": _id(), "source": src, "destination": dst, "amount": amount}
        self._connections.append(edge)
        return edge["id"]

    def modulate(self, source, source_port, destination, parameter_key, *, amount=1,
                 allow_dormant=False):
        """Connect to a native parameter socket using destination-native amount units."""
        if source not in self._nodes or destination not in self._nodes:
            raise ValueError("Both nodes must belong to this patch")
        parameter = destination.describe_parameter(parameter_key)
        port_key = parameter["modulationPortKey"]
        if not parameter["canModulate"] or port_key is None:
            raise ValueError(f"Parameter cannot be modulated: {parameter_key}")
        if not allow_dormant and (not source.is_port_active(source_port)
                                  or not destination.is_port_active(port_key)):
            raise ValueError("Modulation endpoint is dormant; enable it or allow_dormant")
        return self.connect(source, source_port, destination, port_key, amount=amount)

    def is_connection_active(self, connection_id):
        """Configuration activity only; not a claim about reachability or playback."""
        edge = self._connections[self._connection_index(connection_id)]
        for endpoint in ("source", "destination"):
            address = edge[endpoint]
            node = self.get_node_by_id(address["nodeId"])
            port_key = next(p["key"] for p in node._data["ports"]
                            if p["id"].lower() == address["portId"].lower())
            if not node.is_port_active(port_key):
                return False
        return True

    def list_connections(self):
        """Return independent native edge snapshots in graph order."""
        return tuple(deepcopy(self._connections))

    def _connection_index(self, connection_id):
        if isinstance(connection_id, str):
            for index, edge in enumerate(self._connections):
                if edge["id"].lower() == connection_id.lower():
                    return index
        raise KeyError(connection_id)

    def set_connection_amount(self, connection_id, amount):
        index = self._connection_index(connection_id)
        candidate = self.to_dict()
        candidate["rootGraph"]["connections"][index]["amount"] = amount
        self._validate_candidate(candidate)
        self._connections[index]["amount"] = _number(amount, "connection amount")

    def disconnect(self, connection_id):
        index = self._connection_index(connection_id)
        candidate = self._deletion_candidate()
        del candidate["rootGraph"]["connections"][index]
        self._validate_candidate(candidate)
        del self._connections[index]

    def remove_node(self, node):
        if node not in self._nodes:
            raise ValueError("Node must belong to this patch")
        candidate = self._deletion_candidate()
        graph = candidate["rootGraph"]
        node_id = node.id.lower()

        def unrelated(edge):
            return all(edge[endpoint]["nodeId"].lower() != node_id
                       for endpoint in ("source", "destination"))

        graph["nodes"] = [n for n in graph["nodes"] if n["id"].lower() != node_id]
        graph["connections"] = [edge for edge in graph["connections"] if unrelated(edge)]
        for layout in candidate["canvasLayouts"]:
            layout["positions"] = [p for p in layout["positions"]
                                   if p["nodeId"].lower() != node_id]
        self._validate_candidate(candidate)
        # Keep surviving Node handles and their native identities alive.
        self._connections[:] = [edge for edge in self._connections if unrelated(edge)]
        self._nodes.remove(node)
        self._aliases = {alias: item for alias, item in self._aliases.items() if item is not node}
        if self._loaded_document is not None:
            self._loaded_document["rootGraph"]["nodes"] = [n._data for n in self._nodes]
            self._loaded_document["canvasLayouts"] = candidate["canvasLayouts"]
        node._patch = None

    def _validate_candidate(self, document):
        from .validation import validate_patch
        validate_patch(document, self._catalog).raise_for_errors()

    def _deletion_candidate(self):
        from .structural import require_known_deletion_scope
        candidate = self.to_dict()
        self._validate_candidate(candidate)
        require_known_deletion_scope(candidate)
        return candidate

    def to_dict(self):
        if self._loaded_document is not None:
            document = deepcopy(self._loaded_document)
            document["rootGraph"]["nodes"] = deepcopy([n._data for n in self._nodes])
            document["rootGraph"]["connections"] = deepcopy(self._connections)
            return document
        # Empty canvas layout list delegates initial placement to native import.
        return deepcopy({
            "schemaVersion": self._catalog._contract["patchSchemaVersion"],
            "patchId": self._patch_id, "rootGraph": {
                "id": self._graph_id, "nodes": [n._data for n in self._nodes],
                "connections": self._connections,
            },
            "beatsPerMinute": self._tempo, "activeGraphId": self._graph_id,
            "canvasLayouts": [],
            "overview": {"title": self._title, "description": "",
                         "instructions": "", "tags": []},
        })

    def validate(self):
        from .validation import validate_patch
        return validate_patch(self.to_dict(), self._catalog)

    def save(self, path):
        self.validate().raise_for_errors()
        path = Path(path)
        payload = json.dumps(self.to_dict(), ensure_ascii=True, allow_nan=False, indent=2)
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8",
                                             dir=path.parent, delete=False) as output:
                temporary = Path(output.name)
                output.write(payload + "\n")
            temporary.replace(path)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
