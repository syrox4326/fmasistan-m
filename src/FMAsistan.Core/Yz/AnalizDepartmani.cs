using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Yz;

public enum RaporTuru { Taktik, MacOnu, Kadro, Antrenman }

public sealed record AnalizRaporu(RaporTuru Tur, string Baslik, string Metin, DateTime Zaman, string Imza);

/// <summary>
/// Kulübün analiz departmanı: oyundan okunan tüm veriyi (durum dosyası), deterministik motorun
/// bulgularını ve kulübün bilgi tabanını alıp ayrıntılı rapor yazar. Rapor, verinin kendisini
/// tekrarlamaz; veriden sonuca akıl yürütür (tümevarım, tümdengelim, analoji) ve her iddiayı
/// veriye bağlar. Aynı veriyle aynı rapor yeniden yazılmaz (imzaya göre önbellek).
/// </summary>
public sealed class AnalizDepartmani
{
    private readonly IYapayZeka _yz;
    private readonly string _klasor;

    public AnalizDepartmani(IYapayZeka yz, string onbellekKlasoru)
    {
        _yz = yz;
        _klasor = onbellekKlasoru;
        Directory.CreateDirectory(_klasor);
    }

    public static string Baslik(RaporTuru t) => t switch
    {
        RaporTuru.Taktik => "Taktik analizi",
        RaporTuru.MacOnu => "Maç önü raporu",
        RaporTuru.Kadro => "Kadro ve oyuncu analizi",
        RaporTuru.Antrenman => "Antrenman planı",
        _ => t.ToString(),
    };

    /// <summary>Raporun girdisi: durum dosyası. Değişmezse rapor yeniden yazılmaz.</summary>
    public static string Girdi(TakimDurumu d, IReadOnlyList<Bulgu> bulgular, KadroRaporu kadro)
        => DurumDosyasi.Yaz(d, bulgular, kadro);

    public static string Imza(RaporTuru t, string girdi)
    {
        // Kaynak zamanları her taramada değişir; veri aynıysa rapor yeniden yazılmasın.
        string veri = string.Join("\n", girdi.Split('\n').Where(l => !l.StartsWith("Veri kaynakları:")));
        var h = SHA256.HashData(Encoding.UTF8.GetBytes(t + "\n" + Surum + "\n" + veri));
        return Convert.ToHexString(h)[..16];
    }

    // Talimat değişince eski önbellek geçersiz olsun.
    private const string Surum = "1";

    public AnalizRaporu? Onbellek(RaporTuru t, string girdi) => Oku(Path.Combine(_klasor, $"{t}-{Imza(t, girdi)}.json"));

    /// <summary>Bu tür için en son yazılan rapor (veri değişmiş olsa da).</summary>
    public AnalizRaporu? SonRapor(RaporTuru t)
    {
        var f = new DirectoryInfo(_klasor).EnumerateFiles($"{t}-*.json").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        return f == null ? null : Oku(f.FullName);
    }

    public async Task<AnalizRaporu> HazirlaAsync(RaporTuru t, string girdi, string bilgiTabani, string? analist, CancellationToken iptal = default)
    {
        if (Onbellek(t, girdi) is { } hazir) return hazir;
        string sistem = SistemTalimati(t, girdi, bilgiTabani, analist);
        var c = await _yz.SorAsync(sistem, Gorev(t), null, iptal);
        var rapor = new AnalizRaporu(t, Baslik(t), c.Metin.Trim(), DateTime.Now, Imza(t, girdi));
        File.WriteAllText(Path.Combine(_klasor, $"{t}-{rapor.Imza}.json"), JsonSerializer.Serialize(rapor));
        foreach (var eski in new DirectoryInfo(_klasor).EnumerateFiles($"{t}-*.json").OrderByDescending(f => f.LastWriteTimeUtc).Skip(5))
            try { eski.Delete(); } catch (IOException) { }
        return rapor;
    }

    private static AnalizRaporu? Oku(string yol)
    {
        try { return File.Exists(yol) ? JsonSerializer.Deserialize<AnalizRaporu>(File.ReadAllText(yol)) : null; }
        catch (Exception e) when (e is IOException or JsonException) { return null; }
    }

