using System.Text;
using System.Text.Json;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using FMAsistan.Core.Yz;

// FM Asistanım — geliştirme konsolu.
//   fmasistan analiz                 → son eklenti okumasından taktik analizi
//   fmasistan sohbet <ekip-dosyası>  → teknik ekip üyesiyle sohbet (çıkmak için boş satır)
//   fmasistan sor <ekip-dosyası> "soru"  → tek soru

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

string fmDir = Environment.GetEnvironmentVariable("FM_DIZIN") ?? @"C:\XboxGames\Football Manager 27\Content";
string veri = Path.Combine(fmDir, "BepInEx", "FMAsistan", "veri");
string takimKimlik = Environment.GetEnvironmentVariable("FM_TAKIM") ?? "402654676";
string kok = KokBul();

if (args.Length == 0) { Yardim(); return 1; }

try
{
    var durum = SonDurum();
    var bulgular = TaktikAnalizi.Calistir(durum);

    switch (args[0])
    {
        case "analiz":
            Yazdir(durum, bulgular);
            return 0;

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

TakimDurumu SonDurum()
{
    // Tüm okumalar eskiden yeniye birleştirilir (dosya adları zaman damgalı).
    var dosyalar = Directory.GetFiles(Path.Combine(veri, "oku"), "*.json").OrderBy(f => f).ToList();
    if (dosyalar.Count == 0) throw new FileNotFoundException("Eklenti okuması bulunamadı. Oyunda eklentiyle bir okuma yap.");
    var birlesik = EklentiCozucu.Birlestir(dosyalar.Select(File.ReadAllText));
    var adlar = birlesik.Where(k => k.Key.StartsWith("PersonReference:") && k.Value.TryGetValue("Name", out var n) && n.ValueKind == JsonValueKind.String)
                        .ToDictionary(k => k.Key, k => k.Value["Name"].GetString()!);
    return EklentiCozucu.TakimDurumu(birlesik, takimKimlik, adlar);
}

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

static void Yardim() => Console.WriteLine("Kullanım: fmasistan analiz | sohbet <ekip.json> | sor <ekip.json> \"soru\"");
