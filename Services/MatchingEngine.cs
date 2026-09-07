using FreeResumeScanner.Models;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

public sealed class MatchingEngine : IMatchingEngine
{
    public AnalysisResult Calculate(AnalysisResult analysis)
    {
        analysis.TechnicalFit = Clamp(analysis.TechnicalFit);
        analysis.SkillsMatch = Clamp(analysis.SkillsMatch);
        analysis.ExperienceMatch = Clamp(analysis.ExperienceMatch);
        analysis.ResponsibilitiesMatch = Clamp(analysis.ResponsibilitiesMatch);
        analysis.SeniorityMatch = Clamp(analysis.SeniorityMatch);
        analysis.EducationMatch = Clamp(analysis.EducationMatch);
        analysis.DomainMatch = Clamp(analysis.DomainMatch);
        analysis.LeadershipMatch = Clamp(analysis.LeadershipMatch);
        analysis.AchievementMatch = Clamp(analysis.AchievementMatch);
        analysis.AtsScore = Clamp(analysis.AtsScore);

        var values = new[]
        {
            analysis.TechnicalFit,
            analysis.SkillsMatch,
            analysis.ExperienceMatch,
            analysis.ResponsibilitiesMatch,
            analysis.SeniorityMatch,
            analysis.EducationMatch,
            analysis.DomainMatch,
            analysis.LeadershipMatch,
            analysis.AchievementMatch,
            analysis.AtsScore
        };

        analysis.OverallMatch = Clamp((int)Math.Round(values.Average()));
        return analysis;
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
