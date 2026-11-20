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

    async create(context, processor, parameters, source = null) {
        let name;
        let compiled;
        if (source != null) {
            if (!this.#compiler) throw new Error("Dynamic Faust compiler is unavailable.");
            name = processor;
            compiled = await this.#compiler.compile(source);
        } else {
            const specification = this.#processors.get(processor);
            if (!specification) throw new Error("Unknown audio processor: " + processor);
            name = specification.name;
            compiled = await this.#moduleLoader.load(specification.baseUrl);
        }

        const generator = this.#createGenerator();
        const node = await generator.createNode(
            context, name, { ...compiled, soundfiles: {} }, false);

        if (!node) {
            throw new Error("Could not create Faust AudioWorklet: " + processor);
        }

        try {
            for (const [parameter, value] of Object.entries(parameters)) {
                if (!Number.isFinite(value)) {
                    throw new Error("Invalid parameter value: " + parameter);
                }

                const path = node.getParams().find(
                    candidate => candidate.endsWith("/" + parameter));

                if (!path) {
                    throw new Error("Unknown Faust parameter: " + parameter);
                }

                node.setParamValue(path, value);
            }

            return node;
        } catch (error) {
            node.disconnect();
            node.destroy?.();
            throw error;
        }
    }
}
