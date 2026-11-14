\# 🚀 Soft \& Work — Yapay Zeka Destekli Akıllı Çalışma ve Test Platformu



\*\*Soft \& Work\*\*, ders notlarını (PDF, fotoğraf veya düz metin) \*\*Google Gemini AI\*\* ile analiz ederek öğrencilere özel pratik konu özetleri ve çoktan seçmeli testler üreten, yanlış ve boş bırakılan soruları aktif öğrenme yöntemiyle telafi ettiren bulut tabanlı bir web uygulamasıdır.



🌐 \*\*Canlı Demo:\*\* \[Soft \& Work Uygulamasını Görüntüle](https://fatih-softandwork-2026-ewbvg8cvhnefbnfg.swedencentral-01.azurewebsites.net)



\---



\## ✨ Öne Çıkan Özellikler



\### 📚 1. Çoklu Kaynak ve Görsel Analizi

\* Aynı anda \*\*birden fazla PDF dosyası\*\* ve \*\*ders notu fotoğrafı\*\* (PNG, JPG, WebP) yükleyebilir veya doğrudan metin yapıştırabilirsiniz.

\* Yapay zeka yalnızca yüklenen kaynaklardaki bilgilere bağlı kalarak halüsinasyonsuz içerik üretir.



\### 📝 2. Pratik Ders Özeti

\* Kaynak yüklendiği anda sistem otomatik olarak sınav öncesi hızlı tekrar yapılabilecek maddeli bir \*\*Pratik Ders Özeti\*\* çıkarır.

\* Materyal listesindeki \*\*"Pratik Özeti Gör"\*\* penceresinden özetlere dilediğiniz zaman ulaşabilir veya özeti tek tıkla yenileyebilirsiniz.



\### 🎯 3. Dinamik Soru Havuzu ve Genişletme

\* İlk analizde materyalden \*\*15 soruluk\*\* başlangıç testi oluşturulur.

\* İhtiyaç duyulduğunda \*\*"Havuzu Genişlet (+25)"\*\* özelliği ile aynı kaynaktan farklı soru tarzlarında 25 yeni soru daha havuza eklenir.



\### 🧭 4. Esnek Odak Modu ve Soru Paleti

\* \*\*Soru Numarası Paleti:\*\* Üstteki renkli palet üzerinden sorular arasında serbestçe geçiş yapabilir; doğru (yeşil), yanlış (kırmızı) ve boş (sarı) durumlarını anlık takip edebilirsiniz.

\* \*\*İleri / Geri Gezinme ve Boş Bırakma:\*\* Soruları sırayla çözme zorunluluğu olmadan \*Geçmiş Soruyu Gör\*, \*Diğer Soruyu Gör\* ve \*Boş Bırak\* butonlarıyla sınav deneyimi yaşayabilirsiniz.



\### 🧠 5. Akıllı Telafi Turu ve "Daha Basit Anlat" AI Desteği

\* İlk test tamamlandığında sistem yalnızca \*\*yanlış yapılan\*\* ve \*\*boş bırakılan\*\* soruları 2. tura (Telafi Turu) aktarır.

\* \*\*Boş Bırakılan Sorulara Özel Akış:\*\* Boş bırakılan sorularda şıklar başlangıçta kilitli gelir. Önce konunun kısa açıklaması gösterilir ve \*"Konuyu anladın mı?"\* diye sorulur.

\* \*\*Basitleştirilmiş Anlatım:\*\* Kullanıcı \*"Hiç Anlamadım (Daha Basit Anlat)"\* seçeneğine tıkladığında Gemini AI konuyu günlük hayattan örneklerle en sade haliyle yeniden açıklar.



\### 📊 6. Detaylı Performans Analizi ve Zayıf Konu Tespiti

\* Test sonunda \*\*İlk Tur Doğru / Yanlış / Boş\*\* sayıları kartlar halinde sunulur.

\* \*\*Deneme Takip Tablosu:\*\* Yanlış yapılan veya boş bırakılan her bir sorunun telafi turunda \*\*kaçıncı denemede doğru bulunduğu\*\* raporlanır.

\* \*\*Zayıf Konu Başlıkları Kutusu:\*\* Üst üste hata yapılan veya tekrar edilmesi gereken konu başlıkları (`TopicTag`) otomatik tespit edilip tek tıkla kopyalanabilir bir metin kutusunda listelenir.



\---



\## 🛠️ Kullanılan Teknolojiler



| Katman | Teknoloji |

| :--- | :--- |

| \*\*Backend\*\* | ASP.NET Core MVC (.NET 10.0), C# |

| \*\*Veritabanı \& ORM\*\* | Azure SQL Database, Entity Framework Core |

| \*\*Yapay Zeka\*\* | Google Gemini API (`gemini-3.5-flash-lite`) |

| \*\*Frontend\*\* | HTML5, CSS3, Bootstrap 5, JavaScript / jQuery |

| \*\*Kimlik Doğrulama\*\* | ASP.NET Core Cookie Authentication (SHA-256 Şifreleme) |

| \*\*Bulut \& Yayınlama\*\* | Microsoft Azure App Service |



\---



\## ⚙️ Yerel Kurulum (Localhost)



Projeyi kendi bilgisayarınızda çalıştırmak için:



1\. \*\*Depoyu klonlayın:\*\*

&#x20;  ```bash

&#x20;  git clone \[https://github.com/fatihemin00/SoftAndWork.git](https://github.com/fatihemin00/SoftAndWork.git)

&#x20;  cd SoftAndWork

&#x20;  ```



2\. \*\*Gizli ayarları yapılandırın:\*\*

&#x20;  `SoftAndWork/appsettings.Development.json` dosyasını açarak kendi SQL Server bağlantı cümlenizi ve Gemini API anahtarınızı ekleyin:

&#x20;  ```json

&#x20;  {

&#x20;    "ConnectionStrings": {

&#x20;      "DefaultConnection": "Server=SUNUCU\_ADRESINIZ;Database=SoftAndWorkDB;User Id=KULLANICI;Password=SIFRE;TrustServerCertificate=True;"

&#x20;    },

&#x20;    "GeminiApi": {

&#x20;      "ApiKey": "GEMINI\_API\_ANAHTARINIZ"

&#x20;    }

&#x20;  }

&#x20;  ```



3\. \*\*Veritabanı tablolarını oluşturun ve başlatın:\*\*

&#x20;  ```bash

&#x20;  cd SoftAndWork

&#x20;  dotnet ef database update

&#x20;  dotnet run

&#x20;  ```



\---



\## 👨‍💻 Geliştirici



\*\*Fatih Emin Küçük\*\*  

\* GitHub: \[@fatihemin00](https://github.com/fatihemin00)

