using System.Text.Json;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using FMAsistan.Core.Yz;

namespace FMAsistan.Core.Uygulama;

/// <summary>Uygulamanın yapılandırması: oyun klasörü, takım, proje kökü.</summary>
public sealed record Ayarlar(string FmDizini, string TakimKimlik, string ProjeKoku)
{
    public string VeriKlasoru => Path.Combine(FmDizini, "BepInEx", "FMAsistan", "veri");
    public string BilgiTabaniYolu => Path.Combine(ProjeKoku, "bilgi", "TAKTIK_BILGI_TABANI.md");
    public string EkipKlasoru => Path.Combine(ProjeKoku, "ornekler", "ekip");
    public static string UygulamaVerisi => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMAsistan");
    public string BellekKlasoru => Path.Combine(UygulamaVerisi, "bellek");
    public string BellekDosyasi => Path.Combine(BellekKlasoru, "dump.json");

    public static Ayarlar Varsayilan(string? baslangic = null) => new(
        Environment.GetEnvironmentVariable("FM_DIZIN") ?? @"C:\XboxGames\Football Manager 27\Content",
        Environment.GetEnvironmentVariable("FM_TAKIM") ?? "402654676",
        KokBul(baslangic ?? AppContext.BaseDirectory));

    /// <summary>"bilgi" klasörünü içeren üst klasör.</summary>
    public static string KokBul(string baslangic)
    {
        var d = new DirectoryInfo(baslangic);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "bilgi"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("Proje kökü (bilgi klasörü) bulunamadı.");
    }
}

/// <summary>Eklentinin tüm çıktılarından tam takım durumunu kurar.</summary>
public static class DurumYukleyici
{
    public static TakimDurumu Yukle(Ayarlar a)
    {
        var veri = OyunVerisi.Yukle(a.VeriKlasoru);
        if (veri.Count == 0) throw new FileNotFoundException("Eklenti verisi bulunamadı. Oyun açıkken eklenti çalışıyor mu?");
        var adlar = OyunVerisi.Adlar(veri);
        string kimlikDosyasi = Path.Combine(a.VeriKlasoru, "kimlik-adlari-sonuc.json");
        var kimlikAdlari = File.Exists(kimlikDosyasi)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(kimlikDosyasi))
            : null;
        var temel = EklentiCozucu.TakimDurumu(veri, a.TakimKimlik, adlar);
        var kadro = EklentiCozucu.Kadro(veri, a.TakimKimlik, temel.Dizilis.Select(p => p.OyuncuKimlik).ToList(), adlar);
        BellekVerisi? bellek = null;
        try { bellek = BellekVerisi.Oku(a.BellekDosyasi); } catch (Exception e) when (e is IOException or JsonException) { }
        double kur = KadroCozucu.Kur(veri);
        var takimAdlari = veri.Where(k => k.Key.StartsWith("TeamReference:") && k.Value.TryGetValue("Name", out var n) && n.ValueKind == JsonValueKind.String)
            .ToDictionary(k => k.Key, k => k.Value["Name"].GetString()!);

        var kaynaklar = new List<string>();
        string tarama = Path.Combine(a.VeriKlasoru, "tarama.json");
        if (File.Exists(tarama)) kaynaklar.Add($"Oyundan otomatik tarama: {File.GetLastWriteTime(tarama):dd.MM HH:mm}");
        if (bellek != null) kaynaklar.Add($"Bellek okuması: {bellek.Zaman:dd.MM HH:mm} ({bellek.Oyuncular.Count} oyuncu, {bellek.Ekip.Count} ekip üyesi)");

