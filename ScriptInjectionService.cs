using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Arr;

/// <summary>
/// Patches the web client's index.html to load our client script. There is no other
/// hook for injecting JS into the Jellyfin web UI, so we edit the file on disk.
/// The web path is resolved at runtime, so this works on macOS/Linux/Windows/Docker.
/// </summary>
public class ScriptInjectionService : IHostedService
{
    // Relative src: index.html lives at <root>/web/, the API at <root>/, so ../Arr hits the API
    // and still works behind a reverse-proxy base path.
    private const string Tag = "<script defer src=\"../Arr/ClientScript\"></script><!--Jellyfin.Plugin.Arr-->";

    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<ScriptInjectionService> _log;

    public ScriptInjectionService(IApplicationPaths appPaths, ILogger<ScriptInjectionService> log)
    {
        _appPaths = appPaths;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Patch(add: true);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Patch(add: false);
        return Task.CompletedTask;
    }

    private void Patch(bool add)
    {
        try
        {
            var indexPath = Path.Combine(_appPaths.WebPath, "index.html");
            if (!File.Exists(indexPath))
            {
                _log.LogWarning("Arr: index.html not found at {Path}; the UI link is disabled", indexPath);
                return;
            }

            var html = File.ReadAllText(indexPath);
            var present = html.Contains(Tag, StringComparison.Ordinal);

            if (add && !present)
            {
                var idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    _log.LogWarning("Arr: no </body> in index.html; the UI link is disabled");
                    return;
                }

                File.WriteAllText(indexPath, html.Insert(idx, Tag));
                _log.LogInformation("Arr: injected client script into {Path}", indexPath);
            }
            else if (!add && present)
            {
                File.WriteAllText(indexPath, html.Replace(Tag, string.Empty));
                _log.LogInformation("Arr: removed client script from {Path}", indexPath);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogError(ex, "Arr: cannot write index.html (permissions). Add this before </body> manually: {Tag}", Tag);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Arr: failed to patch index.html");
        }
    }
}
