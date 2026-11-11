// FM Asistanım — arayüz.
// Masaüstünde veri WebView2 köprüsünden gelir (FMAsistan.Core). Tarayıcı önizlemesinde
// dışa aktarılmış ornek-veri.json ve ornek-raporlar.json okunur; sohbet orada kapalıdır.
"use strict";

const kopru = window.chrome && window.chrome.webview ? window.chrome.webview : null;
let veri = null;
let faz = "ip";
let seciliEkip = null;
let kadroSirasi = { alan: "durum", yon: 1 };
const acikSatirlar = new Set();
const sohbetler = {};     // ekip kimliği → [{kim, metin, tur}]
const raporlar = {};      // tür → {tur, baslik, metin, zaman, guncel}
const raporDurumu = {};   // tür → "hazirlaniyor" | "hata"
const raporHatasi = {};

const RAPORLAR = [
  { tur: "Taktik", ekran: "taktik", ad: "Taktik analizi" },
  { tur: "MacOnu", ekran: "mac", ad: "Maç önü raporu" },
  { tur: "Kadro", ekran: "kadro", ad: "Kadro ve oyuncu analizi" },
  { tur: "Antrenman", ekran: "antrenman", ad: "Antrenman planı" },
];

// ---------- yardımcılar ----------
const $ = (s) => document.querySelector(s);
const el = (etiket, sinif, metin) => {
  const e = document.createElement(etiket);
  if (sinif) e.className = sinif;
  if (metin !== undefined && metin !== null) e.textContent = metin;
  return e;
};
const kac = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const virgul = (x, n) => Number(x).toFixed(n).replace(".", ",");
const kisaAd = (ad) => { const p = String(ad || "?").trim().split(/\s+/); return p.length > 1 ? p.slice(1).join(" ") : p[0]; };
const puanMetni = (p) => (!p ? "–" : p.tek ? String(p.min) : `${p.min}–${p.max}`);
const puanOrta = (p) => (!p ? null : (p.min + p.max) / 2);
const puanRenk = (v) => (v == null ? "" : v < 50 ? "c-kotu" : v < 60 ? "c-dikkat" : "c-iyi");
const yildizOrta = (y) => (!y ? null : (y.min + y.max) / 2);
const yildizMetni = (y) => (!y ? "–" : y.min === y.max ? virgul(y.min, 1) : `${virgul(y.min, 1)}–${virgul(y.max, 1)}`);
const yildizRenk = (y) => { const o = yildizOrta(y); return o == null ? "" : o >= 4 ? "iyi" : o >= 3 ? "dikkat" : "kotu"; };
const etiket = (metin, ek, sinif) => { const e = el("span", "etiket" + (sinif ? " " + sinif : "")); e.innerHTML = ek != null ? `${kac(metin)} <b>${kac(ek)}</b>` : kac(metin); return e; };
const satir = (ad, deger) => { const d = el("div", "satir"); d.innerHTML = `<span class="ad">${kac(ad)}</span><span class="deger${deger == null ? " yok" : ""}">${kac(deger ?? "–")}</span>`; return d; };
const bos = (kap, metin) => kap.append(el("div", "bos", metin));

// ---------- veri ----------
function veriIste() {
  if (kopru) { kopru.postMessage({ tur: "durum" }); return; }
  fetch("ornek-veri.json").then((r) => r.json()).then(veriGeldi).catch(() => ($("#veri-durum").textContent = "Önizleme verisi yok."));
  fetch("ornek-raporlar.json").then((r) => r.json()).then((l) => { l.forEach((r) => (raporlar[r.tur] = r)); cizRaporlar(); if (veri) cizGenel(); }).catch(() => {});
}

function veriGeldi(v) {
  veri = v;
  $("#kenar-takim").textContent = v.takim || "";
  [cizGenel, cizTaktik, cizMac, cizKadro, cizAntrenman, cizVeri, cizEkip, cizRaporlar]
    .forEach((f) => { try { f(); } catch (e) { console.error(f.name, e); } });
}

if (kopru) {
  kopru.addEventListener("message", (e) => {
    const m = e.data;
    if (m.tur === "durum") veriGeldi(m.veri);
    else if (m.tur === "cevap") cevapGeldi(m.ekip, m.metin, false);
    else if (m.tur === "hata") cevapGeldi(m.ekip, m.metin, true);
    else if (m.tur === "bilgi") $("#veri-durum").textContent = m.metin;
    else if (m.tur === "canli") canliGeldi(m);
    else if (m.tur === "rapor") { raporlar[m.rapor.tur] = m.rapor; delete raporDurumu[m.rapor.tur]; cizRaporlar(); if (veri) cizGenel(); }
    else if (m.tur === "raporDurum") { raporDurumu[m.rapor] = m.durum; raporHatasi[m.rapor] = m.metin; cizRaporlar(); if (veri) cizGenel(); }
  });
}

// ---------- gezinme ----------
function ekranAc(ad) {
  document.querySelectorAll("#menu button").forEach((x) => x.classList.toggle("secili", x.dataset.ekran === ad));
  document.querySelectorAll(".ekran").forEach((x) => x.classList.toggle("secili", x.id === "ekran-" + ad));
  $(".icerik").scrollTop = 0;
}
$("#menu").addEventListener("click", (e) => { const b = e.target.closest("button[data-ekran]"); if (b) ekranAc(b.dataset.ekran); });
const fazSekmesi = (id) => $(id).addEventListener("click", (e) => {
  const b = e.target.closest("button[data-faz]");
  if (!b) return;
  faz = b.dataset.faz;
  document.querySelectorAll("#faz-sekme button, #slot-sekme button").forEach((x) => x.classList.toggle("secili", x.dataset.faz === faz));
  cizBizimSaha();
  cizSlotlar();
});
fazSekmesi("#faz-sekme");
fazSekmesi("#slot-sekme");
$("#yenile").addEventListener("click", () => {
  if (!kopru) { $("#veri-durum").textContent = "Güncelleme masaüstü uygulamasında çalışır."; return; }
  $("#veri-durum").textContent = "Oyundan tam tarama istendi…";
  kopru.postMessage({ tur: "guncelle" });
});

