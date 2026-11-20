// Creates independent AudioWorklet nodes from registered Faust processors.
// Parameter paths come from the node's metadata, not the Blazor UI.
export class FaustNodeFactory {
    #moduleLoader;
    #processors;
    #createGenerator;

    constructor(moduleLoader, processors, createGenerator) {
        this.#moduleLoader = moduleLoader;
        this.#processors = processors;
        this.#createGenerator = createGenerator;
    }

    async create(context, processor, parameters) {
        const specification = this.#processors.get(processor);
        if (!specification) {
            throw new Error("Unknown audio processor: " + processor);
        }

        const compiled = await this.#moduleLoader.load(specification.baseUrl);
        const generator = this.#createGenerator();
        const node = await generator.createNode(
            context, specification.name, { ...compiled, soundfiles: {} }, false);

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
