using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Yükleyicinin çağırdığı giriş noktası. Tüm metotlar Unity ana iş parçacığında çalışır.
    ///
    /// Masaüstü uygulamasıyla iletişim, oyun klasöründeki BepInEx\FMAsistan\veri klasöründeki
    /// dosyalarla yapılır: uygulama bir "*.flag" dosyası bırakır, mantık okuyup siler ve
    /// sonucu yine bu klasöre yazar.
    /// </summary>
    public static class Giris
    {
        public const string Surum = "4";

        /// <summary>İletişim klasörü; yükleyici verir (oyun klasöründe BepInEx\FMAsistan\veri).</summary>
        internal static string Klasor { get; private set; }

        internal static Action<string> Gunluk = _ => { };

        private static readonly Dictionary<string, string> Hatalar = new();
        private static readonly Dictionary<string, int> HataSayisi = new();
        private static DateTime _sonKalp = DateTime.MinValue;

        public static void Baslat(Action<string> gunluk, string klasor)
        {
            Gunluk = gunluk ?? (_ => { });
            Klasor = klasor;
            Directory.CreateDirectory(Klasor);
            Guvenli("Dinleyici kurulumu", Dinleyici.Kur);
            Guvenli("Gözlem kurulumu", Gozlem.Kur);
            Gunluk($"FM Asistanım mantığı v{Surum} yüklendi ({DateTime.Now:HH:mm:ss}).");
        }

        public static void Durdur()
        {
            Guvenli("Okuyucu temizleme", Okuyucu.Temizle);
            Guvenli("Gözlem kaldırma", Gozlem.Kaldir);
            Guvenli("Dinleyici kaldırma", Dinleyici.Kaldir);
        }

        public static void Tick()
        {
            KalpYaz();
            Guvenli("Komutlar", Komutlar);
            Guvenli("Dinleyici", Dinleyici.Tick);
            Guvenli("Okuyucu", Okuyucu.Tick);
            Guvenli("Gözlem", Gozlem.Tick);
        }

        private static void Komutlar()
        {
            string dinle = Path.Combine(Klasor, "dinle.flag");
            if (File.Exists(dinle))
            {
                string icerik = File.ReadAllText(dinle).Trim();
                File.Delete(dinle);
                int saniye = int.TryParse(icerik, out var s) ? Math.Clamp(s, 5, 300) : 30;
                Dinleyici.Baslat(saniye);
            }

            string oku = Path.Combine(Klasor, "oku.json");
            if (File.Exists(oku))
            {
                string icerik = File.ReadAllText(oku);
                File.Delete(oku);
                Okuyucu.Baslat(icerik);
            }

            string gozle = Path.Combine(Klasor, "gozle.flag");
            if (File.Exists(gozle))
            {
                string icerik = File.ReadAllText(gozle).Trim();
                File.Delete(gozle);
                Gozlem.Baslat(int.TryParse(icerik, out var gs) ? Math.Clamp(gs, 5, 600) : 60);
            }

            string sablon = Path.Combine(Klasor, "sablonlar.flag");
            if (File.Exists(sablon))
            {
                File.Delete(sablon);
                Okuyucu.SablonlariYaz();
            }
        }

        internal static void Guvenli(string ad, Action is_)
        {
            try
            {
                is_();
                Hatalar.Remove(ad);
                HataSayisi.Remove(ad);
            }
            catch (Exception e)
            {
                var ic = e.InnerException ?? e;
                Hatalar[ad] = ic.GetType().Name + ": " + ic.Message;
                HataSayisi.TryGetValue(ad, out int n);
                HataSayisi[ad] = n + 1;
                if (n < 3) Gunluk($"Mantık hatası ({ad}): {ic}");
            }
        }

        /// <summary>Uygulamanın eklentinin canlı olduğunu görmesi için 5 sn'de bir yazılır.</summary>
        private static void KalpYaz()
        {
            if ((DateTime.UtcNow - _sonKalp).TotalSeconds < 5) return;
            _sonKalp = DateTime.UtcNow;
            try
            {
                string oyun = null;
                try { oyun = UnityEngine.Application.version; } catch { }
                DosyaYaz("kalp.json", JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["zaman"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ["mantik"] = Surum,
                    ["oyun"] = oyun,
                    ["dinleniyor"] = Dinleyici.Aktif,
                    ["sablon"] = Okuyucu.SablonSayisi,
                    ["hatalar"] = Hatalar,
                }));
            }
            catch { }
        }

        /// <summary>Okuyucu yarım dosya görmesin diye önce geçici dosyaya yazıp taşır.</summary>
        internal static void DosyaYaz(string ad, string icerik)
        {
            string yol = Path.Combine(Klasor, ad);
            Directory.CreateDirectory(Path.GetDirectoryName(yol));
            File.WriteAllText(yol + ".tmp", icerik);
            File.Move(yol + ".tmp", yol, overwrite: true);
        }
    }
}
