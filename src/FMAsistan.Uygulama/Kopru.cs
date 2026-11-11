using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Uygulama;
using FMAsistan.Core.Veri;
using FMAsistan.Core.Yz;
using Microsoft.Web.WebView2.Core;

namespace FMAsistan.Uygulama;

/// <summary>
/// Arayüz ile çekirdek arasındaki köprü ve uygulamanın otomatik veri döngüsü.
///
/// Otomatik: oyun açıkken kullanıcı hiçbir ekranı açmadan
///   - oyundaki eklenti kariyer yüklenince takımı, taktiği, kadroyu, ekibi, antrenmanı ve rakip
///     raporunu tarar (eklentiye takım kimliği veri\ayar.json ile verilir);
///   - bellek okuyucu (yalnızca okur) kendi kulübümüzün oyuncu ve ekip değerlerini alır;
///   - veri klasörleri izlenir, arayüz kendiliğinden yenilenir;
///   - veri değiştiğinde analiz departmanının raporları arka planda yeniden yazılır.
///
/// Arayüzden: {tur:"durum"}, {tur:"guncelle"}, {tur:"sor", ekip, mesaj}, {tur:"rapor-yenile", rapor}.
/// Arayüze: {tur:"durum", veri}, {tur:"canli", ...}, {tur:"rapor", rapor}, {tur:"raporDurum", rapor, durum, metin},
///          {tur:"cevap"|"hata", ekip, metin}, {tur:"bilgi", metin}.
/// </summary>
public sealed class Kopru : IDisposable
{
    private static readonly TimeSpan BellekAraligi = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtomatikRaporAraligi = TimeSpan.FromMinutes(20);

    private readonly CoreWebView2 _web;
    private readonly Dispatcher _ui;
    private readonly Ayarlar _ayarlar = Ayarlar.Varsayilan();
    private readonly IYapayZeka _sohbetYz = new ClaudeCodeCli();
    private readonly AnalizDepartmani _departman;
    private readonly Dictionary<string, string> _oturumlar = new();
    private readonly List<FileSystemWatcher> _izleyiciler = new();
    private readonly DispatcherTimer _gecikme;
    private readonly DispatcherTimer _dongu;
    private readonly SemaphoreSlim _yukleKilidi = new(1, 1);
    private readonly Dictionary<RaporTuru, DateTime> _sonOtomatikRapor = new();
    private readonly HashSet<RaporTuru> _hazirlanan = new();

    private TakimDurumu? _durum;
    private IReadOnlyList<Bulgu> _bulgular = Array.Empty<Bulgu>();
    private IReadOnlyList<EkipProfili> _ekip = Array.Empty<EkipProfili>();
    private string? _girdi;
    private string? _sonGonderilen;
    private bool _bellekCalisiyor;
    private bool _sonBellekBasarisiz;
    private DateTime _sonBellekDenemesi = DateTime.MinValue;
    private DateTime _sonBellek = DateTime.MinValue;
    private string? _sonTaramaImzasi;

    public Kopru(CoreWebView2 web, Dispatcher ui)
    {
        _web = web;
        _ui = ui;
        _departman = new AnalizDepartmani(new ClaudeCodeCli(zamanAsimi: TimeSpan.FromMinutes(10)), Path.Combine(Ayarlar.UygulamaVerisi, "raporlar"));
        _web.WebMessageReceived += MesajGeldi;

        _gecikme = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, async (_, _) =>
        {
            _gecikme!.Stop();
            await DurumGonderAsync(sessiz: true);
        }, _ui);
        _gecikme.Stop();

        AyarYaz();
        foreach (var klasor in new[] { _ayarlar.VeriKlasoru, _ayarlar.BellekKlasoru })
        {
            Directory.CreateDirectory(klasor);
            var iz = new FileSystemWatcher(klasor) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
            iz.Changed += VeriDegisti; iz.Created += VeriDegisti; iz.Renamed += VeriDegisti;
            iz.EnableRaisingEvents = true;
            _izleyiciler.Add(iz);
        }

