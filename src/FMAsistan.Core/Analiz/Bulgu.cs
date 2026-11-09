namespace FMAsistan.Core.Analiz;

public enum Onem { Bilgi, Orta, Yuksek, Kritik }

/// <summary>
/// Analiz motorunun tek çıktı birimi. Her bulgu bir kurala (bilgi/TAKTIK_BILGI_TABANI.md
/// içindeki K-xx) ve dayandığı veriye izlenebilir olmalı: uydurma yorum yok.
/// </summary>
/// <param name="Kural">Bilgi tabanındaki kural kodu, ör. "K-01".</param>
/// <param name="Kanit">Bulgunun dayandığı oyun verileri (ör. "Baskı hattı: 44").</param>
/// <param name="Oneri">Somut ve uygulanabilir öneri; yoksa null.</param>
public sealed record Bulgu(
    string Kural,
    Onem Onem,
    string Baslik,
    string Aciklama,
    IReadOnlyList<string> Kanit,
    string? Oneri);
