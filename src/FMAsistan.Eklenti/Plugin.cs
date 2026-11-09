using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace FMAsistan.Eklenti
{
    [BepInPlugin(Id, Ad, Surum)]
    public class Plugin : BasePlugin
    {
        public const string Id = "com.fmasistan.eklenti";
        public const string Ad = "FM Asistanım Eklentisi";
        public const string Surum = "0.1.0";

        internal static ManualLogSource Gunluk;

        public override void Load()
        {
            Gunluk = Log;
            // BepInEx'in kendi yardımcısı: tipi IL2CPP'ye kaydeder ve kalıcı bir nesneye ekler.
            var bilesen = AddComponent<Yukleyici>();
            Gunluk.LogInfo($"{Ad} {Surum} yüklendi (bileşen: {(bilesen != null ? "eklendi" : "YOK")}).");
        }
    }

    /// <summary>
    /// Ana iş parçacığında (Unity Update) çalışır. Mantık DLL'ini izler, değiştiyse yeniden
    /// yükler ve her karede değil, ~4 kez/sn mantığın Tick metodunu çağırır. Oyun nesnelerine
    /// yalnızca ana iş parçacığından dokunulmalı; bu yüzden tüm iş buradan tetiklenir.
    /// </summary>
    public class Yukleyici : MonoBehaviour
    {
        // Game Pass sürümü paketli uygulama olarak çalışır; %LOCALAPPDATA% altındaki yeni
        // klasörleri göremeyebiliyor. Oyunun kendi BepInEx klasörü ise kesin görünür ve
        // yazılabilir. Mantık "plugins" dışında durur ki BepInEx onu eklenti diye yükleyip
        // kilitlemesin (kilitlenirse canlı güncelleme olmaz).
        internal static readonly string Kok = Path.Combine(Paths.BepInExRootPath, "FMAsistan");
        private static readonly string MantikYolu = Path.Combine(Kok, "mantik", "FMAsistan.Mantik.dll");
        private static readonly string VeriKlasoru = Path.Combine(Kok, "veri");

        private const float Aralik = 0.25f;

        private float _gecen;
        private DateTime _yuklenen = DateTime.MinValue;
        private MethodInfo _tick;
        private MethodInfo _durdur;
        private int _hataSayisi;

        public Yukleyici(IntPtr ptr) : base(ptr) { }

        private bool _ilkKare = true;

        public void Update()
        {
            if (_ilkKare)
            {
                _ilkKare = false;
                Plugin.Gunluk.LogInfo($"Yükleyici çalışıyor. Mantık yolu: {MantikYolu} (var: {File.Exists(MantikYolu)})");
            }
            _gecen += Time.unscaledDeltaTime;
            if (_gecen < Aralik) return;
            _gecen = 0f;

            try
            {
                if (File.Exists(MantikYolu))
                {
                    var zaman = File.GetLastWriteTimeUtc(MantikYolu);
                    if (zaman != _yuklenen) Yukle(zaman);
                }
                _tick?.Invoke(null, null);
                _hataSayisi = 0;
            }
            catch (Exception e)
            {
                if (_hataSayisi++ < 3)
                    Plugin.Gunluk.LogWarning("Mantık hatası: " + (e.InnerException ?? e));
            }
        }

        private void Yukle(DateTime zaman)
        {
            // Önceki sürümün Harmony yamalarını ve kaynaklarını bırakması için.
            try { _durdur?.Invoke(null, null); } catch { }
            _tick = null;
            _durdur = null;

            var tip = Assembly.Load(File.ReadAllBytes(MantikYolu))
                .GetType("FMAsistan.Mantik.Giris", throwOnError: true);
            _tick = tip.GetMethod("Tick", BindingFlags.Static | BindingFlags.Public);
            _durdur = tip.GetMethod("Durdur", BindingFlags.Static | BindingFlags.Public);
            Directory.CreateDirectory(VeriKlasoru);
            tip.GetMethod("Baslat", BindingFlags.Static | BindingFlags.Public)
               ?.Invoke(null, new object[] { (Action<string>)(m => Plugin.Gunluk.LogInfo(m)), VeriKlasoru });
            _yuklenen = zaman;
        }
    }
}
