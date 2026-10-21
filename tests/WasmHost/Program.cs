using System.Globalization;
using FlintSoft.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WasmHost;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddFlintSoftComponents();
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
await builder.Build().RunAsync();
