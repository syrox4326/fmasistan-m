using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FMAsistan.Core.Kaynak;

/// <summary>
/// Bellek okuyucunun (FMSuperScout, yalnızca okur) yalnızca kendi kulübümüz için yazdığı döküm.
/// Gerçekçilik: oyunda görünmeyen değerler (CA/PA, gizli öznitelikler, gizli kişilik puanları)
/// burada okunsa da modele aktarılmaz; yalnızca menajerin oyunda da gördüğü değerler alınır.
/// Para birimi sterlin, maaş haftalık; euro/yıllık çevrimi çağıran yapar.
/// </summary>
public sealed record BellekVerisi(
    DateTime Zaman,
    string? OyunTarihi,
    string? Kulup,
    string? Menajer,
    IReadOnlyList<BellekKisi> Oyuncular,
    IReadOnlyList<BellekKisi> Ekip)
{
    public static BellekVerisi? Oku(string yol)
    {
        if (!File.Exists(yol)) return null;
        using var akis = File.OpenRead(yol);
        using var b = JsonDocument.Parse(akis);
        var k = b.RootElement;
        var meta = k.GetProperty("meta");
        string? M(JsonElement e, string a) => e.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        DateTime.TryParse(M(meta, "generated"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var zaman);
        return new BellekVerisi(zaman, M(meta, "gameDate"), M(meta, "myClub"), M(meta, "manager"),
            Kisiler(k, "players", true), Kisiler(k, "staff", false));
    }

    private static List<BellekKisi> Kisiler(JsonElement kok, string ad, bool oyuncu)
    {
        var l = new List<BellekKisi>();
        if (!kok.TryGetProperty(ad, out var dizi) || dizi.ValueKind != JsonValueKind.Array) return l;
        foreach (var p in dizi.EnumerateArray())
        {
            string? M(string a) => p.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            int? S(string a) => p.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
            long? U(string a) => p.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i) ? i : null;
            var oz = new Dictionary<string, int>();
            if (p.TryGetProperty(oyuncu ? "attrs" : "staffAttrs", out var a) && a.ValueKind == JsonValueKind.Object)
                foreach (var x in a.EnumerateObject())
                    if (x.Value.ValueKind == JsonValueKind.Number) oz[x.Name] = x.Value.GetInt32();
            l.Add(new BellekKisi(M("name") ?? "?", S("age"), M("pos"), M("foot"), S("height"), U("wage"), M("expires"),
                oz, S("conditionRaw"), S("moraleRaw"), Gorev(M("job"))));
        }
        return l;
    }

    /// <summary>FMSuperScout görev adları (Felemenkçe) → FM27'nin Türkçe adları.</summary>
    private static string? Gorev(string? g) => g switch
    {
        null => null,
        "Trainer" => "Antrenör", "Assistent-manager" => "Asistan Menajer", "Scout" => "Gözlemci", "Hoofdscout" => "Baş Gözlemci",
        "Keeperstrainer" => "Kaleci Antrenörü", "Fysiotherapeut" => "Fizyoterapist", "Clubarts" => "Kulüp Doktoru",
        "Conditietrainer" => "Kondisyoner", "Analist" or "Prestatieanalist" => "Analist", "Manager" => "Menajer",
        "Hoofd jeugdopleiding" => "Altyapı Sorumlusu", "Technisch directeur" => "Teknik Direktör",
        "Sportwetenschapper" => "Spor Bilimci", _ => g,
    };

    /// <summary>Ad eşleştirmesi için sadeleştirme (büyük/küçük harf ve aksan farkı yok sayılır).</summary>
    public static string Anahtar(string ad)
    {
        var d = ad.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString().Replace('ı', 'i').Trim();
    }
}

public sealed record BellekKisi(
    string Ad, int? Yas, string? Mevkiler, string? Ayak, int? Boy,
    long? HaftalikMaasSterlin, string? SozlesmeBitis,
    IReadOnlyDictionary<string, int> Oznitelikler, int? KondisyonHam, int? MoralHam, string? Gorev);

