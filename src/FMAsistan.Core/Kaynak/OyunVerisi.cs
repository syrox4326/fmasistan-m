using System.Text.Json;

namespace FMAsistan.Core.Kaynak;

/// <summary>
/// Eklentinin tüm çıktılarını tek bir görünümde birleştirir:
///   veri\oku\*.json     etkin okumalar (eklenti istedi),
///   veri\gozlem\*.json  zamanlı tam gözlemler,
///   veri\canli.json     sürekli izleme (kullanıcı ekranı her açtığında güncellenir).
/// Her (kaynak, özellik) için en yeni geçerli değer kalır. Dosya adları zaman damgalıdır;
/// canli.json her kaydı kendi zamanıyla taşır ve aynı zamanda en yeni sayılır.
/// </summary>
public static class OyunVerisi
{
    public static Dictionary<string, Dictionary<string, JsonElement>> Yukle(string veriKlasoru)
    {
        var parcalar = new List<(string zaman, string json)>();
        foreach (var alt in new[] { "oku", "gozlem" })
        {
            string k = Path.Combine(veriKlasoru, alt);
            if (!Directory.Exists(k)) continue;
            foreach (var f in Directory.GetFiles(k, "*.json"))
                parcalar.Add((Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)));
        }
        string canli = Path.Combine(veriKlasoru, "canli.json");
        if (File.Exists(canli)) parcalar.Add(("99999999-999999", CanliAc(File.ReadAllText(canli))));

        return EklentiCozucu.Birlestir(parcalar.OrderBy(p => p.zaman, StringComparer.Ordinal).Select(p => p.json));
    }

    /// <summary>canli.json: { kaynak: { özellik: { deger, zaman } } } → { kaynak: { özellik: deger } }</summary>
    internal static string CanliAc(string json)
    {
        using var b = JsonDocument.Parse(json);
        var sonuc = new Dictionary<string, Dictionary<string, JsonElement>>();
        foreach (var kaynak in b.RootElement.EnumerateObject())
        {
            var d = sonuc[kaynak.Name] = new();
            foreach (var oz in kaynak.Value.EnumerateObject())
                if (oz.Value.ValueKind == JsonValueKind.Object && oz.Value.TryGetProperty("deger", out var deger))
                    d[oz.Name] = deger.Clone();
        }
        return JsonSerializer.Serialize(sonuc);
    }

    /// <summary>Okunan kişi adları (PersonReference:kimlik → ad).</summary>
    public static Dictionary<string, string> Adlar(Dictionary<string, Dictionary<string, JsonElement>> veri)
        => veri.Where(k => k.Key.StartsWith("PersonReference:") && k.Value.TryGetValue("Name", out var n) && n.ValueKind == JsonValueKind.String)
               .ToDictionary(k => k.Key, k => k.Value["Name"].GetString()!);
}
