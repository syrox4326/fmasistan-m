using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Analiz;

/// <summary>
/// Kendi taktiğimizin deterministik analizi (özellik 3.1). Girdi: oyunun taktiksel
/// verimlilik verisi. Kurallar bilgi/TAKTIK_BILGI_TABANI.md'deki K-xx maddelerine dayanır.
///
/// Eşikler bilinçli olarak sabit ve açık: aynı veri her zaman aynı bulguyu üretir. Eşik
/// değerleri ilk sürüm tahminleridir; gerçek kariyer verisiyle ayarlanacak.
/// </summary>
public static class TaktikAnalizi
{
    /// <summary>Bir alanın "zayıf" sayılması için diğer alanların ortancasından en az bu kadar aşağıda olması.</summary>
    public const int ZayifAlanFarki = 5;
    /// <summary>Mutlak zayıflık eşiği (0-100).</summary>
    public const int ZayifAlanEsigi = 50;
    public const int YuksekKarmasiklik = 65;
    public const int DusukKavrayis = 55;
    public const int DusukYeterlilik = 60;
    public const int DusukRolUygunlugu = 75;
    public const int DusukBenimsemeBileseni = 50;
    public const int DusukRaporKalitesi = 60;

    /// <summary>Taktik alanının oyundaki adı ve onu geliştiren antrenman odağı.</summary>
    public static string AlanAdi(TaktikAlani a) => a switch
    {
        TaktikAlani.OyunKurmaStratejisi => "Oyun kurma stratejisi",
        TaktikAlani.FirsatYaratma => "Fırsat yaratma",
        TaktikAlani.TopluSekil => "Topa sahipken şekil",
        TaktikAlani.BaskiHatti => "Baskı hattı",
        TaktikAlani.TopsuzSekil => "Top rakipteyken şekil",
        TaktikAlani.PasStili => "Pas stili",
        TaktikAlani.PresYogunlugu => "Pres yoğunluğu",
        TaktikAlani.PresTuzagi => "Pres tuzağı",
        _ => a.ToString(),
    };

    public static IReadOnlyList<Bulgu> Calistir(TakimDurumu d)
    {
        var b = new List<Bulgu>();
        var v = d.TakimVerimliligi;

        ZayifAlanlar(v.Anlayis, b);
        Karmasiklik(v.Anlayis, b);
        BenimsemeBulgulari(v.Benimseme, b);
        OyuncuBulgulari(d.Oyuncular, b);
        MacBilgisiBulgulari(v.MacBilgisi, b);
        RolCakismalari(d.Dizilis, b);
        OyunKurmayiOldurenRoller(d.Dizilis, b);
        IzgaraBulgulari(d.VeriMerkezi, b);
        SonMacBulgusu(d.VeriMerkezi, b);

        foreach (var u in d.Uyarilar)
            b.Add(new Bulgu("OYUN", Onem.Orta, "Oyunun uyarısı", u, new[] { "Taktik ekranı durum bayrağı" }, null));

        return b.OrderByDescending(x => x.Onem).ToList();
    }

    /// <summary>Haftalık blok planına alınacak en fazla alan sayısı (her hafta tek odak).</summary>
    public const int PlanlananAlanSayisi = 2;

