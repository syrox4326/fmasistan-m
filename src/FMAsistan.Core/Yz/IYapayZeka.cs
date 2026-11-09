namespace FMAsistan.Core.Yz;

/// <param name="Metin">Modelin cevabı.</param>
/// <param name="OturumKimligi">Sohbetin devamı için saklanacak kimlik.</param>
public sealed record YzCevap(string Metin, string OturumKimligi, decimal? MaliyetUsd);

/// <summary>
/// Yorum katmanının soyutlaması. Analiz motoru sayısal gerçekleri üretir; yapay zekâ
/// yalnızca bunları yorumlar (bkz. PROJE_PLANI 4.2). Bugün Claude Code CLI ile çalışır;
/// gerekirse Anthropic API uygulaması aynı arayüzle eklenir.
/// </summary>
public interface IYapayZeka
{
    /// <param name="sistem">Kişilik ve kurallar (her çağrıda aynı tutulur; önbelleğe uygun).</param>
    /// <param name="oturumKimligi">null: yeni sohbet; dolu: önceki sohbetin devamı.</param>
    Task<YzCevap> SorAsync(string sistem, string mesaj, string? oturumKimligi, CancellationToken iptal = default);
}

public sealed class YapayZekaHatasi(string mesaj) : Exception(mesaj);
