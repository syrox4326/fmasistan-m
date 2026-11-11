using System.Text.Json;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using Xunit;

namespace FMAsistan.Core.Tests;

/// <summary>Otomatik taramanın kadro verisi: para, kur, rol tabloları ve slot analizi.</summary>
public class KadroTestleri
{
    [Theory]
    [InlineData("€80,5B yıllık", 80_500)]
    [InlineData("€1,31M yıllık", 1_310_000)]
    [InlineData("€2,36M yıllık", 2_360_000)]
    [InlineData("€950 yıllık", 950)]
    public void Oyunun_para_metni_euroya_cevrilir(string metin, double beklenen)
        => Assert.Equal(beklenen, KadroCozucu.ParaEuro(metin)!.Value, 3);

    [Fact]
    public void Kur_sozlesme_ekranindan_kalibre_edilir()
    {
        // Emre Utkucan: oyunda €80.500/yıl, bellekte £1.327/hafta.
        var veri = Ayristir("""{ "ContractReference{1=0,}": { "Wage": { "DisplayValue": "€80,5B yıllık", "SortValue": 1327 } } }""");
        Assert.Equal(80_500 / (1327.0 * 52), KadroCozucu.Kur(veri), 4);
    }

    [Fact]
    public void Kur_yoksa_varsayilan_kullanilir()
        => Assert.Equal(KadroCozucu.VarsayilanKur, KadroCozucu.Kur(new()));

    private const string Kadro = """
    {
      "PersonReference:1": { "Name": "Ana Oyuncu", "Condition": 84, "Sharpness": 100, "InjuryRiskLevel": 250 },
      "PersonReference:2": { "Name": "Yedek Oyuncu", "Condition": 100 },
      "RolTablosu|PersonReference:1|16384|1": { "PositionalAbilitiesRoleTable": [
        { "TacticsRoleID": 7, "TacticsRoleName": "Yardımcı Forvet", "ShortName": "YF", "CurrentAbilityStars": { "MinValue": 10, "MaxValue": 10, "IsScoutsOpinion": false } },
        { "TacticsRoleID": 8, "TacticsRoleName": "Hedef Forvet", "ShortName": "HF", "CurrentAbilityStars": { "MinValue": 17, "MaxValue": 17, "IsScoutsOpinion": false } } ] },
      "RolTablosu|PersonReference:2|16384|1": { "PositionalAbilitiesRoleTable": [
        { "TacticsRoleID": 7, "TacticsRoleName": "Yardımcı Forvet", "ShortName": "YF", "CurrentAbilityStars": { "MinValue": 14, "MaxValue": 16, "IsScoutsOpinion": true } } ] }
    }
    """;

    private static (TakimDurumu, IReadOnlyList<OyuncuProfili>) Durum()
    {
        var veri = Ayristir(Kadro);
        var kadro = new[] { new KadroOyuncusu("PersonReference:1", "Ana Oyuncu", KadroDurumu.IlkOnbir), new KadroOyuncusu("PersonReference:2", "Yedek Oyuncu", KadroDurumu.Yedek) };
        var profiller = KadroCozucu.Oyuncular(veri, "9", kadro, new Dictionary<string, string>(), null, KadroCozucu.VarsayilanKur);
        var bos = new Puan(50, 50);
        var verim = new Verimlilik(bos, new OyuncuYetenegi(bos, bos, bos, null), new Benimseme(bos, null, null, null, null),
            new TaktikAnlayisi(bos, new Dictionary<TaktikAlani, Puan>(), bos, bos, bos, null, null), null);
        var d = new TakimDurumu("T", new TaktikOzeti("t", "Dengeli", null, null), verim, Array.Empty<OyuncuVerimliligi>(), Array.Empty<string>(),
            new[] { new TaktikPozisyonu("PersonReference:1", "Ana Oyuncu", 16384, 7, 16384, 0) }) { Kadro = kadro, Profiller = profiller };
        return (d, profiller);
    }

    [Fact]
    public void Rol_tablolari_oyuncu_profiline_baglanir()
    {
        var (_, p) = Durum();
        var ana = p.Single(x => x.Kimlik == "PersonReference:1");
        Assert.Equal(84, ana.Kondisyon);
        var slot = ana.Slot(16384, true)!;
        Assert.Equal("Hedef Forvet", slot.EnIyi!.Rol);
        Assert.True(p.Single(x => x.Kimlik == "PersonReference:2").Slot(16384, true)!.Roller[0].GozlemciGorusu);
    }

    [Fact]
    public void Slot_analizi_yanlis_rolu_daha_uygun_yedegi_ve_fiziksel_riski_bulur()
    {
        var (d, _) = Durum();
        var r = KadroAnalizi.Calistir(d);
        Assert.Contains(r.Bulgular, b => b.Kural == "KADRO-ROL" && b.Baslik.Contains("Ana Oyuncu"));
        Assert.Contains(r.Bulgular, b => b.Kural == "KADRO-ALT" && b.Baslik.Contains("Yedek Oyuncu"));
        Assert.Contains(r.Bulgular, b => b.Kural == "FIZIK" && b.Kanit.Any(k => k.Contains("kondisyon %84")));
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> Ayristir(string json)
        => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json)!;
}
