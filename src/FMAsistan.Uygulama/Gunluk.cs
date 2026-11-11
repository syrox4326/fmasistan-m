using System.IO;

namespace FMAsistan.Uygulama;

/// <summary>%LOCALAPPDATA%\FMAsistan\uygulama.log — sorun giderme için kısa olay günlüğü.</summary>
internal static class Gunluk
{
    private static readonly string Yol = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMAsistan", "uygulama.log");
    private static readonly object Kilit = new();

    public static void Yaz(string metin)
    {
        try
        {
            lock (Kilit)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Yol)!);
                if (File.Exists(Yol) && new FileInfo(Yol).Length > 512 * 1024) File.Delete(Yol);
                File.AppendAllText(Yol, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {metin}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
    }
}
