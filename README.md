# 🧠 Free Resume Scanner

> **Free, private Resume ↔ Job Description analysis using local RAG + Ollama.**

A .NET 8 application that combines document extraction, section-aware chunking, local embeddings, hybrid semantic/lexical retrieval (RAG), and a local LLM to analyze a resume against any job description.

## ✨ What is new in this version

- 🔎 **RAG pipeline** instead of sending the entire resume to the LLM
- 🧠 Local embeddings with `nomic-embed-text`
- ⚡ Batched Ollama embeddings using `/api/embed`, with compatibility fallback
- 🔀 Hybrid retrieval: 75% semantic similarity + 25% lexical overlap
- ✂️ Section-aware chunks with overlap
- 🎯 Requirement-level retrieval so each JD requirement gets relevant resume evidence
- 📉 Prompt reduction by sending only retrieved evidence to the LLM
- 📊 Detailed server performance metrics
- 📈 RAG quality metrics such as evidence coverage and retrieval confidence
- 🧪 Offline classification evaluation endpoint for **real labeled accuracy measurement**
- 🕐 Frontend timestamps and live elapsed timer
- 📊 Frontend performance and RAG metrics dashboard
- 🔒 Fully local AI inference; no OpenAI API key required

---

# 🏗️ Architecture

```text
                        RESUME + JOB DESCRIPTION
                                   │
                                   ▼
                         ┌────────────────────┐
                         │ Document Extraction │
                         └──────────┬─────────┘
                                    ▼
                         ┌────────────────────┐
                         │ Text Normalization  │
                         └──────────┬─────────┘
                                    ▼
                         ┌────────────────────┐
                         │ Section-aware       │
                         │ Chunking + Overlap  │
                         └──────────┬─────────┘
                                    │
                                    ▼
                         ┌────────────────────┐
                         │ Local Embeddings    │
                         │ nomic-embed-text    │
                         └──────────┬─────────┘
                                    │
             JOB REQUIREMENTS       │
                    │               │
                    ▼               ▼
             Requirement      Resume vectors
             extraction             │
                    │               │
                    └───────┬───────┘
                            ▼
                  ┌───────────────────┐
                  │ Hybrid RAG Search  │
                  │ Semantic + Lexical │
                  └─────────┬─────────┘
                            ▼
                   Relevant Evidence
                            │
                            ▼
                  ┌───────────────────┐
                  │ Local LLM          │
                  │ Llama 3.2 3B       │
                  └─────────┬─────────┘
                            ▼
                    Structured JSON
                            │
                            ▼
                    Matching Engine
                            │
                            ▼
                       Web Report
```

---

# 🧩 Project Structure

```text
FreeResumeScanner/
│
├── Controllers/
│   ├── AnalysisController.cs
│   └── EvaluationController.cs
│
├── DTOs/
│   ├── AnalyzeRequest.cs
│   └── AnalyzeResponse.cs
│
├── Evaluation/
│   └── EvaluationDtos.cs
│
├── Models/
│   ├── AnalysisResult.cs
│   ├── AnalyzerOutput.cs
│   ├── RagMetrics.cs
│   ├── RagRetrievalResult.cs
│   ├── Requirement.cs
│   ├── ResumeChunk.cs
│   └── RetrievedEvidence.cs
│
├── Services/
│   ├── AnalysisService.cs
│   ├── DocumentExtractor.cs
│   ├── LocalLlmAnalyzer.cs
│   ├── MatchingEngine.cs
│   ├── OllamaEmbeddingService.cs
│   ├── RagRetriever.cs
│   ├── ResumeChunker.cs
│   ├── TextPreprocessor.cs
│   │
│   └── Interfaces/
│       ├── IAnalysisService.cs
│       ├── IDocumentExtractor.cs
│       ├── IEmbeddingService.cs
│       ├── IMatchingEngine.cs
│       ├── IRagRetriever.cs
│       ├── IResumeAnalyzer.cs
│       ├── IResumeChunker.cs
│       └── ITextPreprocessor.cs
│
├── wwwroot/
│   ├── index.html
│   ├── css/app.css
│   └── js/app.js
│
├── Program.cs
├── appsettings.json
└── FreeResumeScanner.csproj
```

---

# 💻 Prerequisites

- Windows 10/11, Linux or macOS
- .NET 8 SDK
- Git
- Ollama
- Recommended: 8 GB+ RAM
- Additional disk space for local models

## Install .NET 8