function canliGeldi(m) {
  const isaret = (id, s) => ($(id).className = s);
  isaret("#canli-oyun", m.oyun ? (m.eklenti ? "acik" : "bekle") : "");
  $("#canli-oyun-metin").textContent = m.oyun ? (m.eklenti ? "Açık" : "Eklenti bekleniyor") : "Kapalı";
  isaret("#canli-tarama", m.tarama ? "acik" : "");
  $("#canli-tarama-metin").textContent = m.tarama || "Yok";
  isaret("#canli-bellek", m.bellekCalisiyor ? "bekle" : m.bellek ? "acik" : "");
  $("#canli-bellek-metin").textContent = m.bellekCalisiyor ? "Okunuyor" : m.bellek || "Yok";
}

// ---------- genel bakış ----------
function hukumMetni(md) {
  if (!md) return null;
  const parcalar = md.split(/^##\s+/m).filter((x) => x.trim());
  if (!parcalar.length) return null;
  return parcalar[0].split("\n").slice(1).join("\n").trim() || null;
}

function cizGenel() {
  const t = veri.taktik || {};
  $("#genel-baslik").textContent = veri.takim || "—";
  $("#genel-ust").textContent = veri.oyunTarihi
    ? `Genel bakış · ${new Date(veri.oyunTarihi).toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" })}`
    : "Genel bakış";
  const et = $("#genel-etiketler");
  et.replaceChildren();
  if (t.ad) et.append(etiket("Taktik", t.ad));

  const p = veri.profiller || [];
  const ilk = p.filter((o) => o.durum === "İlk 11");
  const ort = (a) => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : null);
  const kond = ort(ilk.filter((o) => o.kondisyon != null).map((o) => o.kondisyon));
  const riskli = ilk.filter((o) => (o.kondisyon != null && o.kondisyon < 88) || (o.risk != null && o.risk >= 200));
  const verim = puanOrta((veri.verimlilik || {}).genel);
  const g = $("#gostergeler");
  g.replaceChildren();
  const gosterge = (ad, sayi, alt, renk) => {
    const d = el("div", "gosterge");
    d.innerHTML = `<div class="ad">${kac(ad)}</div><div class="sayi ${renk || ""}">${kac(sayi ?? "–")}</div><div class="alt">${kac(alt || "")}</div>`;
    g.append(d);
  };
  const sira = veri.fikstur && veri.fikstur.siradakiler && veri.fikstur.siradakiler[0];
  gosterge("Taktiksel verimlilik", verim != null ? puanMetni(veri.verimlilik.genel) : null, "Oyunun 0–100 değerlendirmesi", puanRenk(verim));
  gosterge("Lig sırası", veri.fikstur && veri.fikstur.lig, sira ? `Sıradaki: ${sira.rakip}` : "");
  gosterge("İlk 11 kondisyonu", kond != null ? `%${Math.round(kond)}` : null, "Ortalama", kond == null ? "" : kond < 88 ? "c-kotu" : kond < 94 ? "c-dikkat" : "c-iyi");
  gosterge("Riskli oyuncu", ilk.length ? String(riskli.length) : null,
    riskli.length ? riskli.map((o) => kisaAd(o.ad)).slice(0, 4).join(", ") : "İlk 11'de risk yok", riskli.length ? "c-dikkat" : "c-iyi");

  const h = $("#hukumler");
  h.replaceChildren();
  RAPORLAR.forEach((r) => {
    const rapor = raporlar[r.tur];
    const yaziliyor = raporDurumu[r.tur] === "hazirlaniyor";
    const k = el("div", "kart hukum");
    const metin = hukumMetni(rapor && rapor.metin);
    k.innerHTML = `<div class="kart-ust"><h3>${kac(r.ad)}</h3>${yaziliyor ? `<span class="etiket bilgi">Yazılıyor</span>` : rapor ? `<span class="alt">${kac(rapor.zaman)}</span>` : ""}</div>
      <div class="kart-ic">${metin ? markdown(metin) : `<span class="soluk">${yaziliyor ? "Analiz ekibi raporu yazıyor." : "Rapor henüz yok."}</span>`}</div>
      ${metin ? `<div class="devam">Raporun tamamı →</div>` : ""}`;
    k.addEventListener("click", () => ekranAc(r.ekran));
    h.append(k);
  });

  const r = veri.rakip;
  const mac = $("#genel-mac");
  mac.replaceChildren();
  $("#genel-mac-ne").textContent = sira ? sira.ne : "";
  if (r || sira) {
    mac.append(el("div", "rakip", (r && r.ad) || sira.rakip));
    if (r) {
      if (r.dizilis) mac.append(satir("Muhtemel diziliş", r.dizilis));
      if (r.oyunSistemi) mac.append(satir("Oyun sistemi", r.oyunSistemi));
      if (r.tehdit) mac.append(el("p", "not-metni", r.tehdit));
    }
  } else bos(mac, "Fikstür oyundan taranınca görünür.");

  const o = $("#oncelikler");
  o.replaceChildren();
  const onem = { Kritik: 0, Yuksek: 1, Orta: 2, Bilgi: 3 };
  [...(veri.bulgular || [])].sort((a, b) => onem[a.onem] - onem[b.onem]).slice(0, 8).forEach((b) => {
    const d = el("div", "oncelik");
    d.dataset.onem = b.onem;
    d.innerHTML = `<div><div class="baslik">${kac(b.baslik)}</div><div class="alt">${kac((b.kanit || []).slice(0, 2).join(" · "))}</div></div>`;
    o.append(d);
  });
  if (!o.children.length) bos(o, "Bulgu yok.");
}

