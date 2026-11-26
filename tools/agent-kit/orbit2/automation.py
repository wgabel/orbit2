"""Configure native envelope automation; arbitrary breakpoint curves are unsupported."""
from .patch import Node

_TYPE = "generator.envelope"
_SETTINGS = {
    "attack_ms": "attack", "decay_ms": "decay", "release_ms": "release",
    "rest_ms": "rest", "attack_curve": "attack-curve",
    "decay_curve": "decay-curve", "release_curve": "release-curve",
    "sustain": "sustain", "trigger_source": "trigger-source",
}


def _mode_choices(node):
    if not isinstance(node, Node) or node.type_key != _TYPE:
        raise ValueError("Expected a native Envelope node")
    return {choice["label"].lower(): choice["value"]
            for choice in node._definitions["mode"]["choices"]}


def configure_envelope(node, *, mode=None, **settings):
    """Apply a partial native envelope configuration atomically.

    Durations are milliseconds; curves are native exponents. Omitted values,
    including dormant mode-specific settings, remain unchanged.
    """
    modes = _mode_choices(node)
    unknown = settings.keys() - _SETTINGS.keys()
    if unknown:
        raise ValueError(f"Unsupported envelope settings: {sorted(unknown)}")
    values = {_SETTINGS[key]: value for key, value in settings.items()}
    if mode is not None:
        if not isinstance(mode, str) or mode.lower() not in modes:
            raise ValueError(f"Envelope mode must be one of {sorted(modes)}")
        values["mode"] = modes[mode.lower()]
    node.set_parameters(values)


def read_envelope(node):
    """Return all native base settings, including dormant settings."""
    modes = _mode_choices(node)
    value = node.get("mode")
    mode = next(label for label, code in modes.items() if code == value)
    return {"mode": mode, **{name: node.get(key) for name, key in _SETTINGS.items()}}
