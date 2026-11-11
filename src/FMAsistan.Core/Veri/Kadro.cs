namespace FMAsistan.Core.Veri;

/// <summary>
/// Oyunun bir rol için gösterdiği yıldız aralığı (CurrentAbilityStars, 0–20; 20 = 5 yıldız).
/// Ekibin bilgisi eksikse aralıklıdır ve <see cref="GozlemciGorusu"/> işaretlidir.
/// </summary>
public sealed record RolYildizi(ulong RolKimlik, string Rol, string? Kisa, Puan Deger, bool GozlemciGorusu, string? Aciklama)
{
    public double Yildiz => Deger.Orta / 4.0;
}

/// <summary>Bir oyuncunun taktiğimizdeki bir pozisyon/fazdaki tüm rol uygunlukları.</summary>
public sealed record SlotUygunlugu(ulong Maske, bool TopaSahipken, IReadOnlyList<RolYildizi> Roller)
{
    public RolYildizi? Rol(ulong kimlik) => Roller.FirstOrDefault(r => r.RolKimlik == kimlik);
    public RolYildizi? EnIyi => Roller.OrderByDescending(r => r.Deger.Orta).FirstOrDefault();
}

/// <summary>
/// Kadrodaki bir oyuncunun oyundan ve bellekten toplanan profili. Her alan, kaynağı
/// okunamadıysa boştur; analiz boş alanı "bilinmiyor" sayar, tahmin etmez.
/// </summary>
public sealed record OyuncuProfili(string Kimlik, string Ad)
{
    public int? Yas { get; init; }
    public string? Mevkiler { get; init; }            // oyunun metni: "D (SğM), KB (Sğ)"
    public string? EnIyiMevki { get; init; }
    public KadroDurumu? Durum { get; init; }
    public int? Kondisyon { get; init; }              // %
    public int? MacKeskinligi { get; init; }          // %
    public int? Moral { get; init; }                  // oyunun moral seviyesi (ham)
    public int? SakatlikRiski { get; init; }          // oyunun risk değeri (ham; yüksek = riskli)
    public IReadOnlyList<string> Durumlar { get; init; } = Array.Empty<string>();   // "Dil kursu alıyor" gibi
    public double? SezonOrtalamasi { get; init; }
    public int? Mac { get; init; }
    public int? Gol { get; init; }
    public int? Asist { get; init; }
    public string? Kisilik { get; init; }
    public IReadOnlyList<string> Egilimler { get; init; } = Array.Empty<string>();
    public string? DegerAraligi { get; init; }
    public double? MaasEuroYillik { get; init; }
    public string? SozlesmeBitis { get; init; }
    public double? AntrenmanPuani { get; init; }
    public string? AntrenmanYogunlugu { get; init; }
    public string? AntrenmanMutlulugu { get; init; }
    /// <summary>Görünür öznitelikler (oyunun adıyla, 1–20). Kendi oyuncularımızın değerleri oyunda da tam görünür.</summary>
    public IReadOnlyDictionary<string, int> Oznitelikler { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<SlotUygunlugu> Slotlar { get; init; } = Array.Empty<SlotUygunlugu>();
    public int? Boy { get; init; }
    public string? Ayak { get; init; }

    public SlotUygunlugu? Slot(ulong maske, bool ip) => Slotlar.FirstOrDefault(s => s.Maske == maske && s.TopaSahipken == ip);
}

/// <summary>Teknik ekip üyesinin oyundan ve bellekten toplanan profili.</summary>
public sealed record EkipUyesiProfili(string Kimlik, string Ad, string Gorev)
{
    public int? Yas { get; init; }
    public string? Kisilik { get; init; }
    public string? Lisans { get; init; }
    public Felsefe Felsefe { get; init; } = new(null, null, null, null);
    public string? OyunStili { get; init; }
    /// <summary>Antrenörlük, bilgi ve zihinsel öznitelikleri (oyunun Türkçe adlarıyla, 1–20).</summary>
    public IReadOnlyDictionary<string, int> Oznitelikler { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Egilimler { get; init; } = Array.Empty<string>();
    public double? MaasEuroYillik { get; init; }
    public bool Menajer { get; init; }
}

/// <summary>Antrenman: oyunun antrenman ekranındaki atamalar ve yoğunluklar.</summary>
public sealed record AntrenmanDurumu(
    IReadOnlyList<AntrenmanAtamasi> Atamalar,
    IReadOnlyList<string> Kategoriler)
{
    public static readonly AntrenmanDurumu Bos = new(Array.Empty<AntrenmanAtamasi>(), Array.Empty<string>());
}

public sealed record AntrenmanAtamasi(string Kategori, string? Antrenor, Puan? Yildiz);

/// <summary>Kulüp: bütçe ve tesisler (oyunun gösterdiği metinlerle, para euro).</summary>
public sealed record KulupDurumu(string? Ad)
{
    public double? TransferButcesiEuro { get; init; }
    public string? KalanMaasButcesi { get; init; }
    public string? MaasHarcamasi { get; init; }
    public string? AntrenmanTesisi { get; init; }
    public string? AltyapiTesisi { get; init; }
    public string? YonetimBeklentisi { get; init; }
    public IReadOnlyList<string> YonetimOvgu { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> YonetimElestiri { get; init; } = Array.Empty<string>();
}

/// <summary>Fikstür: sıradaki maçlar ve son form.</summary>
public sealed record FiksturDurumu(IReadOnlyList<YakinMac> Siradakiler, string? LigSirasi, IReadOnlyList<int> SonForm)
{
    public static readonly FiksturDurumu Bos = new(Array.Empty<YakinMac>(), null, Array.Empty<int>());
}

public sealed record YakinMac(string Ne, string Rakip, string? RakipKimlik);
