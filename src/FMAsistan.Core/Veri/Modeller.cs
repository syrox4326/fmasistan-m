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

public sealed record TaktikOzeti(string Ad, string Mentalite, int Yogunluk, string? EnCokKullanilanDizilis);

/// <summary>Teknik ekip üyesinin futbol felsefesi (oyunun PersonReference.Preferred* alanları).</summary>
public sealed record Felsefe(string? Dizilis, string? Mentalite, string? Pres, string? Markaj);

public sealed record TeknikEkipUyesi(string Kimlik, string Ad, string Gorev, Felsefe Felsefe, string? Lisans);

/// <summary>Analizin girdisi: oyundan okunan bir anlık görüntü.</summary>
public sealed record TakimDurumu(
    string TakimAdi,
    TaktikOzeti Taktik,
    Verimlilik TakimVerimliligi,
    IReadOnlyList<OyuncuVerimliligi> Oyuncular,
    IReadOnlyList<string> Uyarilar);
