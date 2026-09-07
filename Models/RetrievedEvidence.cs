namespace FreeResumeScanner.Models;

public sealed record RetrievedEvidence(
    int RequirementId,
    string Requirement,
    string Section,
    string Text,
    double SemanticScore,
    double LexicalScore,
    double HybridScore);
