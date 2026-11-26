"""Native-generated template fixtures for opt-in structural validation."""
from copy import deepcopy
import json
import os
from pathlib import Path
import sys
import unittest
from uuid import uuid4

from orbit2 import Catalog, validate_patch


def load_templates(directory):
    data = json.loads((directory / "composite-templates.json").read_text(encoding="utf-8"))
    if data["compositeTemplatesFormatVersion"] != 1 or data["patchSchemaVersion"] != 1:
        raise ValueError("Unsupported native composite template format")
    return data["templates"]


def wrap(node):
    graph_id = str(uuid4())
    return {"schemaVersion": 1, "patchId": str(uuid4()),
            "rootGraph": {"id": graph_id, "nodes": [deepcopy(node)], "connections": []},
            "beatsPerMinute": 120, "activeGraphId": graph_id, "canvasLayouts": []}


def nested_example(directory):
    drone = deepcopy(load_templates(directory)["preset.drone.layered-fifth"]["node"])
    outer = {"id": str(uuid4()), "typeKey": "composite.graph", "parameters": [], "ports": [],
             "isMuted": False, "isBypassed": False,
             "composite": {"name": "Nested preview", "inputRackId": str(uuid4()),
                           "outputRackId": str(uuid4()), "inputs": [], "outputs": [],
                           "graph": {"id": str(uuid4()), "nodes": [drone], "connections": []},
                           "macros": []}}
    patch = wrap(outer)
    child = patch["rootGraph"]["nodes"][0]["composite"]["graph"]
    inner = child["nodes"][0]["composite"]
    patch["activeGraphId"] = inner["graph"]["id"]
    patch["canvasLayouts"] = [{"graphId": inner["graph"]["id"], "panX": 0, "panY": 0, "zoom": 1,
                                "positions": [{"nodeId": inner["inputRackId"], "x": 0, "y": 0}]}]
    return patch


def inner_composite(document):
    return document["rootGraph"]["nodes"][0]["composite"]["graph"]["nodes"][0]["composite"]


def invalid_endpoint(document):
    document = deepcopy(document)
    edge = inner_composite(document)["graph"]["connections"][0]
    edge["source"]["nodeId"] = document["rootGraph"]["nodes"][0]["id"]
    return document


class CompositeValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = Path(os.environ["ORBIT_AGENT_KIT_DATA"])
        cls.catalog = Catalog.load(cls.data)

    def test_native_macro_free_templates_and_nested_graph(self):
        templates = load_templates(self.data)
        self.assertIn("preset.drone.layered-fifth", templates)

        def has_macros(node):
            composite = node.get("composite")
            return composite is not None and (bool(composite.get("macros")) or
                    any(has_macros(child) for child in composite["graph"]["nodes"]))

        accepted = 0
        for key, entry in templates.items():
            with self.subTest(key=key):
                document = wrap(entry["node"])
                original = deepcopy(document)
                result = validate_patch(document, self.catalog, allow_composites=True)
                if has_macros(entry["node"]):
                    self.assertIn("unsupported-composite-macro", {d.code for d in result.errors})
                else:
                    self.assertTrue(result.is_valid, result.errors)
                    accepted += 1
                self.assertEqual(original, document)
                self.assertFalse(validate_patch(document, self.catalog).is_valid)
        self.assertGreater(accepted, 0)
        document = nested_example(self.data)
        self.assertTrue(validate_patch(document, self.catalog, allow_composites=True).is_valid)

    def test_graph_scope_mirrors_racks_and_layout_rejections(self):
        original = nested_example(self.data)
        broken = invalid_endpoint(original)
        self.assertIn("endpoint", {d.code for d in validate_patch(broken, self.catalog, allow_composites=True).errors})
        changes = [
            ("identity", lambda d: inner_composite(d)["graph"].update(id=d["rootGraph"]["id"])),
            ("identity", lambda d: inner_composite(d).update(outputRackId=inner_composite(d)["inputRackId"])),
            ("boundary", lambda d: inner_composite(d)["inputs"][0].update(signalType=999)),
            ("boundary-mirror", lambda d: d["rootGraph"]["nodes"][0]["composite"]["graph"]["nodes"][0]["ports"].clear()),
            ("layout-node", lambda d: d["canvasLayouts"][0]["positions"][0].update(nodeId=d["rootGraph"]["nodes"][0]["id"])),
        ]
        for code, change in changes:
            with self.subTest(code=code):
                document = deepcopy(original)
                change(document)
                before = deepcopy(document)
                errors = validate_patch(document, self.catalog, allow_composites=True).errors
                self.assertIn(code, {d.code for d in errors}, errors)
                self.assertEqual(before, document)

    def test_macro_fixtures_still_fail_closed(self):
        for name in ("deferred-static-macro.json", "deferred-macro-input.json"):
            document = json.loads((self.data / name).read_text(encoding="utf-8"))
            result = validate_patch(document, self.catalog, allow_composites=True)
            self.assertIn("unsupported-composite-macro", {d.code for d in result.errors})


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--fixtures":
        directory = Path(sys.argv[2])
        catalog = Catalog.load(directory)
        good = nested_example(directory)
        bad = invalid_endpoint(good)
        validate_patch(good, catalog, allow_composites=True).raise_for_errors()
        if validate_patch(bad, catalog, allow_composites=True).is_valid:
            raise ValueError("Offline validation accepted cross-scope endpoint")
        for name, document in (("composite-preview-valid.json", good), ("composite-preview-invalid.json", bad)):
            (directory / name).write_text(json.dumps(document, allow_nan=False), encoding="utf-8")
    else:
        unittest.main()
