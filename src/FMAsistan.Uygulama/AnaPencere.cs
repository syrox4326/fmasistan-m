using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FMAsistan.Uygulama;

/// <summary>Tek pencere: koyu başlık çubuğu ve tüm alanı kaplayan WebView2.</summary>
public sealed class AnaPencere : Window
{
    private const string Alan = "fmasistan.yerel";
    private readonly WebView2 _web = new();
    private Kopru? _kopru;

    public AnaPencere()
    {
        Title = "FM Asistanım";
        Width = 1480; Height = 920;
        MinWidth = 1180; MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x07, 0x0c, 0x18));
        _web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x07, 0x0c, 0x18);
        Content = _web;
        SourceInitialized += (_, _) => KoyuBaslik();
        Loaded += async (_, _) => await BaslatAsync();
        Closed += (_, _) => _kopru?.Dispose();
    }

    private async Task BaslatAsync()
    {
        string kullaniciVerisi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMAsistan", "webview");
        var ortam = await CoreWebView2Environment.CreateAsync(null, kullaniciVerisi);
        await _web.EnsureCoreWebView2Async(ortam);
        var c = _web.CoreWebView2;

        var s = c.Settings;
        s.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
        s.AreDefaultContextMenusEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;

        string web = Path.Combine(AppContext.BaseDirectory, "web");
        c.SetVirtualHostNameToFolderMapping(Alan, web, CoreWebView2HostResourceAccessKind.Deny);
        // Arayüz dışındaki adreslere gidilmez.
        c.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith($"https://{Alan}/")) e.Cancel = true; };
        c.NewWindowRequested += (_, e) => e.Handled = true;

        _kopru = new Kopru(c, Dispatcher);
        Gunluk.Yaz("uygulama açıldı, WebView2 " + ortam.BrowserVersionString);
        c.Navigate($"https://{Alan}/index.html");
    }

    // Windows 11: koyu başlık çubuğu ve arayüzün zemin rengi.
    private void KoyuBaslik()
    {
        var h = new WindowInteropHelper(this).Handle;
        int evet = 1;
        DwmSetWindowAttribute(h, 20, ref evet, sizeof(int));          // DWMWA_USE_IMMERSIVE_DARK_MODE
        int renk = 0x00180c07;                                         // COLORREF (BGR) #070c18
        DwmSetWindowAttribute(h, 35, ref renk, sizeof(int));          // DWMWA_CAPTION_COLOR
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
