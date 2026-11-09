using System.Reflection;
using System.Text.Json;

namespace FMAsistan.Core.Veri;

public enum Faz { TopaSahipken, TopRakipteyken }

public enum Kanat { Merkez, Sol, Sag }

public sealed record Rol(Faz Faz, ulong Kimlik, string Ad, string Kisa);

/// <summary>
/// FM27 rol kataloğu (Veri/fm27-roller.json). Oyundan okundu: rol kimlikleri faza göre
/// ayrı ad alanlarıdır; aynı sayı topa sahipken ve top rakipteyken farklı rolleri gösterir.
/// </summary>
public static class RolKatalogu
{
    private static readonly Lazy<Dictionary<(Faz, ulong), Rol>> Katalog = new(Yukle);

    public static Rol? Bul(Faz faz, ulong kimlik) => Katalog.Value.TryGetValue((faz, kimlik), out var r) ? r : null;

    public static string Ad(Faz faz, ulong kimlik) => Bul(faz, kimlik)?.Ad ?? $"Bilinmeyen rol ({kimlik})";

    public static int Sayi => Katalog.Value.Count;

    private static Dictionary<(Faz, ulong), Rol> Yukle()
    {
        using var akis = Assembly.GetExecutingAssembly().GetManifestResourceStream("FMAsistan.Core.Veri.fm27-roller.json")
            ?? throw new InvalidOperationException("Rol kataloğu gömülü kaynak olarak bulunamadı.");
        using var belge = JsonDocument.Parse(akis);
        var s = new Dictionary<(Faz, ulong), Rol>();
        foreach (var e in belge.RootElement.GetProperty("roller").EnumerateArray())
        {
            var faz = e.GetProperty("faz").GetString() == "IP" ? Faz.TopaSahipken : Faz.TopRakipteyken;
            ulong id = ulong.Parse(e.GetProperty("id").GetString()!);
            s[(faz, id)] = new Rol(faz, id, e.GetProperty("ad").GetString()!, e.GetProperty("kisa").GetString()!);
        }
        return s;
    }
}

/// <summary>
/// Oyunun pozisyon bit maskesi. Doğrulanmış (Galatasaray ilk 11'i ile, 2026-11-10):
/// 1 kaleci, 4 sağ bek, 8 sol bek, 16 stoper, 128 defansif orta saha, 256 sağ orta saha,
/// 512 sol orta saha, 1024 merkez orta saha, 2048 sağ ofansif kanat, 4096 sol ofansif kanat,
/// 8192 ofansif orta saha, 16384 forvet; 2^20 sol iç, 2^21 sağ iç (merkez pozisyonlarda).
/// Çıkarım (doğrulanmadı): 32 sağ kanat bek, 64 sol kanat bek.
/// </summary>
public static class Pozisyon
{
    private const ulong SolIc = 1UL << 20, SagIc = 1UL << 21;

    private static readonly (ulong bit, string ad, Kanat kanat)[] Temel =
    {
        (1, "Kaleci", Kanat.Merkez), (4, "Sağ bek", Kanat.Sag), (8, "Sol bek", Kanat.Sol),
        (16, "Stoper", Kanat.Merkez), (32, "Sağ kanat bek", Kanat.Sag), (64, "Sol kanat bek", Kanat.Sol),
        (128, "Defansif orta saha", Kanat.Merkez), (256, "Sağ orta saha", Kanat.Sag), (512, "Sol orta saha", Kanat.Sol),
        (1024, "Merkez orta saha", Kanat.Merkez), (2048, "Sağ ofansif kanat", Kanat.Sag), (4096, "Sol ofansif kanat", Kanat.Sol),
        (8192, "Ofansif orta saha", Kanat.Merkez), (16384, "Forvet", Kanat.Merkez),
    };

    public static string Ad(ulong maske)
    {
        foreach (var (bit, ad, _) in Temel)
            if ((maske & bit) != 0)
                return (maske & SolIc) != 0 ? $"{ad} (sol)" : (maske & SagIc) != 0 ? $"{ad} (sağ)" : ad;
        return $"Bilinmeyen pozisyon ({maske})";
    }

    public static Kanat KanatOf(ulong maske)
    {
        foreach (var (bit, _, kanat) in Temel)
            if ((maske & bit) != 0)
                return kanat != Kanat.Merkez ? kanat : (maske & SolIc) != 0 ? Kanat.Sol : (maske & SagIc) != 0 ? Kanat.Sag : Kanat.Merkez;
        return Kanat.Merkez;
    }
}

/// <summary>İlk 11'deki bir oyuncunun iki fazdaki pozisyonu ve rolü.</summary>
public sealed record TaktikPozisyonu(string OyuncuKimlik, string? Ad, ulong IpPozisyon, ulong IpRol, ulong OopPozisyon, ulong OopRol)
{
    public Rol? IpRolu => RolKatalogu.Bul(Faz.TopaSahipken, IpRol);
    public Rol? OopRolu => RolKatalogu.Bul(Faz.TopRakipteyken, OopRol);
}