// ---------- rapor okuyucu ----------
const BEKLE = "Analiz ekibi raporu yazıyor. Verinin büyüklüğüne göre birkaç dakika sürer.";

function cizRaporlar() {
  document.querySelectorAll("article.rapor").forEach((kap) => raporCiz(kap, kap.dataset.rapor));
  document.querySelectorAll("#menu .nokta").forEach((i) => {
    const t = i.dataset.rapor;
    i.className = "nokta " + (raporDurumu[t] === "hazirlaniyor" ? "bekle" : raporlar[t] ? (raporlar[t].guncel ? "acik" : "eski") : "");
  });
}

function raporCiz(kap, tur) {
  const r = raporlar[tur];
  const durum = raporDurumu[tur];
  const tanim = RAPORLAR.find((x) => x.tur === tur);

  const ust = el("div", "rapor-ust");
  const sol = el("div");
  sol.innerHTML = `<div class="baslik">${kac((r && r.baslik) || tanim.ad)}</div><div class="alt">Analiz departmanı${r ? " · " + kac(r.zaman) : ""}</div>`;
  const sag = el("div", "sag");
  if (durum === "hazirlaniyor") sag.append(etiket(r ? "Yeni veriyle yeniden yazılıyor" : "Yazılıyor", null, "bilgi"));
  else if (r && !r.guncel) sag.append(etiket("Veri değişti", null, "dikkat"));
  else if (r) sag.append(etiket("Güncel", null, "iyi"));
  if (kopru && durum !== "hazirlaniyor") {
    const b = el("button", "dugme", r ? "Yeniden yaz" : "Raporu yaz");
    b.addEventListener("click", () => { raporDurumu[tur] = "hazirlaniyor"; cizRaporlar(); kopru.postMessage({ tur: "rapor-yenile", rapor: tur }); });
    sag.append(b);
  }
  ust.append(sol, sag);

  const ic = el("div", "rapor-ic");
  const govde = el("div");
  if (r) {
    const metin = el("div", "metin");
    metin.innerHTML = markdown(r.metin);
    const bolumler = [...metin.querySelectorAll("h2")];
    if (bolumler.length >= 3) {
      const nav = el("nav", "rapor-icindekiler");
      bolumler.forEach((h, i) => {
        const ad = h.textContent.replace(/\s*\(.*\)\s*$/, "");
        h.innerHTML = `<span class="no">${String(i + 1).padStart(2, "0")}</span>${kac(ad)}`;
        h.id = `${tur}-b${i}`;
        const a = el("a", null, ad);
        a.href = "#" + h.id;
        a.addEventListener("click", (e) => { e.preventDefault(); h.scrollIntoView({ behavior: "smooth", block: "start" }); });
        nav.append(a);
      });
      ic.append(nav);
      bolumIzle(nav, bolumler);
    } else ic.classList.add("tek");
    if (durum === "hazirlaniyor") govde.append(el("div", "rapor-durum ince", BEKLE));
    if (durum === "hata") govde.append(el("div", "hata-kutu", "Yeni rapor yazılamadı, son rapor gösteriliyor: " + (raporHatasi[tur] || "")));
    govde.append(metin);
  } else {
    ic.classList.add("tek");
    if (durum === "hazirlaniyor") {
      govde.append(el("div", "rapor-durum", BEKLE));
      ["", "k", "", "", "k"].forEach((s) => govde.append(el("div", "iskelet " + s)));
    } else if (durum === "hata") govde.append(el("div", "hata-kutu", "Rapor yazılamadı: " + (raporHatasi[tur] || "bilinmeyen hata")));
    else govde.append(el("div", "rapor-durum", kopru ? "Oyun verisi gelince analiz ekibi bu raporu kendiliğinden yazar." : "Raporlar masaüstü uygulamasında yazılır."));
  }
  ic.append(govde);
  kap.replaceChildren(ust, ic);
}

// İçindekilerde okunan bölümü işaretle.
function bolumIzle(nav, bolumler) {
  const linkler = [...nav.querySelectorAll("a")];
  const gozlem = new IntersectionObserver((girdiler) => {
    girdiler.forEach((g) => {
      if (!g.isIntersecting) return;
      const i = bolumler.indexOf(g.target);
      linkler.forEach((a, j) => a.classList.toggle("aktif", j === i));
    });
  }, { root: $(".icerik"), rootMargin: "-80px 0px -70% 0px" });
  bolumler.forEach((h) => gozlem.observe(h));
}

// ---------- saha ----------
function sahaCizgileri() {
  return `<svg class="cizgiler" viewBox="0 0 68 100" preserveAspectRatio="none" aria-hidden="true">
    <g fill="none" stroke="var(--saha-cizgi)" stroke-width="0.3">
      <rect x="2" y="2" width="64" height="96"/><line x1="2" y1="50" x2="66" y2="50"/><circle cx="34" cy="50" r="8"/>
      <rect x="14.5" y="2" width="39" height="15"/><rect x="25" y="2" width="18" height="5.5"/>
      <rect x="14.5" y="83" width="39" height="15"/><rect x="25" y="92.5" width="18" height="5.5"/>
    </g></svg>`;
}

