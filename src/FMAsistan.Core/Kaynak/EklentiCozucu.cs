using System.Text.Json;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Kaynak;

/// <summary>
/// Oyun içi eklentinin (FMAsistan.Mantik) okuma çıktısını modele çevirir.
/// Çıktı biçimi: { "Kaynak": { "Özellik": değer } } — bkz. docs/VERI_YOLLARI.md.
/// Eksik ya da okunamayan alanlar null kalır; uydurma varsayılan değer verilmez.
/// </summary>
public static class EklentiCozucu
{
    private static readonly Dictionary<string, TaktikAlani> AlanAdlari = new()
    {
        ["BuildUpStrategy"] = TaktikAlani.OyunKurmaStratejisi,
        ["ChanceCreation"] = TaktikAlani.FirsatYaratma,
        ["InPossessionShape"] = TaktikAlani.TopluSekil,
        ["LineOfEngagement"] = TaktikAlani.BaskiHatti,
        ["OutOfPossessionShape"] = TaktikAlani.TopsuzSekil,
        ["PassingStyle"] = TaktikAlani.PasStili,
        ["PressingIntensity"] = TaktikAlani.PresYogunlugu,
        ["PressingTrap"] = TaktikAlani.PresTuzagi,
    };

    private static readonly Dictionary<string, string> UyariAdlari = new()
    {
        ["IncompleteLineupWarning"] = "İlk 11 eksik.",
        ["MultiplePlayersBelowThresholdWarning"] = "Birden fazla oyuncu taktik için eşiğin altında.",
        ["NoLineupWarning"] = "İlk 11 seçilmemiş.",
        ["NoTacticError"] = "Taktik yok.",
        ["NoUpcomingMatchError"] = "Yaklaşan maç yok.",
        ["PlayerBelowThresholdWarning"] = "Bir oyuncu taktik için eşiğin altında.",
        ["PlayerNotFullyScoutedWarning"] = "Tam gözlemlenmemiş oyuncu var (değerlendirme belirsiz).",
        ["PlayerNotInTeamWarning"] = "Taktikte takımda olmayan oyuncu var.",
        ["PlayerNotSelectedWarning"] = "Seçilmemiş oyuncu var.",
    };

