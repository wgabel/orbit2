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
        replacement = native.add_node("generator.oscillator", frequency=220)
        edge = native.connect(replacement, "signal-out", output, "audio-in", amount=0.1)
        native.set_connection_amount(edge, 0.25)
        native.remove_node(oscillator)
        native.save(sys.argv[4])
