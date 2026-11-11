using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Kaynak;

/// <summary>
/// Otomatik taramanın (eklenti) ve bellek dökümünün kadro, teknik ekip, antrenman, kulüp ve
/// fikstür verisini modele çevirir. Para: oyunun metinleri zaten euro/yıllık; bellekteki
/// sterlin/haftalık değerler, oyunun sözleşme ekranından kalibre edilen kurla çevrilir.
/// </summary>
public static class KadroCozucu
{
    public const double VarsayilanKur = 1.1666;   // €/£ (Emre Utkucan: £1.327/hafta = €80.500/yıl)

    // ---------- Oyuncular ----------

    public static IReadOnlyList<OyuncuProfili> Oyuncular(
        Dictionary<string, Dictionary<string, JsonElement>> veri, string takimKimlik,
        IReadOnlyList<KadroOyuncusu> kadro, IReadOnlyDictionary<string, string> adlar, BellekVerisi? bellek, double kur)
    {
        var kimlikler = kadro.Select(k => k.Kimlik).ToList();
        if (Antrenman(veri, takimKimlik) is { } ant && ant.TryGetValue("TrainingSquad", out var ts) && ts.ValueKind == JsonValueKind.Array)
            foreach (var x in ts.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String && x.GetString() is { } s && s.StartsWith("PersonReference:") && !kimlikler.Contains(s)) kimlikler.Add(s);

        var roller = RolTablolari(veri);
        var bellekSozluk = bellek?.Oyuncular.GroupBy(o => BellekVerisi.Anahtar(o.Ad)).ToDictionary(g => g.Key, g => g.ToList()) ?? new();
        var sonuc = new List<OyuncuProfili>();
        foreach (var k in kimlikler)
        {
            veri.TryGetValue(k, out var p);
            p ??= new Dictionary<string, JsonElement>();
            string ad = Metin(p, "Name") ?? (adlar.TryGetValue(k, out var a) ? a : k);
            int? yas = Tam(p, "Age");
            var bk = BellekEsle(bellekSozluk, ad, yas);

            var oz = new Dictionary<string, int>();
            foreach (var blok in new[] { "AttributesTechnical", "AttributesMental", "AttributesPhysical", "AttributesGoalkeeping",
                                         "AttributesSetPieces", "AttributesPlayerTechnical", "AttributesPlayerMental", "AttributesPlayerPhysical" })
                OznitelikListesi(p, blok, oz);
            if (oz.Count == 0 && bk != null)
            {
                bool kaleci = (Metin(p, "PositionCombinedString") ?? bk.Mevkiler ?? "").Contains("KL") || (bk.Mevkiler ?? "").StartsWith("GK");
                foreach (var (anahtar, deger) in bk.Oznitelikler)
                    if (OznitelikAdlari.Oyuncu.TryGetValue(anahtar, out var tr) && (kaleci || !OznitelikAdlari.Kaleci.Contains(anahtar)))
                        oz[tr] = deger;
            }

            var sozlesme = Kaynak(veri, Metin(p, "FullContract"));
            var bireysel = Kaynak(veri, Metin(p, "PlayerTraining"));
            double? maas = ParaEuro(Alt(sozlesme, "Wage", "DisplayValue"));
            if (maas == null && bk?.HaftalikMaasSterlin is long hs && hs > 0) maas = hs * 52 * kur;

            sonuc.Add(new OyuncuProfili(k, ad)
            {
                Yas = yas ?? bk?.Yas,
                Mevkiler = Metin(p, "PositionCombinedString") ?? bk?.Mevkiler,
                EnIyiMevki = Metin(p, "BestPositionShortString"),
                Durum = kadro.FirstOrDefault(x => x.Kimlik == k)?.Durum,
                Kondisyon = Tam(p, "Condition"),
                MacKeskinligi = Tam(p, "Sharpness"),
                Moral = Tam(p, "Morale"),
                SakatlikRiski = Tam(p, "InjuryRiskLevel"),
                Durumlar = Liste(p, "PlayerStatusFlagsForTeam", "StatusTitleFull").Concat(Liste(p, "PlayerStatus", "StatusTitleFull")).Distinct().ToList(),
                SezonOrtalamasi = Ondalik(p, "OverallAverageRating", "PropertyValue") ?? Ondalik(p, "AverageRating"),
                Mac = Tam(p, "OverallAverageRating", "Count"),
                Gol = Tam(p, "OverallGoals", "PropertyValue"),
                Asist = Tam(p, "OverallAssists", "PropertyValue"),
                Kisilik = Metin(p, "Personality"),
                Egilimler = Liste(p, "TendenciesTendsTo", "DisplayString").Concat(Liste(p, "TendenciesTendsNotTo", "DisplayString").Select(s => "Yapmaz: " + s)).ToList(),
                DegerAraligi = DegerAraligi(p),
                MaasEuroYillik = maas,
                SozlesmeBitis = Alt(sozlesme, "EndDateDescription") ?? bk?.SozlesmeBitis,
                AntrenmanPuani = Ondalik(bireysel, "TrainingRating"),
                AntrenmanYogunlugu = Alt(bireysel, "TrainingIntensityOrRest", "IndividualTrainingIntensityRow", "DisplayValue"),
                AntrenmanMutlulugu = Alt(p, "HappinessTraining", "Description"),
                Oznitelikler = oz,
                Slotlar = roller.TryGetValue(k, out var sl) ? sl : Array.Empty<SlotUygunlugu>(),
                Boy = bk?.Boy,
                Ayak = bk?.Ayak,
            });
        }
        return sonuc;
    }

