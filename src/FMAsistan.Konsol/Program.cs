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
    // Etkin okumalar, gözlemler ve sürekli izleme birleştirilir (her alan için en yeni değer).
    var birlesik = OyunVerisi.Yukle(veri);
    if (birlesik.Count == 0) throw new FileNotFoundException("Eklenti verisi bulunamadı. Oyun açıkken eklentiyle bir okuma yap.");
    var adlar = OyunVerisi.Adlar(birlesik);
    string kimlikDosyasi = Path.Combine(veri, "kimlik-adlari-sonuc.json");
    var kimlikAdlari = File.Exists(kimlikDosyasi)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(kimlikDosyasi))
        : null;
    var temel = EklentiCozucu.TakimDurumu(birlesik, takimKimlik, adlar);
    return temel with
    {
        VeriMerkezi = VeriMerkeziCozucu.Coz(birlesik, takimKimlik, adlar),
        Ayarlar = VeriMerkeziCozucu.Ayarlar(birlesik, takimKimlik, kimlikAdlari),
        Rakip = VeriMerkeziCozucu.Rakip(birlesik, takimKimlik, adlar),
        Kadro = EklentiCozucu.Kadro(birlesik, takimKimlik, temel.Dizilis.Select(p => p.OyuncuKimlik).ToList(), adlar),
    };
}

// Oyundan eksik verileri ister: kadronun adları, talimat kimliklerinin iç adları, temel taktik verisi.
async Task<int> Guncelle()
{
    var birlesik = OyunVerisi.Yukle(veri);
    var adlar = OyunVerisi.Adlar(birlesik);
    var durum = SonDurum();
    var istekler = new List<object>
    {
        new { kaynak = "TeamTacticReference:" + takimKimlik, yollar = new[] { "Name", "MentalityString", "MentalityDropdown", "MentalityDropdownOptions",
              "TacticalStyleDropdown", "TacticalStyleDropdownOptions", "TacticalEffectiveness", "TacticalPositionsCombined",
              "FilteredPlayers", "FilteredSubs" }.Concat(Talimatlar.Ozellikler.Keys).ToArray() },
        new { kaynak = "TeamReference:" + takimKimlik, yollar = new[] { "Name", "NextFixture", "MostUsedFormation", "GoalAnalysisScoring", "GoalAnalysisConceding", "ExpectedFormations" } },
    };
    var eksik = durum.Kadro.Select(k => k.Kimlik).Concat(durum.Dizilis.Select(p => p.OyuncuKimlik)).Distinct().Where(k => !adlar.ContainsKey(k)).ToList();
    foreach (var k in eksik) istekler.Add(new { kaynak = k, yollar = new[] { "Name" } });
    Console.WriteLine($"Oyundan isteniyor: {istekler.Count} kaynak ({eksik.Count} eksik oyuncu adı).");
    if (!await EklentiIste("oku.json", JsonSerializer.Serialize(new { sure = 12, istekler }), Path.Combine(veri, "oku", "son.txt"))) return 3;

    // Talimat değerlerinin iç adları (PropertyID → ad).
    birlesik = OyunVerisi.Yukle(veri);
    var kimlikler = new List<uint>();
    if (birlesik.TryGetValue("TeamTacticReference:" + takimKimlik, out var t))
        foreach (var oz in Talimatlar.Ozellikler.Keys)
            if (t.TryGetValue(oz, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetUInt32(out var id)) kimlikler.Add(id);
    if (kimlikler.Count > 0)
    {
        string sonucDosyasi = Path.Combine(veri, "kimlik-adlari-sonuc.json");
        var eski = File.Exists(sonucDosyasi) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(sonucDosyasi))! : new();
        if (!await EklentiIste("kimlik-adlari.json", JsonSerializer.Serialize(kimlikler), sonucDosyasi)) return 3;
        var yeni = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(sonucDosyasi))!;
        foreach (var (k, v) in eski) yeni.TryAdd(k, v);
        File.WriteAllText(sonucDosyasi, JsonSerializer.Serialize(yeni, new JsonSerializerOptions { WriteIndented = true }));
    }
    Console.WriteLine("Güncellendi.");
    return 0;
}

// Eklentiye bir komut dosyası bırakır ve sonuç dosyası yenilenene kadar bekler.
async Task<bool> EklentiIste(string komutDosyasi, string icerik, string sonucDosyasi)
{
    var once = File.Exists(sonucDosyasi) ? File.GetLastWriteTimeUtc(sonucDosyasi) : DateTime.MinValue;
    string yol = Path.Combine(veri, komutDosyasi);
    File.WriteAllText(yol + ".tmp", icerik);
    File.Move(yol + ".tmp", yol, overwrite: true);
    for (int i = 0; i < 120; i++)
    {
        await Task.Delay(250);
        if (File.Exists(sonucDosyasi) && File.GetLastWriteTimeUtc(sonucDosyasi) > once) { await Task.Delay(200); return true; }
    }
    Console.Error.WriteLine("Eklenti yanıt vermedi. Oyun açık ve kariyer yüklü mü?");
    return false;
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
    var yedek = d.Kadro.Where(k => k.Durum == KadroDurumu.Yedek).Select(k => k.Ad ?? k.Kimlik).ToList();
    if (yedek.Count > 0) Console.WriteLine($"Yedekler: {string.Join(", ", yedek)}");
    if (d.Ayarlar.Count > 0)
    {
        Console.WriteLine("\nTaktik ayarları:");
        foreach (var a in d.Ayarlar) Console.WriteLine($"  {a.Ayar}: {a.Deger}");
    }
    if (d.Rakip is { } r)
        Console.WriteLine($"\nRakip: {r.Ad} — {r.TehditDegerlendirmesi} (en çok: {r.EnCokKullanilanDizilis}; {r.Analiz.Notlar.Count} analiz notu)");
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
