"""Safe root graph editing with native-generated metadata."""
from copy import deepcopy
import json
import os
from pathlib import Path
import tempfile
import unittest

from orbit2 import Catalog, Patch, PatchValidationError


class StructuralEditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = Path(os.environ["ORBIT_AGENT_KIT_DATA"])
        cls.catalog = Catalog.load(cls.data)

    def test_remove_preserves_unaffected_native_fields(self):
        source = self.data / "valid-leaf.json"
        patch = Patch.load(source, catalog=self.catalog)
        before = patch.to_dict()
        oscillator, output = patch.list_nodes()
        patch.remove_node(oscillator)
        expected = deepcopy(before)
        expected["rootGraph"]["nodes"] = [expected["rootGraph"]["nodes"][1]]
        expected["rootGraph"]["connections"] = []
        for layout in expected["canvasLayouts"]:
            layout["positions"] = [p for p in layout["positions"] if p["nodeId"] != oscillator.id]
        self.assertEqual(expected, patch.to_dict())
        self.assertIs(output, patch.get_node_by_id(output.id))
        with self.assertRaises(ValueError):
            oscillator.set("frequency", 330)
        with self.assertRaises(ValueError):
            patch.remove_node(oscillator)
        self.assertEqual(expected, patch.to_dict())
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "removed.json"
            patch.save(target)
            self.assertEqual(expected, Patch.load(target, catalog=self.catalog).to_dict())

    def test_connection_amount_and_disconnect(self):
        patch = Patch.create("Routes", catalog=self.catalog)
        a = patch.add_node("generator.oscillator", alias="a")
        b = patch.add_node("generator.oscillator")
        output = patch.add_node("output.master")
        first = patch.connect(a, "signal-out", b, "frequency-modulation-in")
        second = patch.connect(b, "signal-out", output, "audio-in")
        initial = patch.to_dict()
        with self.assertRaises(PatchValidationError):
            patch.set_connection_amount(first, float("nan"))
        with self.assertRaises(KeyError):
            patch.disconnect("missing")
        self.assertEqual(initial, patch.to_dict())
        patch.set_connection_amount(first.upper(), -2)
        expected = deepcopy(initial)
        expected["rootGraph"]["connections"][0]["amount"] = -2
        self.assertEqual(expected, patch.to_dict())
        edges = patch.list_connections()
        edges[0]["amount"] = 100
        self.assertEqual(expected, patch.to_dict())
        patch.disconnect(second)
        patch.remove_node(a)
        with self.assertRaises(KeyError):
            patch.get_node("a")
        self.assertEqual((), patch.list_connections())
        self.assertTrue(patch.validate().is_valid)
        self.assertIs(b, patch.get_node_by_id(b.id))

    def test_opaque_fields_block_deletion_but_not_amount_edit(self):
        original = json.loads((self.data / "valid-leaf.json").read_text())
        edge = original["rootGraph"]["connections"][0]
        original["rootGraph"]["nodes"][1]["unknown"] = {"reference": edge["id"]}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "opaque.json"
            path.write_text(json.dumps(original))
            patch = Patch.load(path, catalog=self.catalog)
            with self.assertRaises(PatchValidationError) as failure:
                patch.disconnect(edge["id"])
            self.assertIn("unsupported-deletion-field", {d.code for d in failure.exception.diagnostics})
            with self.assertRaises(PatchValidationError):
                patch.remove_node(patch.list_nodes()[0])
            self.assertEqual(original, patch.to_dict())
            patch.set_connection_amount(edge["id"], 0.4)
            edge["amount"] = 0.4
            self.assertEqual(original, patch.to_dict())

    def test_event_amount_requires_unity(self):
        # Select a real compatible event route from native metadata.
        rules = self.catalog._rules
        unity = {r["destination"] for r in rules["amounts"] if r["rule"] == "unity-only"}
        pairs = {(r["source"], r["destination"]) for r in rules["compatibleSignalPairs"]}
        defs = [(key, self.catalog.describe(key)) for key in self.catalog.list_nodes()]
        outputs = [(k, p) for k, d in defs if d["metadataStatus"] == "leaf-definition"
                   for p in d["ports"] if p["direction"] == "Output"]
        inputs = [(k, p) for k, d in defs if d["metadataStatus"] == "leaf-definition"
                  for p in d["ports"] if p["direction"] == "Input" and p["signalType"] in unity]
        a, src, b, dst = next((a, src, b, dst) for a, src in outputs for b, dst in inputs
                              if (src["signalType"], dst["signalType"]) in pairs
                              and src["channelCount"] == dst["channelCount"])
        patch = Patch.create("Event", catalog=self.catalog)
        edge = patch.connect(patch.add_node(a), src["key"], patch.add_node(b), dst["key"])
        before = patch.to_dict()
        with self.assertRaises(PatchValidationError):
            patch.set_connection_amount(edge, 0.5)
        self.assertEqual(before, patch.to_dict())
