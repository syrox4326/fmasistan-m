using FMAsistan.Core.Analiz;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using FMAsistan.Core.Yz;
using Xunit;

namespace FMAsistan.Core.Tests;

public class YzTestleri
{
    private static readonly TeknikEkipUyesi Jim = new("PersonReference:16785153", "Jim Fraser", "Asistan Menajer",
        new Felsefe("4-3-3 DOS Geniş", "Riskli", "Çok şiddetli", "Alan"), "Uluslararası Pro Lisansı");

    private static TakimDurumu Durum() => EklentiCozucu.TakimDurumu(
        EklentiCozucu.Oku(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Veri", "galatasaray-2026-11-10.json"))),
        "402654676");

    [Fact]
    public void Persona_felsefeyi_kurallari_ve_bulgulari_icerir()
    {
        var d = Durum();
        string t = EkipPersonasi.SistemTalimati(Jim,
            new EkipPersonasi.Nitelikler(TaktikBilgisi: 16, Otorite: 7, Motivasyon: 20),
            d, TaktikAnalizi.Calistir(d), "K-01 deneme ilkesi");

        Assert.Contains("Jim Fraser", t);
        Assert.Contains("4-3-3 DOS Geniş", t);
        Assert.Contains("Çok şiddetli", t);
        Assert.Contains("onaylamak değil", t);          // objektiflik kuralı
        Assert.Contains("Baskı hattı 44", t);            // oyun verisi
        Assert.Contains("[K-07", t);                     // motor bulgusu
        Assert.Contains("yumuşak", t);                   // otorite 7 → çekingen dil
        Assert.Contains("K-01 deneme ilkesi", t);        // bilgi tabanı
    }

    [Fact]
    public void Persona_deterministiktir()
    {
        var d = Durum();
        var b = TaktikAnalizi.Calistir(d);
        var n = new EkipPersonasi.Nitelikler(TaktikBilgisi: 16);
        Assert.Equal(EkipPersonasi.SistemTalimati(Jim, n, d, b, "x"), EkipPersonasi.SistemTalimati(Jim, n, d, b, "x"));
    }

    [Fact]
    public void Dusuk_taktik_bilgisi_belirsizlik_talimati_verir()
    {
        string t = EkipPersonasi.SistemTalimati(Jim, new EkipPersonasi.Nitelikler(TaktikBilgisi: 6), null, Array.Empty<Bulgu>(), "");
        Assert.Contains("emin olmadığını açıkça söyle", t);
    }

    [Fact]
    public void Cli_argumanlari_araclari_kapatir_ve_oturumu_surdurur()
    {
        var cli = new ClaudeCodeCli(claudeYolu: "claude", calismaKlasoru: Path.GetTempPath());
        var a = cli.Argumanlar("sistem", "abc").ToList();
        int i = a.IndexOf("--tools");
        Assert.Equal("", a[i + 1]);
        Assert.Contains("--strict-mcp-config", a);
        Assert.Equal("abc", a[a.IndexOf("--resume") + 1]);
        Assert.DoesNotContain("--resume", cli.Argumanlar("sistem", null));
    }

    [Fact]
    public void Cli_ciktisi_cozulur_ve_hata_firlatilir()
    {
        var c = ClaudeCodeCli.Coz("{\"result\":\" Merhaba \",\"session_id\":\"s1\",\"is_error\":false,\"total_cost_usd\":0.01}", "", 0);
        Assert.Equal("Merhaba", c.Metin);
        Assert.Equal("s1", c.OturumKimligi);
        Assert.Throws<YapayZekaHatasi>(() => ClaudeCodeCli.Coz("{\"result\":\"limit\",\"is_error\":true}", "", 1));
        Assert.Throws<YapayZekaHatasi>(() => ClaudeCodeCli.Coz("düz metin", "hata", 1));
    }
}