    public static string SistemTalimati(RaporTuru t, string girdi, string bilgiTabani, string? analist)
    {
        var s = new StringBuilder();
        s.AppendLine($"Sen bir Football Manager 27 kariyerinde kulübün analiz departmanının başısın{(analist != null ? $" ({analist})" : "")}. " +
                     "Menajere iç rapor yazıyorsun. Türkçe, profesyonel, net ve ayrıntılı yaz; emoji, ünlem ve pazarlama dili kullanma.");
        s.AppendLine();
        s.AppendLine("## Raporun amacı");
        s.AppendLine("Menajer veriyi zaten oyunda görüyor. Senden verinin tekrarını değil, verinin ne anlama geldiğini, neden böyle olduğunu ve ne yapılması gerektiğini istiyor. " +
                     "Her bölümde veriden sonuca akıl yürüt:");
        s.AppendLine("- Tümevarım: birden çok gözlemi (sayılar, oyunun notları, rol yıldızları, kondisyon, form) bir araya getirip ortak örüntüyü çıkar. Örüntüyü destekleyen ve çürüten veriyi birlikte göster.");
        s.AppendLine("- Tümdengelim: genel bir futbol ilkesinden (kulübün bilgi tabanındaki K-kodlu kurallar) bizim duruma özel sonuç çıkar: ilke → bizdeki veri → sonuç.");
        s.AppendLine("- Analoji: bizdeki durumu bilgi tabanındaki bilinen bir örüntüye ya da futbol mantığındaki benzer bir duruma benzet ve benzerliğin nerede bozulduğunu da söyle.");
        s.AppendLine("Sonuçların birbirine bağlansın: bir ayarın, bir oyuncunun ve bir antrenman kararının aynı soruna nasıl dokunduğunu göster.");
        s.AppendLine();
        s.AppendLine("## Kesin kurallar");
        s.AppendLine("1. Her somut iddia aşağıdaki veriye dayansın; dayandığın sayıyı ya da metni parantez içinde an (ör. 'Baskı hattı 44', 'kondisyon %86', 'K-04'). Veride olmayan bir sayı, istatistik ya da olay uydurma. Bilinmeyeni 'veride yok' diye açıkça söyle.");
        s.AppendLine("2. Yalnızca FM27'de gerçekten var olan ayarları, rolleri ve antrenman seçeneklerini öner; oyundaki adlarıyla. Talimatların seçili değerleri veride var; tam seçenek listesi her talimat için bilinmiyorsa 'talimat adı + yön' olarak öner (ör. 'Tempo: bir kademe düşür'). '6'dan 7'ye' gibi oyunda karşılığı olmayan sayısal ayar önerme.");
        s.AppendLine("3. Rol önerirken slot uygunluk tablosundaki oyunun kendi yıldızlarını kullan. Oyuncu değişikliği önerirken yalnızca kadrodaki oyuncuları kullan.");
        s.AppendLine("4. Para her zaman euro ve maaşlar yıllık. Sterlin ya da haftalık yazma.");
        s.AppendLine("5. Gerçekçilik: rakip hakkında yalnızca analiz ekibimizin raporundakini bil; aralıklı (gözlemci görüşü) değerleri kesinmiş gibi kullanma, belirsizliği söyle.");
        s.AppendLine("6. Menajeri memnun etmek için yazma. Taktiğin ya da bir kararın zayıfsa açıkça söyle ve gerekçelendir.");
        s.AppendLine("7. Uzunluk: ayrıntılı ol. Her öneri; ne yapılacağı (oyunda hangi ekranda hangi ayar/rol/oyuncu), neden (veri + ilke), beklenen etki, risk ve bir sonraki maçta neye bakılarak doğrulanacağı ile birlikte gelsin.");
        s.AppendLine();
        s.AppendLine("## Biçim (Markdown)");
        s.AppendLine("Başlıklar `##` ile. Tablo kullanabilirsin. Şu bölümler bu sırayla olsun:");
        foreach (var b in Bolumler(t)) s.AppendLine($"- ## {b}");
        s.AppendLine("Rapora başlık ya da selamlama ekleme; doğrudan ilk bölümle başla.");
        s.AppendLine();
        s.AppendLine("## Oyundan okunan veri");
        s.AppendLine(girdi.Trim());
        s.AppendLine();
        if (!string.IsNullOrWhiteSpace(bilgiTabani))
        {
            s.AppendLine("## Kulübün taktik bilgi tabanı (ilkeler; K-kodlarıyla an)");
            s.AppendLine(bilgiTabani.Trim());
        }
        return s.ToString();
    }

