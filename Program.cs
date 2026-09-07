using Microsoft.AspNetCore.Http.Features;
using FreeResumeScanner.Services;
using FreeResumeScanner.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("OllamaLlm", client =>
{
    client.Timeout = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("LocalLLM:LlmTimeoutMinutes", 15));
});

builder.Services.AddHttpClient("OllamaEmbedding", client =>
{
    client.Timeout = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("LocalLLM:EmbeddingTimeoutMinutes", 5));
});

builder.Services.AddScoped<IDocumentExtractor, DocumentExtractor>();
builder.Services.AddScoped<ITextPreprocessor, TextPreprocessor>();
builder.Services.AddScoped<IResumeChunker, ResumeChunker>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IRagRetriever, RagRetriever>();
builder.Services.AddScoped<IResumeAnalyzer, LocalLlmAnalyzer>();
builder.Services.AddScoped<IMatchingEngine, MatchingEngine>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("Frontend");
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "FreeResumeScanner",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/error", () => Results.Problem(
    title: "An unexpected error occurred.",
    statusCode: StatusCodes.Status500InternalServerError));

app.Run();