function oyuncuCiz(saha, o, rakip) {
  const d = el("div", "oyuncu" + (rakip ? " rakip" : ""));
  d.style.left = o.k.x + "%";
  d.style.bottom = o.k.y + "%";
  d.title = `${o.ad} · ${o.poz}${o.rol ? " · " + o.rol : ""}${o.yildiz ? " · rol uyumu " + o.yildiz + " yıldız" : ""}`;
  d.append(el("div", "rozet " + (o.renk || ""), o.kisa || "?"), el("div", "isim", kisaAd(o.ad)),
    el("div", "rol", o.yildiz ? `${o.rol || ""} · ${o.yildiz}★` : o.rol || ""));
  saha.append(d);
}

function cizBizimSaha() {
  const saha = $("#saha-bizim");
  saha.innerHTML = sahaCizgileri();
  (veri.ilkOnbir || []).forEach((p) => {
    const f = p[faz];
    const sl = (veri.slotlar || []).find((s) => s.oyuncu === p.ad && s.ip === (faz === "ip"));
    oyuncuCiz(saha, { ad: p.ad, poz: f.poz, rol: f.rol, kisa: f.kisa, k: f.k,
      renk: sl ? yildizRenk(sl.mevcut) : "", yildiz: sl && sl.mevcut ? yildizMetni(sl.mevcut) : null }, false);
  });
}

// ---------- taktik ----------
function cizTaktik() {
  const t = veri.taktik || {};
  $("#taktik-ad").textContent = t.ad || "Taktik";
  const et = $("#taktik-etiketler");
  et.replaceChildren();
  [["Mentalite", t.mentalite], ["Stil", t.stil], ["En çok kullanılan", t.dizilis]].forEach(([a, d]) => d && et.append(etiket(a, d)));

  const v = veri.verimlilik || {};
  const genel = puanOrta(v.genel) ?? 0;
  const cevre = 2 * Math.PI * 42;
  const renk = genel < 50 ? "var(--kotu)" : genel < 60 ? "var(--dikkat)" : "var(--iyi)";
  $("#verim-halka").innerHTML = `<svg viewBox="0 0 100 100"><circle cx="50" cy="50" r="42" fill="none" stroke="var(--yuzey-3)" stroke-width="7"/>
      <circle cx="50" cy="50" r="42" fill="none" stroke="${renk}" stroke-width="7" stroke-linecap="round" stroke-dasharray="${(genel / 100) * cevre} ${cevre}"/></svg>
    <div class="ic"><div><div class="sayi">${kac(puanMetni(v.genel))}</div><div class="ad">Genel</div></div></div>`;
  const bil = $("#verim-bilesenler");
  bil.replaceChildren();
  (v.bilesenler || []).forEach((b) => {
    const o = puanOrta(b.puan);
    const d = el("div", "cubuk-satir");
    d.title = [b.detay, b.tavsiye && `Oyun: “${b.tavsiye}”`].filter(Boolean).join("\n");
    d.innerHTML = `<span class="ad">${kac(b.ad)}</span><span class="sayi ${puanRenk(o)}">${kac(puanMetni(b.puan))}</span><div class="cubuk"><i class="${puanRenk(o)}" style="width:${o ?? 0}%"></i></div>`;
    bil.append(d);
  });

  const alanlar = $("#alanlar");
  alanlar.replaceChildren();
  [...(v.alanlar || [])].sort((a, b) => puanOrta(a.puan) - puanOrta(b.puan)).forEach((a) => {
    const o = puanOrta(a.puan);
    const d = el("div", "cubuk-satir");
    d.innerHTML = `<span class="ad">${kac(a.ad)}</span><span class="sayi ${puanRenk(o)}">${kac(puanMetni(a.puan))}</span><div class="cubuk"><i class="${puanRenk(o)}" style="width:${o}%"></i></div>`;
    alanlar.append(d);
  });
  $("#alan-tavsiyesi").textContent = v.alanTavsiyesi ? `Oyunun tavsiyesi: “${v.alanTavsiyesi}”` : "";

  cizBizimSaha();

  const bul = $("#bulgular");
  const acik = new Set([...bul.querySelectorAll(".bulgu.acik")].map((x) => x.dataset.anahtar));
  bul.replaceChildren();
  const onem = { Kritik: 0, Yuksek: 1, Orta: 2, Bilgi: 3 };
  const liste = [...(veri.bulgular || [])].sort((a, b) => onem[a.onem] - onem[b.onem]);
  $("#bulgu-sayisi").textContent = liste.length ? `${liste.length} bulgu` : "";
  liste.forEach((b) => {
    const d = el("div", "bulgu");
    d.dataset.onem = b.onem;
    d.dataset.anahtar = b.kural + b.baslik;
    if (acik.has(d.dataset.anahtar)) d.classList.add("acik");
    d.innerHTML = `<div class="bulgu-ust"><span class="bulgu-baslik">${kac(b.baslik)}</span><span class="bulgu-kod">${kac(b.kural)}</span></div>
      <div class="bulgu-govde">${kac(b.aciklama)}<ul>${(b.kanit || []).map((k) => `<li>${kac(k)}</li>`).join("")}</ul>${b.oneri ? `<div class="oneri">${kac(b.oneri)}</div>` : ""}</div>`;
    d.addEventListener("click", () => d.classList.toggle("acik"));
    bul.append(d);
  });
  if (!liste.length) bos(bul, "Motor bu veride bir sorun bulmadı.");

  const ay = $("#ayarlar");
  ay.replaceChildren();
  let grup = null;
  (veri.ayarlar || []).forEach((a) => {
    const [g, ad] = a.ad.includes(" / ") ? a.ad.split(" / ") : ["Genel", a.ad];
    if (g !== grup) { ay.append(el("div", "grup-baslik", g)); grup = g; }
    ay.append(satir(ad, a.deger.startsWith("Talimat yok") ? null : a.deger));
  });
  if (veri.ayarlar && veri.ayarlar.length) ay.append(el("p", "not-metni", "Talimatların Türkçe adları yaklaşık çeviridir."));
}

