using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FMAsistan.Core.Yz;

/// <summary>
/// Kullanıcının bilgisayarındaki Claude Code CLI'yi başsız modda (claude -p) çağırır;
/// ayrı API ücreti yerine kullanıcının mevcut aboneliğini kullanır.
///
/// Güvenlik: tüm yerleşik araçlar kapalı (--tools ""), MCP sunucuları yüklenmez
/// (--strict-mcp-config) ve süreç boş, ayrılmış bir klasörde çalışır. Model yalnızca metin
/// üretir; dosya okuyamaz, komut çalıştıramaz.
/// </summary>
public sealed class ClaudeCodeCli : IYapayZeka
{
    private readonly string _claudeYolu;
    private readonly string _calismaKlasoru;
    private readonly string _model;
    private readonly TimeSpan _zamanAsimi;

    public ClaudeCodeCli(string? claudeYolu = null, string model = "claude-opus-5-5", TimeSpan? zamanAsimi = null, string? calismaKlasoru = null)
    {
        _claudeYolu = claudeYolu ?? VarsayilanYol();
        _model = model;
        _zamanAsimi = zamanAsimi ?? TimeSpan.FromMinutes(3);
        _calismaKlasoru = calismaKlasoru ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMAsistan", "yz");
        Directory.CreateDirectory(_calismaKlasoru);
    }

    public static string VarsayilanYol()
    {
        string ev = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string yerel = Path.Combine(ev, ".local", "bin", OperatingSystem.IsWindows() ? "claude.exe" : "claude");
        return File.Exists(yerel) ? yerel : "claude";
    }

    public async Task<YzCevap> SorAsync(string sistem, string mesaj, string? oturumKimligi, CancellationToken iptal = default)
    {
        var bilgi = new ProcessStartInfo(_claudeYolu)
        {
            WorkingDirectory = _calismaKlasoru,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // Sistem talimatı dosyadan verilir: Windows komut satırı 32.767 karakterle sınırlı ve
        // talimat (bilgi tabanı + Veri Merkezi) bunu aşabiliyor. İçerik özetiyle adlandırılır;
        // aynı talimat aynı dosyayı kullanır.
        string sistemDosyasi = SistemDosyasi(sistem);
        foreach (var a in Argumanlar(sistemDosyasi, oturumKimligi)) bilgi.ArgumentList.Add(a);

        using var surec = Process.Start(bilgi) ?? throw new YapayZekaHatasi("Claude Code başlatılamadı.");
        using var zaman = CancellationTokenSource.CreateLinkedTokenSource(iptal);
        zaman.CancelAfter(_zamanAsimi);

        await surec.StandardInput.WriteAsync(mesaj.AsMemory(), zaman.Token);
        surec.StandardInput.Close();

        var cikti = surec.StandardOutput.ReadToEndAsync(zaman.Token);
        var hata = surec.StandardError.ReadToEndAsync(zaman.Token);
        try { await surec.WaitForExitAsync(zaman.Token); }
        catch (OperationCanceledException)
        {
            try { surec.Kill(entireProcessTree: true); } catch { }
            throw new YapayZekaHatasi(iptal.IsCancellationRequested ? "İstek iptal edildi." : "Claude Code zaman aşımına uğradı.");
        }

        return Coz(await cikti, await hata, surec.ExitCode);
    }

    internal string SistemDosyasi(string sistem)
    {
        var ozet = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sistem)))[..16];
        string yol = Path.Combine(_calismaKlasoru, $"sistem-{ozet}.txt");
        if (!File.Exists(yol)) File.WriteAllText(yol, sistem, new UTF8Encoding(false));
        return yol;
    }

    internal IEnumerable<string> Argumanlar(string sistemDosyasi, string? oturumKimligi)
    {
        yield return "-p";
        yield return "--output-format"; yield return "json";
        yield return "--model"; yield return _model;
        yield return "--tools"; yield return "";
        yield return "--strict-mcp-config";
        yield return "--system-prompt-file"; yield return sistemDosyasi;
        if (oturumKimligi != null) { yield return "--resume"; yield return oturumKimligi; }
    }

    internal static YzCevap Coz(string cikti, string hata, int cikisKodu)
    {
        JsonDocument belge;
        try { belge = JsonDocument.Parse(cikti); }
        catch (JsonException)
        {
            throw new YapayZekaHatasi($"Claude Code beklenmeyen çıktı verdi (çıkış kodu {cikisKodu}). {Kisalt(hata)}");
        }
        using (belge)
        {
            var k = belge.RootElement;
            bool hataVar = k.TryGetProperty("is_error", out var h) && h.ValueKind == JsonValueKind.True;
            string metin = k.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()! : "";
            if (hataVar || cikisKodu != 0)
                throw new YapayZekaHatasi($"Claude Code hata döndü: {Kisalt(metin.Length > 0 ? metin : hata)}");
            string oturum = k.TryGetProperty("session_id", out var s) ? s.GetString() ?? "" : "";
            decimal? maliyet = k.TryGetProperty("total_cost_usd", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetDecimal() : null;
            return new YzCevap(metin.Trim(), oturum, maliyet);
        }
    }

    private static string Kisalt(string s) => s.Length <= 400 ? s : s[..400] + "…";
}
