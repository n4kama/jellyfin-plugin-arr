using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Arr.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Base URL of Sonarr as reachable from the browser, e.g. https://sonarr.example.com.</summary>
    public string SonarrUrl { get; set; } = string.Empty;

    /// <summary>Sonarr API key. Required: used server-side to resolve tvdbId -> series titleSlug.</summary>
    public string SonarrApiKey { get; set; } = string.Empty;

    /// <summary>Base URL of Radarr as reachable from the browser, e.g. https://radarr.example.com.</summary>
    public string RadarrUrl { get; set; } = string.Empty;
}
