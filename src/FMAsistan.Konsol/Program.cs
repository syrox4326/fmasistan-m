using System.Text;
using System.Text.Json;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using FMAsistan.Core.Yz;
using FMAsistan.Core.Uygulama;

// FM Asistanım — geliştirme konsolu.
//   fmasistan analiz                 → son eklenti okumasından taktik analizi
//   fmasistan sohbet <ekip-dosyası>  → teknik ekip üyesiyle sohbet (çıkmak için boş satır)
//   fmasistan sor <ekip-dosyası> "soru"  → tek soru

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

string fmDir = Environment.GetEnvironmentVariable("FM_DIZIN") ?? @"C:\XboxGames\Football Manager 27\Content";
string takimKimlik = Environment.GetEnvironmentVariable("FM_TAKIM") ?? "402654676";
string kok = KokBul();

if (args.Length == 0) { Yardim(); return 1; }

if (args[0] == "guncelle") return await Guncelle();

try
{
    var durum = SonDurum();
    var bulgular = TaktikAnalizi.Calistir(durum);

    switch (args[0])
    {
        case "analiz":
            Yazdir(durum, bulgular);
            return 0;

        case "disa-aktar" when args.Length >= 2:
        {
            var ekip = EkipProfili.Oyundan(durum.TeknikEkip).Concat(EkipProfili.HepsiniYukle(Path.Combine(kok, "ornekler", "ekip"))).ToList();
            File.WriteAllText(args[1], ArayuzModeli.JsonOlustur(durum, bulgular, ekip, DateTime.Now));
            // Tarayıcı önizlemesi için son raporlar.
            var dep = new AnalizDepartmani(new ClaudeCodeCli(), Path.Combine(Ayarlar.UygulamaVerisi, "raporlar"));
            var raporlar = Enum.GetValues<RaporTuru>().Select(dep.SonRapor).Where(r => r != null)
                .Select(r => new { tur = r!.Tur.ToString(), baslik = r.Baslik, metin = r.Metin, zaman = r.Zaman.ToString("dd.MM HH:mm"), guncel = true });
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "ornek-raporlar.json"), JsonSerializer.Serialize(raporlar, ArayuzModeli.Json));
            Console.WriteLine("Arayüz verisi yazıldı: " + args[1]);
            return 0;
        }

        case "rapor" when args.Length >= 2 && Enum.TryParse<RaporTuru>(args[1], true, out var tur):
        {
            var dep = new AnalizDepartmani(new ClaudeCodeCli(zamanAsimi: TimeSpan.FromMinutes(10)), Path.Combine(Ayarlar.UygulamaVerisi, "raporlar"));
            string girdi = AnalizDepartmani.Girdi(durum, bulgular, KadroAnalizi.Calistir(durum));
            if (args.Length >= 3 && args[2] == "--girdi") { Console.WriteLine(girdi); return 0; }
            string bilgi = File.ReadAllText(Path.Combine(kok, "bilgi", "TAKTIK_BILGI_TABANI.md"));
            var sure = System.Diagnostics.Stopwatch.StartNew();
            var r = await dep.HazirlaAsync(tur, girdi, bilgi, durum.TeknikEkip.FirstOrDefault(e => e.Gorev.Contains("Analist"))?.Ad);
            Console.WriteLine($"[{r.Baslik} · {sure.Elapsed.TotalSeconds:0} sn · {r.Metin.Length} karakter]");
            Console.WriteLine();
            Console.WriteLine(r.Metin);
            return 0;
        }

        case "sohbet" or "sor" when args.Length >= 2:
        {
            var (uye, nit) = EkipOku(args[1]);
            string bilgi = File.ReadAllText(Path.Combine(kok, "bilgi", "TAKTIK_BILGI_TABANI.md"));
            string sistem = EkipPersonasi.SistemTalimati(uye, nit, durum, bulgular, bilgi);
            var yz = new ClaudeCodeCli();
            string oturumDosyasi = Path.Combine(Path.GetDirectoryName(args[1])!, Path.GetFileNameWithoutExtension(args[1]) + ".oturum");
            string? oturum = File.Exists(oturumDosyasi) ? File.ReadAllText(oturumDosyasi).Trim() : null;

            if (args[0] == "sor")
            {
                var c = await yz.SorAsync(sistem, args[2], oturum);
                File.WriteAllText(oturumDosyasi, c.OturumKimligi);
                Console.WriteLine($"{uye.Ad}: {c.Metin}");
                return 0;
            }

            Console.WriteLine($"{uye.Ad} ({uye.Gorev}) ile sohbet. Çıkmak için boş satır.");
            while (true)
            {
                Console.Write("\nSen: ");
                string? satir = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(satir)) break;
                var c = await yz.SorAsync(sistem, satir, oturum);
                oturum = c.OturumKimligi;
                File.WriteAllText(oturumDosyasi, oturum);
                Console.WriteLine($"\n{uye.Ad}: {c.Metin}");
            }
            return 0;
        }

        default:
            Yardim();
            return 1;
    }
}
catch (Exception e) when (e is InvalidDataException or YapayZekaHatasi or FileNotFoundException or DirectoryNotFoundException)
{
    Console.Error.WriteLine("Hata: " + e.Message);
    return 2;
}