/// <summary>Bellekteki öznitelik anahtarlarının oyundaki Türkçe adları. Gizli öznitelikler listede yoktur.</summary>
public static class OznitelikAdlari
{
    public static readonly IReadOnlyDictionary<string, string> Oyuncu = new Dictionary<string, string>
    {
        ["Corners"] = "Korner", ["Crossing"] = "Orta Yapma", ["Dribbling"] = "Top Sürme", ["Finishing"] = "Bitiricilik",
        ["FirstTouch"] = "İlk Kontrol", ["FreeKicks"] = "Serbest Vuruş", ["Heading"] = "Kafa Vuruşu", ["LongShots"] = "Uzaktan Şut",
        ["LongThrows"] = "Uzun Taç", ["Marking"] = "Markaj", ["Passing"] = "Pas", ["PenaltyTaking"] = "Penaltı Kullanma",
        ["Tackling"] = "Top Kapma", ["Technique"] = "Teknik",
        ["Aggression"] = "Agresiflik", ["Anticipation"] = "Önsezi", ["Bravery"] = "Cesaret", ["Composure"] = "Soğukkanlılık",
        ["Concentration"] = "Konsantrasyon", ["Decisions"] = "Karar Alma", ["Determination"] = "Kararlılık", ["Flair"] = "Özel Yetenek",
        ["Leadership"] = "Liderlik", ["OffTheBall"] = "Topsuz Alan", ["Positioning"] = "Pozisyon Alma", ["Teamwork"] = "Takım Oyunu",
        ["Vision"] = "Vizyon", ["WorkRate"] = "Çalışkanlık",
        ["Acceleration"] = "Hızlanma", ["Agility"] = "Çeviklik", ["Balance"] = "Denge", ["JumpingReach"] = "Zıplama",
        ["NaturalFitness"] = "Doğal Kondisyon", ["Pace"] = "Hız", ["Stamina"] = "Dayanıklılık", ["Strength"] = "Güç",
        ["AerialReach"] = "Hava Hakimiyeti", ["CommandOfArea"] = "Ceza Sahası Hakimiyeti", ["Communication"] = "İletişim",
        ["Eccentricity"] = "Eksantriklik", ["Handling"] = "Elle Kontrol", ["Kicking"] = "Vuruş", ["OneOnOnes"] = "Bire Bir",
        ["Punching"] = "Yumruklama Eğilimi", ["Reflexes"] = "Refleksler", ["RushingOut"] = "Kaleden Çıkma", ["Throwing"] = "Elle Oyun Başlatma",
    };

    public static readonly IReadOnlyCollection<string> Kaleci = new HashSet<string>
    {
        "AerialReach", "CommandOfArea", "Communication", "Eccentricity", "Handling", "Kicking", "OneOnOnes", "Punching",
        "Reflexes", "RushingOut", "Throwing",
    };

    /// <summary>FMSuperScout'un teknik ekip anahtarları (Felemenkçe) → FM27'nin Türkçe adları.</summary>
    public static readonly IReadOnlyDictionary<string, string> Ekip = new Dictionary<string, string>
    {
        ["Aanvallen"] = "Hücum", ["Verdedigen"] = "Savunma", ["Fitheid"] = "Fitness", ["Balbezit"] = "Topa Sahip Olma",
        ["Technisch"] = "Teknik", ["Tactisch"] = "Taktik", ["Standaardsituaties"] = "Duran Toplar", ["KV_reflexen"] = "Kalecilik",
        ["Jeugd"] = "Gençlerle Çalışma", ["Vastberadenheid"] = "Kararlılık", ["Man-management"] = "Futbolcu İdare Kabiliyeti",
        ["Motiveren"] = "Motivasyon", ["Otorite"] = "Otorite", ["Aanpassingsvermogen"] = "Adaptasyon",
        ["Oordeel_vermogen"] = "Oyuncu Yeteneğini Değerlendirme", ["Oordeel_potentie"] = "Oyuncu Potansiyelini Değerlendirme",
        ["Oordeel_staf"] = "Personel Yeteneğini Değerlendirme", ["Onderhandelen"] = "Pazarlık Kabiliyeti",
        ["Tactische_kennis"] = "Taktik Bilgisi", ["Fysiotherapie"] = "Fizyoterapi", ["Sportwetenschap"] = "Spor Bilimi",
        ["Data_analyse"] = "Veri Analizi",
    };
}