// ---------- maç hazırlığı ----------
function notGruplari(kap, gruplar) {
  kap.replaceChildren();
  (gruplar || []).forEach((g) => {
    const det = el("details", "not-grup");
    det.innerHTML = `<summary><span>${kac(g.bolum)}</span><span class="sayac">${g.notlar.length}</span></summary>`;
    g.notlar.forEach((n) => { const p = el("div", "not"); p.innerHTML = (n.baslik ? `<b>${kac(n.baslik)}</b>` : "") + kac(n.metin); det.append(p); });
    kap.append(det);
  });
  if (!kap.children.length) bos(kap, "Kayıtlı not yok.");
}

function cizMac() {
  const f = veri.fikstur || {};
  $("#lig-sirasi").textContent = f.lig ? `Lig sırası ${f.lig}` : "";
  const fk = $("#fikstur");
  fk.replaceChildren();
  (f.siradakiler || []).forEach((m) => fk.append(satir(m.ne, m.rakip)));
  if (!fk.children.length) bos(fk, "Fikstür oyundan taranınca görünür.");

  const r = veri.rakip;
  const et = $("#rakip-etiketler");
  et.replaceChildren();
  if (!r) { $("#rakip-ad").textContent = (f.siradakiler && f.siradakiler[0] && f.siradakiler[0].rakip) || "Rakip raporu yok"; return; }
  $("#rakip-ad").textContent = r.ad || "Rakip";
  if (r.dizilis) et.append(etiket("Muhtemel", r.dizilis));
  if (r.oyunSistemi) et.append(etiket("Oyun sistemi", r.oyunSistemi));
  const uy = $("#rakip-analist");
  uy.hidden = !r.analist;
  uy.textContent = r.analist ? `Raporun güvenilirliği: ${r.analist}` : "";

  $("#rakip-dizilis").textContent = r.dizilis || "";
  const saha = $("#saha-rakip");
  saha.innerHTML = sahaCizgileri();
  (r.muhtemelOnbir || []).forEach((o) => oyuncuCiz(saha, o, true));
  $("#rakip-ozet").textContent = r.ozet || r.tehdit || "";

  const dz = $("#rakip-dizilisler");
  dz.replaceChildren();
  (r.dizilisler || []).forEach((x) => {
    const d = el("div", "cubuk-satir");
    d.innerHTML = `<span class="ad">${kac(x.dizilis)} <span class="soluk">· ${kac(x.faz)}</span></span><span class="sayi">%${kac(x.yuzde)}</span><div class="cubuk"><i style="width:${x.yuzde}%"></i></div>`;
    dz.append(d);
  });
  if (!dz.children.length) bos(dz, "Diziliş verisi yok.");
  const madde = (kap, liste) => {
    kap.replaceChildren();
    (liste || []).forEach((m) => { const li = el("li"); li.innerHTML = `<b>${kac(m.baslik)}</b>${kac(m.metin)}`; kap.append(li); });
    if (!kap.children.length) bos(kap, "Yok.");
  };
  madde($("#rakip-guclu"), r.guclu);
  madde($("#rakip-zayif"), r.zayif);
  notGruplari($("#rakip-notlar"), r.notlar);
}

// ---------- kadro ----------
function cizSlotlar() {
  const st = $("#slot-tablosu");
  st.replaceChildren();
  const slotlar = (veri.slotlar || []).filter((s) => s.ip === (faz === "ip"));
  if (!slotlar.length || slotlar.every((s) => !s.mevcut)) { bos(st, "Rol uygunluğu oyunun otomatik taramasıyla gelir."); return; }
  slotlar.forEach((s) => {
    const d = el("div", "slot");
    const alt = (s.alternatifler || [])[0];
    d.innerHTML = `<div class="slot-ust"><span>${kac(s.slot.replace(/ \(.*\)$/, ""))}</span><span>${kac(s.rol)}</span></div>
      <div class="slot-satir"><span>${kac(s.oyuncu)}</span><b class="c-${yildizRenk(s.mevcut)}">${yildizMetni(s.mevcut)}★</b></div>
      ${alt ? `<div class="slot-satir alt"><span class="ad">${kac(alt.ad)}</span><b class="c-${yildizRenk(alt.yildiz)}">${yildizMetni(alt.yildiz)}★</b></div>` : ""}
      ${s.enIyiRol && s.enIyiRol !== s.rol ? `<div class="slot-not">En iyi rolü: ${kac(s.enIyiRol)} (${yildizMetni(s.enIyi)}★)</div>` : ""}`;
    st.append(d);
  });
}

