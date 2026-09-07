namespace FreeResumeScanner.Models;

public sealed record ResumeChunk(
    int Id,
    string Section,
    string Text,
    float[]? Embedding = null);
