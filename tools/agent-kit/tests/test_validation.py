"""Compare the offline validator with the native contract fixture manifest."""
from copy import deepcopy
import json
import os
from pathlib import Path
import tempfile
import unittest
from uuid import uuid4

from orbit2 import Catalog, Patch, PatchValidationError, validate_patch


class ValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = Path(os.environ["ORBIT_AGENT_KIT_DATA"])
        cls.catalog = Catalog.load(cls.data)
        cls.contract = json.loads((cls.data / "patch-contract.json").read_text())

    def test_deferred_macro_contract_is_native_shape_not_offline_support(self):
        feature = self.contract["deferredFeatures"]["compositeMacroModulation"]
        self.assertEqual("modulationInputId", feature["field"])
        self.assertTrue(feature["nativePersistence"])
        self.assertFalse(feature["runtimeModulation"])
        self.assertFalse(feature["offlineAuthoring"])
        self.assertFalse(feature["offlineEditing"])
        for fixture in self.contract["deferredFixtures"]:
            with self.subTest(fixture=fixture["file"]):
                document = json.loads((self.data / fixture["file"]).read_text())
                original = deepcopy(document)
                wrapper = next(n for n in document["rootGraph"]["nodes"] if n["composite"] is not None)
                macro = wrapper["composite"]["macros"][0]
                if fixture["file"] == "deferred-macro-input.json":
                    self.assertEqual(wrapper["composite"]["inputs"][0]["id"], macro["modulationInputId"])
                else:
                    self.assertNotIn("modulationInputId", macro)
                result = validate_patch(document, self.catalog)
                self.assertFalse(result.is_valid)
                self.assertIn(fixture["offlineRejection"], {d.code for d in result.errors})
                self.assertEqual(original, document)

    def test_native_fixture_manifest(self):
        for fixture in self.contract["fixtures"]:
            with self.subTest(fixture=fixture["file"]):
                document = json.loads((self.data / fixture["file"]).read_text())
                original = deepcopy(document)
                result = validate_patch(document, self.catalog)
                self.assertEqual(fixture["accepted"], result.is_valid, result.errors)
                if not fixture["accepted"]:
                    self.assertIn(fixture["rejection"], {d.code for d in result.errors})
                self.assertEqual(original, document)

    def test_every_leaf_and_structured_errors(self):
        patch = Patch.create("Validation", catalog=self.catalog)
        for key in self.catalog.list_nodes():
            if self.catalog.describe(key)["metadataStatus"] == "leaf-definition":
                patch.add_node(key)
        self.assertTrue(patch.validate().is_valid, patch.validate().errors)
        patch = Patch.create("Errors", catalog=self.catalog)
        osc = patch.add_node("generator.oscillator")
        output = patch.add_node("output.master")
        patch.connect(osc, "signal-out", output, "audio-in")
        doc = patch.to_dict()
        node = doc["rootGraph"]["nodes"][0]
        param = next(p for p in node["parameters"] if p["key"] == "frequency")
        param["baseValue"] = -1
        node["ports"].append(deepcopy(node["ports"][0]))
        edge = deepcopy(doc["rootGraph"]["connections"][0])
        edge["id"] = str(uuid4())
        doc["rootGraph"]["connections"].append(edge)
        before = deepcopy(doc)
        result = validate_patch(doc, self.catalog)
        self.assertFalse(result.is_valid)
        self.assertEqual(before, doc)
        self.assertTrue({"parameter-value", "definition-key", "identity", "routing"}
                        <= {d.code for d in result.errors}, result.errors)
        with self.assertRaises(PatchValidationError) as caught:
            result.raise_for_errors()
        self.assertEqual(result.errors, caught.exception.diagnostics)

    def test_cycle_and_case_insensitive_guid_resolution(self):
        patch = Patch.create("Cycle", catalog=self.catalog)
        a = patch.add_node("generator.oscillator")
        b = patch.add_node("generator.oscillator")
        patch.connect(a, "signal-out", b, "frequency-modulation-in")
        doc = patch.to_dict()
        edge = doc["rootGraph"]["connections"][0]
        edge["source"]["nodeId"] = edge["source"]["nodeId"].upper()
        self.assertTrue(validate_patch(doc, self.catalog).is_valid)
        nodes = doc["rootGraph"]["nodes"]
        def endpoint(node, key):
            return {"nodeId": node["id"], "portId": next(p["id"] for p in node["ports"] if p["key"] == key)}
        doc["rootGraph"]["connections"].append({"id": str(uuid4()),
            "source": endpoint(nodes[1], "signal-out"),
            "destination": endpoint(nodes[0], "frequency-modulation-in"), "amount": 1})
        self.assertIn("routing", {d.code for d in validate_patch(doc, self.catalog).errors})

    def test_malformed_and_unsupported_shapes(self):
        for document in (None, [], {}, {"schemaVersion": 1, "rootGraph": []}):
            self.assertFalse(validate_patch(document, self.catalog).is_valid)
        patch = Patch.create("Unsupported", catalog=self.catalog)
        patch.add_node("generator.oscillator")
        doc = patch.to_dict()
        doc["rootGraph"]["nodes"][0]["composite"] = {}
        self.assertIn("unsupported-composite", {d.code for d in validate_patch(doc, self.catalog).errors})

    def test_save_rejects_invalid_before_replacing_file(self):
        patch = Patch.create("Atomic", catalog=self.catalog)
        patch.add_node("generator.oscillator")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "patch.json"
            patch.save(path)
            previous = path.read_bytes()
            patch._tempo = float("nan")
            with self.assertRaises(PatchValidationError):
                patch.save(path)
            self.assertEqual(previous, path.read_bytes())
