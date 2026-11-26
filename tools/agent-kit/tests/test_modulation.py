"""Activation and parameter routing against the generated native catalog."""
import os
from pathlib import Path
import tempfile
import unittest

from orbit2 import Catalog, Patch


class ModulationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = Catalog.load(os.environ["ORBIT_AGENT_KIT_DATA"])

    def setUp(self):
        self.patch = Patch.create("Modulation", catalog=self.catalog)

    def test_associated_parameter_and_structural_selectors(self):
        osc = self.patch.add_node("generator.oscillator")
        self.assertTrue(osc.is_port_active("frequency-modulation-in"))
        self.assertFalse(osc.is_parameter_active("pitch-offset"))
        self.assertFalse(osc.is_port_active("pitch-modulation-in"))
        osc.set("pitch-tracking", 1)
        self.assertTrue(osc.is_port_active("pitch-modulation-in"))
        osc.set("timing", 1)
        self.assertFalse(osc.is_port_active("pitch-modulation-in"))
        self.assertFalse(osc.is_port_active("frequency-modulation-in"))
        self.assertTrue(osc.is_port_active("amplitude-modulation-in"))
        active = {p["key"] for p in osc.list_ports(active_only=True)}
        self.assertNotIn("pitch-modulation-in", active)
        self.assertIn("signal-out", active)
        p = osc.describe_parameter("frequency")
        self.assertEqual("Hertz", p["unit"])
        self.assertFalse(p["isActive"])
        p["activeWhen"].clear()
        self.assertFalse(osc.is_parameter_active("frequency"))

    def test_envelope_alternatives_and_direct_port_conditions(self):
        env = self.patch.add_node("generator.envelope")
        self.assertTrue(env.is_port_active("signal-trigger-in"))
        self.assertFalse(env.is_port_active("gate-in"))
        env.set("trigger-source", 2)
        self.assertTrue(env.is_port_active("gate-in"))
        env.set("mode", 1)
        self.assertTrue(env.is_port_active("gate-in"))
        self.assertFalse(env.is_port_active("decay-modulation-in"))
        env.set("mode", 2)
        self.assertTrue(env.is_port_active("gate-in"))
        self.assertTrue(env.is_port_active("decay-modulation-in"))
        env.set("mode", 3)
        self.assertFalse(env.is_port_active("gate-in"))
        self.assertTrue(env.is_port_active("rest-modulation-in"))
        self.assertFalse(env.is_port_active("release-modulation-in"))

    def test_multiple_sources_dormant_routes_and_atomic_rejection(self):
        a = self.patch.add_node("generator.oscillator")
        b = self.patch.add_node("generator.oscillator")
        target = self.patch.add_node("generator.oscillator", frequency=220)
        first = self.patch.modulate(a, "signal-out", target, "frequency", amount=8)
        second = self.patch.modulate(b, "signal-out", target, "frequency", amount=-3)
        before = self.patch.to_dict()
        for parameter in ("pitch-offset", "waveform", "unknown", "frequency"):
            with self.assertRaises(ValueError):
                self.patch.modulate(a, "signal-out", target, parameter)
            self.assertEqual(before, self.patch.to_dict())
        with self.assertRaises(ValueError):
            self.patch.modulate(target, "signal-out", a, "frequency")
        self.assertEqual(before, self.patch.to_dict())
        pitch = self.patch.modulate(a, "signal-out", target, "pitch-offset",
                                    amount=1, allow_dormant=True)
        self.assertTrue(self.patch.is_connection_active(first.upper()))
        self.assertTrue(self.patch.is_connection_active(second))
        self.assertFalse(self.patch.is_connection_active(pitch))
        edges = self.patch.list_connections()
        target.set("pitch-tracking", 1)
        self.assertTrue(self.patch.is_connection_active(pitch))
        target.set("timing", 1)
        self.assertFalse(self.patch.is_connection_active(first))
        self.assertFalse(self.patch.is_connection_active(pitch))
        self.assertEqual(edges, self.patch.list_connections())
        self.assertEqual(220, target.get("frequency"))
        self.assertTrue(self.patch.validate().is_valid)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "routes.json"
            self.patch.save(path)
            loaded = Patch.load(path, catalog=self.catalog)
            self.assertEqual(self.patch.to_dict(), loaded.to_dict())
            self.assertFalse(loaded.is_connection_active(pitch))
