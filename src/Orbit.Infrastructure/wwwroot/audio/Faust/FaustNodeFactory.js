// Creates independent AudioWorklet nodes from compiled or dynamic Faust source.
// Parameter paths come from the node's metadata, not the Blazor UI.
export class FaustNodeFactory {
    #moduleLoader;
    #processors;
    #createGenerator;
    #compiler;

    constructor(moduleLoader, processors, createGenerator, compiler) {
        this.#moduleLoader = moduleLoader;
        this.#processors = processors;
        this.#createGenerator = createGenerator;
        this.#compiler = compiler;
    }

    async create(context, processor, parameters, source = null, timings = null) {
        const collecting = () => timings && timings.collecting !== false;
        let name;
        let compiled;
        if (source != null) {
            if (!this.#compiler) throw new Error("Dynamic Faust compiler is unavailable.");
            name = processor;
            const started = collecting() ? globalThis.performance.now() : null;
            try { compiled = await this.#compiler.compile(source); }
            finally { if (started !== null && collecting()) timings.workerRoundTripMs = globalThis.performance.now() - started; }
        } else {
            const specification = this.#processors.get(processor);
            if (!specification) throw new Error("Unknown audio processor: " + processor);
            name = specification.name;
            compiled = await this.#moduleLoader.load(specification.baseUrl);
        }

        const generator = this.#createGenerator();
        const started = collecting() ? globalThis.performance.now() : null;
        let node;
        try { node = await generator.createNode(context, name, { ...compiled, soundfiles: {} }, false); }
        finally { if (started !== null && collecting()) timings.workletCreateMs = globalThis.performance.now() - started; }

        if (!node) {
            throw new Error("Could not create Faust AudioWorklet: " + processor);
        }

        try {
            const bindingStarted = collecting() ? globalThis.performance.now() : null;
            try { this.setParameters(node, parameters); }
            finally { if (bindingStarted !== null && collecting()) timings.parameterBindingMs = globalThis.performance.now() - bindingStarted; }
            return node;
        } catch (error) {
            node.disconnect();
            node.destroy?.();
            throw error;
        }
    }

    // Shared by initialization and live edits. Resolve and validate the entire
    // batch before writing, so an unknown control cannot partially apply it.
    setParameters(node, parameters) {
        const paths = node.getParams();
        const writes = Object.entries(parameters).map(([parameter, value]) => {
            if (!Number.isFinite(value)) throw new Error("Invalid parameter value: " + parameter);
            const matches = paths.filter(candidate => candidate.endsWith("/" + parameter));
            if (matches.length !== 1) throw new Error("Unknown or ambiguous Faust parameter: " + parameter);
            return [matches[0], value];
        });
        for (const [path, value] of writes) node.setParamValue(path, value);
    }
}
