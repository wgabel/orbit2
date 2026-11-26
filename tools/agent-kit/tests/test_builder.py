"""Integration-oriented Python tests driven by the native exported contracts."""
import os
from pathlib import Path
import tempfile
import unittest
from uuid import UUID
import json

from orbit2 import Catalog, Patch


class BuilderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = Catalog.load(os.environ["ORBIT_AGENT_KIT_DATA"])

    def setUp(self):
        self.patch = Patch.create("Builder test", catalog=self.catalog)

    def test_every_leaf_has_complete_distinct_native_identities(self):
        ids = set()
        for key in self.catalog.list_nodes():
            definition = self.catalog.describe(key)
            if definition["metadataStatus"] != "leaf-definition":
                with self.assertRaises(ValueError):
                    self.patch.add_node(key)
                continue
            node = self.patch.add_node(key)
            saved = self.patch.to_dict()["rootGraph"]["nodes"][-1]
            self.assertEqual(node.id, saved["id"])
            self.assertEqual(definition["persistedTypeKey"], saved["typeKey"])
            self.assertEqual({p["key"]: p["factoryBaseValue"] for p in definition["parameters"]},
                             {p["key"]: p["baseValue"] for p in saved["parameters"]})
            self.assertEqual({p["key"] for p in definition["ports"]},
                             {p["key"] for p in saved["ports"]})
            for item in [saved, *saved["ports"], *saved["parameters"]]:
                self.assertNotEqual(UUID(item["id"]).int, 0)
                self.assertNotIn(item["id"], ids)
                ids.add(item["id"])

    def test_failed_edits_are_atomic(self):
        oscillator = self.patch.add_node("generator.oscillator", alias="osc")
        output = self.patch.add_node("output.master")
        self.assertIs(oscillator, self.patch.get_node("osc"))
        before = self.patch.to_dict()
        with self.assertRaises(ValueError):
            oscillator.set_parameters({"frequency": 110, "missing": 1})
        with self.assertRaises(ValueError):
            oscillator.set("frequency", float("nan"))
        with self.assertRaises(ValueError):
            oscillator.set("waveform", 0.5)
        with self.assertRaises(ValueError):
            self.patch.add_node("generator.oscillator", frequency=-1)
        with self.assertRaises(ValueError):
            self.patch.connect(output, "audio-in", oscillator, "frequency-modulation-in")
        with self.assertRaises(ValueError):
            self.patch.connect(oscillator, "signal-out", oscillator, "frequency-modulation-in")
        other = Patch.create("Other", catalog=self.catalog).add_node("output.master")
        with self.assertRaises(ValueError):
            self.patch.connect(oscillator, "signal-out", other, "audio-in")
        self.assertEqual(before, self.patch.to_dict())
        self.patch.connect(oscillator, "signal-out", output, "audio-in", amount=0.2)
        before = self.patch.to_dict()
        with self.assertRaises(ValueError):
            self.patch.connect(oscillator, "signal-out", output, "audio-in")
        self.assertEqual(before, self.patch.to_dict())

    def test_save_and_snapshot_are_independent(self):
        node = self.patch.add_node("generator.oscillator", frequency=110)
        snapshot = self.patch.to_dict()
        snapshot["rootGraph"]["nodes"].clear()
        self.assertEqual(1, len(self.patch.to_dict()["rootGraph"]["nodes"]))
        node.set("frequency", 220)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "patch.json"
            self.patch.save(path)
            self.assertEqual(self.patch.to_dict(), json.loads(path.read_text(encoding="utf-8")))

    def test_unknown_version_is_rejected(self):
        with self.assertRaises(ValueError):
            Catalog({"catalogFormatVersion": 999}, {}, {})


if __name__ == "__main__":
    unittest.main()
