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

# Exercise native loop automation, with a dormant event route retained.
if len(sys.argv) > 6:
    from orbit2 import configure_envelope
    automation_patch = Patch.create("Python envelope smoke", catalog=catalog)
    env = automation_patch.add_node("generator.envelope")
    clock = automation_patch.add_node("generator.clock")
    carrier = automation_patch.add_node("generator.oscillator", amplitude=0.1)
    master = automation_patch.add_node("output.master")
    configure_envelope(env, mode="ad", trigger_source=1)
    automation_patch.connect(clock, "clock-out", env, "event-trigger-in")
    automation_patch.connect(env, "envelope-out", carrier, "amplitude-modulation-in", amount=0.5)
    automation_patch.connect(carrier, "signal-out", master, "audio-in", amount=0.2)
    configure_envelope(env, mode="loop", attack_ms=250, decay_ms=1200,
                       rest_ms=500, attack_curve=2, decay_curve=3)
    automation_patch.save(sys.argv[6])

# Native import of multiple signed modulation sources and dormant pitch routing.
if len(sys.argv) > 7:
    modulation_patch = Patch.create("Python modulation smoke", catalog=catalog)
    a = modulation_patch.add_node("generator.oscillator", frequency=0.5)
    b = modulation_patch.add_node("generator.oscillator", frequency=0.8)
    target = modulation_patch.add_node("generator.oscillator", frequency=220)
    master = modulation_patch.add_node("output.master")
    modulation_patch.modulate(a, "signal-out", target, "frequency", amount=8)
    modulation_patch.modulate(b, "signal-out", target, "frequency", amount=-3)
    modulation_patch.modulate(a, "signal-out", target, "pitch-offset", amount=1,
                              allow_dormant=True)
    modulation_patch.connect(target, "signal-out", master, "audio-in", amount=0.2)
    modulation_patch.save(sys.argv[7])