function cizKadro() {
  const p = veri.profiller || [];
  $("#kadro-baslik").textContent = p.length ? `${p.length} oyuncu` : "Kadro";
  const et = $("#kadro-etiketler");
  et.replaceChildren();
  const say = (d) => p.filter((o) => o.durum === d).length;
  if (p.length) [["İlk 11", "İlk 11"], ["Yedek", "Yedek"], ["Kadro dışı", "Kadro dışı"]].forEach(([a, d]) => et.append(etiket(a, say(d))));
  cizSlotlar();

  const sutunlar = [
    ["ad", "Oyuncu"], ["yas", "Yaş", 1], ["mevkiler", "Mevki"], ["kondisyon", "Kondisyon", 1], ["keskinlik", "Keskinlik", 1],
    ["risk", "Sakatlık riski", 1], ["ortalama", "Ortalama", 1], ["mac", "Maç", 1], ["gol", "Gol", 1], ["asist", "Asist", 1],
    ["antrenman", "Antrenman", 1], ["kisilik", "Kişilik"], ["maas", "Maaş"], ["sozlesme", "Sözleşme"],
  ];
  const durumSira = { "İlk 11": 0, "Yedek": 1, "Kadro dışı": 2 };
  const deger = (o, a) => (a === "durum" ? durumSira[o.durum] ?? 3 : o[a]);
  const sirali = [...p].sort((x, y) => {
    const a = deger(x, kadroSirasi.alan), b = deger(y, kadroSirasi.alan);
    if (a == null && b == null) return 0; if (a == null) return 1; if (b == null) return -1;
    return (typeof a === "string" ? a.localeCompare(b, "tr") : a - b) * kadroSirasi.yon;
  });
  const t = $("#kadro-tablosu");
  if (!p.length) { t.innerHTML = `<tbody><tr><td class="bos">Kadro verisi oyunun otomatik taramasıyla gelir.</td></tr></tbody>`; return; }
  t.innerHTML = `<thead><tr>${sutunlar.map(([a, b, s]) => `<th data-alan="${a}" class="${s ? "sag " : ""}${kadroSirasi.alan === a ? (kadroSirasi.yon > 0 ? "artan" : "azalan") : ""}">${b}</th>`).join("")}</tr></thead>`;
  const hucre = (o, a, sag) => {
    const v = o[a];
    const c = sag ? "sag " : "";
    if (a === "ad") return `<td class="ad-hucre"><span class="durum ${o.durum === "İlk 11" ? "ilk" : o.durum === "Yedek" ? "yedek" : ""}" title="${kac(o.durum || "")}"></span>${kac(v)}</td>`;
    if (v == null || v === "") return `<td class="${c}soluk">–</td>`;
    if (a === "kondisyon") return `<td class="${c}${v < 85 ? "c-kotu" : v < 93 ? "c-dikkat" : ""}">%${v}</td>`;
    if (a === "keskinlik") return `<td class="${c}${v < 70 ? "c-kotu" : v < 85 ? "c-dikkat" : ""}">%${v}</td>`;
    if (a === "risk") return `<td class="${c}${v >= 200 ? "c-kotu" : v >= 150 ? "c-dikkat" : ""}">${v}</td>`;
    if (a === "ortalama") return `<td class="${c}">${v > 0 ? virgul(v, 2) : "–"}</td>`;
    if (a === "antrenman") return `<td class="${c}">${v > 0 ? virgul(v, 1) : "–"}</td>`;
    if (a === "sozlesme" && /^\d{4}-\d{2}-\d{2}$/.test(v)) return `<td class="${c}">${v.split("-").reverse().join(".")}</td>`;
    return `<td class="${c}">${kac(v)}</td>`;
  };
  const govde = el("tbody");
  sirali.forEach((o) => {
    const tr = el("tr", "ac");
    tr.innerHTML = sutunlar.map(([a, , s]) => hucre(o, a, s)).join("");
    const detay = el("tr", "detay");
    detay.hidden = !acikSatirlar.has(o.kimlik);
    tr.classList.toggle("acik", !detay.hidden);
    const oz = Object.entries(o.oznitelikler || {});
    const ek = [
      o.egilimler && o.egilimler.length && `Eğilimler: ${o.egilimler.join(" · ")}`,
      o.durumlar && o.durumlar.length && `Durum: ${o.durumlar.join(" · ")}`,
      o.mutluluk && `Antrenman mutluluğu: ${o.mutluluk}${o.antrenmanYogunlugu ? " · bireysel yoğunluk: " + o.antrenmanYogunlugu : ""}`,
      o.deger && `Değer aralığı: ${o.deger}`,
    ].filter(Boolean);
    detay.innerHTML = `<td colspan="${sutunlar.length}"><div class="oz-izgara">${oz.map(([k, v]) => `<span><i>${kac(k)}</i><b class="${v >= 15 ? "c-iyi" : v <= 8 ? "c-kotu" : ""}">${v}</b></span>`).join("") || "<span class='soluk'>Öznitelikler bellek okumasıyla gelir.</span>"}</div>${ek.map((x) => `<div class="detay-not">${kac(x)}</div>`).join("")}</td>`;
    tr.addEventListener("click", () => {
      detay.hidden = !detay.hidden;
      tr.classList.toggle("acik", !detay.hidden);
      if (detay.hidden) acikSatirlar.delete(o.kimlik); else acikSatirlar.add(o.kimlik);
    });
    govde.append(tr, detay);
  });
  t.append(govde);
  t.querySelectorAll("th").forEach((th) => th.addEventListener("click", () => {
    const a = th.dataset.alan;
    const metinsel = ["ad", "mevkiler", "kisilik", "sozlesme", "maas"].includes(a);
    kadroSirasi = { alan: a, yon: kadroSirasi.alan === a ? -kadroSirasi.yon : metinsel ? 1 : -1 };
    cizKadro();
  }));
}