    /// <summary>Etiketli rol tablosu okumaları: "RolTablosu|PersonReference:X|maske|ip".</summary>
    private static Dictionary<string, List<SlotUygunlugu>> RolTablolari(Dictionary<string, Dictionary<string, JsonElement>> veri)
    {
        var s = new Dictionary<string, List<SlotUygunlugu>>();
        foreach (var (anahtar, d) in veri)
        {
            if (!anahtar.StartsWith("RolTablosu|")) continue;
            var p = anahtar.Split('|');
            if (p.Length != 4 || !ulong.TryParse(p[2], out var maske)) continue;
            if (!d.TryGetValue("PositionalAbilitiesRoleTable", out var t) || t.ValueKind != JsonValueKind.Array) continue;
            var roller = new List<RolYildizi>();
            foreach (var r in t.EnumerateArray())
            {
                if (!r.TryGetProperty("CurrentAbilityStars", out var y)) continue;
                int min = Tam(y, "MinValue") ?? -1, max = Tam(y, "MaxValue") ?? -1;
                if (min < 0 || max < 0) continue;
                roller.Add(new RolYildizi(Ulong(r, "TacticsRoleID"), Metin(r, "TacticsRoleName") ?? "?", Metin(r, "ShortName"),
                    new Puan(min, max), y.TryGetProperty("IsScoutsOpinion", out var g) && g.ValueKind == JsonValueKind.True, Metin(r, "StarRatingString")));
            }
            if (!s.TryGetValue(p[1], out var l)) s[p[1]] = l = new List<SlotUygunlugu>();
            l.Add(new SlotUygunlugu(maske, p[3] == "1", roller));
        }
        return s;
    }

    private static BellekKisi? BellekEsle(Dictionary<string, List<BellekKisi>> sozluk, string ad, int? yas)
    {
        if (!sozluk.TryGetValue(BellekVerisi.Anahtar(ad), out var l)) return null;
        return l.Count == 1 ? l[0] : l.FirstOrDefault(x => x.Yas == yas) ?? l[0];
    }

    // ---------- Teknik ekip ----------

