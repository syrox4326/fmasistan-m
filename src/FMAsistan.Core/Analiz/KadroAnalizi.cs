using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Analiz;

/// <summary>Taktikteki bir slotun (pozisyon + faz) değerlendirmesi: seçili oyuncu, rolü ve kadrodaki alternatifler.</summary>
public sealed record SlotDegerlendirmesi(
    string Slot, bool TopaSahipken, string OyuncuKimlik, string Oyuncu, string Rol,
    RolYildizi? Mevcut, RolYildizi? OyuncununEnIyiRolu,
    IReadOnlyList<(OyuncuProfili Oyuncu, RolYildizi Yildiz)> Alternatifler);

/// <summary>Kadro analizinin tamamı: slot tablosu ve bulgular.</summary>
public sealed record KadroRaporu(IReadOnlyList<SlotDegerlendirmesi> Slotlar, IReadOnlyList<Bulgu> Bulgular)
{
    public static readonly KadroRaporu Bos = new(Array.Empty<SlotDegerlendirmesi>(), Array.Empty<Bulgu>());
}

/// <summary>
/// Kadro analizi. Kaynak oyunun kendi değerlendirmesidir: rol yıldızları (PositionalAbilitiesRoleTable,
/// teknik ekibin bilgisiyle sınırlı, aralıklı olabilir), kondisyon, maç keskinliği, sakatlık riski,
/// sezon ortalaması ve antrenman puanı. Eşikler bilgi tabanındaki kurallara dayanır.
/// </summary>
public static class KadroAnalizi
{
    // Yıldız birimi: 0–20 (4 birim = 1 yıldız). Yarım yıldız (2 birim) altı fark gürültüdür.
    private const double AnlamliFark = 2;

