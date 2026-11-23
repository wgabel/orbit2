# Graph model and fundamental concepts

[Architecture overview](../architecture/overview.md) · [Roadmap](../roadmap/overview.md)

Related: [signals and ports](signals-and-ports.md) · [commands and events](commands-and-events.md) · [composite nodes](../editor/composite-nodes.md).

## 2. Fundamental concepts

| Concept | Definition |
|---|---|
| Signal | A stream of values indexed by time/sample frame. |
| Sample | A numerical value for one channel at a particular sample frame. |
| Sample frame | One time position containing a sample for each channel. |
| Node | A graph object with a stable identity, ports and parameters. |
| Generator | A node that produces a signal, e.g. sine oscillator, noise generator or clock. |
| Processor | A node that transforms one or more incoming signals, e.g. filter, gain, delay, mixer or reverb. |
| Modulator | A role: any suitable generator or processor whose output influences a parameter. |
| Parameter | A configurable object with a base value, metadata and, where supported, incoming modulation. |
| Port | A named, typed endpoint through which a signal enters or leaves a node. |
| Connection | An edge linking one output port to one input port. |
| SignalGraph | The model owning the nodes, connections and graph invariants. |
| Composite node | A node that contains an internal graph and exposes selected ports at its boundary. |
| Input Rack | The internal representation of a composite node's public inputs. |
| Output Rack | The internal representation of a composite node's public outputs. |
| AudioClock | The sample-frame and audio-time reference. |
| Transport | Musical time: play position, tempo, beats, bars and synchronization. |
| Phase accumulator | The evolving phase of an oscillator cycle. |
| Patch | A complete root graph with its nested graphs, settings and layout. |

The term **modulator** does not need to be a separate mandatory inheritance hierarchy. A sine generator at 440 Hz generates audible sound; the same generator at 0.5 Hz can modulate another signal. A filter can process an audible signal or a modulation signal. A low-pass-filtered noise source can create smoothly varying random modulation without requiring an entirely separate random-modulation architecture.

~~~mermaid
classDiagram
    class ISignalNode {
        Id
        Parameters
        Inputs
        Outputs
    }
    ISignalNode <|-- IGenerator
    ISignalNode <|-- IProcessor
    ISignalNode <|-- ICompositeNode
    IGenerator <|-- OscillatorGenerator
    IGenerator <|-- NoiseGenerator
    IProcessor <|-- Filter
    IProcessor <|-- Reverb
    IProcessor <|-- Mixer
~~~

Examples of node families (both implemented and planned; see the roadmap for delivery status):

- Generators: sine, triangle, band-limited sawtooth, square/pulse, noise, random, envelope, stepper, clock.
- Processors: gain, mixer, pan, low-pass/high-pass filters, EQ, delay, reverb, chorus, flanger, compressor, distortion, signal mapping, rectification.
- Composites: drone, effects rack, sequencer, reusable modulation system, complete synthesizer voice.

An ADSR envelope can be a generator with trigger/gate inputs. The distinction between generator and processor is useful for describing behavior, but the graph should rely on actual port contracts rather than assuming all generators have no inputs.
