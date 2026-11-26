"""Envelope helper tests against native-generated metadata."""
import os
from pathlib import Path
import tempfile
import unittest

from orbit2 import Catalog, Patch, configure_envelope, read_envelope


class AutomationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = Catalog.load(os.environ["ORBIT_AGENT_KIT_DATA"])

    def setUp(self):
        self.patch = Patch.create("Envelope", catalog=self.catalog)
        self.env = self.patch.add_node("generator.envelope")

    def test_modes_and_round_trip_keep_dormant_values_and_wires(self):
        clock = self.patch.add_node("generator.clock")
        osc = self.patch.add_node("generator.oscillator")
        configure_envelope(self.env, mode="AD", trigger_source=1)
        self.patch.connect(clock, "clock-out", self.env, "event-trigger-in")
        self.patch.connect(self.env, "envelope-out", osc, "amplitude-modulation-in")
        before = self.patch.to_dict()["rootGraph"]
        configure_envelope(self.env, mode="ar", release_ms=1700, release_curve=2)
        self.assertEqual("ar", read_envelope(self.env)["mode"])
        configure_envelope(self.env, mode="adsr", sustain=0.7)
        self.assertEqual("adsr", read_envelope(self.env)["mode"])
        configure_envelope(self.env, mode="loop", attack_ms=250, decay_ms=1200,
                           rest_ms=500, attack_curve=2)
        settings = read_envelope(self.env)
        self.assertEqual("loop", settings["mode"])
        self.assertEqual(1700, settings["release_ms"])
        self.assertEqual(0.7, settings["sustain"])
        self.assertEqual(1, settings["trigger_source"])
        self.assertEqual(500, settings["rest_ms"])
        after = self.patch.to_dict()["rootGraph"]
        self.assertEqual(before["connections"], after["connections"])
        self.assertEqual(before["nodes"][0]["ports"], after["nodes"][0]["ports"])
        self.assertEqual([(p["key"], p["id"]) for p in before["nodes"][0]["parameters"]],
                         [(p["key"], p["id"]) for p in after["nodes"][0]["parameters"]])
        self.assertEqual(before["nodes"][1:], after["nodes"][1:])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "envelope.json"
            self.patch.save(path)
            loaded = Patch.load(path, catalog=self.catalog)
            self.assertEqual(self.patch.to_dict(), loaded.to_dict())
            self.assertEqual(settings, read_envelope(loaded.get_node_by_id(self.env.id)))
            settings["attack_ms"] = 100
            self.assertEqual(250, read_envelope(self.env)["attack_ms"])

    def test_reject_unsupported_or_invalid_batch_atomically(self):
        before = self.patch.to_dict()
        for settings in ({"mode": "custom"}, {"points": [(0, 0), (1, 1)]},
                         {"steps": [0, 1]}, {"mode": "loop", "attack_ms": 0},
                         {"attack_ms": 500, "decay_curve": 9}, {"sustain": 1.1},
                         {"trigger_source": 3}, {"rest_ms": float("nan")}):
            with self.subTest(settings=settings):
                with self.assertRaises(ValueError):
                    configure_envelope(self.env, **settings)
                self.assertEqual(before, self.patch.to_dict())
        with self.assertRaises(ValueError):
            configure_envelope(self.patch.add_node("generator.oscillator"), mode="loop")

    def test_partial_edit_preserves_mode_and_omitted_values(self):
        configure_envelope(self.env, mode="adsr", release_ms=750)
        expected = read_envelope(self.env)
        configure_envelope(self.env, attack_ms=100)
        expected["attack_ms"] = 100
        self.assertEqual(expected, read_envelope(self.env))
