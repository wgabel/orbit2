"""Native fixed-slot monophonic note-pattern adapter (no playback simulation)."""
from dataclasses import dataclass

from .patch import Node

_TYPE = "generator.note-sequencer"
_FAMILIES = ("pattern-step-", "pattern-step-gate-length-",
             "pattern-step-velocity-", "pattern-step-timing-offset-")
_SETTINGS = {
    "reference_note": "reference-note", "initial_pitch": "initial-pitch",
    "timing": "pattern-timing", "division": "pattern-division",
    "multiplier": "pattern-multiplier", "seconds": "pattern-seconds",
    "glide": "pattern-glide", "articulation": "pattern-articulation",
    "gate_length": "pattern-gate-length", "velocity": "pattern-velocity",
}


@dataclass(frozen=True)
class NoteStep:
    """MIDI 0..127, 'hold' or 'rest'; None overrides inherit native defaults."""
    note: int | str
    gate_length: float | None = None
    velocity: float | None = None
    timing_offset: float = 0


def _check_node(node):
    if not isinstance(node, Node) or node.type_key != _TYPE:
        raise ValueError("Expected a native Note Sequencer node")


def _keys(index):
    return tuple(f"{prefix}{index:02d}" for prefix in _FAMILIES)


def _encode(step):
    if not isinstance(step, NoteStep):
        raise ValueError("Each step must be a NoteStep")
    if type(step.note) is int and 0 <= step.note <= 127:
        code = step.note + 1
    elif step.note == "hold":
        code = 0
    elif step.note == "rest":
        code = 129
    else:
        raise ValueError("Step note must be MIDI 0..127, hold or rest")
    if step.gate_length is not None and step.gate_length == 0:
        raise ValueError("Use None to inherit gate length")
    if step.velocity is not None and step.velocity == -1:
        raise ValueError("Use None to inherit velocity")
    return (code, 0 if step.gate_length is None else step.gate_length,
            -1 if step.velocity is None else step.velocity, step.timing_offset)


def configure_note_pattern(node, steps, **settings):
    """Replace active slots and enable Loop; retain hidden slots and unspecified settings.

    Settings are native numeric values, validated by the exported definitions.
    Explicit overrides on hold/rest slots are stored but ignored by native playback.
    """
    _check_node(node)
    unknown = settings.keys() - _SETTINGS.keys()
    if unknown:
        raise ValueError(f"Unknown pattern settings: {sorted(unknown)}")
    steps = tuple(steps)
    capacity = int(node._definitions["pattern-count"]["maximum"])
    if not 1 <= len(steps) <= capacity:
        raise ValueError(f"Pattern must have 1..{capacity} steps")
    values = {_SETTINGS[key]: value for key, value in settings.items()}
    values.update({"pattern-loop": 1, "pattern-count": len(steps)})
    for index, step in enumerate(steps, 1):
        values.update(zip(_keys(index), _encode(step)))
    # Shared atomic native-bound and choice checks; never round or clamp here.
    node.set_parameters(values)


def read_note_pattern(node):
    """Read active slots as immutable NoteStep values, including dormant overrides."""
    _check_node(node)
    steps = []
    for index in range(1, int(node.get("pattern-count")) + 1):
        code, gate, velocity, timing = (node.get(key) for key in _keys(index))
        note = "hold" if code == 0 else "rest" if code == 129 else int(code) - 1
        steps.append(NoteStep(note, None if gate == 0 else gate,
                              None if velocity == -1 else velocity, timing))
    return tuple(steps)
