"""Read versioned native exports; no source code or runtime required."""
from copy import deepcopy
import json
from pathlib import Path


class Catalog:
    def __init__(self, catalog, rules, contract):
        for data, field in ((catalog, "catalogFormatVersion"),
                            (rules, "connectionRulesFormatVersion"),
                            (contract, "patchContractFormatVersion"),
                            (contract, "patchSchemaVersion")):
            if type(data.get(field)) is not int or data[field] != 1:
                raise ValueError(f"Unsupported {field}")
        if rules["channelRule"] != "equal" or rules["topology"] != {
            "endpoints": "existing-output-to-existing-input-in-same-graph",
            "uniqueConnectionIds": True, "uniqueEndpointPairs": True,
            "allowsDependencyCycles": False,
        }:
            raise ValueError("Unsupported routing contract")
        if any(item["rule"] not in ("finite-signed", "unity-only")
               for item in rules["amounts"]):
            raise ValueError("Unsupported amount rule")
        self._nodes = deepcopy(catalog["nodes"])
        self._rules = deepcopy(rules)
        self._contract = deepcopy(contract)

    @classmethod
    def load(cls, directory):
        directory = Path(directory)

        def read(name):
            return json.loads((directory / name).read_text(encoding="utf-8"))

        return cls(read("catalog.json"), read("connection-rules.json"),
                   read("patch-contract.json"))

    def list_nodes(self, category=None):
        return [key for key, entry in sorted(self._nodes.items())
                if category is None or entry["category"] == category]

    def describe(self, key):
        if key not in self._nodes:
            raise ValueError(f"Unknown node creation key: {key}")
        return deepcopy(self._nodes[key])
