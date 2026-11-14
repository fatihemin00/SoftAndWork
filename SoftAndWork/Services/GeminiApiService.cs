using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SoftAndWork.Models;

namespace SoftAndWork.Services
{
    public class UploadedFileData
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class MaterialAiResult
    {
        public string Summary { get; set; } = string.Empty;
        public string ExtractedContent { get; set; } = string.Empty;
        public List<Question> Questions { get; set; } = new();
    }

    public class GeminiApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiApi:ApiKey"]?.Trim() ?? "";
            _model = configuration["GeminiApi:Model"]?.Trim() ?? "gemini-3.5-flash-lite";
        }

        private string GetEndpointUrl()
        {
            return "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(_model) + ":generateContent";
        }

        public async Task<string> ExplainSimplyAsync(string extractedText, string topic, CancellationToken cancellationToken = default)
        {
            string prompt = "'" + topic + "' konusunu hiç bilmeyen birine, çok ama çok basit, günlük hayattan eğlenceli örneklerle (halk tabiriyle mala anlatır gibi) kısaca açıkla.\n" +
                            "Maksimum 2-3 paragraf olsun. KESİNLİKLE JSON KULLANMA, sadece düz metin olarak açıklamayı ver.\n" +
                            "Referans Metin:\n" +
                            extractedText;

            var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            using var request = new HttpRequestMessage(HttpMethod.Post, GetEndpointUrl());
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) return "Açıklama şu an oluşturulamadı, sistem yoğun olabilir.";
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
            }
            catch { return "Bağlantı hatası sebebiyle açıklama getirilemedi."; }
        }

        public async Task<string> GenerateOnlySummaryAsync(string extractedText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("Gemini API anahtarı bulunamadı.");

            string prompt = "Aşağıdaki ders notlarını inceleyerek öğrencinin sınavdan önce unuttukça hızlıca açıp bakabileceği, akıcı, maddeler halinde, en kritik kavramları, tanımları ve püf noktalarını içeren PRATİK BİR DERS ÖZETİ hazırla.\n" +
                            "Sadece düz metin ve madde işaretleri kullan (JSON kullanma).\n" +
                            "DERS NOTU:\n" +
                            extractedText;

            var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            using var request = new HttpRequestMessage(HttpMethod.Post, GetEndpointUrl());
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("API Hatası: " + response.StatusCode);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        }

        public async Task<MaterialAiResult> GenerateMaterialContentAsync(
            string extractedText,
            List<UploadedFileData>? files,
            int questionCount,
            string difficulty,
            bool generateSummary,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("Gemini API anahtarı bulunamadı.");

            string summaryInstruction = generateSummary
                ? "1. \"summary\": Öğrencinin sınav öncesi unuttukça açıp bakabileceği, akıcı, maddeler halinde, önemli kavramları ve püf noktalarını özetleyen PRATİK BİR DERS ÖZETİ yaz.\n" +
                  "2. \"extractedContent\": Yüklenen tüm PDF, fotoğraf ve metinlerdeki ders içeriğinin kapsamlı bir metin dökümünü/özetini çıkar (daha sonra yeni sorular üretmek için referans olacak).\n"
                : "1. \"summary\": \"\"\n" +
                  "2. \"extractedContent\": \"\"\n";

            string prompt = "Yüklenen tüm PDF dosyalarını, fotoğrafları ve yazılı ders metnini birlikte analiz et.\n" +
                            "Görseller okunamıyorsa \"questions\" dizisinin ilk elemanının \"text\" alanına 'GÖRSEL_OKUNAMIYOR' yaz.\n" +
                            "DİKKAT: KESİNLİKLE SADECE VERİLEN METİN/PDF/GÖRSELLERE BAĞLI KAL! KAYNAKLARDA OLMAYAN HİÇBİR BİLGİYİ EKLEME.\n\n" +
                            "Aşağıdaki yapıda tek bir JSON nesnesi döndür:\n" +
                            summaryInstruction +
                            "3. \"questions\": Türkçe, " + difficulty + " düzeyinde tam " + questionCount + " adet dört şıklı soru içeren JSON dizisi.\n" +
                            "Her soru nesnesinde şu alanlar bulunmalı:\n" +
                            "- \"text\": Soru metni\n" +
                            "- \"optionA\": A şıkkı\n" +
                            "- \"optionB\": B şıkkı\n" +
                            "- \"optionC\": C şıkkı\n" +
                            "- \"optionD\": D şıkkı\n" +
                            "- \"correctOption\": Doğru şık (\"A\", \"B\", \"C\" veya \"D\")\n" +
                            "- \"shortExplanation\": Sorunun cevabının kısa ve öğretici açıklaması\n" +
                            "- \"topicTag\": Sorunun ait olduğu net ve kısa konu başlığı (Örn: \"Osmanlı Kuruluş Dönemi\", \"Türev Kuralları\", \"SQL Join İşlemleri\")\n\n" +
                            "DERS METNİ:\n" +
                            extractedText;

            var partsList = new List<object> { new { text = prompt } };
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file.Bytes != null && file.Bytes.Length > 0 && !string.IsNullOrEmpty(file.MimeType))
                    {
                        partsList.Add(new { inlineData = new { mimeType = file.MimeType, data = Convert.ToBase64String(file.Bytes) } });
                    }
                }
            }

            var body = new
            {
                contents = new[] { new { parts = partsList.ToArray() } },
                generationConfig = new { responseMimeType = "application/json", maxOutputTokens = 8192 }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, GetEndpointUrl());
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(150));

            try
            {
                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("API Hatası: " + response.StatusCode);

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var candidate = doc.RootElement.GetProperty("candidates")[0];
                var result = string.Concat(candidate.GetProperty("content").GetProperty("parts").EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));

                result = result.Trim();
                if (result.StartsWith("```json")) result = result.Substring(7);
                if (result.StartsWith("```")) result = result.Substring(3);
                if (result.EndsWith("```")) result = result.Substring(0, result.Length - 3);
                result = result.Trim();

                var aiResult = JsonSerializer.Deserialize<MaterialAiResult>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (aiResult == null || aiResult.Questions == null || aiResult.Questions.Count == 0)
                    throw new InvalidOperationException("Yapay zeka geçerli soru üretemedi.");

                if (aiResult.Questions[0].Text == "GÖRSEL_OKUNAMIYOR")
                    throw new InvalidOperationException("Yüklenen görsellerden biri çok bulanık, lütfen daha net yükleyin.");

                foreach (var q in aiResult.Questions)
                {
                    q.CorrectOption = q.CorrectOption?.Trim().ToUpperInvariant() ?? "";
                    q.TopicTag = string.IsNullOrWhiteSpace(q.TopicTag) ? "Genel Konu" : q.TopicTag.Trim();
                    q.Id = 0;
                    q.MaterialId = 0;
                    q.Material = null!;
                }

                return aiResult;
            }
            catch (Exception ex) { throw new InvalidOperationException(ex.Message); }
        }
    }
}