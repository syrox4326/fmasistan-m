using System.Text.Json;

namespace FMAsistan.Core.Veri;

/// <summary>Oyunun analiz ekibinin bir değerlendirmesi (Veri Merkezi'ndeki bir görselin metni).</summary>
/// <param name="Bolum">Veri Merkezi bölümü (Önemli Bulgular, Son Maç, Oyuncu Analizi...).</param>
/// <param name="Baslik">Görselin oyundaki başlığı (GraphType), ör. "Şut Haritası".</param>
/// <param name="Metin">Oyunun yazdığı değerlendirme; değiştirilmeden aktarılır.</param>
public sealed record AnalizNotu(string Bolum, string? Baslik, string Metin);

/// <summary>Taktik ızgarasında bir saha bölgesi (oyunun 6×5 değerlendirmesi).</summary>
public sealed record SahaBolgesi(int Satir, int Sutun, string Durum, int Puan, IReadOnlyList<string> Yorumlar)
{
    /// <summary>
    /// Izgara yönü (Galatasaray dizilişiyle doğrulandı: sol bek satır 1–2 sütun 0, sol kanat
    /// satır 5 sütun 0): satırlar kendi kalemizden rakip kaleye (0–5), sütunlar soldan sağa (0–4).
    /// </summary>
    public string Ad
    {
        get
        {
            string hat = Satir switch { 0 => "kendi ceza sahamızın önü", 1 => "savunma bölgesi", 2 => "orta sahanın gerisi",
                                       3 => "orta sahanın ilerisi", 4 => "hücum bölgesi", 5 => "rakip ceza sahası çevresi", _ => $"satır {Satir}" };
            string kanat = Sutun switch { 0 => "sol kanat", 1 => "sol iç koridor", 2 => "merkez", 3 => "sağ iç koridor", 4 => "sağ kanat", _ => $"sütun {Sutun}" };
            return $"{hat}, {kanat}";
        }
    }
}

public sealed record OyuncuSonMac(string OyuncuKimlik, string? Ad, int? PasIsabeti, int? IsabetliPas);

public sealed record SonMacOzeti(string? Mac, double? Xg, int? Sut, int? IsabetliSut, string? Degerlendirme);

/// <summary>
/// Takımın Veri Merkezi: oyunun analiz ekibinin ürettiği her şey. Metinler oyunun kendi
/// cümleleridir; yapay zekâ bunları yorumlar ama değiştirmez ya da yerlerine uydurmaz.
/// </summary>
public sealed record VeriMerkezi(
    IReadOnlyList<AnalizNotu> Notlar,
    SonMacOzeti? SonMac,
    IReadOnlyList<OyuncuSonMac> OyuncularSonMac,
    IReadOnlyList<SahaBolgesi> Izgara)
{
    public static readonly VeriMerkezi Bos = new(Array.Empty<AnalizNotu>(), null, Array.Empty<OyuncuSonMac>(), Array.Empty<SahaBolgesi>());
    public bool BosMu => Notlar.Count == 0 && SonMac == null && Izgara.Count == 0;
}

/// <summary>
/// Sıradaki rakip hakkındaki rapor. Ne kadarının bilindiği kulübün analiz ekibine bağlıdır
/// (gerçekçilik): oyun raporu ekibin kalitesine göre üretir, biz de yalnızca bunu aktarırız.
/// </summary>
public sealed record RakipRaporu(
    string TakimKimlik,
    string? Ad,
    string? TehditDegerlendirmesi,
    string? EnCokKullanilanDizilis,
    string? AnalistSeviyesi,
    VeriMerkezi Analiz)
{
    /// <summary>Raporun "muhtemel 11" bölümü (TeamReference.ExpectedTactic).</summary>
    public MuhtemelTaktik? MuhtemelTaktik { get; init; }
    /// <summary>Raporun "muhtemel oyun sistemi" (ExpectedTacticalStyle), okunduysa.</summary>
    public string? MuhtemelOyunSistemi { get; init; }
    /// <summary>Gözlemci raporunun güçlü / zayıf yönleri (ScoutReportStrengths/Weaknesses).</summary>
    public IReadOnlyList<RaporMaddesi> GucluYonler { get; init; } = Array.Empty<RaporMaddesi>();
    public IReadOnlyList<RaporMaddesi> ZayifYonler { get; init; } = Array.Empty<RaporMaddesi>();
    /// <summary>Kullandığı dizilişler (ExpectedFormations.FormationsUsed), faz başına.</summary>
    public IReadOnlyList<DizilisKullanimi> Dizilisler { get; init; } = Array.Empty<DizilisKullanimi>();
}

public sealed record RaporMaddesi(string Baslik, string Metin);

public sealed record MuhtemelOyuncu(string OyuncuKimlik, string? Ad, ulong Pozisyon, ulong RolKodu)
{
    /// <summary>
    /// Rol kodu = topa sahipken rolü + 2^21/2^22/2^23 bitlerinden biri (muhtemelen görev;
    /// doğrulanmadığı için kullanılmaz). Rol adı bu bitler çıkarılarak bulunur.
    /// </summary>
    public Rol? Rolu => RolKatalogu.Bul(Faz.TopaSahipken, RolKodu & ~((1UL << 21) | (1UL << 22) | (1UL << 23)));
}

public sealed record MuhtemelTaktik(string? Dizilis, string? Ozet, IReadOnlyList<MuhtemelOyuncu> Oyuncular);

public sealed record DizilisKullanimi(Faz Faz, string Dizilis, double YuzdeKullanim, int IlkOnbirMaci, int YaratilanPozisyon);

/// <summary>Takım talimatının seçili değeri (oyunun iç adıyla) ve oyunun sunduğu seçenekler.</summary>
public sealed record OyunAyari(string Ayar, string Deger, IReadOnlyList<string> Secenekler);
