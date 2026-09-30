using System.Diagnostics;
using System.Windows;

namespace GameTranslator.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Trace.TraceInformation("GameTranslator started.");
        DispatcherUnhandledException += (_, args) =>
            Trace.TraceError("Unhandled application error: {0}", args.Exception);
    }
}