    public static IReadOnlyList<EkipUyesiProfili> Ekip(
        Dictionary<string, Dictionary<string, JsonElement>> veri, string takimKimlik, BellekVerisi? bellek, double kur)
    {
        var sonuc = new List<EkipUyesiProfili>();
        if (!veri.TryGetValue("TeamReference:" + takimKimlik, out var t) || !t.TryGetValue("StaffAllListFiltered", out var liste) || liste.ValueKind != JsonValueKind.Array)
            return sonuc;
        var bellekSozluk = bellek?.Ekip.GroupBy(o => BellekVerisi.Anahtar(o.Ad)).ToDictionary(g => g.Key, g => g.ToList()) ?? new();
        string? menajer = Metin(t, "Manager");
        foreach (var x in liste.EnumerateArray())
        {
            if (x.ValueKind != JsonValueKind.String || x.GetString() is not { } k || !veri.TryGetValue(k, out var p)) continue;
            string? ad = Metin(p, "Name");
            if (ad == null) continue;
            var bk = BellekEsle(bellekSozluk, ad, Tam(p, "Age"));
            var oz = new Dictionary<string, int>();
            foreach (var blok in new[] { "AttributesCoaching", "AttributesCoachingKnowledge", "AttributesCoachingMental" }) OznitelikListesi(p, blok, oz);
            if (bk != null)
                foreach (var (anahtar, deger) in bk.Oznitelikler)
                    if (OznitelikAdlari.Ekip.TryGetValue(anahtar, out var tr)) oz.TryAdd(tr, deger);
            var sozlesme = Kaynak(veri, Metin(p, "FullContract"));
            double? maas = ParaEuro(Alt(sozlesme, "Wage", "DisplayValue"));
            if (maas == null && bk?.HaftalikMaasSterlin is long hs && hs > 0) maas = hs * 52 * kur;
            sonuc.Add(new EkipUyesiProfili(k, ad, Metin(p, "JobString") ?? bk?.Gorev ?? "Teknik ekip")
            {
                Yas = Tam(p, "Age") ?? bk?.Yas,
                Kisilik = Metin(p, "Personality"),
                Lisans = Alt(p, "CoachingBadges", "CoachingQualificationBadgeString"),
                Felsefe = new Felsefe(Bos(Metin(p, "PreferredFormationString")), Bos(Metin(p, "PreferredMentality")),
                                      Bos(Metin(p, "PreferredPressing")), Bos(Metin(p, "PreferredMarking"))),
                OyunStili = Bos(Metin(p, "MostLikelyTacticalStyle")),
                Oznitelikler = oz,
                Egilimler = Liste(p, "TendenciesTendsTo", "DisplayString").ToList(),
                MaasEuroYillik = maas,
                Menajer = k == menajer,
            });
        }
        return sonuc;
    }

    // ---------- Antrenman, kulüp, fikstür ----------

    private static Dictionary<string, JsonElement>? Antrenman(Dictionary<string, Dictionary<string, JsonElement>> veri, string takim)
        => veri.TryGetValue("TeamReference:" + takim, out var t) ? Kaynak(veri, Metin(t, "TeamTraining")) : null;

    public static AntrenmanDurumu AntrenmanDurumu(Dictionary<string, Dictionary<string, JsonElement>> veri, string takim, IReadOnlyDictionary<string, string> adlar)
    {
        var a = Antrenman(veri, takim);
        if (a == null) return Veri.AntrenmanDurumu.Bos;
        var kategoriler = new List<string>();
        foreach (var (k, v) in a)
            if (k.EndsWith("AttributesTooltip") && v.ValueKind == JsonValueKind.String) kategoriler.Add(v.GetString()!.Replace("\n", ": "));
        var atamalar = new List<AntrenmanAtamasi>();
        if (a.TryGetValue("CoachAssignments", out var ca) && ca.TryGetProperty("Assignments", out var liste) && liste.ValueKind == JsonValueKind.Array)
            foreach (var at in liste.EnumerateArray())
            {
                string? antrenor = null;
                foreach (var oz in at.EnumerateObject())
                    if (oz.Value.ValueKind == JsonValueKind.String && oz.Value.GetString() is { } s && s.StartsWith("PersonReference:"))
                        antrenor = adlar.TryGetValue(s, out var n) ? n : Metin(veri.GetValueOrDefault(s), "Name");
                foreach (var oz in at.EnumerateObject())
                    if (oz.Name.EndsWith("Category") && oz.Value.ValueKind == JsonValueKind.Object && oz.Value.TryGetProperty("IsAssigned", out var ia) && ia.ValueKind == JsonValueKind.True)
                    {
                        Puan? y = oz.Value.TryGetProperty("StarRange", out var sr) && Tam(sr, "MinValue") is int mn && Tam(sr, "MaxValue") is int mx && mx > 0 ? new Puan(mn, mx) : null;
                        atamalar.Add(new AntrenmanAtamasi(KategoriAdi(oz.Name), antrenor, y));
                    }
            }
        return new AntrenmanDurumu(atamalar, kategoriler);
    }