    public static Dictionary<string, Dictionary<string, JsonElement>> Oku(string json)
        => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json)
           ?? throw new InvalidDataException("Eklenti çıktısı boş.");

    /// <summary>
    /// Birden fazla okumayı eskiden yeniye birleştirir: her (kaynak, özellik) için en yeni
    /// geçerli değer kalır. Okunamamış ("!..." ile işaretli) değerler eski geçerli değeri ezmez.
    /// </summary>
    public static Dictionary<string, Dictionary<string, JsonElement>> Birlestir(IEnumerable<string> eskidenYeniyeJsonlar)
    {
        var sonuc = new Dictionary<string, Dictionary<string, JsonElement>>();
        foreach (var json in eskidenYeniyeJsonlar)
        {
            foreach (var (kaynak, ozellikler) in Oku(json))
            {
                if (!sonuc.TryGetValue(kaynak, out var hedef)) sonuc[kaynak] = hedef = new();
                foreach (var (ad, deger) in ozellikler)
                {
                    bool hatali = deger.ValueKind == JsonValueKind.String && (deger.GetString() ?? "").StartsWith('!');
                    if (!hatali || !hedef.ContainsKey(ad)) hedef[ad] = deger.Clone();
                }
            }
        }
        return sonuc;
    }

    /// <param name="takimKimlik">DatabaseReferenceField değeri, ör. "402654676".</param>
    /// <param name="adlar">Kişi kimliği → ad (ayrı bir okumadan); yoksa ad boş kalır.</param>
    public static TakimDurumu TakimDurumu(
        Dictionary<string, Dictionary<string, JsonElement>> veri,
        string takimKimlik,
        IReadOnlyDictionary<string, string>? adlar = null)
    {
        var taktik = Kaynak(veri, "TeamTacticReference:" + takimKimlik);
        var takim = veri.TryGetValue("TeamReference:" + takimKimlik, out var t) ? t : new();

        var te = Alan(taktik, "TacticalEffectiveness")
                 ?? throw new InvalidDataException("TacticalEffectiveness okunamamış.");

        var oyuncular = new List<OyuncuVerimliligi>();
        if (te.TryGetProperty("Players", out var liste) && liste.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in liste.EnumerateArray())
            {
                string kimlik = Metin(p, "Player") ?? "?";
                if (!p.TryGetProperty("TacticalEffectiveness", out var pte)) continue;
                string? ad = adlar != null && adlar.TryGetValue(kimlik, out var a) ? a : null;
                oyuncular.Add(new OyuncuVerimliligi(kimlik, ad, Verimlilik(pte)));
            }
        }

        var dizilis = new List<TaktikPozisyonu>();
        if (Alan(taktik, "TacticalPositionsCombined") is { ValueKind: JsonValueKind.Array } poz)
        {
            foreach (var p in poz.EnumerateArray())
            {
                string kimlik = Metin(p, "Person") ?? Metin(p, "Player") ?? "?";
                string? ad = adlar != null && adlar.TryGetValue(kimlik, out var a) ? a : null;
                dizilis.Add(new TaktikPozisyonu(kimlik, ad,
                    Sayi(p, "IPPosition"), Sayi(p, "IPRole"), Sayi(p, "OOPPosition"), Sayi(p, "OOPRole")));
            }
        }

        var uyarilar = new List<string>();
        if (te.TryGetProperty("StatusFlags", out var bayraklar) && bayraklar.ValueKind == JsonValueKind.Object)
            foreach (var b in bayraklar.EnumerateObject())
                if (b.Value.ValueKind == JsonValueKind.True && UyariAdlari.TryGetValue(b.Name, out var u)) uyarilar.Add(u);

        return new TakimDurumu(
            Metin(takim, "Name") ?? takimKimlik,
            new TaktikOzeti(
                Metin(taktik, "Name") ?? "?",
                Metin(taktik, "MentalityString") ?? "?",
                Alan(taktik, "TacticalIntensity") is { ValueKind: JsonValueKind.Number } y ? (int)y.GetDouble() : 0,
                Metin(takim, "MostUsedFormation")),
            Verimlilik(te),
            oyuncular,
            uyarilar,
            dizilis);
    }

    /// <summary>Rol/pozisyon kimlikleri 2^53'ü aşabilir; double'a çevirmeden okunur.</summary>
    private static ulong Sayi(JsonElement e, string ad)
    {
        if (!e.TryGetProperty(ad, out var v) || v.ValueKind != JsonValueKind.Number) return 0;
        if (v.TryGetUInt64(out var u)) return u;
        if (v.TryGetInt64(out var l)) return unchecked((ulong)l);
        return (ulong)v.GetDouble();
    }

    // --- Verimlilik ---

    public static Verimlilik Verimlilik(JsonElement te)
    {
        var pa = te.GetProperty("PlayerAbility");
        var pb = te.GetProperty("PlayerBuyIn");
        var tu = te.GetProperty("TacticalUnderstanding");

        var alanlar = new Dictionary<TaktikAlani, Puan>();
        foreach (var (ad, alan) in AlanAdlari)
            if (PuanOku(tu, ad) is { } p) alanlar[alan] = p;

        MacBilgisi? mac = null;
        if (te.TryGetProperty("NextMatchInformation", out var nm) && nm.ValueKind == JsonValueKind.Object)
            mac = new MacBilgisi(PuanOku(nm) ?? default,
                PuanOku(nm, "QualityOfReportKeyFactor"), PuanOku(nm, "OppositionPlayerKnowledgeKeyFactor"),
                Tavsiye(nm, "AdviceForComponent"));

        return new Verimlilik(
            PuanOku(te) ?? default,
            new OyuncuYetenegi(PuanOku(pa) ?? default,
                PuanOku(pa, "CapabilityScore") ?? default,
                PuanOku(pa, "RoleSuitabilityScore") ?? default,
                Tavsiye(pa, "AdviceForComponent")),
            new Benimseme(PuanOku(pb) ?? default,
                PuanOku(pb, "ManagerSupportScore"),
                PuanOku(pb, "TeammateAndStaffRelationshipsScore"),
                PuanOku(pb, "ManagerTendencies"),
                Tavsiye(pb, "AdviceForComponent")),
            new TaktikAnlayisi(PuanOku(tu) ?? default, alanlar,
                PuanOku(tu, "TacticalComplexity") ?? default,
                PuanOku(tu, "TacticalComprehension") ?? default,
                PuanOku(tu, "TacticalAbilityKeyFactor") ?? default,
                Tavsiye(tu, "AdviceForTacticalAreas"),
                Tavsiye(tu, "AdviceForTacticalComprehension")),
            mac);
    }

    /// <summary>
    /// Oyunun puan biçimleri: {MinValue,MaxValue}, {OverallEffectiveness:{...}} ya da düz sayı.
    /// </summary>
    internal static Puan? PuanOku(JsonElement e, string? alan = null)
    {
        if (alan != null && (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(alan, out e))) return null;
        if (e.ValueKind == JsonValueKind.Number) return Puan.Tek((int)e.GetDouble());
        if (e.ValueKind != JsonValueKind.Object) return null;
        if (e.TryGetProperty("MinValue", out var mn) && e.TryGetProperty("MaxValue", out var mx)
            && mn.ValueKind == JsonValueKind.Number && mx.ValueKind == JsonValueKind.Number)
            return new Puan((int)mn.GetDouble(), (int)mx.GetDouble());
        if (e.TryGetProperty("OverallEffectiveness", out var oe)) return PuanOku(oe);
        return null;
    }

    private static OyunTavsiyesi? Tavsiye(JsonElement e, string alan)
    {
        if (!e.TryGetProperty(alan, out var t) || t.ValueKind != JsonValueKind.Object) return null;
        string? metin = Metin(t, "Text");
        if (string.IsNullOrWhiteSpace(metin)) return null;
        bool olumlu = t.TryGetProperty("IsPro", out var p) && p.ValueKind == JsonValueKind.True;
        return new OyunTavsiyesi(metin, olumlu);
    }

    // --- JSON yardımcıları ---

    private static Dictionary<string, JsonElement> Kaynak(Dictionary<string, Dictionary<string, JsonElement>> v, string ad)
        => v.TryGetValue(ad, out var k) ? k : throw new InvalidDataException($"'{ad}' kaynağı yok.");

    /// <summary>Okunamamış alanlar eklentide "!..." metniyle işaretlenir; onları yok sayar.</summary>
    private static JsonElement? Alan(Dictionary<string, JsonElement> k, string ad)
    {
        if (!k.TryGetValue(ad, out var e)) return null;
        if (e.ValueKind == JsonValueKind.String && (e.GetString() ?? "").StartsWith('!')) return null;
        return e;
    }

    private static string? Metin(Dictionary<string, JsonElement> k, string ad)
        => Alan(k, ad) is { ValueKind: JsonValueKind.String } e ? e.GetString() : null;

    private static string? Metin(JsonElement e, string ad)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
