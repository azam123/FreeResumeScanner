using FreeResumeScanner.Models;

namespace FreeResumeScanner.Services.Interfaces;

public interface IResumeChunker
{
    IReadOnlyList<ResumeChunk> Chunk(string resumeText);
}
