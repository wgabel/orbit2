# Worked drone patch

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [composite nodes](../editor/composite-nodes.md) · [persistence and history](../editor/persistence-and-history.md) · [pitch control](../instruments/pitch-control.md).

For using drones as layers of a longer musical piece, see [generative ambient composition](generative-ambient-composition.md).

## 12. Worked patch example

A concrete example that can be used as a reference for the first-to-later implementation.

### Root patch

- External sine generator acting as LFO at 0.1 Hz.
- Drone A composite node exposing Pitch, Volume, Cutoff and Reverb mix.
- Drone B composite node exposing similar controls.
- Master mixer/output.

The external LFO can affect Drone A pitch with a depth of +/-20 cents and Drone B volume with a different mapping. Each drone has independent internal generators and state, but both take timing from the same audio engine.

### Drone A internal graph

- Input Rack: Pitch, Volume, Cutoff, Reverb mix.
- Sine A: base 110 Hz.
- Triangle B: base 220 Hz.
- Noise source: low level.
- Internal LFO at 0.2 Hz modulating the filter cutoff.
- Mixer sums oscillator outputs with gain staging.
- Low-pass filter.
- Reverb.
- Gain processor.
- Output Rack: Audio (stereo, with channel adaptation as required).

~~~mermaid
flowchart TD
    IR["Input rack: Pitch"] --> A[Sine 110 Hz]
    IR --> B[Triangle 220 Hz]
    A --> M[Mixer]
    B --> M
    N[Noise] --> M
    M --> F[Low-pass]
    L["Internal LFO"] --> F
    F --> R[Reverb]
    R --> G[Gain]
    G --> OR[Output rack]
~~~

Other exposed inputs connect to the matching cutoff, reverb mix and gain parameter sockets. The pitch input maps to cents for both pitched generators so their intervals remain intact.

### Illustrative persisted graph

The following JSON is a compact illustrative schema only; a production schema uses stable IDs uniformly (examples use short strings for readability). It shows the idea of recording nodes, parameters, connections and boundary definitions rather than serializing live browser objects.

~~~json
{
  "schemaVersion": 1,
  "name": "Two-sine modulation proof",
  "transport": { "beatsPerMinute": 120 },
  "rootGraph": {
    "id": "root",
    "nodes": [
      {
        "id": "lfo",
        "type": "generator.oscillator",
        "parameters": { "frequency": 0.5 }
      },
      {
        "id": "oscillator",
        "type": "generator.oscillator",
        "parameters": { "frequency": 440, "amplitude": 0.1 }
      },
      {
        "id": "output",
        "type": "output.master"
      }
    ],
    "connections": [
      {
        "id": "mod-route",
        "source": { "nodeId": "lfo", "portId": "signal-out" },
        "destination": {
          "nodeId": "oscillator",
          "portId": "frequency-modulation-in"
        },
        "amount": 100,
        "amountUnit": "hertz"
      },
      {
        "id": "audio-route",
        "source": { "nodeId": "oscillator", "portId": "signal-out" },
        "destination": { "nodeId": "output", "portId": "audio-in" }
      }
    ]
  },
  "canvasLayouts": {
    "root": {
      "lfo": { "x": 20, "y": 80 },
      "oscillator": { "x": 250, "y": 80 },
      "output": { "x": 480, "y": 80 }
    }
  }
}
~~~

A composite node would additionally contain its nested graph and the shared exposed port definitions. Persist data, not C# interface implementations/delegates or live DSP runtime instances. Use a type registry and versioned DTOs to deserialize.