    /// <summary>
    /// K-01: Antrenmanla düzeltilecek zayıf taktik alanları. Tek bulgu üretir: zayıf alanlar
    /// öncelik sırasıyla, ilk ikisi için haftalık blok planı (FM27'de etki birikmesi için
    /// aynı alanın arka arkaya çalışılması gerekir).
    /// </summary>
    private static void ZayifAlanlar(TaktikAnlayisi a, List<Bulgu> b)
    {
        if (a.Alanlar.Count < 3) return;
        var zayiflar = new List<(TaktikAlani alan, Puan puan, double fark)>();
        foreach (var (alan, puan) in a.Alanlar)
        {
            var digerleri = a.Alanlar.Where(x => x.Key != alan).Select(x => x.Value.Orta).OrderBy(x => x).ToList();
            double fark = digerleri[digerleri.Count / 2] - puan.Orta;
            if (fark >= ZayifAlanFarki || puan.Orta < ZayifAlanEsigi) zayiflar.Add((alan, puan, fark));
        }
        if (zayiflar.Count == 0) return;
        zayiflar = zayiflar.OrderBy(z => z.puan.Orta).ToList();

        double ortanca = a.Alanlar.Values.Select(p => p.Orta).OrderBy(x => x).ElementAt(a.Alanlar.Count / 2);
        var kanit = zayiflar.Select(z => $"{AlanAdi(z.alan)}: {z.puan}").ToList();
        kanit.Add($"Tüm alanların ortancası: {ortanca:0}");
        // Oyunun tavsiyesi yalnızca adını andığı alanla ilgilidir; ilgisiz alanlara eklenmez.
        if (a.AlanTavsiyesi is { } t && zayiflar.Any(z => t.Metin.Contains(AlanAdi(z.alan), StringComparison.CurrentCultureIgnoreCase)))
            kanit.Add($"Oyunun tavsiyesi: \"{t.Metin}\"");

        var plan = zayiflar.Take(PlanlananAlanSayisi)
            .Select((z, i) => $"{i + 1}. hafta: '{AlanAdi(z.alan)}' ağırlıklı blok")
            .ToList();
        var en = zayiflar[0];
        b.Add(new Bulgu("K-01",
            en.fark >= 10 || en.puan.Orta < 45 ? Onem.Yuksek : Onem.Orta,
            "Antrenmanla düzeltilecek alanlar: " + string.Join(", ", zayiflar.Take(PlanlananAlanSayisi).Select(z => AlanAdi(z.alan))),
            "Takım bu alanları taktiğin geri kalanından belirgin şekilde daha az kavramış. FM27'de bu açık maçta doğrudan görünür (ör. pres yerleşiminde kopukluk). Dengeli haftalık programlar bu açığı kapatmıyor.",
            kanit,
            string.Join("; ", plan) + ". Her hafta odak alanı arka arkaya seanslarla çalış; tek seanslık dokunuşların etkisi birikmiyor."));
    }

    // K-02/K-03: Karmaşıklık ve kavrayış.
    private static void Karmasiklik(TaktikAnlayisi a, List<Bulgu> b)
    {
        if (a.Karmasiklik.Orta < YuksekKarmasiklik || a.Kavrayis.Orta >= DusukKavrayis) return;
        var kanit = new List<string> { $"Taktiksel karmaşıklık: {a.Karmasiklik}", $"Taktiksel kavrayış: {a.Kavrayis}", $"Oyuncuların taktik yeteneği: {a.TaktikYetenegi}" };
        if (a.KavrayisTavsiyesi is { } t) kanit.Add($"Oyunun tavsiyesi: \"{t.Metin}\"");
        b.Add(new Bulgu("K-03", Onem.Yuksek,
            "Taktik bu kadro için fazla karmaşık",
            "Karmaşıklık yüksek, kavrayış düşük: takım taktiği yavaş öğreniyor. Karmaşıklığı en çok bireysel oyuncu talimatları ve sık rotasyon gerektiren rol kombinasyonları artırır.",
            kanit,
            "Getirisi belirsiz bireysel oyuncu talimatlarını kaldır. Taktik oturduktan sonra talimatları tek tek geri ekle ve verimliliğe etkisini izle."));
    }

    // K-06/K-07/K-08: Oyuncu benimsemesi.
    private static void BenimsemeBulgulari(Benimseme bm, List<Bulgu> b)
    {
        if (bm.MenajerEgilimleri is { } eg && eg.Orta < DusukBenimsemeBileseni)
        {
            var kanit = new List<string> { $"Menajer eğilimleri: {eg}", $"Benimseme genel: {bm.Genel}" };
            if (bm.Tavsiye is { } t) kanit.Add($"Oyunun tavsiyesi: \"{t.Metin}\"");
            b.Add(new Bulgu("K-07", Onem.Yuksek,
                "Oynanan futbol menajer profilinle çelişiyor",
                "Oyuncular, menajer profilindeki eğilimle sahadaki oyun arasında tutarsızlık görüyor. Bu, benimsemeyi doğrudan düşürür ve kendiliğinden düzelmez.",
                kanit,
                "Ya taktiği profil eğilimine yaklaştır ya da mevcut yaklaşımla sonuç alarak önyargıyı zamanla kır. İkisinin ortasında kalmak en kötü seçenek."));
        }
        if (bm.MenajerDestegi is { } ds && ds.Orta < DusukBenimsemeBileseni)
            b.Add(new Bulgu("K-06", Onem.Orta,
                "Menajere destek düşük",
                "Menajere saygı ve destek, kulüp itibarına göre düşük. Bu zamanla, başarıyla ve oyuncularla ilgilenerek artar.",
                new[] { $"Menajer desteği: {ds}" },
                "Kilit oyuncularla düzenli bire bir görüşmeler yap; kısa vadede taktiği değiştirerek çözülecek bir sorun değil."));
        if (bm.IliskilerPuani is { } il && il.Orta < DusukBenimsemeBileseni)
            b.Add(new Bulgu("K-08", Onem.Orta,
                "Takım içi ve personel ilişkileri zayıf",
                "Takım arkadaşı ve personel ilişkileri benimsemeyi aşağı çekiyor.",
                new[] { $"İlişkiler: {il}" },
                "Transferlerde gözlemci raporunda 'takıma uyum sağlayabilir' notu olan oyuncuları önceliklendir; uygun kişilikte antrenörler seç."));
    }

