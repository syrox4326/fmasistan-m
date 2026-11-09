using FMAsistan.Core.Analiz;
using FMAsistan.Core.Kaynak;
using FMAsistan.Core.Veri;
using Xunit;

namespace FMAsistan.Core.Tests;

/// <summary>
/// Gerçek kariyer verisiyle (Galatasaray, 2026-11-10, FM 27.0) çözücü ve analiz testleri.
/// Beklenen değerler oyunun taktik ekranında görülen değerlerdir.
/// </summary>
public class TaktikAnaliziTestleri
{
    private static TakimDurumu Durum()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Veri", "galatasaray-2026-11-10.json"));
        return EklentiCozucu.TakimDurumu(EklentiCozucu.Oku(json), "402654676");
    }

    [Fact]
    public void Cozucu_takim_verimliligini_okur()
    {
        var d = Durum();
        Assert.Equal("Galatasaray", d.TakimAdi);
        Assert.Equal("Pozitif Futbol", d.Taktik.Mentalite);
        Assert.Equal(6, d.Taktik.Yogunluk);

        var v = d.TakimVerimliligi;
        Assert.Equal(Puan.Tek(58), v.Genel);
        Assert.Equal(Puan.Tek(68), v.Yetenek.Yeterlilik);
        Assert.Equal(Puan.Tek(97), v.Yetenek.RolUygunlugu);
        Assert.Equal(Puan.Tek(39), v.Benimseme.MenajerEgilimleri);
        Assert.Equal(Puan.Tek(85), v.MacBilgisi!.RaporKalitesi);
    }

    [Fact]
    public void Cozucu_taktik_alanlarini_okur()
    {
        var a = Durum().TakimVerimliligi.Anlayis;
        Assert.Equal(8, a.Alanlar.Count);
        Assert.Equal(Puan.Tek(44), a.Alanlar[TaktikAlani.BaskiHatti]);
        Assert.Equal(Puan.Tek(63), a.Alanlar[TaktikAlani.PasStili]);
        Assert.Equal(Puan.Tek(67), a.Karmasiklik);
        Assert.Equal(Puan.Tek(48), a.Kavrayis);
        Assert.Contains("Baskı Hattı", a.AlanTavsiyesi!.Metin);
    }

    [Fact]
    public void Cozucu_oyuncu_bazinda_verimliligi_okur()
    {
        var oyuncular = Durum().Oyuncular;
        Assert.Equal(4, oyuncular.Count);
        Assert.All(oyuncular, o => Assert.StartsWith("PersonReference:", o.OyuncuKimlik));
    }

    [Fact]
    public void Analiz_en_zayif_alani_baski_hatti_olarak_bulur()
    {
        var bulgular = TaktikAnalizi.Calistir(Durum());
        var k01 = Assert.Single(bulgular, b => b.Kural == "K-01");
        Assert.Equal("Antrenmanla düzeltilecek alanlar: Baskı hattı, Pres yoğunluğu", k01.Baslik);
        Assert.Equal(Onem.Yuksek, k01.Onem);
        Assert.Equal("Baskı hattı: 44", k01.Kanit[0]);
        Assert.StartsWith("1. hafta: 'Baskı hattı'", k01.Oneri);
        // Oyunun tavsiyesi Baskı Hattı'nı andığı için bir kez eklenir.
        Assert.Single(k01.Kanit, k => k.StartsWith("Oyunun tavsiyesi"));
    }

    [Fact]
    public void Analiz_karmasiklik_ve_egilim_celiskisini_bulur()
    {
        var kurallar = TaktikAnalizi.Calistir(Durum()).Select(b => b.Kural).ToHashSet();
        Assert.Contains("K-03", kurallar); // karmaşıklık 67, kavrayış 48
        Assert.Contains("K-07", kurallar); // menajer eğilimleri 39
        Assert.DoesNotContain("K-09", kurallar); // rapor kalitesi 85, rakip bilgisi 74: yeterli
    }

    [Fact]
    public void Her_bulgu_kurala_ve_kanita_baglidir()
    {
        foreach (var b in TaktikAnalizi.Calistir(Durum()))
        {
            Assert.False(string.IsNullOrWhiteSpace(b.Kural));
            Assert.NotEmpty(b.Kanit);
        }
    }

    [Fact]
    public void Aralikli_puan_belirsizligi_korur()
    {
        var p = new Puan(55, 70);
        Assert.True(p.Aralikli);
        Assert.Equal("55-70", p.ToString());
        Assert.Equal(62.5, p.Orta);
    }
}