        return temel with
        {
            VeriMerkezi = VeriMerkeziCozucu.Coz(veri, a.TakimKimlik, adlar),
            Ayarlar = VeriMerkeziCozucu.Ayarlar(veri, a.TakimKimlik, kimlikAdlari),
            Rakip = VeriMerkeziCozucu.Rakip(veri, a.TakimKimlik, adlar),
            Kadro = kadro,
            Profiller = KadroCozucu.Oyuncular(veri, a.TakimKimlik, kadro, adlar, bellek, kur),
            TeknikEkip = KadroCozucu.Ekip(veri, a.TakimKimlik, bellek, kur),
            Antrenman = KadroCozucu.AntrenmanDurumu(veri, a.TakimKimlik, adlar),
            Kulup = KadroCozucu.Kulup(veri, a.TakimKimlik, kur),
            Fikstur = KadroCozucu.Fikstur(veri, a.TakimKimlik, takimAdlari),
            OyunTarihi = bellek?.OyunTarihi,
            Kaynaklar = kaynaklar,
        };
    }
}

/// <summary>Sohbet edilebilen teknik ekip üyesi: oyundan otomatik ya da ornekler/ekip/*.json.</summary>
public sealed record EkipProfili(string Kimlik, TeknikEkipUyesi Uye, EkipPersonasi.Nitelikler Nitelikler)
{
    /// <summary>
    /// Oyundan okunan ekip: sohbet için asistan menajer, antrenörler, analistler ve
    /// gözlemciler. Nitelikler oyunun öznitelik adlarından eşlenir.
    /// </summary>
    public static IReadOnlyList<EkipProfili> Oyundan(IEnumerable<EkipUyesiProfili> ekip)
    {
        int? N(EkipUyesiProfili e, string ad) => e.Oznitelikler.TryGetValue(ad, out var v) ? v : null;
        int Sira(string g) => g.Contains("Asistan") ? 0 : g.Contains("Analist") ? 1 : g.Contains("Antrenör") && !g.Contains("Kaleci") && !g.Contains("Fitness") ? 2 : g.Contains("Gözlemci") ? 3 : 4;
        return ekip.Where(e => !e.Menajer && Sira(e.Gorev) < 4)
            .OrderBy(e => Sira(e.Gorev)).ThenByDescending(e => N(e, "Taktik Bilgisi") ?? 0)
            .Select(e => new EkipProfili(e.Kimlik.Replace("PersonReference:", "oyun-"),
                new TeknikEkipUyesi(e.Kimlik, e.Ad, e.Gorev, e.Felsefe, e.Lisans),
                new EkipPersonasi.Nitelikler(N(e, "Taktik Bilgisi"), N(e, "Oyuncu Yeteneğini Değerlendirme"),
                    N(e, "Oyuncu Potansiyelini Değerlendirme"), N(e, "Otorite"), N(e, "Motivasyon"),
                    N(e, "Futbolcu İdare Kabiliyeti"), N(e, "Kararlılık"), N(e, "Adaptasyon"), N(e, "Veri Analizi"))))
            .ToList();
    }

    public static IReadOnlyList<EkipProfili> HepsiniYukle(string klasor)
        => Directory.Exists(klasor)
            ? Directory.GetFiles(klasor, "*.json").OrderBy(f => f).Select(Yukle).ToList()
            : Array.Empty<EkipProfili>();

    public static EkipProfili Yukle(string yol)
    {
        using var b = JsonDocument.Parse(File.ReadAllText(yol));
        var k = b.RootElement;
        string? M(string a) => k.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int? S(string a) => k.TryGetProperty("nitelikler", out var n) && n.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
        var uye = new TeknikEkipUyesi(M("kimlik") ?? "?", M("ad") ?? "?", M("gorev") ?? "Teknik ekip",
            new Felsefe(M("dizilis"), M("mentalite"), M("pres"), M("markaj")), M("lisans"));
        var nit = new EkipPersonasi.Nitelikler(S("taktikBilgisi"), S("yetenekDegerlendirme"), S("potansiyelDegerlendirme"),
            S("otorite"), S("motivasyon"), S("adamYonetimi"), S("kararlilik"), S("uyum"), S("veriAnalizi"));
        return new EkipProfili(Path.GetFileNameWithoutExtension(yol), uye, nit);
    }
}