// Etkin okumalar, gözlemler ve sürekli izleme birleştirilir (her alan için en yeni değer).
TakimDurumu SonDurum() => DurumYukleyici.Yukle(new Ayarlar(fmDir, takimKimlik, kok));

// Oyundan eksik verileri ister (FMAsistan.Core.Uygulama.OyunGuncelleyici).
async Task<int> Guncelle()
    => await OyunGuncelleyici.GuncelleAsync(new Ayarlar(fmDir, takimKimlik, kok), Console.WriteLine) ? 0 : 3;

static (TeknikEkipUyesi, EkipPersonasi.Nitelikler) EkipOku(string yol)
{
    using var b = JsonDocument.Parse(File.ReadAllText(yol));
    var k = b.RootElement;
    string? M(string a) => k.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    int? S(string a) => k.TryGetProperty("nitelikler", out var n) && n.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    var uye = new TeknikEkipUyesi(M("kimlik") ?? "?", M("ad") ?? "?", M("gorev") ?? "Teknik ekip",
        new Felsefe(M("dizilis"), M("mentalite"), M("pres"), M("markaj")), M("lisans"));
    var nit = new EkipPersonasi.Nitelikler(S("taktikBilgisi"), S("yetenekDegerlendirme"), S("potansiyelDegerlendirme"),
        S("otorite"), S("motivasyon"), S("adamYonetimi"), S("kararlilik"), S("uyum"), S("veriAnalizi"));
    return (uye, nit);
}

static void Yazdir(TakimDurumu d, IReadOnlyList<Bulgu> bulgular)
{
    var v = d.TakimVerimliligi;
    Console.WriteLine($"{d.TakimAdi} — {d.Taktik.Ad}");
    Console.WriteLine($"Verimlilik {v.Genel} | yetenek {v.Yetenek.Genel} | benimseme {v.Benimseme.Genel} | anlayış {v.Anlayis.Genel}" + (v.MacBilgisi != null ? $" | maç bilgisi {v.MacBilgisi.Genel}" : ""));
    if (d.Dizilis.Count > 0)
    {
        Console.WriteLine("\nİlk 11 (topa sahipken → top rakipteyken):");
        foreach (var p in d.Dizilis)
            Console.WriteLine($"  {p.Ad ?? p.OyuncuKimlik,-24} {Pozisyon.Ad(p.IpPozisyon)}, {p.IpRolu?.Ad ?? "?"} → {Pozisyon.Ad(p.OopPozisyon)}, {p.OopRolu?.Ad ?? "?"}");
    }
    var yedek = d.Kadro.Where(k => k.Durum == KadroDurumu.Yedek).Select(k => k.Ad ?? k.Kimlik).ToList();
    if (yedek.Count > 0) Console.WriteLine($"Yedekler: {string.Join(", ", yedek)}");
    if (d.Ayarlar.Count > 0)
    {
        Console.WriteLine("\nTaktik ayarları:");
        foreach (var a in d.Ayarlar) Console.WriteLine($"  {a.Ayar}: {a.Deger}");
    }
    if (d.Rakip is { } r)
    {
        Console.WriteLine($"\nRakip: {r.Ad} — {r.TehditDegerlendirmesi} (en çok: {r.EnCokKullanilanDizilis}; {r.Analiz.Notlar.Count} analiz notu)");
        if (r.MuhtemelTaktik is { } mt)
        {
            Console.WriteLine($"  Muhtemel 11: {mt.Dizilis} — {mt.Ozet}");
            foreach (var o in mt.Oyuncular)
                Console.WriteLine($"    {Pozisyon.Ad(o.Pozisyon),-26} {o.Ad ?? o.OyuncuKimlik}" + (o.Rolu != null ? $" ({o.Rolu.Ad})" : ""));
        }
        foreach (var dz in r.Dizilisler)
            Console.WriteLine($"  {(dz.Faz == Faz.TopaSahipken ? "Topa sahipken" : "Top rakipteyken")}: {dz.Dizilis} %{dz.YuzdeKullanim:0}");
        Console.WriteLine($"  Gözlemci raporu: {r.GucluYonler.Count} güçlü, {r.ZayifYonler.Count} zayıf yön");
    }
    Console.WriteLine();
    foreach (var b in bulgular)
    {
        Console.WriteLine($"[{b.Onem}] {b.Kural} {b.Baslik}");
        foreach (var k in b.Kanit) Console.WriteLine($"    · {k}");
        if (b.Oneri != null) Console.WriteLine($"    → {b.Oneri}");
    }
}

static string KokBul()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !Directory.Exists(Path.Combine(d.FullName, "bilgi"))) d = d.Parent;
    return d?.FullName ?? throw new DirectoryNotFoundException("Proje kökü (bilgi klasörü) bulunamadı.");
}

static void Yardim() => Console.WriteLine("Kullanım: fmasistan guncelle | analiz | sohbet <ekip.json> | sor <ekip.json> \"soru\"");
