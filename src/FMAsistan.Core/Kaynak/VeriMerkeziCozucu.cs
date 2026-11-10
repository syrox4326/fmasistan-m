using System.Text.Json;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Kaynak;

/// <summary>
/// Takımın Veri Merkezi verisini (TeamReference.* analiz özellikleri) ve taktik ızgarasını
/// modele çevirir. Oyunun yazdığı değerlendirme metinleri olduğu gibi toplanır.
/// </summary>
public static class VeriMerkeziCozucu
{
    /// <summary>Özellik → Veri Merkezi'ndeki bölüm adı.</summary>
    public static readonly IReadOnlyList<(string ozellik, string bolum)> Bolumler = new[]
    {
        ("KeyFindingsVisuals", "Önemli Bulgular"),
        ("LastMatchAnalysis", "Son Maç"),
        ("TeamAnalytics", "Takım Analizi"),
        ("PlayerAnalytics", "Oyuncu Analizi"),
        ("ExpectedFormations", "Diziliş Analizi"),
        ("GoalAnalysisScoring", "Gol Analizi (atılan)"),
        ("GoalAnalysisConceding", "Gol Analizi (yenen)"),
        ("SetPieceAnalysis", "Duran Top Analizi"),
        ("SetPieceDataInsights", "Duran Top Analizi"),
        ("ActionZonesVisualisation", "Hareket Bölgeleri"),
    };

    private static readonly string[] MetinAlanlari = { "PerformanceSummaryExtended", "PerformanceSummary", "AnalysisPointString", "Text" };

    public static VeriMerkezi Coz(
        Dictionary<string, Dictionary<string, JsonElement>> veri,
        string takimKimlik,
        IReadOnlyDictionary<string, string>? adlar = null)
    {
        if (!veri.TryGetValue("TeamReference:" + takimKimlik, out var takim)) takim = new();

        var notlar = new List<AnalizNotu>();
        var gorulen = new HashSet<string>();
        foreach (var (oz, bolum) in Bolumler)
        {
            if (!takim.TryGetValue(oz, out var e)) continue;
            var bu = new List<AnalizNotu>();
            MetinTopla(e, bolum, null, bu);
            // Aynı metin farklı alanlarda tekrar eder; kısa özet uzun metnin başıysa yalnızca uzun kalır.
            foreach (var n in bu.OrderByDescending(n => n.Metin.Length))
            {
                if (gorulen.Any(g => g.StartsWith(n.Metin, StringComparison.Ordinal))) continue;
                gorulen.Add(n.Metin);
                notlar.Add(n);
            }
        }

        return new VeriMerkezi(notlar, SonMac(takim), OyuncularSonMac(takim, adlar), Izgara(veri, takimKimlik));
    }

