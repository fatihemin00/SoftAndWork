using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftAndWork.Data;
using SoftAndWork.Models;
using SoftAndWork.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SoftAndWork.Controllers
{
    [Authorize]
    public class CoursesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly GeminiApiService _geminiService;

        public CoursesController(AppDbContext context, GeminiApiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        public async Task<IActionResult> Index()
        {
            var courses = await _context.Courses.Include(c => c.Materials).Where(c => c.UserId == GetCurrentUserId()).ToListAsync();
            return View(courses);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return RedirectToAction(nameof(Index));
            _context.Courses.Add(new Course { Title = title, UserId = GetCurrentUserId() });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id && c.UserId == GetCurrentUserId());
            if (course != null)
            {
                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ders ve içindeki tüm kaynaklar başarıyla silindi.";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses.Include(c => c.Materials).ThenInclude(m => m.GeneratedQuestions).FirstOrDefaultAsync(c => c.Id == id && c.UserId == GetCurrentUserId());
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> AddMaterial(int courseId, string fileName, string extractedText, List<IFormFile>? uploadedFiles)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.UserId == GetCurrentUserId());
            if (course == null) return Unauthorized();

            var filePayloads = new List<UploadedFileData>();
            long totalBytes = 0;

            if (uploadedFiles != null && uploadedFiles.Count > 0)
            {
                foreach (var file in uploadedFiles)
                {
                    if (file.Length > 0)
                    {
                        if (file.Length > 20 * 1024 * 1024)
                        {
                            TempData["Error"] = $"'{file.FileName}' dosyası 20 MB sınırını aşıyor.";
                            return RedirectToAction(nameof(Details), new { id = courseId });
                        }
                        totalBytes += file.Length;
                        if (totalBytes > 50 * 1024 * 1024)
                        {
                            TempData["Error"] = "Toplam dosya boyutu 50 MB'ı aşamaz.";
                            return RedirectToAction(nameof(Details), new { id = courseId });
                        }

                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        filePayloads.Add(new UploadedFileData
                        {
                            Bytes = ms.ToArray(),
                            MimeType = file.ContentType,
                            FileName = file.FileName
                        });
                    }
                }
            }

            if (filePayloads.Count == 0 && string.IsNullOrWhiteSpace(extractedText))
            {
                TempData["Error"] = "Lütfen en az bir PDF/fotoğraf yükleyin veya metin yapıştırın.";
                return RedirectToAction(nameof(Details), new { id = courseId });
            }

            string defaultTitle = filePayloads.Count > 1
                ? $"{filePayloads[0].FileName} (+{filePayloads.Count - 1} dosya)"
                : (filePayloads.FirstOrDefault()?.FileName ?? "Ders Notu");

            var material = new Material
            {
                CourseId = courseId,
                FileName = string.IsNullOrWhiteSpace(fileName) ? defaultTitle : fileName,
                ExtractedText = extractedText ?? ""
            };

            try
            {
                var aiResult = await _geminiService.GenerateMaterialContentAsync(
                    extractedText ?? "",
                    filePayloads,
                    15,
                    "orta",
                    generateSummary: true,
                    HttpContext.RequestAborted);

                material.Summary = aiResult.Summary;
                if (string.IsNullOrWhiteSpace(material.ExtractedText) && !string.IsNullOrWhiteSpace(aiResult.ExtractedContent))
                {
                    material.ExtractedText = aiResult.ExtractedContent;
                }

                foreach (var q in aiResult.Questions)
                {
                    q.Material = material;
                    _context.Questions.Add(q);
                }

                _context.Materials.Add(material);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{aiResult.Questions.Count} soruluk ilk test ve pratik ders özeti başarıyla oluşturuldu!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Uyarı: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        [HttpPost]
        public async Task<IActionResult> GenerateSummary(int materialId)
        {
            var material = await _context.Materials
                .Include(m => m.Course)
                .Include(m => m.GeneratedQuestions)
                .FirstOrDefaultAsync(m => m.Id == materialId && m.Course.UserId == GetCurrentUserId());
            if (material == null) return Unauthorized();

            try
            {
                string sourceText = !string.IsNullOrWhiteSpace(material.ExtractedText)
                    ? material.ExtractedText
                    : string.Join("\n", material.GeneratedQuestions.Select(q => $"{q.TopicTag}: {q.Text} -> {q.ShortExplanation}"));

                if (string.IsNullOrWhiteSpace(sourceText)) sourceText = material.FileName;

                material.Summary = await _geminiService.GenerateOnlySummaryAsync(sourceText, HttpContext.RequestAborted);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{material.FileName}' için pratik ders özeti oluşturuldu!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Özet oluşturulamadı: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = material.CourseId });
        }

        [HttpPost]
        public async Task<IActionResult> ExpandPool(int materialId)
        {
            var material = await _context.Materials.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == materialId && m.Course.UserId == GetCurrentUserId());
            if (material == null) return Unauthorized();

            try
            {
                string sourceText = !string.IsNullOrWhiteSpace(material.ExtractedText) ? material.ExtractedText : material.Summary;
                var aiResult = await _geminiService.GenerateMaterialContentAsync(sourceText, null, 25, "farklı tarzlarda", generateSummary: false, HttpContext.RequestAborted);
                foreach (var q in aiResult.Questions)
                {
                    q.MaterialId = material.Id;
                    _context.Questions.Add(q);
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Harika! Soru havuzuna {aiResult.Questions.Count} yeni soru eklendi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Havuz genişletilemedi: " + ex.Message;
            }
            return RedirectToAction("Details", new { id = material.CourseId });
        }
    }
}