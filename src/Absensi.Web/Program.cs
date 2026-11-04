using Absensi.Web;
using Absensi.Web.Layanan;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton<JamBrowser>();
builder.Services.AddSingleton<StatusAplikasi>();

await builder.Build().RunAsync();
