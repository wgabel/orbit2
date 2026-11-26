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
