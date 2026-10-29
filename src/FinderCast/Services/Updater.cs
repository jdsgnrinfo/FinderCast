using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace FinderCast.Services;

/// <summary>
/// Actualizaciones desde GitHub Releases: compara la última versión publicada con la que está en
/// marcha y, si hay una nueva, descarga su instalador, comprueba su huella SHA-256 (la que publica
/// GitHub para cada archivo) y lo ejecuta en silencio. El instalador cierra FinderCast, lo
/// sustituye y lo vuelve a abrir.
/// </summary>
internal static class Updater
{
    private const string LatestRelease = "https://api.github.com/repos/jdsgnrinfo/FinderCast/releases/latest";

    // Solo se descarga de las releases de este repositorio.
    private const string DownloadPrefix = "https://github.com/jdsgnrinfo/FinderCast/releases/download/";

    private static readonly HttpClient Http = CreateClient();

    /// <summary>Una versión publicada con su instalador.</summary>
    public sealed record Release(Version Version, string SetupUrl, string Sha256);

    /// <summary>La versión que está en marcha (mayor.menor.revisión).</summary>
    public static Version Current { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    /// <summary>La última versión publicada si es más nueva que esta; null si no hay ninguna.</summary>
    /// <exception cref="HttpRequestException">Sin conexión o GitHub no responde.</exception>
    /// <exception cref="InvalidDataException">La release no trae un instalador válido.</exception>
    public static async Task<Release?> FindNewerAsync()
    {
        using var response = await Http.GetAsync(LatestRelease);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) throw new InvalidDataException($"Versión no válida: {tag}");
        latest = Normalize(latest);
        if (latest <= Current) return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)) continue;
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            // Sin huella no hay forma de saber que el archivo es el publicado: no se instala.
            if (!url.StartsWith(DownloadPrefix, StringComparison.Ordinal) || !digest.StartsWith("sha256:", StringComparison.Ordinal)) break;
            return new Release(latest, url, digest["sha256:".Length..]);
        }
        throw new InvalidDataException($"La versión {latest} no trae un instalador con huella.");
    }

    /// <summary>
    /// Descarga el instalador a la carpeta temporal, comprueba su huella y lo ejecuta en silencio.
    /// Después hay que cerrar FinderCast: el instalador lo sustituye y lo vuelve a abrir.
    /// </summary>
    /// <exception cref="InvalidDataException">El archivo descargado no coincide con la huella.</exception>
    public static async Task InstallAsync(Release release)
    {
        var folder = Path.Combine(Path.GetTempPath(), "FinderCast-update");
        Directory.CreateDirectory(folder);
        var setup = Path.Combine(folder, $"FinderCast-{release.Version}-setup.exe");

        using (var response = await Http.GetAsync(release.SetupUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(setup);
            await response.Content.CopyToAsync(file);
        }

        string hash;
        await using (var file = File.OpenRead(setup)) hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
        if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(setup);
            throw new InvalidDataException("El instalador descargado no coincide con el publicado.");
        }

        Process.Start(new ProcessStartInfo(setup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // GitHub rechaza las peticiones sin User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"FinderCast/{Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0))}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
