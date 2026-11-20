using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Application.Services;
using Orbit.UI;
using Orbit.UI.Infrastructure;

// Application entry point and composition root. Blazor's browser bootstrap
// script loads the .NET runtime, then runs these top-level C# statements.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Mount the App component into the #app element defined in wwwroot/index.html.
builder.RootComponents.Add<App>("#app");

// Wire the application contract to its implementation. Blazor injects the
// service into App.razor.cs and shares one instance within this client host.
builder.Services.AddSingleton<ICounterService, CounterService>();

// Register the browser-specific audio adapter behind the application contract.
builder.Services.AddSingleton<IAudioService, BrowserAudioService>();

// Start the host, resolve dependencies and render the registered root component.
await builder.Build().RunAsync();
