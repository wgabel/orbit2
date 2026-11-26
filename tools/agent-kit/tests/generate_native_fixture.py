"""Generate a real Python-produced graph for independent native import in CI."""
import sys
from orbit2 import Catalog, Patch

catalog = Catalog.load(sys.argv[1])
patch = Patch.create("Python builder smoke", catalog=catalog, tempo=92)
lfo = patch.add_node("generator.oscillator", frequency=0.5)
oscillator = patch.add_node("generator.oscillator", frequency=220, amplitude=0.3)
output = patch.add_node("output.master")
patch.connect(lfo, "signal-out", oscillator, "frequency-modulation-in", amount=2)
patch.connect(oscillator, "signal-out", output, "audio-in", amount=0.2)
patch.save(sys.argv[2])

# Independent native import of an edited, native-origin patch with saved layout.
if len(sys.argv) > 3:
    from pathlib import Path
    native = Patch.load(Path(sys.argv[1]) / "valid-leaf.json", catalog=catalog)
    oscillator = next(node for node in native.list_nodes()
                      if node.type_key == "generator.oscillator")
    oscillator.set("frequency", 330)
    native.save(sys.argv[3])

    if len(sys.argv) > 4:
        output = next(node for node in native.list_nodes() if node.type_key == "output.master")
        native.remove_node(oscillator)
        replacement = native.add_node("generator.oscillator", frequency=220)
        edge = native.connect(replacement, "signal-out", output, "audio-in", amount=0.1)
        native.set_connection_amount(edge, 0.25)
        native.save(sys.argv[4])

# A distinct native-import fixture exercises note, tie, rest and expression fields.
if len(sys.argv) > 5:
    from orbit2 import NoteStep, configure_note_pattern
    sequencer = patch.add_node("generator.note-sequencer")
    configure_note_pattern(sequencer, [NoteStep(60, 0.6, 0.7, -0.05),
                           NoteStep("hold"), NoteStep("rest"), NoteStep(64, velocity=0)],
                           articulation=1, division=2, reference_note=60)
    patch.save(sys.argv[5])
