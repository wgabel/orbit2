"""Lossless current-format load/edit tests using native-generated metadata."""
from copy import deepcopy
import json
import os
from pathlib import Path
import tempfile
import unittest

from orbit2 import Catalog, Patch, PatchValidationError


class LoadTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = Path(os.environ["ORBIT_AGENT_KIT_DATA"])
        cls.catalog = Catalog.load(cls.data)

    def test_native_round_trip_and_parameter_edit(self):
        source = self.data / "valid-leaf.json"
        original = json.loads(source.read_text())
        patch = Patch.load(source, catalog=self.catalog)
        self.assertEqual(original, patch.to_dict())
        node = next(n for n in patch.list_nodes() if n.type_key == "generator.oscillator")
        self.assertIs(node, patch.get_node_by_id(node.id.upper()))
        expected = deepcopy(original)
        saved = next(n for n in expected["rootGraph"]["nodes"] if n["id"] == node.id)
        parameter = next(p for p in saved["parameters"] if p["key"] == "frequency")
        self.assertEqual(parameter["baseValue"], node.get("frequency"))
        with self.assertRaises(ValueError):
            node.set_parameters({"frequency": 330, "missing": 1})
        self.assertEqual(original, patch.to_dict())
        node.set("frequency", 330)
        parameter["baseValue"] = 330
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "edited.json"
            patch.save(target)
            self.assertEqual(expected, json.loads(target.read_text()))
            self.assertEqual(expected, Patch.load(target, catalog=self.catalog).to_dict())
        self.assertEqual(original, json.loads(source.read_text()))

    def test_unknown_fields_and_guid_spelling_survive(self):
        original = json.loads((self.data / "valid-leaf.json").read_text())
        graph = original["rootGraph"]
        graph["future"] = {"data": [1, "x", None]}
        graph["nodes"][0]["future"] = {"enabled": True}
        edge = graph["connections"][0]
        # Native fixture IDs use mostly digits; force alphabetic GUID digits.
        old_node = edge["source"]["nodeId"]
        old_port = edge["source"]["portId"]
        node_id = "abcdef01-2345-6789-abcd-ef0123456789"
        port_id = "abcdef02-2345-6789-abcd-ef0123456789"
        node = next(n for n in graph["nodes"] if n["id"] == old_node)
        node["id"] = node_id
        next(p for p in node["ports"] if p["id"] == old_port)["id"] = port_id
        for layout in original["canvasLayouts"]:
            for position in layout["positions"]:
                if position["nodeId"] == old_node:
                    position["nodeId"] = node_id
        edge["source"]["nodeId"] = node_id.upper()
        edge["source"]["portId"] = port_id.upper()
        edge["source"]["future"] = 4
        original["future"] = "untouched"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "source.json"
            path.write_text(json.dumps(original))
            patch = Patch.load(path, catalog=self.catalog)
            self.assertEqual(original, patch.to_dict())
            osc = patch.get_node_by_id(edge["source"]["nodeId"])
            out = patch.get_node_by_id(edge["destination"]["nodeId"])
            with self.assertRaises(ValueError):
                patch.connect(osc, "signal-out", out, "audio-in")
            self.assertEqual(original, patch.to_dict())
            copy = patch.to_dict()
            copy["rootGraph"]["future"]["data"].clear()
            self.assertEqual(original, patch.to_dict())
            patch.save(path)
            self.assertEqual(original, json.loads(path.read_text()))

    def test_reject_invalid_load_without_touching_source(self):
        for name in ("invalid-version.json", "invalid-tempo.json", "invalid-endpoint.json"):
            path = self.data / name
            before = path.read_bytes()
            with self.assertRaises(PatchValidationError):
                Patch.load(path, catalog=self.catalog)
            self.assertEqual(before, path.read_bytes())
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "bad.json"
            for payload in ('{"schemaVersion": 1, "schemaVersion": 1}', '{"x": NaN}'):
                path.write_text(payload)
                with self.assertRaises(ValueError):
                    Patch.load(path, catalog=self.catalog)
                self.assertEqual(payload, path.read_text())

    def test_append_to_loaded_graph(self):
        patch = Patch.load(self.data / "valid-leaf.json", catalog=self.catalog)
        old = patch.to_dict()
        oscillator = patch.add_node("generator.oscillator", frequency=110)
        target = next(n for n in patch.list_nodes() if n.type_key == "generator.oscillator"
                      and n is not oscillator)
        patch.connect(oscillator, "signal-out", target, "frequency-modulation-in", amount=2)
        new = patch.to_dict()
        self.assertEqual(old["canvasLayouts"], new["canvasLayouts"])
        self.assertEqual(old["rootGraph"]["nodes"], new["rootGraph"]["nodes"][:-1])
        self.assertEqual(old["rootGraph"]["connections"], new["rootGraph"]["connections"][:-1])
        self.assertTrue(patch.validate().is_valid)
