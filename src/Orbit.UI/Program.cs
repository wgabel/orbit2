using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Application.Services;
using Orbit.UI;
using Orbit.Application.Interfaces;
using Orbit.Infrastructure.Audio;
using Orbit.Audio.Compilation;
using Orbit.Audio.Playback;
using Orbit.Audio.Analysis;
using Orbit.Application.Persistence;
using Orbit.Infrastructure.Persistence;
using Orbit.Application.Graphs;
using Orbit.Application.Presets;
using Orbit.Infrastructure.Presets;
using Orbit.UI.Components.Analysis;

// Application entry point and composition root. Blazor's browser bootstrap
// script loads the .NET runtime, then runs these top-level C# statements.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Mount the App component into the #app element defined in wwwroot/index.html.
builder.RootComponents.Add<App>("#app");

// Register the browser-specific audio adapter behind the application contract.
// Both ports resolve to the same adapter and therefore the same browser context.
builder.Services.AddSingleton<BrowserAudioEngine>();
builder.Services.AddSingleton<IAudioEngine>(services => services.GetRequiredService<BrowserAudioEngine>());
builder.Services.AddSingleton<IAudioClock>(services => services.GetRequiredService<BrowserAudioEngine>());
builder.Services.AddSingleton<ITransportSignalSink>(services => services.GetRequiredService<BrowserAudioEngine>());
builder.Services.AddSingleton<IMusicalTransportService, MusicalTransportService>();
var emitters = BuiltInFaustEmitters.Create();
builder.Services.AddSingleton(new FaustGraphCompiler(emitters));
builder.Services.AddSingleton<IGraphModulationRangeEstimator>(new GraphModulationRangeEstimator(emitters));
builder.Services.AddSingleton<FaustGraphPlaybackService>();
builder.Services.AddSingleton<IGraphPlaybackService>(services => services.GetRequiredService<FaustGraphPlaybackService>());
builder.Services.AddSingleton<ISignalAnalysisService>(services => services.GetRequiredService<FaustGraphPlaybackService>());
builder.Services.AddSingleton(new ScopeBrowserAssets(BrowserAudioEngine.AudioModulePath));
builder.Services.AddSingleton<BrowserPatchStorage>();
builder.Services.AddSingleton<IPatchStorage>(services => services.GetRequiredService<BrowserPatchStorage>());
builder.Services.AddSingleton<IPatchLibraryStorage>(services => services.GetRequiredService<BrowserPatchStorage>());
builder.Services.AddSingleton<PatchLibraryService>();
builder.Services.AddSingleton<PatchDocumentSerializer>();
builder.Services.AddTransient<PatchPersistenceService>();
builder.Services.AddSingleton(BuiltInNodes.CreateEditorRegistry());
builder.Services.AddSingleton<INodePresetCatalog, EmbeddedNodePresetCatalog>();
builder.Services.AddSingleton<NodePresetValidator>();
builder.Services.AddSingleton<NodePresetService>();
builder.Services.AddSingleton<INodePresetStorage>(services => services.GetRequiredService<BrowserPatchStorage>());
builder.Services.AddSingleton<NodePresetLibraryService>();

// Start the host, resolve dependencies and render the registered root component.
var host = builder.Build();
// Validate the shipped catalog before rendering; the workspace loads browser-local presets.
_ = host.Services.GetRequiredService<NodePresetService>();
await host.RunAsync();
