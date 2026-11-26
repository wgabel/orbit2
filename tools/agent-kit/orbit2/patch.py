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


class Node:
    def __init__(self, patch, definition):
        self._patch = patch
        self._definitions = {p["key"]: p for p in definition["parameters"]}
        self._ports = {p["key"]: p for p in definition["ports"]}
        self._data = {
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

    def set(self, key, value):
        self.set_parameters({key: value})

    def set_parameters(self, values):
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
        pair = (output["signalType"], input_port["signalType"])
        if pair not in self._pairs or output["channelCount"] != input_port["channelCount"]:
            raise ValueError("Incompatible signal types or channels")
        amount = _number(amount, "connection amount")
        if self._amount_rules[pair[1]] == "unity-only" and amount != 1:
            raise ValueError("Event connections require unity amount")
        for edge in self._connections:
            if edge["destination"] == dst:
                if edge["source"] == src:
                    raise ValueError("Duplicate connection")
                if not self._fan_in[input_port["fanIn"]]:
                    raise ValueError("Input accepts only one connection")
        # Existing audio and modulation edges share one dependency graph.
        pending, visited = [destination.id], set()
        while pending:
            current = pending.pop()
            if current == source.id:
                raise ValueError("Connection would create a dependency cycle")
            if current not in visited:
                visited.add(current)
                pending.extend(e["destination"]["nodeId"] for e in self._connections
                               if e["source"]["nodeId"] == current)
        edge = {"id": _id(), "source": src, "destination": dst, "amount": amount}
        self._connections.append(edge)
        return edge["id"]

    def to_dict(self):
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

    def save(self, path):
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