    private static string KategoriAdi(string k) => k switch
    {
        "AttackingTacticalCategory" => "Hücum - Taktiksel", "AttackingTechnicalCategory" => "Hücum - Teknik",
        "DefendingTacticalCategory" => "Savunma - Taktiksel", "DefendingTechnicalCategory" => "Savunma - Teknik",
        "PossessionTacticalCategory" => "Topa Sahip Olma - Taktiksel", "PossessionTechnicalCategory" => "Topa Sahip Olma - Teknik",
        "FitnessCategory" => "Fitness", "GoalkeepingCategory" => "Kalecilik", "SetPiecesCategory" => "Duran Toplar",
        _ => k.Replace("Category", ""),
    };

    public static KulupDurumu? Kulup(Dictionary<string, Dictionary<string, JsonElement>> veri, string takim, double kur)
    {
        if (!veri.TryGetValue("TeamReference:" + takim, out var t)) return null;
        var c = Kaynak(veri, Metin(t, "Club"));
        var g = Kaynak(veri, Metin(t, "TeamConfidence"));
        if (c == null && g == null) return null;
        return new KulupDurumu(Metin(c, "Name"))
        {
            TransferButcesiEuro = Ondalik(c, "TransferBudget") is double tb ? tb * kur : null,
            KalanMaasButcesi = Metin(c, "RemainingWageBudgetWithText"),
            MaasHarcamasi = Metin(c, "CurrentWageSpendingWithText"),
            AntrenmanTesisi = Alt(c, "TrainingFacilityRating", "Description"),
            AltyapiTesisi = Alt(c, "YouthFacilityRating", "Description"),
            YonetimBeklentisi = Metin(g, "BoardVisionString") ?? Alt(g, "BoardVisionGrade", "DisplayValue"),
            YonetimOvgu = Metinler(g, "BoardFeedbackHighlights").ToList(),
            YonetimElestiri = Metinler(g, "BoardFeedbackCriticism").ToList(),
        };
    }

    public static FiksturDurumu Fikstur(Dictionary<string, Dictionary<string, JsonElement>> veri, string takim, IReadOnlyDictionary<string, string> takimAdlari)
    {
        if (!veri.TryGetValue("TeamReference:" + takim, out var t)) return FiksturDurumu.Bos;
        var mac = new List<YakinMac>();
        if (t.TryGetValue("Next5Fixtures", out var n5) && n5.ValueKind == JsonValueKind.Array)
            foreach (var m in n5.EnumerateArray())
            {
                string? r = Metin(m, "PropertyValue");
                string ad = r != null && veri.TryGetValue(r, out var rt) && Metin(rt, "Name") is { } rn ? rn : r != null && takimAdlari.TryGetValue(r, out var ta) ? ta : r ?? "?";
                mac.Add(new YakinMac(Metin(m, "DisplayValue") ?? "", ad, r));
            }
        var form = new List<int>();
        if (t.TryGetValue("RecentFormValues", out var rf) && rf.ValueKind == JsonValueKind.Array)
            foreach (var x in rf.EnumerateArray()) if (x.ValueKind == JsonValueKind.Number) form.Add(x.GetInt32());
        return new FiksturDurumu(mac, Metin(t, "LeaguePositionString"), form);
    }

    /// <summary>Oyunun sözleşme ekranından €/£ kuru: yıllık euro metni ÷ (haftalık sterlin × 52).</summary>
    public static double Kur(Dictionary<string, Dictionary<string, JsonElement>> veri)
    {
        var kurlar = new List<double>();
        foreach (var (k, d) in veri)
        {
            if (!k.StartsWith("ContractReference") || !d.TryGetValue("Wage", out var w) || w.ValueKind != JsonValueKind.Object) continue;
            double? euro = ParaEuro(Metin(w, "DisplayValue"));
            double? sterlin = Ondalik(w, "SortValue");
            if (euro is > 0 && sterlin is > 100) kurlar.Add(euro.Value / (sterlin.Value * 52));
        }
        if (kurlar.Count == 0) return VarsayilanKur;
        kurlar.Sort();
        return kurlar[kurlar.Count / 2];
    }

    // ---------- Yardımcılar ----------

