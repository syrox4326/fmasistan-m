using System.Text.Json;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using Xunit;

namespace FMAsistan.Core.Tests;

/// <summary>Rakip raporu çözümü (Amed Sportif, FM 27.0.1 kaydından kırpılmış biçim).</summary>
public class RakipTestleri
{
    private const string Json = """
    {
      "TeamReference:402654676": {
        "NextFixture": "FixtureReference{UIRecordID=50,AwayTeam=402657641,HomeTeam=402654676,}"
      },
      "TeamReference:402657641": {
        "Name": "Amed Sportif",
        "MostUsedFormation": "4-2-3-1 DOS OOS Geniş",
        "ExpectedTacticalStyle": "!zaman aşımı",
        "ExpectedTactic": {
          "FormationType": "4-2-3-1 DOS OOS Geniş",
          "TacticSummaryString": "Deplasmanlarda daha temkinli bir oyun anlayışını benimsiyorlar.",
          "Players": [
            { "Position": 1, "Role": 2097153, "Player": "PersonReference:16819195" },
            { "Position": 2048, "Role": 35184380477440, "Player": "PersonReference:16835664" }
          ]
        },
        "ScoutReportStrengths": [ { "ProConsShortString": "Savunma", "ProConsLongString": "David Bates, defansın ortasında iyi bir oyuncu.", "IsPro": 1 } ],
        "ScoutReportWeaknesses": [ { "ProConsShortString": "Top Kapma", "ProConsLongString": "Kadroda topa hamle konusunda zafiyet mevcut.", "IsPro": 0 } ],
        "ExpectedFormations": { "FormationsUsed": {
          "InPossessionFormations": [ { "FormationType": "4-2-3-1 DOS OOS Geniş", "FormationUsedPercentage": 93.43, "FormationStarted": 16, "ChancesCreated": 24 } ],
          "OutPossessionFormations": [ { "FormationType": "4-4-1-1 2DOS", "FormationUsedPercentage": 74.85, "FormationStarted": 16, "ChancesCreated": 11 } ]
        } }
      }
    }
    """;

    private static RakipRaporu Rapor()
    {
        var veri = EklentiCozucu.Oku(Json);
        var adlar = new Dictionary<string, string> { ["PersonReference:16835664"] = "Samuel Ballet" };
        return VeriMerkeziCozucu.Rakip(veri, "402654676", adlar)!;
    }

    [Fact]
    public void Rakip_siradaki_mactan_bulunur()
    {
        var r = Rapor();
        Assert.Equal("402657641", r.TakimKimlik);
        Assert.Equal("Amed Sportif", r.Ad);
    }

    [Fact]
    public void Muhtemel_onbir_rol_ve_adlarla_cozulur()
    {
        var mt = Rapor().MuhtemelTaktik!;
        Assert.Equal("4-2-3-1 DOS OOS Geniş", mt.Dizilis);
        Assert.Contains("temkinli", mt.Ozet);
        Assert.Equal("Kaleci", mt.Oyuncular[0].Rolu!.Ad);       // 2^21 + 1
        var ballet = mt.Oyuncular[1];
        Assert.Equal("Samuel Ballet", ballet.Ad);
        Assert.Equal("Dış Kanat Forvet", ballet.Rolu!.Ad);      // 2^23 + 2^45
        Assert.Equal("Sağ ofansif kanat", Pozisyon.Ad(ballet.Pozisyon));
    }

    [Fact]
    public void Gozlemci_raporu_ve_dizilisler_okunur()
    {
        var r = Rapor();
        Assert.Single(r.GucluYonler);
        Assert.Equal("Top Kapma", r.ZayifYonler[0].Baslik);
        Assert.Equal(2, r.Dizilisler.Count);
        Assert.Contains(r.Dizilisler, d => d.Faz == Faz.TopRakipteyken && d.Dizilis == "4-4-1-1 2DOS" && d.IlkOnbirMaci == 16);
    }

    [Fact]
    public void Okunamayan_alan_deger_sayilmaz()
    {
        Assert.Null(Rapor().MuhtemelOyunSistemi); // "!zaman aşımı" ekibe sızmamalı
    }
}