[Download .NET 8 SDK — Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

Verify:

```powershell
dotnet --version
```

## Install Git

[Download Git](https://git-scm.com/downloads)

Verify:

```powershell
git --version
```

## Install Ollama

[Download Ollama](https://ollama.com/download)

Verify:

```powershell
ollama --version
```

---

# 📥 Clone the Repository

```powershell
git clone https://github.com/YOUR_USERNAME/free-resume-scanner.git
cd free-resume-scanner
```

Verify:

```powershell
dir
```

You should see `FreeResumeScanner.csproj`, `Program.cs`, `Services`, `Models`, `Controllers` and `wwwroot`.

---

# 📦 Restore NuGet Packages

Run:

```powershell
dotnet restore
```

The project uses:

| Package | Version | Purpose |
|---|---:|---|
| Swashbuckle.AspNetCore | 6.6.2 | Swagger |
| DocumentFormat.OpenXml | 3.0.2 | DOCX extraction |
| UglyToad.PdfPig | 0.1.9 | PDF extraction |

No vector database package is required. For one resume at a time, vectors are kept in memory to avoid database/network overhead.

---

# 🤖 Install Ollama Models

## 1. LLM

For fast local development:

```powershell
ollama pull llama3.2:3b
```

## 2. Embedding model

```powershell
ollama pull nomic-embed-text
```

Verify:

```powershell
ollama list
```

You should see both models.

---

# 🧪 Test Ollama

Test the LLM:

```powershell
ollama run llama3.2:3b
```

Ask:

```text
Explain dependency injection in C# in three sentences.
```

Exit:

```text
/bye
```

Test the local API:

```powershell
curl http://localhost:11434/api/tags
```

---

# ⚙️ Configuration

`appsettings.json`:

```json
{
  "LocalLLM": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2:3b",
    "EmbeddingModel": "nomic-embed-text",
    "LlmTimeoutMinutes": 15,
    "EmbeddingTimeoutMinutes": 5,
    "ContextSize": 8192,
    "MaxOutputTokens": 1800,
    "Temperature": 0.05,
    "TopP": 0.9,
    "MaxEmbeddingChunks": 80,
    "TopChunksPerRequirement": 2,
    "MaxRetrievedChunks": 20,
    "HighConfidenceThreshold": 0.65,
    "EmbeddingConcurrency": 4
  }
}
```

### Performance tuning

For a normal 1–4 page resume:

```text
MaxEmbeddingChunks       80
TopChunksPerRequirement   2
MaxRetrievedChunks       20
ContextSize            8192
```

If your computer is slow, try:

```text
Model = llama3.2:3b
MaxRetrievedChunks = 12
MaxOutputTokens = 1200
```

For better reasoning, use a larger local model if your hardware can handle it.

---

# ▶️ Build and Run

```powershell
dotnet restore
dotnet build
dotnet run
```

Open the URL printed by ASP.NET Core, for example:

```text
https://localhost:7001
```

Swagger:

```text
https://localhost:7001/swagger
```

---

# 🔎 How RAG Works

The system does **not** simply send the complete resume to the LLM.

### Step 1 — Chunk the resume

The resume is divided into meaningful sections such as:

```text
Summary
Skills
Experience
Education
Projects
Certifications
Achievements
```

Chunks use a small overlap so evidence near a chunk boundary is less likely to be lost.

### Step 2 — Create embeddings

Each resume chunk is converted into a vector using:

```text
nomic-embed-text
```

The job requirements are embedded using the same model.

### Step 3 — Retrieve evidence

For every requirement, the system calculates:

```text
Hybrid Score =
    75% Semantic Similarity
  + 25% Lexical Overlap
```

This reduces false positives caused by semantic similarity alone.

### Step 4 — Send only relevant evidence to the LLM

Instead of:

```text
Entire Resume + Entire JD → LLM
```

we use:

```text
JD Requirement
      ↓
Semantic + lexical retrieval
      ↓
Top relevant resume evidence
      ↓
LLM
```

This reduces context size and improves grounding.

---

# 📊 Performance Metrics

The API returns detailed timings:

```text
Total
Extraction
Normalization
Chunking
Requirement extraction
Embeddings
Retrieval
LLM
```

It also reports:

```text
Resume characters
Resume words
Resume chunks
Chunks embedded
Requirements extracted
Evidence retrieved
Prompt characters
```

This makes it possible to identify the actual bottleneck.

---

# 📈 RAG Quality Metrics

The API reports:

### Evidence Coverage

Percentage of requirements for which retrieval found sufficiently similar semantic evidence.

### High-Confidence Retrieval

Percentage of requirements whose top hybrid retrieval score crosses the configured confidence threshold.

### Semantic + Lexical Agreement

Percentage of requirements where semantic retrieval and lexical overlap provide supporting signals.

### Average Semantic Similarity

Average top semantic similarity across requirements.

### Average Hybrid Score

Average score after combining semantic and lexical relevance.

### Evidence / Requirement

Average number of retrieved evidence items per JD requirement.

> ⚠️ These are **retrieval-quality proxies**, not actual model accuracy.

---

# 🎯 Measuring Real Accuracy

A production-grade accuracy number requires a **labeled benchmark dataset**.

For example:

```text
Expected    Predicted
---------   ---------
Strong      Strong
Partial     Strong
Missing     Missing
Strong      Partial
```

The project includes:

```http
POST /api/v1/Evaluation/requirement-status
```

Example:

```json
{
  "cases": [
    { "expected": "Strong", "predicted": "Strong" },
    { "expected": "Strong", "predicted": "Partial" },
    { "expected": "Missing", "predicted": "Missing" },
    { "expected": "Partial", "predicted": "Partial" }
  ]
}
```

It returns:

```text
Accuracy
Macro Precision
Macro Recall
Macro F1
Confusion Matrix
```

This lets you create a real benchmark of manually labeled resumes/JDs and compare changes to prompts, models and retrieval parameters.

---

# 🧪 Recommended Accuracy Benchmark

Create a benchmark containing at least:

```text
50+ resume/JD requirement pairs   → initial benchmark
200+ pairs                       → useful development benchmark
500+ pairs                       → stronger evaluation
```

Label each requirement:

```text
Strong
Partial
Missing
```

Then compare:

```text
Model A + no RAG
vs
Model A + RAG
vs
Model B + RAG
```

Track:

```text
Accuracy
Macro F1
Retrieval Coverage
High-confidence retrieval
Latency P50
Latency P95
```

---

# ⏱️ Frontend Monitoring

During analysis the UI displays:

```text
🤖 Generating embeddings                 00:37
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Started: Sep 07, 2026, 08:10:21
Elapsed: 00:37
```

The phase indicator cycles through:

```text
🔍 Extracting
✂️ Chunking
🧠 Embeddings
🔎 Retrieval
🤖 Local LLM
📊 Report
```

After completion:

```text
✅ Analysis Completed

Server processing time: 01:42
Browser elapsed:        01:43

Extraction:     120 ms
Chunking:        14 ms
Embeddings:   8,200 ms
Retrieval:       32 ms
LLM:         92,000 ms
```

This makes it immediately obvious that local LLM inference is usually the dominant latency component.

---

# 🚀 Performance Optimization Strategy

The pipeline is optimized in this order:

```text
1. Reduce unnecessary text
        ↓
2. Section-aware chunking
        ↓
3. Batch embeddings
        ↓
4. Hybrid RAG retrieval
        ↓
5. Send only relevant evidence to LLM
        ↓
6. Limit LLM output
        ↓
7. Use a smaller local model during development
```

The embedding API is batched to avoid one HTTP request per resume chunk.

---

# 🔒 Privacy

With Ollama, inference stays local:

```text
Resume
  ↓
Your Computer
  ↓
ASP.NET Core
  ↓
Ollama
  ↓
Local Embedding Model / Local LLM
```

No OpenAI API key is required.

---

# 🔌 Provider Abstraction

The core AI abstraction remains:

```csharp
public interface IResumeAnalyzer
{
    Task<AnalyzerOutput> AnalyzeAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken);
}
```

Future implementations can include:

```text
IResumeAnalyzer
   ├── LocalLlmAnalyzer
   ├── OpenAiAnalyzer
   └── GeminiAnalyzer
```

The controller and frontend do not need to know which provider is being used.

---

# 🏷️ GitHub Topics

```text
resume-scanner
free-resume-scanner
resume-analyzer
resume-matcher
job-description-analyzer
ats-scanner
ats-resume
ai-resume
ai-recruitment
rag
retrieval-augmented-generation
local-llm
ollama
llama
embeddings
semantic-search
vector-search
generative-ai
artificial-intelligence
dotnet
dotnet8
aspnetcore
csharp
rest-api
swagger
solid-principles
clean-architecture
```

---

# 🧰 Skills

`C#` `ASP.NET Core` `.NET 8` `REST API` `RAG` `LLM` `Embeddings` `Semantic Search` `Vector Search` `Ollama` `Llama` `Prompt Engineering` `Generative AI` `PDF Processing` `DOCX Processing` `SOLID` `Dependency Injection` `Clean Architecture` `Swagger` `HTML` `CSS` `JavaScript`

---

# 🛣️ Roadmap

- [x] Local LLM
- [x] Resume chunking
- [x] Local embeddings
- [x] Hybrid RAG retrieval
- [x] Performance metrics
- [x] RAG quality metrics
- [x] Accuracy evaluation endpoint
- [x] Frontend progress/timing
- [ ] Persistent vector database
- [ ] Background job processing
- [ ] Streaming/SSE progress
- [ ] React frontend
- [ ] Gemini provider
- [ ] OpenAI provider
- [ ] Resume improvement engine
- [ ] Interview question generation
- [ ] Skill-gap analysis
- [ ] Job recommendation engine
- [ ] Automated benchmark dataset
- [ ] Docker deployment

---

# ⭐ Support

If you find **Free Resume Scanner** useful, please ⭐ star the repository and contribute improvements.

## 📌 Repository Name

```text
free-resume-scanner
```

## 📌 Description

```text
🧠 Free AI Resume Scanner — Analyze any resume against any job description using local RAG, embeddings, Ollama and Llama. Built with C#, .NET 8 and ASP.NET Core.
```
