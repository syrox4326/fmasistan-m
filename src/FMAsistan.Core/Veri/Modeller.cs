namespace FMAsistan.Core.Veri;

/// <summary>
/// Oyunun gösterdiği bir puan. Oyun, bilgisi eksik olduğunda tek değer yerine aralık verir
/// (UsesRange); bu belirsizlik analizde korunur. Gerçekçilik kuralı gereği oyunun bize
/// göstermediği kesinliği uydurmayız.
/// </summary>
public readonly record struct Puan(int Min, int Max)
{
    public bool Aralikli => Min != Max;
    public double Orta => (Min + Max) / 2.0;
    public override string ToString() => Aralikli ? $"{Min}-{Max}" : Min.ToString();
    public static Puan Tek(int v) => new(v, v);
}

/// <summary>FM27 taktik anlayışı alanları (oyunun TacticalUnderstanding yapısı).</summary>
public enum TaktikAlani
{
    OyunKurmaStratejisi,   // BuildUpStrategy
    FirsatYaratma,         // ChanceCreation
    TopluSekil,            // InPossessionShape
    BaskiHatti,            // LineOfEngagement
    TopsuzSekil,           // OutOfPossessionShape
    PasStili,              // PassingStyle
    PresYogunlugu,         // PressingIntensity
    PresTuzagi,            // PressingTrap
}

public sealed record OyunTavsiyesi(string Metin, bool Olumlu);

public sealed record TaktikAnlayisi(
    Puan Genel,
    IReadOnlyDictionary<TaktikAlani, Puan> Alanlar,
    Puan Karmasiklik,
    Puan Kavrayis,
    Puan TaktikYetenegi,
    OyunTavsiyesi? AlanTavsiyesi,
    OyunTavsiyesi? KavrayisTavsiyesi);

public sealed record OyuncuYetenegi(Puan Genel, Puan Yeterlilik, Puan RolUygunlugu, OyunTavsiyesi? Tavsiye);

public sealed record Benimseme(Puan Genel, Puan? MenajerDestegi, Puan? IliskilerPuani, Puan? MenajerEgilimleri, OyunTavsiyesi? Tavsiye);

public sealed record MacBilgisi(Puan Genel, Puan? RaporKalitesi, Puan? RakipOyuncuBilgisi, OyunTavsiyesi? Tavsiye);

/// <summary>Taktiksel verimlilik: takım ya da tek oyuncu için.</summary>
public sealed record Verimlilik(
    Puan Genel,
    OyuncuYetenegi Yetenek,
    Benimseme Benimseme,
    TaktikAnlayisi Anlayis,
    MacBilgisi? MacBilgisi);

public sealed record OyuncuVerimliligi(string OyuncuKimlik, string? Ad, Verimlilik Verimlilik);

/// <summary>
/// Taktiğin oyunda görünen özeti. Yalnızca oyunun arayüzde gösterdiği kavramlar (oyundaki
/// adlarıyla) tutulur; oyunun iç sayıları (ör. TacticalIntensity) tutulmaz, çünkü yapay
/// zekâya verilince oyunda olmayan ayarlar önerilmesine yol açıyor.
/// </summary>
public sealed record TaktikOzeti(string Ad, string Mentalite, string? TaktikStili, string? EnCokKullanilanDizilis);

/// <summary>Teknik ekip üyesinin futbol felsefesi (oyunun PersonReference.Preferred* alanları).</summary>
public sealed record Felsefe(string? Dizilis, string? Mentalite, string? Pres, string? Markaj);

public sealed record TeknikEkipUyesi(string Kimlik, string Ad, string Gorev, Felsefe Felsefe, string? Lisans);

/// <summary>Analizin girdisi: oyundan okunan bir anlık görüntü.</summary>
/// <param name="Dizilis">İlk 11'in iki fazdaki pozisyon ve rolleri; okunamadıysa boş.</param>
public sealed record TakimDurumu(
    string TakimAdi,
    TaktikOzeti Taktik,
    Verimlilik TakimVerimliligi,
    IReadOnlyList<OyuncuVerimliligi> Oyuncular,
    IReadOnlyList<string> Uyarilar,
    IReadOnlyList<TaktikPozisyonu> Dizilis)
{
    /// <summary>Takımın Veri Merkezi (oyunun analiz ekibinin raporları); okunmadıysa boş.</summary>
    public VeriMerkezi VeriMerkezi { get; init; } = VeriMerkezi.Bos;

    /// <summary>Taktik ekranındaki ayarlar, oyunun sunduğu seçeneklerle.</summary>
    public IReadOnlyList<OyunAyari> Ayarlar { get; init; } = Array.Empty<OyunAyari>();

    /// <summary>Sıradaki rakip hakkındaki rapor; kaydedilmemişse null.</summary>
    public RakipRaporu? Rakip { get; init; }

    /// <summary>Kadro: ilk 11, yedekler ve kadro dışı (taktik ekranındaki oyuncu listesi).</summary>
    public IReadOnlyList<KadroOyuncusu> Kadro { get; init; } = Array.Empty<KadroOyuncusu>();

    /// <summary>Kadrodaki oyuncuların ayrıntılı profili (otomatik tarama + bellek).</summary>
    public IReadOnlyList<OyuncuProfili> Profiller { get; init; } = Array.Empty<OyuncuProfili>();

    /// <summary>Teknik ekip (oyundan otomatik).</summary>
    public IReadOnlyList<EkipUyesiProfili> TeknikEkip { get; init; } = Array.Empty<EkipUyesiProfili>();

    public AntrenmanDurumu Antrenman { get; init; } = AntrenmanDurumu.Bos;
    public KulupDurumu? Kulup { get; init; }
    public FiksturDurumu Fikstur { get; init; } = FiksturDurumu.Bos;

    /// <summary>Oyun içi tarih (bellekten), ör. 2026-11-07.</summary>
    public string? OyunTarihi { get; init; }

    /// <summary>Verinin kaynakları ve zamanları (arayüzde gösterilir).</summary>
    public IReadOnlyList<string> Kaynaklar { get; init; } = Array.Empty<string>();

    public OyuncuProfili? Profil(string kimlik) => Profiller.FirstOrDefault(p => p.Kimlik == kimlik);
}

public enum KadroDurumu { IlkOnbir, Yedek, KadroDisi }

public sealed record KadroOyuncusu(string Kimlik, string? Ad, KadroDurumu Durum);
