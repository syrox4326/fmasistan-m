using System;
using System.Collections.Generic;
using System.Text.Json;
using FM.GamePlugin;
using HarmonyLib;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;
using BKey = SI.Bindable.Bindings.Key;
using DKey = SI.Bindable.Bindings.DataKey;
using DateTime = System.DateTime;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Pasif yakalama: kayıt açıkken arayüzün native taraftan aldığı her değeri, hangi
    /// referansın hangi özelliği olduğuyla birlikte saklar. Kullanıcı oyunda bir ekranı
    /// (taktik, antrenman, rakip raporu) açtığında o ekrandaki her şey (rol adları, haftalık
    /// program gibi bağlam isteyen alanlar dahil) kaydedilir.
    ///
    /// Akış (oyunun kendi veri sistemi):
    ///   OpenChannel(referans, özellik, Key)  → hangi Key neyi istiyor
    ///   SetTarget(Key, DataKey)               → Key'in değeri hangi veri kutusunda
    ///   SetData(DataKey, değer)               → değer geldi
    ///
    /// Komut: veri\gozle.flag (içerik: saniye) → veri\gozlem\&lt;zaman&gt;.json
    /// Yalnızca gözlemler; değerleri değiştirmez.
    /// </summary>
    internal static class Gozlem
    {
        private const string HarmonyId = "com.fmasistan.mantik.gozlem";
        private const int DegerSiniri = 20000;

        private static DateTime _bitis = DateTime.MinValue;
        private static readonly Dictionary<ulong, string> KeyKaynak = new();   // Key → "Tür:kimlik|Özellik"
        private static readonly Dictionary<ulong, List<ulong>> DataKeyler = new(); // DataKey → Key'ler
        private static readonly Dictionary<string, object> Degerler = new();
        private static int _sayac;

        public static bool Aktif => _bitis != DateTime.MinValue;

        public static void Kur()
        {
            Harmony.UnpatchID(HarmonyId);
            var h = new Harmony(HarmonyId);
            var t = typeof(GameInteropSubsystem);
            h.Patch(AccessTools.Method(t, nameof(GameInteropSubsystem.SetTarget)), prefix: new HarmonyMethod(typeof(Gozlem), nameof(OnceSetTarget)));
            h.Patch(AccessTools.Method(t, nameof(GameInteropSubsystem.SetData)), prefix: new HarmonyMethod(typeof(Gozlem), nameof(OnceSetData)));
            Giris.Gunluk("Gözlem kuruldu (SetTarget, SetData).");
        }

        public static void Kaldir() => Harmony.UnpatchID(HarmonyId);

        public static void Baslat(int saniye)
        {
            KeyKaynak.Clear();
            DataKeyler.Clear();
            Degerler.Clear();
            _sayac = 0;
            _bitis = DateTime.Now.AddSeconds(saniye);
            Giris.Gunluk($"Gözlem başladı ({saniye} sn). Oyunda ekranları açabilirsin.");
        }

        /// <summary>Dinleyici her OpenChannel çağrısında bunu çağırır.</summary>
        public static void Kanal(InteropReference r, PropertyID ozellik, BKey key)
        {
            if (!Aktif) return;
            KeyKaynak[key.m_key] = Okuyucu.KaynakAdi(r) + "|" + Okuyucu.OzellikAdiKisa(ozellik.id);
        }

        public static void Tick()
        {
            if (!Aktif || DateTime.Now < _bitis) return;
            _bitis = DateTime.MinValue;
            string zaman = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            // "Tür:kimlik" → { özellik → değer }
            var sonuc = new SortedDictionary<string, SortedDictionary<string, object>>();
            foreach (var kv in Degerler)
            {
                int ayir = kv.Key.LastIndexOf('|');
                string kaynak = kv.Key.Substring(0, ayir), ozellik = kv.Key.Substring(ayir + 1);
                if (!sonuc.TryGetValue(kaynak, out var d)) sonuc[kaynak] = d = new SortedDictionary<string, object>();
                d[ozellik] = kv.Value;
            }
            Giris.DosyaYaz($"gozlem/{zaman}.json", JsonSerializer.Serialize(sonuc, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            Giris.DosyaYaz("gozlem/son.txt", zaman);
            Giris.Gunluk($"Gözlem bitti: {Degerler.Count} değer, {sonuc.Count} kaynak.");
            KeyKaynak.Clear();
            DataKeyler.Clear();
            Degerler.Clear();
        }

        private static void OnceSetTarget(BKey key, DKey targetKey)
        {
            if (!Aktif) return;
            try
            {
                if (!DataKeyler.TryGetValue(targetKey.m_key, out var l)) DataKeyler[targetKey.m_key] = l = new List<ulong>(1);
                if (!l.Contains(key.m_key)) l.Add(key.m_key);
            }
            catch { }
        }

        private static void OnceSetData(DKey key, TypedValue value)
        {
            if (!Aktif || _sayac >= DegerSiniri) return;
            try
            {
                if (!DataKeyler.TryGetValue(key.m_key, out var keyler)) return;
                object deger = null;
                bool cevrildi = false;
                foreach (var k in keyler)
                {
                    if (!KeyKaynak.TryGetValue(k, out var ad)) continue;
                    if (!cevrildi) { deger = Okuyucu.Cevir(value, 0); cevrildi = true; }
                    Degerler[ad] = deger;
                    _sayac++;
                }
            }
            catch { }
        }
    }
}
