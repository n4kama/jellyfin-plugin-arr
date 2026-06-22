using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Arr.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// ControllerBase also has a property named MetadataProvider, so alias the enum to avoid the clash.
using MetaProvider = MediaBrowser.Model.Entities.MetadataProvider;

namespace Jellyfin.Plugin.Arr;

[ApiController]
[Route("Arr")]
public class ArrController : ControllerBase
{
    private readonly ILibraryManager _lib;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ArrController> _log;

    public ArrController(ILibraryManager lib, IHttpClientFactory http, ILogger<ArrController> log)
    {
        _lib = lib;
        _http = http;
        _log = log;
    }

    /// <summary>The client script injected into the web UI. Anonymous: it is loaded by a plain script tag.</summary>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    public ActionResult GetClientScript()
    {
        var stream = GetType().Assembly.GetManifestResourceStream("Jellyfin.Plugin.Arr.Web.inject.js");
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, "application/javascript; charset=utf-8");
    }

    /// <summary>Redirects to the Radarr/Sonarr favicon so the link can show the real logo. Anonymous: a public icon.</summary>
    [HttpGet("Icon")]
    [AllowAnonymous]
    public ActionResult Icon([FromQuery] string? kind)
    {
        var cfg = Plugin.Instance!.Configuration;
        var baseUrl = string.Equals(kind, "movie", StringComparison.OrdinalIgnoreCase) ? cfg.RadarrUrl : cfg.SonarrUrl;
        return string.IsNullOrWhiteSpace(baseUrl) ? NotFound() : Redirect($"{Trim(baseUrl)}/favicon.ico");
    }

    /// <summary>Resolves a Jellyfin item to its Sonarr/Radarr page and 302-redirects there.</summary>
    [HttpGet("Resolve/{itemId}")]
    [Authorize]
    public async Task<ActionResult> Resolve([FromRoute] Guid itemId)
    {
        var item = _lib.GetItemById(itemId);
        item = item switch
        {
            Episode ep => ep.Series,
            Season se => se.Series,
            _ => item,
        };

        if (item is null)
        {
            return NotFound("Item not found");
        }

        var cfg = Plugin.Instance!.Configuration;

        if (item is Movie)
        {
            if (string.IsNullOrWhiteSpace(cfg.RadarrUrl))
            {
                return BadRequest("Radarr URL is not configured");
            }

            var tmdb = item.GetProviderId(MetaProvider.Tmdb);
            return string.IsNullOrEmpty(tmdb)
                ? NotFound("Movie has no TMDb id")
                : Redirect($"{Trim(cfg.RadarrUrl)}/movie/{tmdb}");
        }

        if (item is Series)
        {
            if (string.IsNullOrWhiteSpace(cfg.SonarrUrl) || string.IsNullOrWhiteSpace(cfg.SonarrApiKey))
            {
                return BadRequest("Sonarr URL/API key is not configured");
            }

            var tvdb = item.GetProviderId(MetaProvider.Tvdb);
            if (string.IsNullOrEmpty(tvdb))
            {
                return NotFound("Series has no TVDb id");
            }

            var slug = await SonarrSlugByTvdb(cfg, tvdb).ConfigureAwait(false);
            return slug is null ? NotFound("Series not found in Sonarr") : Redirect($"{Trim(cfg.SonarrUrl)}/series/{slug}");
        }

        return NotFound("Unsupported item type");
    }

    private static string Trim(string url) => url.TrimEnd('/');

    private async Task<string?> SonarrSlugByTvdb(PluginConfiguration cfg, string tvdb)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{Trim(cfg.SonarrUrl)}/api/v3/series?tvdbId={Uri.EscapeDataString(tvdb)}");
            req.Headers.Add("X-Api-Key", cfg.SonarrApiKey);

            using var resp = await _http.CreateClient().SendAsync(req).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
            if (doc.RootElement.ValueKind == JsonValueKind.Array
                && doc.RootElement.GetArrayLength() > 0
                && doc.RootElement[0].TryGetProperty("titleSlug", out var slug))
            {
                return slug.GetString();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Arr: Sonarr lookup failed for tvdbId {Tvdb}", tvdb);
        }

        return null;
    }
}
