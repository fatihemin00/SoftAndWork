using System;
using System.Collections.Generic;

namespace SoftAndWork.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public List<Course> Courses { get; set; } = new();
    }

    public class Course
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public List<Material> Materials { get; set; } = new();
        public List<StudySession> StudySessions { get; set; } = new();
    }

    public class Material
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;

        public Course Course { get; set; } = null!;
        public List<Question> GeneratedQuestions { get; set; } = new();
    }

    public class Question
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
        public bool IsVariantQuestion { get; set; } = false;

        public Material Material { get; set; } = null!;
    }

    public class StudySession
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int? MaterialId { get; set; }
        public int CurrentQuestionIndex { get; set; } = 0;
        public string CurrentPhase { get; set; } = "FirstTest";

        public Course Course { get; set; } = null!;
        public List<SessionResponse> Responses { get; set; } = new();
    }

    public class SessionResponse
    {
        public int Id { get; set; }
        public int StudySessionId { get; set; }
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public bool IsBlank { get; set; } = false;
        public bool? Understood { get; set; }
        public string Round { get; set; } = "First";

        public StudySession StudySession { get; set; } = null!;
        public Question Question { get; set; } = null!;
    }
}