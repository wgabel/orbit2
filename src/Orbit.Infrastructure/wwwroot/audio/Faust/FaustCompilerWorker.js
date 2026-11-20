// The bundled package includes libfaust and its standard libraries.
// Import it only when the user first starts a dynamic graph.
import { instantiateFaustModule, LibFaust, FaustCompiler } from "../generated/compiler/index.js";

let compilerPromise;
function getCompiler() {
    if (!compilerPromise) {
        compilerPromise = instantiateFaustModule()
            .then(module => new FaustCompiler(new LibFaust(module)))
            .catch(error => { compilerPromise = undefined; throw error; });
    }
    return compilerPromise;
}

// Serialize calls into libfaust; the vendor compiler caches factories by
// source hash and returns independent WebAssembly modules.
let queue = Promise.resolve();
self.onmessage = event => {
    const { id, source } = event.data;
    queue = queue.then(async () => {
        try {
            const compiler = await getCompiler();
            const factory = await compiler.createMonoDSPFactory("OrbitGraph", source, "");
            if (!factory) throw new Error("Faust returned no factory.");
            // The native factory pointer is worker-local; only the transferable
            // JavaScript representation is usable by the browser node factory.
            self.postMessage({ id, factory: { shaKey: factory.shaKey, module: factory.module, json: factory.json } });
        } catch (error) {
            self.postMessage({ id, error: error instanceof Error ? error.message : String(error) });
        }
    });
};
