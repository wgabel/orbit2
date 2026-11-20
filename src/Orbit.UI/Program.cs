using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Application.Services;
using Orbit.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<ICounterService, CounterService>();

await builder.Build().RunAsync();
