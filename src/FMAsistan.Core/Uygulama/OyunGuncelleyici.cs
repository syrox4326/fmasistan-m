using System.Text.Json;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Uygulama;

/// <summary>
/// Oyundaki eklentiden eksik verileri ister: temel taktik verisi, rakip raporu,
/// kadronun ve rakibin adları, talimat kimliklerinin iç adları. Eklentiyle iletişim
/// veri klasörüne bırakılan komut dosyalarıyla yapılır.
/// </summary>
public static class OyunGuncelleyici
{
    public static async Task<bool> GuncelleAsync(Ayarlar a, Action<string> bildir, CancellationToken iptal = default)
    {
        string veri = a.VeriKlasoru;
        string takim = a.TakimKimlik;
        var birlesik = OyunVerisi.Yukle(veri);
        var adlar = OyunVerisi.Adlar(birlesik);
        var durum = DurumYukleyici.Yukle(a);

        var istekler = new List<object>
        {
            new { kaynak = "TeamTacticReference:" + takim, yollar = new[] { "Name", "MentalityString", "MentalityDropdown", "MentalityDropdownOptions",
                  "TacticalStyleDropdown", "TacticalStyleDropdownOptions", "TacticalEffectiveness", "TacticalPositionsCombined",
                  "FilteredPlayers", "FilteredSubs" }.Concat(Talimatlar.Ozellikler.Keys).ToArray() },
            new { kaynak = "TeamReference:" + takim, yollar = new[] { "Name", "NextFixture", "MostUsedFormation", "GoalAnalysisScoring", "GoalAnalysisConceding", "ExpectedFormations" } },
        };
        if (durum.Rakip is { } rk)
            istekler.Add(new { kaynak = "TeamReference:" + rk.TakimKimlik, yollar = new[] { "Name", "ScoutReportStrengths", "ScoutReportWeaknesses",
                "ExpectedFormations", "MostUsedFormation", "NextOppositionThreatLevelAssessment", "DataAnalystLevelInfo" } });
        var rakipOyunculari = durum.Rakip?.MuhtemelTaktik?.Oyuncular.Select(o => o.OyuncuKimlik) ?? Enumerable.Empty<string>();
        var eksik = durum.Kadro.Select(k => k.Kimlik).Concat(durum.Dizilis.Select(p => p.OyuncuKimlik)).Concat(rakipOyunculari)
            .Distinct().Where(k => !adlar.ContainsKey(k)).ToList();
        foreach (var k in eksik) istekler.Add(new { kaynak = k, yollar = new[] { "Name" } });

        bildir($"Oyundan isteniyor: {istekler.Count} kaynak ({eksik.Count} eksik oyuncu adı).");
        if (!await EklentiIste(veri, "oku.json", JsonSerializer.Serialize(new { sure = 12, istekler }), Path.Combine(veri, "oku", "son.txt"), iptal))
        {
            bildir("Eklenti yanıt vermedi. Oyun açık ve kariyer yüklü mü?");
            return false;
        }

        // Talimat değerlerinin iç adları (PropertyID → ad).
        birlesik = OyunVerisi.Yukle(veri);
        var kimlikler = new List<uint>();
        if (birlesik.TryGetValue("TeamTacticReference:" + takim, out var t))
            foreach (var oz in Talimatlar.Ozellikler.Keys)
                if (t.TryGetValue(oz, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetUInt32(out var id)) kimlikler.Add(id);
        if (kimlikler.Count > 0)
        {
            string sonucDosyasi = Path.Combine(veri, "kimlik-adlari-sonuc.json");
            var eski = File.Exists(sonucDosyasi) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(sonucDosyasi))! : new();
            if (!await EklentiIste(veri, "kimlik-adlari.json", JsonSerializer.Serialize(kimlikler), sonucDosyasi, iptal))
            {
                bildir("Eklenti talimat adlarını döndürmedi.");
                return false;
            }
            var yeni = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(sonucDosyasi))!;
            foreach (var (k, v) in eski) yeni.TryAdd(k, v);
            File.WriteAllText(sonucDosyasi, JsonSerializer.Serialize(yeni, new JsonSerializerOptions { WriteIndented = true }));
        }
        bildir("Güncellendi.");
        return true;
    }

    /// <summary>Eklentiye bir komut dosyası bırakır ve sonuç dosyası yenilenene kadar (en çok 30 sn) bekler.</summary>
    public static async Task<bool> EklentiIste(string veri, string komutDosyasi, string icerik, string sonucDosyasi, CancellationToken iptal = default)
    {
        var once = File.Exists(sonucDosyasi) ? File.GetLastWriteTimeUtc(sonucDosyasi) : DateTime.MinValue;
        string yol = Path.Combine(veri, komutDosyasi);
        File.WriteAllText(yol + ".tmp", icerik);
        File.Move(yol + ".tmp", yol, overwrite: true);
        for (int i = 0; i < 120; i++)
        {
            await Task.Delay(250, iptal);
            if (File.Exists(sonucDosyasi) && File.GetLastWriteTimeUtc(sonucDosyasi) > once) { await Task.Delay(200, iptal); return true; }
        }
        return false;
    }
}