    public static KadroRaporu Calistir(TakimDurumu d)
    {
        if (d.Profiller.Count == 0 || d.Dizilis.Count == 0) return KadroRaporu.Bos;
        var slotlar = new List<SlotDegerlendirmesi>();
        var bulgular = new List<Bulgu>();
        // Alternatif: ilk 11 dışındaki, sakat olmayan oyuncular (ilk 11'deki biri zaten başka bir slotta oynuyor).
        var ilkOnbir = d.Dizilis.Select(p => p.OyuncuKimlik).ToHashSet();
        var uygunOyuncular = d.Profiller
            .Where(p => !ilkOnbir.Contains(p.Kimlik) && !p.Durumlar.Any(x => x.Contains("Sakat")))
            .ToList();

        foreach (var p in d.Dizilis)
            foreach (bool ip in new[] { true, false })
            {
                ulong maske = ip ? p.IpPozisyon : p.OopPozisyon;
                ulong rol = ip ? p.IpRol : p.OopRol;
                var rolBilgi = ip ? p.IpRolu : p.OopRolu;
                var profil = d.Profil(p.OyuncuKimlik);
                var slot = profil?.Slot(maske, ip);
                var mevcut = slot?.Rol(rol);
                var alternatifler = uygunOyuncular
                    .Where(o => o.Kimlik != p.OyuncuKimlik)
                    .Select(o => (Oyuncu: o, Yildiz: o.Slot(maske, ip)?.Rol(rol)))
                    .Where(x => x.Yildiz != null)
                    .Select(x => (x.Oyuncu, x.Yildiz!))
                    .OrderByDescending(x => x.Item2.Deger.Orta).Take(3).ToList();
                string slotAdi = $"{Pozisyon.Ad(maske)} ({(ip ? "topa sahipken" : "top rakipteyken")})";
                slotlar.Add(new SlotDegerlendirmesi(slotAdi, ip, p.OyuncuKimlik, p.Ad ?? p.OyuncuKimlik,
                    rolBilgi?.Ad ?? "?", mevcut, slot?.EnIyi, alternatifler));
            }

        // KADRO-ROL: oyuncu aynı slotta başka bir rolde belirgin daha iyi.
        foreach (var s in slotlar)
        {
            if (s.Mevcut == null || s.OyuncununEnIyiRolu == null) continue;
            double fark = s.OyuncununEnIyiRolu.Deger.Orta - s.Mevcut.Deger.Orta;
            if (fark < AnlamliFark || s.OyuncununEnIyiRolu.RolKimlik == s.Mevcut.RolKimlik) continue;
            bulgular.Add(new Bulgu("KADRO-ROL", fark >= 4 ? Onem.Yuksek : Onem.Orta,
                $"{s.Oyuncu}: {s.Slot} için seçili rol oyuncuya en uygun rol değil",
                $"Oyunun rol tablosuna göre {s.Oyuncu}, aynı pozisyonda '{s.OyuncununEnIyiRolu.Rol}' rolünde {Yildiz(s.OyuncununEnIyiRolu)} " +
                $"değerlendiriliyor; seçili '{s.Rol}' rolünde {Yildiz(s.Mevcut)}. Fark {fark / 4:0.#} yıldız. Rol değişikliği taktik planı " +
                "bozmuyorsa oyuncunun verimi artar; bozuyorsa bu rol için daha uygun bir oyuncu gerekir.",
                new[] { $"{s.Rol}: {Yildiz(s.Mevcut)}", $"{s.OyuncununEnIyiRolu.Rol}: {Yildiz(s.OyuncununEnIyiRolu)}" },
                $"Taktik ekranında {s.Oyuncu} için rolü '{s.OyuncununEnIyiRolu.Rol}' olarak dene ya da bu role uygun alternatifi değerlendir."));
        }

        // KADRO-ALT: kadroda bu rol için belirgin daha uygun bir oyuncu var.
        foreach (var s in slotlar)
        {
            if (s.Mevcut == null || s.Alternatifler.Count == 0) continue;
            var (o, y) = s.Alternatifler[0];
            double fark = y.Deger.Orta - s.Mevcut.Deger.Orta;
            if (fark < AnlamliFark) continue;
            var kanit = new List<string> { $"{s.Oyuncu} ({s.Rol}): {Yildiz(s.Mevcut)}", $"{o.Ad}: {Yildiz(y)}" };
            if (o.Kondisyon != null) kanit.Add($"{o.Ad} kondisyon %{o.Kondisyon}" + (o.MacKeskinligi != null ? $", maç keskinliği %{o.MacKeskinligi}" : ""));
            if (o.SezonOrtalamasi != null) kanit.Add($"{o.Ad} sezon ortalaması {o.SezonOrtalamasi:0.00} ({o.Mac} maç)");
            var mevcutProfil = d.Profil(s.OyuncuKimlik);
            if (mevcutProfil?.SezonOrtalamasi != null) kanit.Add($"{s.Oyuncu} sezon ortalaması {mevcutProfil.SezonOrtalamasi:0.00} ({mevcutProfil.Mac} maç)");
            bulgular.Add(new Bulgu("KADRO-ALT", fark >= 4 ? Onem.Yuksek : Onem.Orta,
                $"{s.Slot} / {s.Rol}: {o.Ad} daha uygun (şu an {s.Oyuncu})",
                $"Oyunun kendi değerlendirmesinde {o.Ad}, '{s.Rol}' rolünde {Yildiz(y)}; şu an oynayan {s.Oyuncu} {Yildiz(s.Mevcut)}. " +
                (y.GozlemciGorusu || s.Mevcut.GozlemciGorusu ? "Değerlerden en az biri gözlemci görüşü (aralıklı): ekip bu oyuncuyu tam tanımıyor. " : "") +
                "Fark yalnızca rol uyumunu gösterir; form, kondisyon ve takım uyumu ayrıca tartılmalı.",
                kanit, $"{o.Ad}'ı bu rol için ilk 11 adayı olarak değerlendir; en azından rotasyonda ilk tercih yap."));
        }

        // FİZİK: ilk 11'de kondisyonu, keskinliği düşük ya da sakatlık riski yüksek oyuncular.
        foreach (var p in d.Dizilis)
        {
            var o = d.Profil(p.OyuncuKimlik);
            if (o == null) continue;
            var kanit = new List<string>();
            if (o.Kondisyon is int k && k < 90) kanit.Add($"kondisyon %{k}");
            if (o.MacKeskinligi is int m && m < 75) kanit.Add($"maç keskinliği %{m}");
            if (o.SakatlikRiski is int r && r >= 150) kanit.Add($"sakatlık riski yüksek (oyun değeri {r})");
            if (kanit.Count == 0) continue;
            var adaylar = slotlar.FirstOrDefault(s => s.OyuncuKimlik == p.OyuncuKimlik && s.TopaSahipken)?.Alternatifler
                ?? Array.Empty<(OyuncuProfili Oyuncu, RolYildizi Yildiz)>();
            var yedek = adaylar.FirstOrDefault(a => a.Oyuncu.Kondisyon is null or >= 92);
            var yedekVar = yedek.Oyuncu != null;
            bulgular.Add(new Bulgu("FIZIK", o.Kondisyon is < 80 || o.SakatlikRiski is >= 200 ? Onem.Yuksek : Onem.Orta,
                $"{o.Ad}: fiziksel durum riski ({string.Join(", ", kanit)})",
                $"{o.Ad} ilk 11'de ama {string.Join(", ", kanit)}. Düşük kondisyonla oynayan oyuncunun hem performansı hem sakatlanma olasılığı artar; " +
                "maç keskinliği düşük oyuncu karar ve teknikte geri kalır.",
                kanit, yedek.Oyuncu != null
                    ? $"Rotasyon: {yedek.Oyuncu.Ad} bu slot için {Yildiz(yedek.Yildiz)} ve kondisyonu %{yedek.Oyuncu.Kondisyon?.ToString() ?? "?"}. Antrenmanda {o.Ad} için bireysel yoğunluğu düşür."
                    : $"Antrenmanda {o.Ad} için bireysel yoğunluğu düşür, gerekiyorsa maç içinde erken değiştir."));
        }

        // FORM: ilk 11'de sezon ortalaması takım ortalamasının belirgin altında kalanlar ve tersi yedekler.
        var ortalamali = d.Profiller.Where(o => o.SezonOrtalamasi is > 0 && o.Mac is >= 3).ToList();
        if (ortalamali.Count >= 8)
        {
            double takim = ortalamali.Average(o => o.SezonOrtalamasi!.Value);
            var ilk = d.Dizilis.Select(p => d.Profil(p.OyuncuKimlik)).Where(o => o?.SezonOrtalamasi is > 0 && o.Mac is >= 3).ToList();
            foreach (var o in ilk.Where(o => o!.SezonOrtalamasi < takim - 0.25))
                bulgular.Add(new Bulgu("FORM", Onem.Orta, $"{o!.Ad}: sezon ortalaması takımın altında",
                    $"{o.Ad} {o.Mac} maçta {o.SezonOrtalamasi:0.00} ortalamada; takımın ortalaması {takim:0.00}. Tek başına karar gerekçesi değil " +
                    "(rolün doğası puanı etkiler), ama rol uyumu ve antrenman puanıyla birlikte okunmalı.",
                    new[] { $"{o.Ad}: {o.SezonOrtalamasi:0.00} ({o.Mac} maç)", $"takım ortalaması {takim:0.00}" }
                        .Concat(o.AntrenmanPuani != null ? new[] { $"antrenman puanı {o.AntrenmanPuani:0.0}" } : Array.Empty<string>()).ToList(),
                    null));
        }

        // ANTRENMAN: antrenman puanı ile maç puanı arasındaki uçurum.
        foreach (var o in d.Profiller.Where(o => o.AntrenmanPuani is > 0 && o.SezonOrtalamasi is > 0 && o.Mac is >= 3))
        {
            double fark = o.AntrenmanPuani!.Value - o.SezonOrtalamasi!.Value;
            if (Math.Abs(fark) < 1.2) continue;
            bulgular.Add(new Bulgu("ANTR-FORM", Onem.Bilgi,
                fark > 0 ? $"{o.Ad}: antrenmanda parlıyor, maçta yansımıyor" : $"{o.Ad}: maçta antrenmandan iyi",
                fark > 0
                    ? $"{o.Ad} antrenmanda {o.AntrenmanPuani:0.0}, maçlarda {o.SezonOrtalamasi:0.00}. Rol uyumsuzluğu, baskı altında karar ya da az süre bu farkı açıklayabilir."
                    : $"{o.Ad} maçlarda {o.SezonOrtalamasi:0.00}, antrenmanda {o.AntrenmanPuani:0.0}. Antrenman yükü ya da mutluluğu kontrol edilmeli.",
                new[] { $"antrenman {o.AntrenmanPuani:0.0}", $"maç {o.SezonOrtalamasi:0.00} ({o.Mac} maç)" }
                    .Concat(o.AntrenmanMutlulugu != null ? new[] { $"antrenman mutluluğu: {o.AntrenmanMutlulugu}" } : Array.Empty<string>()).ToList(),
                null));
        }

        return new KadroRaporu(slotlar, bulgular);
    }

    public static string Yildiz(RolYildizi? y) => y == null ? "bilinmiyor"
        : (y.Deger.Aralikli ? $"{y.Deger.Min / 4.0:0.#}–{y.Deger.Max / 4.0:0.#}" : $"{y.Deger.Min / 4.0:0.#}") + " yıldız" + (y.GozlemciGorusu ? " (gözlemci görüşü)" : "");
}
