using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftAndWork.Data;
using SoftAndWork.Models;
using SoftAndWork.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SoftAndWork.Controllers
{
    [Authorize]
    public class StudyController : Controller
    {
        private readonly AppDbContext _context;
        private readonly GeminiApiService _geminiService;

        private class QuestionDto
        {
            public int Id { get; set; }
            public int MaterialId { get; set; }
            public string Text { get; set; } = string.Empty;
            public string OptionA { get; set; } = string.Empty;
            public string OptionB { get; set; } = string.Empty;
            public string OptionC { get; set; } = string.Empty;
            public string OptionD { get; set; } = string.Empty;
            public string CorrectOption { get; set; } = string.Empty;
            public string ShortExplanation { get; set; } = string.Empty;
            public string TopicTag { get; set; } = string.Empty;
        }

        public StudyController(AppDbContext context, GeminiApiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        [HttpPost]
        public async Task<IActionResult> Start(int materialId, bool forceNew = false)
        {
            var material = await _context.Materials.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == materialId);
            if (material == null || material.Course.UserId != GetCurrentUserId()) return Unauthorized();

            if (forceNew)
            {
                var oldSessions = await _context.StudySessions
                    .Where(s => s.CourseId == material.CourseId && s.MaterialId == materialId && s.CurrentPhase != "Completed")
                    .ToListAsync();
                foreach (var old in oldSessions) old.CurrentPhase = "Completed";
                await _context.SaveChangesAsync();
            }

            var session = await _context.StudySessions
                .FirstOrDefaultAsync(s => s.CourseId == material.CourseId && s.MaterialId == materialId && s.CurrentPhase != "Completed");

            if (session == null)
            {
                session = new StudySession
                {
                    CourseId = material.CourseId,
                    MaterialId = materialId,
                    CurrentPhase = "FirstTest",
                    CurrentQuestionIndex = 0
                };
                _context.StudySessions.Add(session);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Work", new { sessionId = session.Id, materialId = materialId });
        }

        [HttpGet]
        public async Task<IActionResult> Work(int sessionId, int materialId)
        {
            ViewBag.MaterialId = materialId;
            return View(await _context.StudySessions.FindAsync(sessionId));
        }

        [HttpGet]
        public async Task<IActionResult> GetNextQuestion(int sessionId, int materialId, int? index = null)
        {
            try
            {
                var session = await _context.StudySessions
                    .Include(s => s.Responses)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                var questions = await _context.Questions
                    .Where(q => q.MaterialId == materialId)
                    .OrderBy(q => q.Id)
                    .Select(q => new QuestionDto
                    {
                        Id = q.Id,
                        MaterialId = q.MaterialId,
                        Text = q.Text ?? "",
                        OptionA = q.OptionA ?? "",
                        OptionB = q.OptionB ?? "",
                        OptionC = q.OptionC ?? "",
                        OptionD = q.OptionD ?? "",
                        CorrectOption = q.CorrectOption ?? "",
                        ShortExplanation = q.ShortExplanation ?? "",
                        TopicTag = string.IsNullOrWhiteSpace(q.TopicTag) ? "Genel Konu" : q.TopicTag
                    })
                    .ToListAsync();

                if (session == null)
                    return Json(new { phase = "Error", message = "Çalışma oturumu bulunamadı. Lütfen ders sayfasından tekrar başlatın." });

                if (questions.Count == 0)
                    return Json(new { phase = "Error", message = "Bu materyale ait soru bulunamadı." });

                if (session.CurrentPhase == "FirstTest")
                {
                    if (index.HasValue && index.Value >= 0 && index.Value < questions.Count)
                    {
                        session.CurrentQuestionIndex = index.Value;
                        await _context.SaveChangesAsync();
                    }

                    int currentIndex = Math.Clamp(session.CurrentQuestionIndex, 0, questions.Count - 1);
                    var currentQ = questions[currentIndex];
                    var firstResponses = session.Responses.Where(r => r.Round == "First").ToList();

                    var statuses = questions.Select((q, idx) =>
                    {
                        var resp = firstResponses.FirstOrDefault(r => r.QuestionId == q.Id);
                        string st = resp == null ? "unanswered" : (resp.IsBlank ? "blank" : (resp.IsCorrect ? "correct" : "wrong"));
                        return new { index = idx, questionId = q.Id, status = st };
                    }).ToList();

                    var existingResp = firstResponses.FirstOrDefault(r => r.QuestionId == currentQ.Id);
                    object? existingPayload = existingResp == null ? null : new
                    {
                        responseId = existingResp.Id,
                        selectedOption = existingResp.SelectedOption ?? "",
                        isCorrect = existingResp.IsCorrect,
                        isBlank = existingResp.IsBlank,
                        correctOption = currentQ.CorrectOption,
                        explanation = currentQ.ShortExplanation
                    };

                    return Json(new
                    {
                        phase = "FirstTest",
                        question = currentQ,
                        index = currentIndex,
                        total = questions.Count,
                        statuses = statuses,
                        existingResponse = existingPayload
                    });
                }
                else if (session.CurrentPhase == "ReTest")
                {
                    var firstResponses = session.Responses.Where(r => r.Round == "First").ToList();
                    var reTestResponses = session.Responses.Where(r => r.Round == "ReTest").ToList();

                    var failedIds = questions
                        .Where(q => firstResponses.Any(r => r.QuestionId == q.Id && !r.IsCorrect))
                        .Select(q => q.Id)
                        .ToList();

                    var solvedInReTest = reTestResponses
                        .Where(r => r.IsCorrect)
                        .Select(r => r.QuestionId)
                        .ToHashSet();

                    var remainingIds = failedIds
                        .Where(id => !solvedInReTest.Contains(id))
                        .OrderBy(id => reTestResponses.Count(r => r.QuestionId == id && !r.IsCorrect))
                        .ToList();

                    if (remainingIds.Count == 0)
                    {
                        session.CurrentPhase = "Completed";
                        await _context.SaveChangesAsync();
                        return Json(BuildCompletedResult(session, questions));
                    }

                    var currentQ = questions.First(x => x.Id == remainingIds[0]);
                    var firstResp = firstResponses.FirstOrDefault(r => r.QuestionId == currentQ.Id);
                    bool wasBlank = firstResp?.IsBlank ?? false;
                    int reTestWrongCount = reTestResponses.Count(r => r.QuestionId == currentQ.Id && !r.IsCorrect);

                    bool requiresPreExplanation = wasBlank && reTestWrongCount == 0;

                    return Json(new
                    {
                        phase = "ReTest",
                        question = currentQ,
                        index = failedIds.Count - remainingIds.Count,
                        total = failedIds.Count,
                        remainingCount = remainingIds.Count,
                        wasBlank = wasBlank,
                        requiresPreExplanation = requiresPreExplanation,
                        explanation = currentQ.ShortExplanation,
                        topicTag = currentQ.TopicTag
                    });
                }

                return Json(BuildCompletedResult(session, questions));
            }
            catch (Exception ex)
            {
                return Json(new { phase = "Error", message = "Sunucu hatası: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SkipQuestion(int sessionId, int questionId, int currentIndex, int totalCount)
        {
            var session = await _context.StudySessions.Include(s => s.Responses).FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return NotFound();

            if (session.CurrentPhase == "FirstTest")
            {
                var existing = session.Responses.FirstOrDefault(r => r.Round == "First" && r.QuestionId == questionId);
                if (existing == null)
                {
                    _context.SessionResponses.Add(new SessionResponse
                    {
                        StudySessionId = sessionId,
                        QuestionId = questionId,
                        SelectedOption = "BOŞ",
                        IsCorrect = false,
                        IsBlank = true,
                        Round = "First"
                    });
                }

                if (currentIndex + 1 < totalCount)
                {
                    session.CurrentQuestionIndex = currentIndex + 1;
                }
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, nextIndex = session.CurrentQuestionIndex, isLast = (currentIndex + 1 >= totalCount) });
        }

        [HttpPost]
        public async Task<IActionResult> FinishFirstTest(int sessionId, int materialId)
        {
            var session = await _context.StudySessions.Include(s => s.Responses).FirstOrDefaultAsync(s => s.Id == sessionId);
            var questionIds = await _context.Questions.Where(q => q.MaterialId == materialId).OrderBy(q => q.Id).Select(q => q.Id).ToListAsync();

            if (session == null) return NotFound();

            foreach (var qId in questionIds)
            {
                if (!session.Responses.Any(r => r.Round == "First" && r.QuestionId == qId))
                {
                    _context.SessionResponses.Add(new SessionResponse
                    {
                        StudySessionId = sessionId,
                        QuestionId = qId,
                        SelectedOption = "BOŞ",
                        IsCorrect = false,
                        IsBlank = true,
                        Round = "First"
                    });
                }
            }
            await _context.SaveChangesAsync();

            var firstResponses = await _context.SessionResponses.Where(r => r.StudySessionId == sessionId && r.Round == "First").ToListAsync();
            int wrongCount = firstResponses.Count(r => !r.IsCorrect && !r.IsBlank);
            int blankCount = firstResponses.Count(r => r.IsBlank);

            if (wrongCount + blankCount == 0)
            {
                session.CurrentPhase = "Completed";
                await _context.SaveChangesAsync();
                return Json(new { phase = "Completed" });
            }

            session.CurrentPhase = "ReTest";
            session.CurrentQuestionIndex = 0;
            await _context.SaveChangesAsync();

            return Json(new
            {
                phase = "ReTest",
                message = "İlk test bitti! " + wrongCount + " yanlış ve " + blankCount + " boş bıraktığın soru var. Şimdi telafi turuna geçiyoruz!"
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAnswer(int sessionId, int questionId, string selectedOption)
        {
            var session = await _context.StudySessions.Include(s => s.Responses).FirstOrDefaultAsync(s => s.Id == sessionId);
            var question = await _context.Questions
                .Where(q => q.Id == questionId)
                .Select(q => new { q.Id, CorrectOption = q.CorrectOption ?? "", ShortExplanation = q.ShortExplanation ?? "" })
                .FirstOrDefaultAsync();

            if (session == null || question == null) return NotFound();

            bool isCorrect = question.CorrectOption.Equals(selectedOption, StringComparison.OrdinalIgnoreCase);

            if (session.CurrentPhase == "FirstTest")
            {
                var existing = session.Responses.FirstOrDefault(r => r.Round == "First" && r.QuestionId == questionId);
                if (existing != null)
                {
                    if (existing.IsBlank)
                    {
                        existing.SelectedOption = selectedOption;
                        existing.IsCorrect = isCorrect;
                        existing.IsBlank = false;
                        await _context.SaveChangesAsync();
                    }
                    return Json(new { isCorrect = existing.IsCorrect, correctOption = question.CorrectOption, explanation = question.ShortExplanation, responseId = existing.Id });
                }
            }

            var response = new SessionResponse
            {
                StudySessionId = sessionId,
                QuestionId = questionId,
                SelectedOption = selectedOption,
                IsCorrect = isCorrect,
                IsBlank = false,
                Round = session.CurrentPhase == "FirstTest" ? "First" : "ReTest"
            };
            _context.SessionResponses.Add(response);
            await _context.SaveChangesAsync();

            return Json(new { isCorrect, correctOption = question.CorrectOption, explanation = question.ShortExplanation, responseId = response.Id });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitFeedback(int sessionId, int responseId, bool understood, int? nextIndex = null)
        {
            var session = await _context.StudySessions.FindAsync(sessionId);
            var response = await _context.SessionResponses.FindAsync(responseId);
            if (response != null) response.Understood = understood;
            if (session != null && session.CurrentPhase == "FirstTest" && nextIndex.HasValue)
            {
                session.CurrentQuestionIndex = nextIndex.Value;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> GetSimpleExplanation(int questionId)
        {
            var q = await _context.Questions
                .Where(x => x.Id == questionId)
                .Select(x => new
                {
                    TopicTag = x.TopicTag ?? "Genel Konu",
                    ExtractedText = x.Material.ExtractedText ?? "",
                    Summary = x.Material.Summary ?? ""
                })
                .FirstOrDefaultAsync();

            if (q == null) return Json(new { success = false, explanation = "Soru bulunamadı." });
            try
            {
                string contextText = !string.IsNullOrWhiteSpace(q.ExtractedText) ? q.ExtractedText : q.Summary;
                return Json(new { success = true, explanation = await _geminiService.ExplainSimplyAsync(contextText, q.TopicTag) });
            }
            catch
            {
                return Json(new { success = false, explanation = "Şu an bağlantı kurulamadı." });
            }
        }

        private object BuildCompletedResult(StudySession session, List<QuestionDto> questions)
        {
            var firstResponses = session.Responses.Where(r => r.Round == "First").ToList();
            var reTestResponses = session.Responses.Where(r => r.Round == "ReTest").ToList();

            int totalQuestions = questions.Count;
            int firstCorrect = firstResponses.Count(r => r.IsCorrect);
            int firstWrong = firstResponses.Count(r => !r.IsCorrect && !r.IsBlank);
            int firstBlank = firstResponses.Count(r => r.IsBlank);
            int reTestWrongCount = reTestResponses.Count(r => !r.IsCorrect);

            var recoveryStats = new List<object>();
            foreach (var q in questions)
            {
                var fResp = firstResponses.FirstOrDefault(r => r.QuestionId == q.Id);
                if (fResp != null && !fResp.IsCorrect)
                {
                    int wrongInFirst = fResp.IsBlank ? 0 : 1;
                    int wrongInReTest = reTestResponses.Count(r => r.QuestionId == q.Id && !r.IsCorrect);
                    int totalWrongTries = wrongInFirst + wrongInReTest;
                    int foundOnAttempt = totalWrongTries + 1;

                    recoveryStats.Add(new
                    {
                        questionText = q.Text.Length > 75 ? q.Text.Substring(0, 75) + "..." : q.Text,
                        topicTag = string.IsNullOrWhiteSpace(q.TopicTag) ? "Genel Konu" : q.TopicTag,
                        initialStatus = fResp.IsBlank ? "Boş Bırakıldı" : "Yanlış Yapıldı",
                        wrongAttempts = totalWrongTries,
                        foundOnAttempt = foundOnAttempt
                    });
                }
            }

            var consecutiveTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < questions.Count - 1; i++)
            {
                var r1 = firstResponses.FirstOrDefault(r => r.QuestionId == questions[i].Id);
                var r2 = firstResponses.FirstOrDefault(r => r.QuestionId == questions[i + 1].Id);
                if (r1 != null && !r1.IsCorrect && r2 != null && !r2.IsCorrect)
                {
                    string t1 = questions[i].TopicTag?.Trim() ?? "";
                    string t2 = questions[i + 1].TopicTag?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(t1) && t1.Equals(t2, StringComparison.OrdinalIgnoreCase))
                    {
                        consecutiveTopics.Add(t1);
                    }
                }
            }

            var topicMistakeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var resp in session.Responses.Where(r => !r.IsCorrect && !r.IsBlank))
            {
                var q = questions.FirstOrDefault(x => x.Id == resp.QuestionId);
                string tag = string.IsNullOrWhiteSpace(q?.TopicTag) ? "Genel Konu" : q.TopicTag.Trim();
                topicMistakeCounts[tag] = topicMistakeCounts.GetValueOrDefault(tag, 0) + 1;
                if (topicMistakeCounts[tag] >= 2)
                {
                    consecutiveTopics.Add(tag);
                }
            }

            var allWeakTopics = session.Responses
                .Where(r => !r.IsCorrect)
                .Select(r => questions.FirstOrDefault(q => q.Id == r.QuestionId)?.TopicTag ?? "Genel Konu")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .GroupBy(t => t.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new { topic = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            return new
            {
                phase = "Completed",
                message = "Tebrikler! Tüm yanlışlarını ve boş bıraktığın soruları başarıyla tamamladın.",
                stats = new
                {
                    totalQuestions,
                    firstCorrect,
                    firstWrong,
                    firstBlank,
                    reTestWrongCount,
                    recoveryStats,
                    consecutiveTopics = consecutiveTopics.ToList(),
                    allWeakTopics
                }
            };
        }
    }
}