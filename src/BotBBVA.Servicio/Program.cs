using BotBBVA.Contratos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BotBBVA.Servicio;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "BotBBVA.Servicio");
builder.Services.AddSingleton<EstadoPersistenteServicio>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<EstadoPersistenteServicio>().CargarConfiguracion());
builder.Services.AddSingleton<OrquestadorBot>();
builder.Services.AddSingleton<CanalControlServidor>();
builder.Services.AddHostedService<BotWorker>();
await builder.Build().RunAsync();
