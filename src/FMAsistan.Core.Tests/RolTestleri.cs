using FMAsistan.Core.Analiz;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using Xunit;

namespace FMAsistan.Core.Tests;

public class RolTestleri
{
    private static TakimDurumu Gercek()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Veri", "galatasaray-2026-11-10.json"));
        var veri = EklentiCozucu.Oku(json);
        var adlar = veri.Where(k => k.Key.StartsWith("PersonReference:") && k.Value.ContainsKey("Name"))
                        .ToDictionary(k => k.Key, k => k.Value["Name"].GetString()!);
        return EklentiCozucu.TakimDurumu(veri, "402654676", adlar);
    }

    [Fact]
    public void Katalog_oyundan_okunan_rolleri_icerir()
    {
        Assert.Equal(79, RolKatalogu.Sayi);
        // Aynı kimlik iki fazda farklı roldür.
        Assert.Equal("Yardımcı Forvet", RolKatalogu.Ad(Faz.TopaSahipken, 1024));
        Assert.Equal("Presçi Bek", RolKatalogu.Ad(Faz.TopRakipteyken, 1024));
        // 2^53 üstü kimlikler hassasiyet kaybetmeden.
        Assert.Equal("Oyun Kurucu Kanat Bek", RolKatalogu.Ad(Faz.TopaSahipken, 36028797018963968UL));
    }

    [Fact]
    public void Pozisyon_maskesi_cozulur()
    {
        Assert.Equal("Sağ bek", Pozisyon.Ad(4));
        Assert.Equal("Stoper (sağ)", Pozisyon.Ad(2097168));
        Assert.Equal("Stoper (sol)", Pozisyon.Ad(1048592));
        Assert.Equal(Kanat.Sol, Pozisyon.KanatOf(4096));
        Assert.Equal(Kanat.Sag, Pozisyon.KanatOf(2098176));
    }

    [Fact]
    public void Gercek_dizilis_oyundaki_rollerle_okunur()
    {
        var d = Gercek().Dizilis;
        Assert.Equal(11, d.Count);
        var kaleci = d[0];
        Assert.Equal("Uğurcan Çakır", kaleci.Ad);
        Assert.Equal("Ayağı İyi Kaleci", kaleci.IpRolu!.Ad);
        var lemina = d.Single(p => p.Ad == "Mario Lemina");
        Assert.Equal("Gizli Libero", lemina.IpRolu!.Ad);
        Assert.Equal("Defansif orta saha (sol)", Pozisyon.Ad(lemina.OopPozisyon));
        Assert.Equal("Kanat Forvet", d.Single(p => p.Ad == "Rafael Leão").IpRolu!.Ad);
    }

    [Fact]
    public void Gercek_dizilişte_rol_kurali_bulgusu_yok()
    {
        var kurallar = TaktikAnalizi.Calistir(Gercek()).Select(b => b.Kural).ToList();
        Assert.DoesNotContain("K-22", kurallar);
        Assert.DoesNotContain("K-25", kurallar);
    }

    private static TakimDurumu Sentetik(params TaktikPozisyonu[] dizilis)
    {
        var g = Gercek();
        return g with { Dizilis = dizilis, Oyuncular = Array.Empty<OyuncuVerimliligi>() };
    }

    [Fact]
    public void K22_ayni_kanatta_kanat_oyuncusu_ve_kanat_bek()
    {
        var d = Sentetik(
            new TaktikPozisyonu("p1", "Bek", 4, 8, 4, 512),          // sağ bek, Kanat Bek
            new TaktikPozisyonu("p2", "Kanat", 2048, 128, 256, 0));  // sağ ofansif kanat, Kanat Oyuncusu
        var k = Assert.Single(TaktikAnalizi.Calistir(d), b => b.Kural == "K-22");
        Assert.Contains("Sağ kanatta", k.Baslik);
    }

    [Fact]
    public void K22_farkli_kanatlarda_cakisma_yok()
    {
        var d = Sentetik(
            new TaktikPozisyonu("p1", "Bek", 8, 8, 8, 512),          // sol bek
            new TaktikPozisyonu("p2", "Kanat", 2048, 128, 256, 0));  // sağ kanat
        Assert.DoesNotContain(TaktikAnalizi.Calistir(d), b => b.Kural == "K-22");
    }

    [Fact]
    public void K25_risksiz_kaleci_ve_cakili_stoper()
    {
        var d = Sentetik(
            new TaktikPozisyonu("k", "Kaleci", 1, 9007199254740992UL, 1, 1),
            new TaktikPozisyonu("s", "Stoper", 1048592, 536870912, 1048592, 8));
        Assert.Contains(TaktikAnalizi.Calistir(d), b => b.Kural == "K-25" && b.Onem == Onem.Yuksek);
    }
}