    /// <summary>"€80,5B yıllık", "€1,31M yıllık" → euro.</summary>
    public static double? ParaEuro(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        var m = Regex.Match(metin, @"([\d.,]+)\s*([BbMmKk]|bin|milyon)?");
        if (!m.Success) return null;
        string sayi = m.Groups[1].Value.Replace(".", "").Replace(',', '.');
        if (!double.TryParse(sayi, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
        string ek = m.Groups[2].Value.ToLowerInvariant();
        return ek switch { "b" or "k" or "bin" => v * 1_000, "m" or "milyon" => v * 1_000_000, _ => v };
    }

    private static string? DegerAraligi(Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("TransferValueRange", out var t) || t.ValueKind != JsonValueKind.Object) return null;
        double? min = Ondalik(t, "TransferValueRangeMinValue"), max = Ondalik(t, "TransferValueRangeMaxValue");
        if (min is null && max is null) return null;
        return min == max || min == null ? $"{max:N0}" : $"{min:N0} – {max:N0}";
    }

    private static void OznitelikListesi(Dictionary<string, JsonElement>? p, string blok, Dictionary<string, int> hedef)
    {
        if (p == null || !p.TryGetValue(blok, out var l) || l.ValueKind != JsonValueKind.Array) return;
        foreach (var x in l.EnumerateArray())
            if (Metin(x, "Name") is { } ad && Tam(x, "PropertyValue") is int v) hedef[ad] = v;
    }

    internal static Dictionary<string, JsonElement>? Kaynak(Dictionary<string, Dictionary<string, JsonElement>> veri, string? metin)
    {
        if (metin == null) return null;
        if (veri.TryGetValue(metin, out var d)) return d;
        // Kimliksiz referanslarda UIRecordID değişebilir: onu atarak eşleştir.
        string sade = Regex.Replace(metin, @"UIRecordID=\d+,", "");
        foreach (var (k, v) in veri)
            if (k.StartsWith(sade.Substring(0, Math.Min(sade.Length, sade.IndexOf('{') + 1))) && Regex.Replace(k, @"UIRecordID=\d+,", "") == sade) return v;
        return null;
    }

    private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    internal static string? Metin(Dictionary<string, JsonElement>? d, string a)
        => d != null && d.TryGetValue(a, out var v) && v.ValueKind == JsonValueKind.String && !(v.GetString()!.StartsWith("!")) ? v.GetString() : null;

    private static string? Metin(JsonElement e, string a)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Tam(Dictionary<string, JsonElement>? d, string a, string? alt = null)
    {
        if (d == null || !d.TryGetValue(a, out var v)) return null;
        if (alt != null) return Tam(v, alt);
        return v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var x) ? (int)Math.Round(x) : null;
    }

    private static int? Tam(JsonElement e, string a)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var x) ? (int)Math.Round(x) : null;

    private static ulong Ulong(JsonElement e, string a)
        => e.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetUInt64(out var u) ? u : 0;

    private static double? Ondalik(Dictionary<string, JsonElement>? d, string a, string? alt = null)
    {
        if (d == null || !d.TryGetValue(a, out var v)) return null;
        if (alt != null) return v.ValueKind == JsonValueKind.Object && v.TryGetProperty(alt, out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : null;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    }

    private static double? Ondalik(JsonElement e, string a)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? Alt(Dictionary<string, JsonElement>? d, string a, params string[] yol)
    {
        if (d == null || !d.TryGetValue(a, out var v)) return null;
        foreach (var y in yol)
        {
            if (v.ValueKind != JsonValueKind.Object || !v.TryGetProperty(y, out v)) return null;
        }
        return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.ToString() : null;
    }

    private static IEnumerable<string> Liste(Dictionary<string, JsonElement>? d, string a, string alan)
    {
        if (d == null || !d.TryGetValue(a, out var v) || v.ValueKind != JsonValueKind.Array) yield break;
        foreach (var x in v.EnumerateArray())
            if (Metin(x, alan) is { Length: > 0 } s) yield return s;
    }

    private static IEnumerable<string> Metinler(Dictionary<string, JsonElement>? d, string a)
    {
        if (d == null || !d.TryGetValue(a, out var v)) yield break;
        var yigin = new Stack<JsonElement>(); yigin.Push(v);
        while (yigin.Count > 0)
        {
            var e = yigin.Pop();
            switch (e.ValueKind)
            {
                case JsonValueKind.String when e.GetString() is { Length: > 3 } s && !s.Contains("Reference"): yield return s; break;
                case JsonValueKind.Array: foreach (var x in e.EnumerateArray()) yigin.Push(x); break;
                case JsonValueKind.Object: foreach (var x in e.EnumerateObject()) yigin.Push(x.Value); break;
            }
        }
    }
}
