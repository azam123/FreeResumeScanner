namespace FreeResumeScanner.Models;

public sealed class AnalysisResult
{
    public string CandidateName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Seniority { get; set; } = "";
    public int OverallMatch { get; set; }
    public int TechnicalFit { get; set; }
    public int SkillsMatch { get; set; }
    public int ExperienceMatch { get; set; }
    public int ResponsibilitiesMatch { get; set; }
    public int SeniorityMatch { get; set; }
    public int EducationMatch { get; set; }
    public int DomainMatch { get; set; }
    public int LeadershipMatch { get; set; }
    public int AchievementMatch { get; set; }
    public int AtsScore { get; set; }
    public string ExecutiveVerdict { get; set; } = "";
    public List<string> StrongestEvidence { get; set; } = [];
    public List<string> CriticalGaps { get; set; } = [];
    public List<string> ExistingKeywords { get; set; } = [];
    public List<string> MissingKeywords { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public List<string> InterviewRisks { get; set; } = [];
    public List<RequirementAnalysis> Requirements { get; set; } = [];
    public AtsAssessment Ats { get; set; } = new();
}

public sealed class RequirementAnalysis
{
    public string Requirement { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Status { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public sealed class AtsAssessment
{
    public List<string> Strengths { get; set; } = [];
    public List<string> Risks { get; set; } = [];
}

public sealed class ProcessingMetrics
{
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public long TotalMilliseconds { get; init; }
    public long NormalizationMilliseconds { get; init; }
    public long ExtractionMilliseconds { get; init; }
    public long ChunkingMilliseconds { get; init; }
    public long RequirementExtractionMilliseconds { get; init; }
    public long EmbeddingMilliseconds { get; init; }
    public long RetrievalMilliseconds { get; init; }
    public long LlmMilliseconds { get; init; }
    public int ResumeCharacters { get; init; }
    public int ResumeWords { get; init; }
    public int ResumeChunks { get; init; }
    public int ChunksEmbedded { get; init; }
    public int RequirementsExtracted { get; init; }
    public int RequirementsRetrieved { get; init; }
    public int PromptCharacters { get; init; }
    public RagMetrics Rag { get; init; } = new();
}