    private static IEnumerable<string> Bolumler(RaporTuru t) => t switch
    {
        RaporTuru.Taktik => new[]
        {
            "Hüküm (taktiğin bugünkü durumu üç-beş cümlede: en büyük sorun, en büyük güç, öncelik)",
            "Taktiksel verimliliğin teşhisi (genel puanın bileşenlerine ayrıştırılması: hangi bileşen ve alan puanı aşağı çekiyor, neden)",
            "Topa sahipken: oyun planı ve uyumu (talimatlar, roller, oyuncuların bu plana uygunluğu; birbirini destekleyen ve çelişen ayarlar)",
            "Top rakipteyken: savunma ve pres yapısı (baskı hattı, savunma hattı, pres yoğunluğu ve tuzağı; oyuncu profilleriyle uyum, açık bırakılan alanlar)",
            "Geçişler ve dinlenme savunması (top kaybı/kazanımı talimatları, kontraya açık alanlar, taktik ızgarasındaki sıkıntılı bölgelerle ilişkisi)",
            "Rol ve oyuncu uyumu (slot uygunluk tablosu: yanlış role konmuş oyuncular, kadroda daha uygun olanlar, gerekçeleriyle)",
            "Veri Merkezi'nin söyledikleri (oyunun analiz notları ve son maç verisinin taktikle bağlantısı)",
            "Öneriler (öncelik sırasıyla, numaralı; her biri: oyundaki ayar/rol/oyuncu, neden, beklenen etki, risk, doğrulama ölçütü)",
            "Riskler ve belirsizlikler (verinin sınırları, aralıklı değerler, eksik okumalar)",
        },
        RaporTuru.MacOnu => new[]
        {
            "Hüküm (maçın anahtarı üç-beş cümlede)",
            "Rakibin oyun modeli (muhtemel 11, dizilişler ve kullanım yüzdeleri, muhtemel oyun sistemi; raporun güvenilirliği)",
            "Rakibin güçlü yönleri ve bize etkisi (her güçlü yön için: bizim hangi oyuncumuz/bölgemiz bundan etkilenir, neden)",
            "Rakibin zayıf yönleri ve nasıl kullanılır (her zayıf yön için: hangi oyuncumuz, hangi talimat ya da rol bunu kullanır)",
            "Bire bir eşleşmeler (muhtemel 11'e göre sahadaki karşılıklı oyuncular; avantaj ve dezavantajlar)",
            "Bizim durumumuz (kondisyon, maç keskinliği, sakatlık riski, form; ilk 11 için risk listesi)",
            "Maç planı (ilk 11 ve roller, takım talimatlarında bu maça özel değişiklikler, duran toplar, oyuncuya özel talimatlar; gerekçeleriyle)",
            "Maç içi senaryolar (öne geçersek, geriye düşersek, 60. dakikada beraberlikte: hangi yedek, hangi talimat; yalnızca yedeklerden)",
            "Riskler ve belirsizlikler",
        },
        RaporTuru.Kadro => new[]
        {
            "Hüküm (kadronun bugünkü durumu üç-beş cümlede)",
            "İlk 11'in değerlendirmesi (her slot: oyuncu, rol uyumu yıldızları, form, fiziksel durum; zayıf halkalar)",
            "Rotasyon ve derinlik (her mevki için kadrodaki alternatifler ve aradaki fark; derinliği zayıf mevkiler)",
            "Fiziksel durum ve yük yönetimi (kondisyon, maç keskinliği, sakatlık riski; fikstür yoğunluğuna göre kimin dinlenmesi gerektiği)",
            "Form ve gelişim (sezon ortalaması, antrenman puanı, ikisi arasındaki farklar ve olası nedenleri)",
            "Kişilik ve soyunma odası (kişilikler, eğilimler, antrenman mutluluğu; liderlik ve risk taşıyan profiller)",
            "Sözleşme ve maliyet (maaşlar €/yıl, biten sözleşmeler, maliyet-verim dengesi)",
            "Öneriler (numaralı; her biri: kim, ne, neden, beklenen etki, risk)",
            "Riskler ve belirsizlikler",
        },
        RaporTuru.Antrenman => new[]
        {
            "Hüküm (antrenmanın bugünkü önceliği üç-beş cümlede)",
            "Taktik anlayışı açıkları ve antrenmanla ilişkisi (zayıf taktik alanları; hangi antrenman kategorisi bunlara dokunur)",
            "Antrenör atamaları (her kategoride kim çalıştırıyor, yıldızları; daha iyi atama mümkün mü, ekibin öznitelikleriyle)",
            "Haftalık plan (fikstüre göre: maç günü, maç sonrası, iki maç arası; her gün için oyundaki antrenman kategorisi ve odak, gerekçeleriyle)",
            "Bireysel antrenman (oyuncu bazında: yoğunluk, rol eğitimi, odak; fiziksel durum ve rol uyumu verisine göre)",
            "Riskler (yük, sakatlık, mutluluk) ve izlenecek ölçütler",
        },
        _ => Array.Empty<string>(),
    };

    private static string Gorev(RaporTuru t) => t switch
    {
        RaporTuru.Taktik => "Taktiğimizin ayrıntılı analiz raporunu yaz.",
        RaporTuru.MacOnu => "Sıradaki maç için ayrıntılı maç önü raporunu yaz. Rakip raporu yoksa bunu en başta söyle ve yalnızca bizim durumumuzu analiz et.",
        RaporTuru.Kadro => "Kadronun ayrıntılı analiz raporunu yaz.",
        RaporTuru.Antrenman => "Önümüzdeki hafta için ayrıntılı antrenman planı raporunu yaz.",
        _ => "Rapor yaz.",
    };
}
