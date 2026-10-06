using System.Security.Cryptography;
using System.Text.Json;
using BotBBVA.Contratos;

namespace BotBBVA.Servicio;

/// <summary>Administra configuración, credenciales y último estado del servicio.</summary>
public sealed class EstadoPersistenteServicio
{
    private readonly string _directorio = AppContext.BaseDirectory;
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record Credenciales(string TenantId, string ClientId, string ClientSecret);

    public ConfigOperativa CargarConfiguracion()
    {
        var ruta = Path.Combine(_directorio, "config.json");
        var configuracion = File.Exists(ruta) ? JsonSerializer.Deserialize<ConfigOperativa>(File.ReadAllText(ruta), Opciones) : null;
        configuracion ??= new ConfigOperativa();
        var credenciales = CargarCredenciales();
        if (credenciales is not null) { configuracion.TenantId = credenciales.TenantId; configuracion.ClientId = credenciales.ClientId; configuracion.ClientSecret = credenciales.ClientSecret; }
        return configuracion;
    }

    public void GuardarConfiguracion(ConfigOperativa configuracion)
    {
        var credenciales = new Credenciales(configuracion.TenantId, configuracion.ClientId, configuracion.ClientSecret);
        var copia = JsonSerializer.Deserialize<ConfigOperativa>(JsonSerializer.Serialize(configuracion, Opciones), Opciones) ?? new();
        copia.TenantId = copia.ClientId = copia.ClientSecret = string.Empty;
        File.WriteAllText(Path.Combine(_directorio, "config.json"), JsonSerializer.Serialize(copia, Opciones));
        var datos = JsonSerializer.SerializeToUtf8Bytes(credenciales, Opciones);
        File.WriteAllBytes(Path.Combine(_directorio, "credenciales.dat"), ProtectedData.Protect(datos, null, DataProtectionScope.LocalMachine));
    }

    public void GuardarEstado(EstadoBot estado)
        => File.WriteAllText(Path.Combine(_directorio, "estado.json"), JsonSerializer.Serialize(estado, Opciones));

    public EstadoBot? CargarEstado()
    {
        var ruta = Path.Combine(_directorio, "estado.json");
        return File.Exists(ruta) ? JsonSerializer.Deserialize<EstadoBot>(File.ReadAllText(ruta), Opciones) : null;
    }

    private Credenciales? CargarCredenciales()
    {
        try
        {
            var ruta = Path.Combine(_directorio, "credenciales.dat");
            if (!File.Exists(ruta)) return null;
            var datos = ProtectedData.Unprotect(File.ReadAllBytes(ruta), null, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<Credenciales>(datos, Opciones);
        }
        catch (CryptographicException) { return null; }
    }
}