    // K-04/K-05: Oyuncu bazında yeterlilik ve rol uygunluğu.
    private static void OyuncuBulgulari(IReadOnlyList<OyuncuVerimliligi> oyuncular, List<Bulgu> b)
    {
        foreach (var o in oyuncular)
        {
            var y = o.Verimlilik.Yetenek;
            string ad = o.Ad ?? o.OyuncuKimlik;
            if (y.Yeterlilik.Orta < DusukYeterlilik)
            {
                var kanit = new List<string> { $"{ad} yeterlilik: {y.Yeterlilik}", $"Rol uygunluğu: {y.RolUygunlugu}" };
                if (y.Tavsiye is { } t) kanit.Add($"Oyunun tavsiyesi: \"{t.Metin}\"");
                b.Add(new Bulgu("K-04", Onem.Orta,
                    $"{ad}: taktiğin istediği kaliteye yetmiyor",
                    "Rolüne uygun olsa bile oyuncunun genel nitelikleri bu oyun tarzı için yetersiz. Genç oyuncularda oynayarak gelişebilir; olgun oyuncularda pek değişmez.",
                    kanit,
                    "Yaşı gelişime uygunsa sabret; değilse bu pozisyon için alternatif ya da transfer hedefi belirle."));
            }
            if (y.RolUygunlugu.Orta < DusukRolUygunlugu)
            {
                b.Add(new Bulgu("K-05", Onem.Orta,
                    $"{ad}: rolüne uygun değil",
                    "Oyuncunun doğal oyunu verilen rolle çelişiyor; sezon boyu verim kaybı demek.",
                    new[] { $"{ad} rol uygunluğu: {y.RolUygunlugu}" },
                    "Oyuncunun güçlü yönlerine uyan bir rol dene ya da bireysel antrenmanda bu rolü çalıştır."));
            }
        }
    }

    /// <summary>
    /// K-22: Aynı kanatta oyun kurulumunda aynı alanı kullanan rol çiftleri (topa sahipken).
    /// Bilgi tabanındaki iki çift: Kanat Oyuncusu + Kanat Bek, Dış Kanat Forvet + Kanat Bek.
    /// </summary>
    private static readonly (string onde, string arkada)[] CakisanCiftler =
    {
        ("Kanat Oyuncusu", "Kanat Bek"),
        ("Dış Kanat Forvet", "Kanat Bek"),
    };

    private static void RolCakismalari(IReadOnlyList<TaktikPozisyonu> dizilis, List<Bulgu> b)
    {
        foreach (var kanat in new[] { Kanat.Sol, Kanat.Sag })
        {
            var buKanat = dizilis.Where(p => Pozisyon.KanatOf(p.IpPozisyon) == kanat && p.IpRolu != null).ToList();
            foreach (var (onde, arkada) in CakisanCiftler)
            {
                var o = buKanat.FirstOrDefault(p => p.IpRolu!.Ad == onde);
                var a = buKanat.FirstOrDefault(p => p.IpRolu!.Ad == arkada);
                if (o == null || a == null) continue;
                string k = kanat == Kanat.Sol ? "Sol" : "Sağ";
                b.Add(new Bulgu("K-22", Onem.Orta,
                    $"{k} kanatta rol çakışması: {onde} + {arkada}",
                    "İki oyuncu oyun kurulumunda aynı geniş alanı kullanıyor; birbirlerinin alanını daraltır ve iç koridoru boş bırakır.",
                    new[] { $"{o.Ad ?? o.OyuncuKimlik}: {Pozisyon.Ad(o.IpPozisyon)}, {onde}", $"{a.Ad ?? a.OyuncuKimlik}: {Pozisyon.Ad(a.IpPozisyon)}, {arkada}" },
                    "Beki 'İç Koridorda Oynayan Kanat Bek' yap ya da yarım alanı dolduracak ileri bir orta saha rolü kullan."));
            }
        }
    }

