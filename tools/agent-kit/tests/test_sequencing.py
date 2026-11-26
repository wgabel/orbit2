"""Note patterns use the actual native catalog and preserve fixed-slot identities."""
import os
from pathlib import Path
import tempfile
import unittest

from orbit2 import Catalog, Patch, NoteStep, configure_note_pattern, read_note_pattern


class SequencingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = Catalog.load(os.environ["ORBIT_AGENT_KIT_DATA"])

    def setUp(self):
        self.patch = Patch.create("Notes", catalog=self.catalog)
        self.node = self.patch.add_node("generator.note-sequencer")

    def test_round_trip_expression_and_hidden_slots(self):
        self.node.set("pattern-step-64", 72)
        steps = (NoteStep(0, 0.4, 0, -0.05), NoteStep("hold"),
                 NoteStep("rest"), NoteStep(127, velocity=0.75, timing_offset=0.1))
        before = self.patch.to_dict()["rootGraph"]["nodes"][0]
        configure_note_pattern(self.node, steps, timing=1, seconds=0.2, articulation=1)
        self.assertEqual(steps, read_note_pattern(self.node))
        self.assertEqual(1, self.node.get("pattern-loop"))
        self.assertEqual(4, self.node.get("pattern-count"))
        self.assertEqual(72, self.node.get("pattern-step-64"))
        self.assertEqual(1, self.node.get("pattern-step-01"))
        self.assertEqual(129, self.node.get("pattern-step-03"))
        self.assertEqual(128, self.node.get("pattern-step-04"))
        self.assertEqual(-1, self.node.get("pattern-step-velocity-02"))
        self.assertEqual(0, self.node.get("pattern-step-gate-length-02"))
        after = self.patch.to_dict()["rootGraph"]["nodes"][0]
        self.assertEqual([(p["key"], p["id"]) for p in before["parameters"]],
                         [(p["key"], p["id"]) for p in after["parameters"]])
        self.assertEqual(before["ports"], after["ports"])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "notes.json"
            self.patch.save(path)
            loaded = Patch.load(path, catalog=self.catalog)
            self.assertEqual(self.patch.to_dict(), loaded.to_dict())
            self.assertEqual(steps, read_note_pattern(loaded.list_nodes()[0]))

    def test_invalid_pattern_does_not_mutate(self):
        configure_note_pattern(self.node, [NoteStep(60)])
        before = self.patch.to_dict()
        for steps in ([], [NoteStep(60)] * 65, [NoteStep(True)],
                      [NoteStep(128)], [NoteStep("C4")], [NoteStep(60, velocity=0.123)],
                      [NoteStep(60, timing_offset=0.46)], [NoteStep(60, gate_length=0)],
                      [NoteStep(60, velocity=-1)], [NoteStep(60), NoteStep(64, gate_length=2)]):
            with self.subTest(steps=steps):
                with self.assertRaises(ValueError):
                    configure_note_pattern(self.node, steps)
                self.assertEqual(before, self.patch.to_dict())
        for settings in ({"division": 3}, {"unknown": 1}, {"timing": 1, "seconds": 0}):
            with self.assertRaises(ValueError):
                configure_note_pattern(self.node, [NoteStep(62)], **settings)
            self.assertEqual(before, self.patch.to_dict())

    def test_shrink_preserves_hidden_and_unspecified_settings(self):
        configure_note_pattern(self.node, [NoteStep(60), NoteStep(64, velocity=0.5)],
                               articulation=1, division=4)
        configure_note_pattern(self.node, [NoteStep("rest")])
        self.assertEqual((NoteStep("rest"),), read_note_pattern(self.node))
        self.assertEqual(65, self.node.get("pattern-step-02"))
        self.assertEqual(0.5, self.node.get("pattern-step-velocity-02"))
        self.assertEqual(4, self.node.get("pattern-division"))
        self.assertEqual(1, self.node.get("pattern-articulation"))
        with self.assertRaises(ValueError):
            configure_note_pattern(self.patch.add_node("output.master"), [NoteStep(60)])