    private static void MetinTopla(JsonElement e, string bolum, string? baslik, List<AnalizNotu> hedef)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("GraphType", out var gt) && gt.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(gt.GetString()))
                baslik = gt.GetString();
            foreach (var alan in MetinAlanlari)
                if (e.TryGetProperty(alan, out var m) && m.ValueKind == JsonValueKind.String && Anlamli(m.GetString()))
                    hedef.Add(new AnalizNotu(bolum, baslik, Temizle(m.GetString()!)));
            if (e.TryGetProperty("SummaryStrings", out var ss))
                foreach (var s in Metinler(ss))
                    if (Anlamli(s)) hedef.Add(new AnalizNotu(bolum, baslik, Temizle(s)));
            foreach (var p in e.EnumerateObject())
                if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    MetinTopla(p.Value, bolum, baslik, hedef);
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in e.EnumerateArray()) MetinTopla(x, bolum, baslik, hedef);
        }
    }

    private static IEnumerable<string> Metinler(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String) yield return e.GetString()!;
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var x in e.EnumerateArray()) foreach (var s in Metinler(x)) yield return s;
        else if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject()) foreach (var s in Metinler(p.Value)) yield return s;
    }

    private static bool Anlamli(string? s) => !string.IsNullOrWhiteSpace(s) && s.Trim().Length > 10 && !s.StartsWith('!');

    private static string Temizle(string s) => string.Join("\n", s.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));

    private static SonMacOzeti? SonMac(Dictionary<string, JsonElement> takim)
    {
        if (!takim.TryGetValue("LastMatchAnalysis", out var lm) || lm.ValueKind != JsonValueKind.Object) return null;
        string? mac = null; double? xg = null; int? sut = null, isabetli = null;
        if (lm.TryGetProperty("ShotMapVisualisation", out var sm) && sm.ValueKind == JsonValueKind.Object)
        {
            xg = Sayi(sm, "ExpectedGoals");
            sut = (int?)Sayi(sm, "ShotAttempts");
            isabetli = (int?)Sayi(sm, "ShotsOnTarget");
            if (sm.TryGetProperty("TeamMatchVisualisationData", out var tmv) && tmv.TryGetProperty("Fixture", out var f) && f.ValueKind == JsonValueKind.String)
                mac = f.GetString();
        }
        string? degerlendirme = lm.TryGetProperty("PerformanceSummary", out var ps) && ps.ValueKind == JsonValueKind.String ? ps.GetString() : null;
        return new SonMacOzeti(mac, xg, sut, isabetli, degerlendirme);
    }

    private static IReadOnlyList<OyuncuSonMac> OyuncularSonMac(Dictionary<string, JsonElement> takim, IReadOnlyDictionary<string, string>? adlar)
    {
        var l = new List<OyuncuSonMac>();
        if (!takim.TryGetValue("LastMatchAnalysis", out var lm) || lm.ValueKind != JsonValueKind.Object) return l;
        if (!lm.TryGetProperty("PassMapVisualisation", out var pm) || !pm.TryGetProperty("SubjectTeamPlayerData", out var oyuncular)
            || oyuncular.ValueKind != JsonValueKind.Array) return l;
        foreach (var o in oyuncular.EnumerateArray())
        {
            string? kimlik = o.TryGetProperty("PlayerMatchData", out var pmd) && pmd.TryGetProperty("Player", out var p) ? p.GetString() : null;
            if (kimlik == null) continue;
            int? isabet = (int?)Sayi(o, "PassCompletionPercentage");
            int? toplam = null;
            foreach (var alan in o.EnumerateObject())
                if (alan.Name.StartsWith("TotalPass", StringComparison.Ordinal) && alan.Value.ValueKind == JsonValueKind.Number) toplam = (int)alan.Value.GetDouble();
            l.Add(new OyuncuSonMac(kimlik, adlar != null && adlar.TryGetValue(kimlik, out var ad) ? ad : null, isabet, toplam));
        }
        return l;
    }

    private static IReadOnlyList<SahaBolgesi> Izgara(Dictionary<string, Dictionary<string, JsonElement>> veri, string takimKimlik)
    {
        var l = new List<SahaBolgesi>();
        if (!veri.TryGetValue("TeamTacticReference:" + takimKimlik, out var t) || !t.TryGetValue("TacticsGrid", out var g)
            || g.ValueKind != JsonValueKind.Array) return l;
        foreach (var b in g.EnumerateArray())
        {
            var yorumlar = new List<string>();
            if (b.TryGetProperty("GridItemList", out var gl) && gl.ValueKind == JsonValueKind.Array)
                foreach (var y in gl.EnumerateArray())
                    if (y.TryGetProperty("GridItemString", out var ys) && ys.ValueKind == JsonValueKind.String) yorumlar.Add(ys.GetString()!);
            l.Add(new SahaBolgesi(
                (int)(Sayi(b, "TacticsGridRow") ?? 0), (int)(Sayi(b, "TacticsGridColumn") ?? 0),
                b.TryGetProperty("GridItemString", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()! : "?",
                (int)(Sayi(b, "Score") ?? 0), yorumlar));
        }
        return l;
    }

    /// <summary>
    /// Taktik ekranındaki ayarlar ve oyunun sunduğu seçenekler. Takım talimatlarının seçili
    /// değerleri oyunun iç adlarıyla gelir (kimlik-adlari-sonuc.json eşlemesiyle).
    /// </summary>
    public static IReadOnlyList<OyunAyari> Ayarlar(
        Dictionary<string, Dictionary<string, JsonElement>> veri,
        string takimKimlik,
        IReadOnlyDictionary<string, string>? kimlikAdlari = null)
    {
        var l = new List<OyunAyari>();
        if (!veri.TryGetValue("TeamTacticReference:" + takimKimlik, out var t)) return l;

        void Liste(string ad, string secili, string secenekler)
        {
            string? deger = t.TryGetValue(secili, out var s) && s.ValueKind == JsonValueKind.Object && s.TryGetProperty("DisplayValue", out var dv) ? dv.GetString() : null;
            var sec = new List<string>();
            if (t.TryGetValue(secenekler, out var o) && o.ValueKind == JsonValueKind.Array)
                foreach (var x in o.EnumerateArray())
                    if (x.TryGetProperty("DisplayValue", out var d) && d.ValueKind == JsonValueKind.String) sec.Add(d.GetString()!);
            if (deger != null || sec.Count > 0) l.Add(new OyunAyari(ad, deger ?? "?", sec));
        }
        Liste("Mentalite", "MentalityDropdown", "MentalityDropdownOptions");
        Liste("Taktik stili", "TacticalStyleDropdown", "TacticalStyleDropdownOptions");

        // Takım talimatları: seçili değer bir PropertyID; iç ada (kimlik-adlari eşlemesi) ve
        // oradan Türkçeye çevrilir. Çevrilemeyen kimlik "kod" olarak kalır, uydurulmaz.
        foreach (var (oz, (ad, faz)) in Talimatlar.Ozellikler)
        {
            if (!t.TryGetValue(oz, out var v) || v.ValueKind != JsonValueKind.Number) continue;
            string id = v.GetRawText();
            string deger = kimlikAdlari != null && kimlikAdlari.TryGetValue(id, out var ic)
                ? Talimatlar.DegerAdi(ic.Split(':')[0])
                : $"bilinmeyen değer (kod {id})";
            string fazAdi = faz switch
            {
                Talimatlar.TalimatFazi.TopaSahipken => "Topa sahipken",
                Talimatlar.TalimatFazi.Gecis => "Geçiş",
                Talimatlar.TalimatFazi.TopRakipteyken => "Top rakipteyken",
                _ => "Kaleci",
            };
            l.Add(new OyunAyari($"{fazAdi} / {ad}", deger, Array.Empty<string>()));
        }
        return l;
    }

    /// <summary>
    /// Sıradaki rakibi takımın NextFixture referansından bulur ve onun hakkında kaydedilmiş
    /// raporu (Veri Merkezi'ndeki aynı analizler + rapor alanları) çözer.
    /// </summary>
    public static RakipRaporu? Rakip(
        Dictionary<string, Dictionary<string, JsonElement>> veri,
        string takimKimlik,
        IReadOnlyDictionary<string, string>? adlar = null)
    {
        if (!veri.TryGetValue("TeamReference:" + takimKimlik, out var takim)) return null;
        string? rakip = null;
        if (takim.TryGetValue("NextFixture", out var nf) && nf.ValueKind == JsonValueKind.String)
        {
            var m = System.Text.RegularExpressions.Regex.Matches(nf.GetString()!, @"(?:HomeTeam|AwayTeam)=(\d+)");
            rakip = m.Select(x => x.Groups[1].Value).FirstOrDefault(x => x != takimKimlik);
        }
        if (rakip == null || !veri.TryGetValue("TeamReference:" + rakip, out var r)) return null;

        string? M(string ad) => r.TryGetValue(ad, out var e) && e.ValueKind == JsonValueKind.String && !Okunamadi(e.GetString()) ? e.GetString() : null;
        string? analist = r.TryGetValue("DataAnalystLevelInfo", out var dai) && dai.ValueKind == JsonValueKind.Object
                          && dai.TryGetProperty("AnalystLevelDescription", out var ald) ? ald.GetString() : null;
        return new RakipRaporu(rakip, M("Name"), M("NextOppositionThreatLevelAssessment"), M("MostUsedFormation"), analist,
            Coz(veri, rakip, adlar))
        {
            MuhtemelTaktik = MuhtemelTaktikCoz(r, adlar),
            MuhtemelOyunSistemi = r.TryGetValue("ExpectedTacticalStyle", out var es) ? DisplayMetni(es) : null,
            GucluYonler = Maddeler(r, "ScoutReportStrengths"),
            ZayifYonler = Maddeler(r, "ScoutReportWeaknesses"),
            Dizilisler = DizilisKullanimlari(r),
        };
    }

    private static MuhtemelTaktik? MuhtemelTaktikCoz(Dictionary<string, JsonElement> r, IReadOnlyDictionary<string, string>? adlar)
    {
        if (!r.TryGetValue("ExpectedTactic", out var et) || et.ValueKind != JsonValueKind.Object) return null;
        var oyuncular = new List<MuhtemelOyuncu>();
        if (et.TryGetProperty("Players", out var ps) && ps.ValueKind == JsonValueKind.Array)
            foreach (var p in ps.EnumerateArray())
            {
                string? k = p.TryGetProperty("Player", out var pl) && pl.ValueKind == JsonValueKind.String ? pl.GetString() : null;
                if (k == null) continue;
                oyuncular.Add(new MuhtemelOyuncu(k, adlar != null && adlar.TryGetValue(k, out var ad) ? ad : null,
                    UzunSayi(p, "Position"), UzunSayi(p, "Role")));
            }
        string? M(string ad) => et.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return new MuhtemelTaktik(M("FormationType"), M("TacticSummaryString"), oyuncular);
    }

    private static IReadOnlyList<RaporMaddesi> Maddeler(Dictionary<string, JsonElement> r, string ozellik)
    {
        var l = new List<RaporMaddesi>();
        if (!r.TryGetValue(ozellik, out var e) || e.ValueKind != JsonValueKind.Array) return l;
        foreach (var m in e.EnumerateArray())
        {
            string? baslik = m.TryGetProperty("ProConsShortString", out var b) ? b.GetString() : null;
            string? metin = m.TryGetProperty("ProConsLongString", out var t) ? t.GetString() : null;
            if (!string.IsNullOrWhiteSpace(metin)) l.Add(new RaporMaddesi(baslik ?? "", metin!));
        }
        return l;
    }

    private static IReadOnlyList<DizilisKullanimi> DizilisKullanimlari(Dictionary<string, JsonElement> r)
    {
        var l = new List<DizilisKullanimi>();
        if (!r.TryGetValue("ExpectedFormations", out var ef) || !ef.TryGetProperty("FormationsUsed", out var fu)) return l;
        foreach (var (alan, faz) in new[] { ("InPossessionFormations", Faz.TopaSahipken), ("OutPossessionFormations", Faz.TopRakipteyken) })
        {
            if (!fu.TryGetProperty(alan, out var liste) || liste.ValueKind != JsonValueKind.Array) continue;
            foreach (var d in liste.EnumerateArray())
            {
                string? ad = d.TryGetProperty("FormationType", out var f) ? f.GetString() : null;
                if (ad == null) continue;
                l.Add(new DizilisKullanimi(faz, ad, Sayi(d, "FormationUsedPercentage") ?? 0,
                    (int)(Sayi(d, "FormationStarted") ?? 0), (int)(Sayi(d, "ChancesCreated") ?? 0)));
            }
        }
        return l;
    }

    /// <summary>Eklentinin "okunamadı" işareti ("!..." ile başlayan metin) değer sayılmaz.</summary>
    private static bool Okunamadi(string? s) => s != null && s.StartsWith('!');

    private static string? DisplayMetni(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => Okunamadi(e.GetString()) ? null : e.GetString(),
        JsonValueKind.Object when e.TryGetProperty("DisplayValue", out var d) && d.ValueKind == JsonValueKind.String => d.GetString(),
        _ => null,
    };

    private static ulong UzunSayi(JsonElement e, string ad)
    {
        if (!e.TryGetProperty(ad, out var v) || v.ValueKind != JsonValueKind.Number) return 0;
        return v.TryGetUInt64(out var u) ? u : (ulong)v.GetDouble();
    }

    private static double? Sayi(JsonElement e, string ad)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