// ---------- antrenman ----------
function cizAntrenman() {
  const a = veri.antrenman || {};
  const kap = $("#atamalar");
  kap.replaceChildren();
  (a.atamalar || []).forEach((x) => {
    const y = x.yildiz ? virgul((x.yildiz.min + x.yildiz.max) / 8, 1) + "★" : "";
    kap.append(satir(x.kategori, `${x.antrenor || "atanmamış"}${y ? " · " + y : ""}`));
  });
  if (!kap.children.length) bos(kap, "Antrenör atamaları oyunun otomatik taramasıyla gelir.");

  const ap = $("#antrenman-puanlari");
  ap.replaceChildren();
  const puanli = [...(veri.profiller || [])].filter((o) => o.antrenman > 0).sort((x, y) => y.antrenman - x.antrenman);
  puanli.forEach((o) => {
    const c = o.antrenman >= 7.5 ? "c-iyi" : o.antrenman >= 6.8 ? "c-dikkat" : "c-kotu";
    const d = el("div", "cubuk-satir");
    d.innerHTML = `<span class="ad">${kac(o.ad)}</span><span class="sayi ${c}">${virgul(o.antrenman, 1)}</span><div class="cubuk"><i class="${c}" style="width:${o.antrenman * 10}%"></i></div>`;
    ap.append(d);
  });
  if (!puanli.length) bos(ap, "Antrenman puanları oyunun otomatik taramasıyla gelir.");
  const et = $("#antrenman-etiketler");
  et.replaceChildren();
  if (puanli.length) et.append(etiket("Ortalama antrenman puanı", virgul(puanli.reduce((x, o) => x + o.antrenman, 0) / puanli.length, 2)));

  const k = veri.kulup;
  const kk = $("#kulup");
  kk.replaceChildren();
  if (k) [["Transfer bütçesi", k.transfer], ["Kalan maaş bütçesi", k.kalanMaas], ["Maaş harcaması", k.maasHarcamasi], ["Antrenman tesisi", k.tesis]]
    .forEach(([ad, d]) => d && kk.append(satir(ad, d)));
  if (k && k.beklenti) kk.append(el("p", "not-metni", k.beklenti));
  if (!kk.children.length) bos(kk, "Kulüp verisi oyunun otomatik taramasıyla gelir.");
}

// ---------- veri merkezi ----------
function cizVeri() {
  const s = veri.sonMac;
  const sm = $("#son-mac");
  sm.innerHTML = `<div class="kart-ust"><h3>Son maç</h3></div>`;
  const ic = el("div", "kart-ic");
  if (s) {
    ic.innerHTML = `<div style="font-weight:500">${kac(s.mac || "")}</div>
      <div class="istatistikler">
        <div class="istatistik"><div class="sayi">${s.xg != null ? virgul(s.xg, 2) : "–"}</div><div class="ad">xG</div></div>
        <div class="istatistik"><div class="sayi">${s.sut ?? "–"}</div><div class="ad">Şut</div></div>
        <div class="istatistik"><div class="sayi">${s.isabetli ?? "–"}</div><div class="ad">İsabetli</div></div>
      </div>${s.degerlendirme ? `<p class="not-metni">${kac(s.degerlendirme)}</p>` : ""}`;
  } else bos(ic, "Veri yok.");
  sm.append(ic);

  const iz = $("#izgara-saha");
  iz.replaceChildren();
  const bolgeler = [...(veri.izgara || [])].sort((a, b) => b.satir - a.satir || a.sutun - b.sutun);
  bolgeler.forEach((z) => {
    const sinif = /Sıkıntısız/i.test(z.durum) ? "" : /Güçlü/i.test(z.durum) ? "guclu" : /İyi/.test(z.durum) ? "iyi" : /Hafif/i.test(z.durum) ? "hafif" : /Sıkıntılı/i.test(z.durum) ? "sikinti" : "";
    const b = el("div", "bolge " + sinif);
    b.addEventListener("mouseenter", () => {
      iz.querySelectorAll(".bolge").forEach((x) => x.classList.remove("secili"));
      b.classList.add("secili");
      $("#izgara-detay").innerHTML = `<b>${kac(z.ad)}</b> · ${kac(z.durum)}<br>${(z.yorumlar || []).map(kac).join("<br>") || "Yorum yok."}`;
    });
    iz.append(b);
  });
  if (!bolgeler.length) $("#izgara-detay").textContent = "Veri yok.";

  const pas = $("#pas-listesi");
  pas.replaceChildren();
  [...(veri.sonMacPas || [])].sort((a, b) => b.isabet - a.isabet).forEach((o) => {
    const c = o.isabet < 75 ? "c-kotu" : o.isabet < 85 ? "c-dikkat" : "c-iyi";
    const d = el("div", "cubuk-satir");
    d.innerHTML = `<span class="ad">${kac(o.ad)}</span><span class="sayi ${c}">%${o.isabet}</span><div class="cubuk"><i class="${c}" style="width:${o.isabet}%"></i></div>`;
    pas.append(d);
  });
  if (!pas.children.length) bos(pas, "Veri yok.");
  notGruplari($("#veri-notlar"), veri.veriMerkezi);
}

// ---------- teknik ekip ----------
function cizEkip() {
  const liste = $("#ekip-liste");
  const ekip = veri.ekip || [];
  liste.innerHTML = `<div class="kart-ust"><h3>Ekip</h3><span class="alt">${ekip.length} kişi</span></div>`;
  if (!ekip.length) liste.append(el("div", "kart-ic bos", "Teknik ekip oyundan taranınca görünür."));
  ekip.forEach((u) => {
    const k = el("div", "ekip-kart");
    k.dataset.kimlik = u.kimlik;
    k.innerHTML = `<div class="ad" lang="en">${kac(u.ad)}</div><div class="gorev">${kac(u.gorev)}${u.felsefe && u.felsefe.dizilis ? " · " + kac(u.felsefe.dizilis) : ""}</div>`;
    k.addEventListener("click", () => ekipSec(u));
    liste.append(k);
  });
  const onceki = seciliEkip && ekip.find((u) => u.kimlik === seciliEkip.kimlik);
  if (onceki || ekip.length) ekipSec(onceki || ekip[0]);
}

