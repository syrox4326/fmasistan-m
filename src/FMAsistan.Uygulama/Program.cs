using System.Windows;

namespace FMAsistan.Uygulama;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        var uygulama = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        uygulama.DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.Message, "FM Asistanım", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        return uygulama.Run(new AnaPencere());
    }
}
