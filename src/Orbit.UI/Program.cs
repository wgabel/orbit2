using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Application.Services;
using Orbit.UI;
using Orbit.Application.Interfaces;
using Orbit.Infrastructure.Audio;
using Orbit.Audio.Compilation;
using Orbit.Audio.Playback;
using Orbit.Audio.Analysis;

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
var emitters = BuiltInFaustEmitters.Create();
builder.Services.AddSingleton(new FaustGraphCompiler(emitters));
builder.Services.AddSingleton<IGraphModulationRangeEstimator>(new GraphModulationRangeEstimator(emitters));
builder.Services.AddSingleton<IGraphPlaybackService, FaustGraphPlaybackService>();

// Start the host, resolve dependencies and render the registered root component.
await builder.Build().RunAsync();