function ekipSec(u) {
  seciliEkip = u;
  document.querySelectorAll(".ekip-kart").forEach((k) => k.classList.toggle("secili", k.dataset.kimlik === u.kimlik));
  $("#sohbet-ust").innerHTML = `<span lang="en">${kac(u.ad)}</span>`;
  const f = u.felsefe || {};
  $("#sohbet-alt").textContent = [u.gorev, u.lisans, f.mentalite && `${f.mentalite}, pres ${f.pres || "?"}`].filter(Boolean).join(" · ");
  sohbetCiz();
  const kapali = !kopru;
  $("#sohbet-metin").disabled = kapali;
  $("#sohbet-gonder").disabled = kapali;
  if (kapali) $("#sohbet-metin").placeholder = "Sohbet masaüstü uygulamasında çalışır.";
}

function sohbetCiz() {
  const akis = $("#sohbet-akis");
  akis.replaceChildren();
  const liste = sohbetler[seciliEkip.kimlik] || [];
  if (!liste.length) bos(akis, `${seciliEkip.ad} ile taktik, rakip, kadro ya da antrenman hakkında konuşabilirsin. Kendi felsefesi var; katılmadığında gerekçesiyle söyler.`);
  liste.forEach((m) => {
    const d = el("div", `mesaj ${m.kim}${m.tur ? " " + m.tur : ""}`);
    if (m.kim === "o" && !m.tur) d.innerHTML = markdown(m.metin); else d.textContent = m.metin;
    akis.append(d);
  });
  akis.scrollTop = akis.scrollHeight;
}

$("#sohbet-form").addEventListener("submit", (e) => {
  e.preventDefault();
  const metin = $("#sohbet-metin").value.trim();
  if (!metin || !seciliEkip || !kopru) return;
  const liste = (sohbetler[seciliEkip.kimlik] ||= []);
  liste.push({ kim: "ben", metin }, { kim: "o", metin: `${seciliEkip.ad} yazıyor…`, tur: "bekliyor" });
  $("#sohbet-metin").value = "";
  $("#sohbet-gonder").disabled = true;
  sohbetCiz();
  kopru.postMessage({ tur: "sor", ekip: seciliEkip.kimlik, mesaj: metin });
});
$("#sohbet-metin").addEventListener("keydown", (e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); $("#sohbet-form").requestSubmit(); } });

function cevapGeldi(ekip, metin, hata) {
  const liste = (sohbetler[ekip] ||= []);
  const i = liste.findIndex((m) => m.tur === "bekliyor");
  if (i >= 0) liste.splice(i, 1);
  liste.push({ kim: "o", metin, tur: hata ? "hata" : undefined });
  $("#sohbet-gonder").disabled = false;
  if (seciliEkip && seciliEkip.kimlik === ekip) sohbetCiz();
}

// ---------- Markdown (güvenli: önce kaçış) ----------
function markdown(md) {
  const satirlar = kac(md).split(/\r?\n/);
  const cikti = [];
  let liste = null, tablo = null;
  const satirIci = (s) => s.replace(/`([^`]+)`/g, "<code>$1</code>").replace(/\*\*(.+?)\*\*/g, "<b>$1</b>").replace(/(^|[^*])\*(?!\s)(.+?)\*/g, "$1<i>$2</i>");
  const listeKapat = () => { if (liste) { cikti.push(`</${liste}>`); liste = null; } };
  const tabloKapat = () => {
    if (!tablo) return;
    const [bas, ...govde] = tablo.satirlar;
    const h = tablo.baslikli ? `<thead><tr>${bas.map((x) => `<th>${x}</th>`).join("")}</tr></thead>` : "";
    const g = (tablo.baslikli ? govde : tablo.satirlar).map((r) => `<tr>${r.map((x) => `<td>${x}</td>`).join("")}</tr>`).join("");
    cikti.push(`<div class="tablo-kap"><table>${h}<tbody>${g}</tbody></table></div>`);
    tablo = null;
  };
  const kapat = () => { listeKapat(); tabloKapat(); };
  for (const s of satirlar) {
    let m;
    if (/^\s*\|.*\|\s*$/.test(s)) {
      listeKapat();
      if (/^\s*\|[\s:|-]+\|\s*$/.test(s)) { if (tablo && tablo.satirlar.length === 1) tablo.baslikli = true; continue; }
      tablo ||= { satirlar: [], baslikli: false };
      tablo.satirlar.push(s.trim().slice(1, -1).split("|").map((h) => satirIci(h.trim())));
    } else if ((m = s.match(/^(#{1,4})\s+(.*)$/))) {
      kapat(); const d = Math.min(4, Math.max(2, m[1].length)); cikti.push(`<h${d}>${satirIci(m[2])}</h${d}>`);
    } else if ((m = s.match(/^\s*[-*•]\s+(.*)$/))) {
      tabloKapat();
      if (liste !== "ul") { listeKapat(); cikti.push("<ul>"); liste = "ul"; }
      cikti.push(`<li>${satirIci(m[1])}</li>`);
    } else if ((m = s.match(/^\s*(\d+)[.)]\s+(.*)$/))) {
      tabloKapat();
      if (liste !== "ol") { listeKapat(); cikti.push(`<ol start="${m[1]}">`); liste = "ol"; }
      cikti.push(`<li>${satirIci(m[2])}</li>`);
    } else if ((m = s.match(/^\s*&gt;\s?(.*)$/))) {
      kapat(); cikti.push(`<blockquote>${satirIci(m[1])}</blockquote>`);
    } else if (/^\s*(---|\*\*\*)\s*$/.test(s)) {
      kapat(); cikti.push("<hr>");
    } else if (s.trim() === "") {
      tabloKapat();
    } else {
      kapat(); cikti.push(`<p>${satirIci(s)}</p>`);
    }
  }
  kapat();
  return cikti.join("");
}

veriIste();