    /// <summary>K-25: Risksiz oynayan kaleci + çakılı stoper → oyun kurulamaz, top dikine vurulur.</summary>
    private static void OyunKurmayiOldurenRoller(IReadOnlyList<TaktikPozisyonu> dizilis, List<Bulgu> b)
    {
        var kaleci = dizilis.FirstOrDefault(p => p.IpRolu?.Ad == "Risksiz Oynayan Kaleci");
        var stoperler = dizilis.Where(p => p.IpRolu?.Ad == "Çakılı Stoper").ToList();
        if (kaleci == null || stoperler.Count == 0) return;
        var kanit = new List<string> { $"{kaleci.Ad ?? kaleci.OyuncuKimlik}: Risksiz Oynayan Kaleci" };
        kanit.AddRange(stoperler.Select(s => $"{s.Ad ?? s.OyuncuKimlik}: Çakılı Stoper"));
        b.Add(new Bulgu("K-25", Onem.Yuksek,
            "Bu rollerle geriden oyun kurulamaz",
            "Risksiz oynayan kaleci ve çakılı stoper topu dikine uzaklaştırmaya meyillidir; hangi oyun kurulumunu seçersen seç kısa kurulum gerçekleşmez.",
            kanit,
            "Ayağı zayıf oyunculara odak rolü vermek istemiyorsan nötr 'Kaleci' ve 'Stoper' rollerini kullan."));
    }

    /// <summary>
    /// Taktik ızgarası: oyunun kendi bölge değerlendirmesi. "Sıkıntılı" geçen bölgeler oyunun
    /// tespit ettiği açıklardır; oyunun bölge yorumları kanıt olarak aktarılır.
    /// </summary>
    private static void IzgaraBulgulari(VeriMerkezi vm, List<Bulgu> b)
    {
        var sorunlu = vm.Izgara.Where(z => z.Durum.Contains("Sıkıntılı", StringComparison.CurrentCultureIgnoreCase)
                                        && !z.Durum.Contains("Sıkıntısız", StringComparison.CurrentCultureIgnoreCase)).ToList();
        if (sorunlu.Count == 0) return;
        var kanit = sorunlu.Select(z => $"{z.Ad}: {z.Durum}" +
                                        (z.Yorumlar.Count > 0 ? " — " + string.Join(" ", z.Yorumlar) : "")).ToList();
        b.Add(new Bulgu("OYUN-IZGARA", sorunlu.Any(z => !z.Durum.StartsWith("Hafif", StringComparison.CurrentCultureIgnoreCase)) ? Onem.Yuksek : Onem.Orta,
            $"Taktik ızgarasında {sorunlu.Count} sıkıntılı bölge",
            "Oyunun taktik ızgarası bu bölgeleri mevcut diziliş ve oyuncularla sorunlu görüyor.",
            kanit,
            "Bu bölgeleri kapsayan oyuncuların rol ve görevlerini gözden geçir; oyunun bölge yorumlarındaki oyuncu adlarına bak."));
    }

    /// <summary>Son maç: oyunun analiz ekibinin genel değerlendirmesi ve temel sayılar.</summary>
    private static void SonMacBulgusu(VeriMerkezi vm, List<Bulgu> b)
    {
        if (vm.SonMac is not { } s) return;
        var kanit = new List<string>();
        if (s.Mac != null) kanit.Add(s.Mac);
        if (s.Xg != null) kanit.Add($"xG: {s.Xg:0.00}");
        if (s.Sut != null) kanit.Add($"Şut: {s.Sut}, isabetli: {s.IsabetliSut}");
        if (s.Degerlendirme != null) kanit.Add($"Analiz ekibi: \"{s.Degerlendirme}\"");
        if (kanit.Count == 0) return;
        b.Add(new Bulgu("OYUN-SONMAC", Onem.Bilgi, "Son maç", "Veri Merkezi'nin son maç değerlendirmesi.", kanit, null));
    }

    // K-09: Rakip raporu kalitesi (gerçekçilik: ekip kalitesine bağlı).
    private static void MacBilgisiBulgulari(MacBilgisi? m, List<Bulgu> b)
    {
        if (m == null) return;
        var zayif = new List<string>();
        if (m.RaporKalitesi is { } r && r.Orta < DusukRaporKalitesi) zayif.Add($"Rapor kalitesi: {r}");
        if (m.RakipOyuncuBilgisi is { } k && k.Orta < DusukRaporKalitesi) zayif.Add($"Rakip oyuncu bilgisi: {k}");
        if (zayif.Count == 0) return;
        b.Add(new Bulgu("K-09", Onem.Orta,
            "Rakip hakkında yeterli bilgimiz yok",
            "Sonraki maç bilgisi yalnızca gözlemci ve analist kalitesiyle yükselir.",
            zayif,
            "Sorumluluklar ekranında rakip gözlemci ve analiz raporlarını ekipteki en iyi rakip değerlendiren kişilere ata."));
    }
}