        _dongu = new DispatcherTimer(TimeSpan.FromSeconds(20), DispatcherPriority.Background, (_, _) => OtomatikDongu(), _ui);
        _dongu.Start();
        _ui.BeginInvoke(OtomatikDongu, DispatcherPriority.Background);
    }

    // ---------- Otomatik veri döngüsü ----------

    /// <summary>Eklentiye hangi takımı tarayacağını söyler.</summary>
    private void AyarYaz()
    {
        try { File.WriteAllText(Path.Combine(_ayarlar.VeriKlasoru, "ayar.json"), JsonSerializer.Serialize(new { takim = _ayarlar.TakimKimlik })); }
        catch (IOException) { }
    }

    private static bool OyunAcik() => Process.GetProcessesByName("fm").Length > 0;

    private void OtomatikDongu()
    {
        bool acik = OyunAcik();
        var tarama = TaramaDurumu();
        if (File.Exists(_ayarlar.BellekDosyasi)) _sonBellek = File.GetLastWriteTime(_ayarlar.BellekDosyasi);

        // Bellek okuması: oyun açıkken 15 dakikada bir ve oyundan yeni tarama geldiğinde
        // (tarama bellek dökümünden yeniyse kariyer yüklü demektir). Başarısız okuma 1 dk sonra yinelenir.
        bool taramaYeni = tarama.zaman is DateTime tz && tz > _sonBellek;
        bool zamani = DateTime.Now - _sonBellekDenemesi > (_sonBellekBasarisiz ? TimeSpan.FromMinutes(1) : BellekAraligi);
        if (acik && !_bellekCalisiyor && (zamani || taramaYeni))
            _ = BellekOkuAsync();
        if (tarama.imza != null) _sonTaramaImzasi = tarama.imza;

        Gonder(new
        {
            tur = "canli",
            oyun = acik,
            eklenti = EklentiCanli(),
            tarama = tarama.zaman?.ToString("dd.MM HH:mm"),
            bellek = _sonBellek == DateTime.MinValue ? null : _sonBellek.ToString("dd.MM HH:mm"),
            bellekCalisiyor = _bellekCalisiyor,
        });
    }

    private (string? imza, DateTime? zaman) TaramaDurumu()
    {
        string yol = Path.Combine(_ayarlar.VeriKlasoru, "tarama.json");
        if (!File.Exists(yol)) return (null, null);
        var z = File.GetLastWriteTime(yol);
        return (z.Ticks.ToString(), z);
    }

    private bool EklentiCanli()
    {
        string kalp = Path.Combine(_ayarlar.VeriKlasoru, "kalp.json");
        return File.Exists(kalp) && DateTime.Now - File.GetLastWriteTime(kalp) < TimeSpan.FromSeconds(30);
    }

    /// <summary>Bellek okuyucuyu (FMSuperScout, yalnızca okur) çalıştırır; yalnızca kendi kulübümüz yazılır.</summary>
    private async Task BellekOkuAsync()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "bellek", "FMSuperScout.Scanner.exe");
        if (!File.Exists(exe)) { Gunluk.Yaz("bellek okuyucu yok: " + exe); return; }
        _bellekCalisiyor = true;
        _sonBellekDenemesi = DateTime.Now;
        try
        {
            var bilgi = new ProcessStartInfo(exe, "scan") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)! };
            bilgi.Environment["FMASISTAN_BELLEK_DIZINI"] = _ayarlar.BellekKlasoru;
            bilgi.Environment["FMASISTAN_SADECE_KULUP"] = "1";
            using var p = Process.Start(bilgi)!;
            using var zaman = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try { await p.WaitForExitAsync(zaman.Token); }
            catch (OperationCanceledException) { try { p.Kill(); } catch { } Gunluk.Yaz("bellek okuması zaman aşımı"); return; }
            Gunluk.Yaz($"bellek okuması bitti (çıkış {p.ExitCode})");
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Gunluk.Yaz("bellek okuması başarısız: " + e.Message);
        }
        finally
        {
            _bellekCalisiyor = false;
            string durum = Path.Combine(_ayarlar.BellekKlasoru, "status.json");
            _sonBellekBasarisiz = !File.Exists(durum) || !File.ReadAllText(durum).Contains("\"done\"");
            if (_sonBellekBasarisiz) Gunluk.Yaz("bellek okuması sonuç vermedi: " + (File.Exists(durum) ? File.ReadAllText(durum) : "durum yok"));
            _sonBellek = File.Exists(_ayarlar.BellekDosyasi) ? File.GetLastWriteTime(_ayarlar.BellekDosyasi) : DateTime.MinValue;
        }
    }

    private void VeriDegisti(object? _, FileSystemEventArgs e)
    {
        string ad = Path.GetFileName(e.FullPath);
        bool ilgili = ad is "canli.json" or "kimlik-adlari-sonuc.json" or "tarama.json" or "dump.json"
            || e.FullPath.Contains($"{Path.DirectorySeparatorChar}oku{Path.DirectorySeparatorChar}")
            || e.FullPath.Contains($"{Path.DirectorySeparatorChar}gozlem{Path.DirectorySeparatorChar}");
        if (!ilgili || !ad.EndsWith(".json")) return;
        _ui.BeginInvoke(() => { _gecikme.Stop(); _gecikme.Start(); });
    }

    // ---------- Mesajlar ----------

    private async void MesajGeldi(object? _, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var b = JsonDocument.Parse(e.WebMessageAsJson);
            var k = b.RootElement;
            switch (k.GetProperty("tur").GetString())
            {
                case "durum":
                    _sonGonderilen = null;
                    await DurumGonderAsync(sessiz: false);
                    OtomatikDongu();
                    break;
                case "guncelle":
                    Guncelle();
                    break;
                case "sor":
                    await SorAsync(k.GetProperty("ekip").GetString()!, k.GetProperty("mesaj").GetString()!);
                    break;
                case "rapor-yenile":
                    if (Enum.TryParse<RaporTuru>(k.GetProperty("rapor").GetString(), out var t)) _ = RaporHazirlaAsync(t);
                    break;
            }
        }
        catch (Exception ex)
        {
            Gunluk.Yaz("mesaj hatası: " + ex);
            Gonder(new { tur = "bilgi", metin = "Hata: " + ex.Message });
        }
    }

    /// <summary>"Şimdi güncelle": eklentiden hemen tam tarama ve bellek okuması.</summary>
    private void Guncelle()
    {
        if (!OyunAcik()) { Gonder(new { tur = "bilgi", metin = "Oyun açık değil. Oyunu açıp kariyeri yükleyince veriler kendiliğinden gelir." }); return; }
        AyarYaz();
        try { File.WriteAllText(Path.Combine(_ayarlar.VeriKlasoru, "tara.flag"), "1"); } catch (IOException) { }
        if (!_bellekCalisiyor) _ = BellekOkuAsync();
        Gonder(new { tur = "bilgi", metin = "Oyundan tam tarama istendi; bir dakika içinde güncellenir." });
    }

    private async Task DurumGonderAsync(bool sessiz)
    {
        await _yukleKilidi.WaitAsync();
        try
        {
            var (json, hata) = await Task.Run(() =>
            {
                try
                {
                    var durum = DurumYukleyici.Yukle(_ayarlar);
                    var bulgular = TaktikAnalizi.Calistir(durum);
                    var ekip = EkipListesi(durum);
                    _durum = durum; _bulgular = bulgular; _ekip = ekip;
                    _girdi = AnalizDepartmani.Girdi(durum, bulgular, KadroAnalizi.Calistir(durum));
                    return (ArayuzModeli.JsonOlustur(durum, bulgular, ekip, SonVeriZamani()), (string?)null);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or KeyNotFoundException)
                {
                    return ((string?)null, ex.Message);
                }
            });
            if (json == null)
            {
                Gunluk.Yaz("durum yüklenemedi: " + hata);
                if (!sessiz) Gonder(new { tur = "bilgi", metin = hata });
                return;
            }
            if (json != _sonGonderilen)
            {
                _sonGonderilen = json;
                Gunluk.Yaz($"durum gönderildi ({json.Length / 1024} KB, {(sessiz ? "otomatik" : "istek")})");
                _web.PostWebMessageAsJson("{\"tur\":\"durum\",\"veri\":" + json + "}");
            }
            RaporlariDenetle();
        }
        finally { _yukleKilidi.Release(); }
    }

    /// <summary>Sohbet edilebilen ekip: oyundan okunan teknik ekip; yoksa elle yazılmış profiller.</summary>
    private IReadOnlyList<EkipProfili> EkipListesi(TakimDurumu d)
    {
        var oyundan = EkipProfili.Oyundan(d.TeknikEkip).ToList();
        foreach (var elle in EkipProfili.HepsiniYukle(_ayarlar.EkipKlasoru))
            if (!oyundan.Any(o => o.Uye.Ad == elle.Uye.Ad)) oyundan.Add(elle);
        return oyundan;
    }

    private DateTime SonVeriZamani()
    {
        var d = new DirectoryInfo(_ayarlar.VeriKlasoru);
        return d.Exists
            ? d.EnumerateFiles("*.json", SearchOption.AllDirectories).Select(f => f.LastWriteTime).DefaultIfEmpty(DateTime.Now).Max()
            : DateTime.Now;
    }

    // ---------- Analiz departmanı raporları ----------

    private void RaporlariDenetle()
    {
        if (_girdi == null) return;
        foreach (var t in Enum.GetValues<RaporTuru>())
        {
            var guncel = _departman.Onbellek(t, _girdi);
            if (guncel != null) { RaporGonder(guncel, true); continue; }
            var son = _departman.SonRapor(t);
            if (son != null) RaporGonder(son, false);
            bool zamani = son == null || !_sonOtomatikRapor.TryGetValue(t, out var z) || DateTime.Now - z > OtomatikRaporAraligi;
            if (zamani) _ = RaporHazirlaAsync(t);
        }
    }

    private async Task RaporHazirlaAsync(RaporTuru t)
    {
        if (_girdi == null || !_hazirlanan.Add(t)) return;
        _sonOtomatikRapor[t] = DateTime.Now;
        string girdi = _girdi;
        Gonder(new { tur = "raporDurum", rapor = t.ToString(), durum = "hazirlaniyor" });
        try
        {
            string bilgi = File.Exists(_ayarlar.BilgiTabaniYolu) ? File.ReadAllText(_ayarlar.BilgiTabaniYolu) : "";
            string? analist = _durum?.TeknikEkip.FirstOrDefault(e => e.Gorev.Contains("Analist"))?.Ad;
            var r = await Task.Run(() => _departman.HazirlaAsync(t, girdi, bilgi, analist));
            Gunluk.Yaz($"rapor hazır: {t} ({r.Metin.Length} karakter)");
            RaporGonder(r, girdi == _girdi);
        }
        catch (Exception ex) when (ex is YapayZekaHatasi or IOException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            Gunluk.Yaz($"rapor hatası ({t}): {ex.Message}");
            Gonder(new { tur = "raporDurum", rapor = t.ToString(), durum = "hata", metin = ex.Message });
        }
        finally { _hazirlanan.Remove(t); }
    }

    private void RaporGonder(AnalizRaporu r, bool guncel)
        => Gonder(new { tur = "rapor", rapor = new { tur = r.Tur.ToString(), baslik = r.Baslik, metin = r.Metin, zaman = r.Zaman.ToString("dd.MM HH:mm"), guncel } });

    // ---------- Sohbet ----------

    private async Task SorAsync(string ekipKimlik, string mesaj)
    {
        var profil = _ekip.FirstOrDefault(p => p.Kimlik == ekipKimlik);
        if (profil == null || _durum == null)
        {
            Gonder(new { tur = "hata", ekip = ekipKimlik, metin = "Ekip üyesi ya da oyun verisi bulunamadı." });
            return;
        }
        try
        {
            string bilgi = File.Exists(_ayarlar.BilgiTabaniYolu) ? File.ReadAllText(_ayarlar.BilgiTabaniYolu) : "";
            string sistem = EkipPersonasi.SistemTalimati(profil.Uye, profil.Nitelikler, _durum, _bulgular, bilgi);
            _oturumlar.TryGetValue(ekipKimlik, out var oturum);
            var c = await Task.Run(() => _sohbetYz.SorAsync(sistem, mesaj, oturum));
            _oturumlar[ekipKimlik] = c.OturumKimligi;
            Gonder(new { tur = "cevap", ekip = ekipKimlik, metin = c.Metin });
        }
        catch (Exception ex) when (ex is YapayZekaHatasi or IOException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            Gunluk.Yaz("sohbet hatası: " + ex.Message);
            Gonder(new { tur = "hata", ekip = ekipKimlik, metin = ex.Message });
        }
    }

    private void Gonder(object mesaj)
    {
        string json = JsonSerializer.Serialize(mesaj, ArayuzModeli.Json);
        if (_ui.CheckAccess()) _web.PostWebMessageAsJson(json);
        else _ui.BeginInvoke(() => _web.PostWebMessageAsJson(json));
    }

    public void Dispose()
    {
        foreach (var i in _izleyiciler) i.Dispose();
        _gecikme.Stop();
        _dongu.Stop();
    }
}
