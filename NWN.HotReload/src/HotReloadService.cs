using System;
using System.IO;
using System.Threading;
using Anvil;
using Anvil.API;
using Anvil.Internal;
using Anvil.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using NLog;

namespace Jorteck.HotReload
{
  [ServiceBinding(typeof(HotReloadService))]
  public sealed class HotReloadService : IDisposable
  {
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly PhysicalFileProvider fileProvider;
    private readonly CancellationTokenSource cts = new CancellationTokenSource();

    public HotReloadService()
    {
      if (!EnvironmentConfig.ReloadEnabled)
      {
        Log.Error("Hot Reload is not available as ANVIL_RELOAD_ENABLED is not set, or set to false!");
        return;
      }

      string path = Path.Combine(Path.GetDirectoryName(typeof(AnvilCore).Assembly.Location)!, "Plugins");
      Log.Warn($"Setting watch on directory {path} for plugin binary changes...");

      fileProvider = new PhysicalFileProvider(path)
      {
        UsePollingFileWatcher = true,
        UseActivePolling = true,
      };

      IChangeToken fileChangeToken = fileProvider.Watch("**/*.dll");
      fileChangeToken.RegisterChangeCallback(OnFileChanged, default);
    }

    private async void OnFileChanged(object state)
    {
      fileProvider.Dispose();

      await NwTask.Delay(TimeSpan.FromSeconds(0.5f), cts.Token);
      if (cts.IsCancellationRequested)
      {
        return;
      }

      Log.Error("Plugin assembly change detected! Triggering reload of Anvil.");

      AnvilCore.Reload();
    }

    public void Dispose()
    {
      cts.Cancel();
    }
  }
}
