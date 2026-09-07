const resumeInput = document.getElementById("resume");
const jobDescriptionInput = document.getElementById("jobDescription");
const analyzeButton = document.getElementById("analyzeButton");
const status = document.getElementById("status");
const report = document.getElementById("report");

let timerInterval = null;
let phaseInterval = null;
let startedAtClient = null;
let currentPhase = 0;

const phases = [
    "🔍 Extracting resume text",
    "✂️ Creating semantic chunks",
    "🧠 Generating embeddings",
    "🔎 Retrieving relevant evidence",
    "🤖 Running local LLM analysis",
    "📊 Building final report"
];

analyzeButton.addEventListener("click", analyze);

async function analyze() {
    const resume = resumeInput.files[0];
    const jobDescription = jobDescriptionInput.value.trim();

    if (!resume) return showStatus("Please select a resume.", "error");
    if (!jobDescription) return showStatus("Please enter a job description.", "error");

    startedAtClient = new Date();
    startProcessing();

    const formData = new FormData();
    formData.append("resume", resume);
    formData.append("jobDescription", jobDescription);

    try {
        const response = await fetch("/api/v1/Analysis", {
            method: "POST",
            body: formData
        });

        const data = await response.json();
        if (!response.ok) {
            throw new Error(data.error || `Request failed (${response.status}).`);
        }

        const completedAtClient = new Date();
        const clientMs = completedAtClient - startedAtClient;

        stopProcessing();
        renderReport(data, clientMs, completedAtClient);
        showStatus(`✅ Analysis completed in ${formatDuration(clientMs)}.`, "success");
    } catch (error) {
        stopProcessing();
        const elapsed = Date.now() - startedAtClient.getTime();
        showStatus(`❌ Analysis failed after ${formatDuration(elapsed)}: ${error.message}`, "error");
    }
}

function startProcessing() {
    analyzeButton.disabled = true;
    analyzeButton.textContent = "⏳ Analyzing...";
    report.innerHTML = "";
    currentPhase = 0;

    status.className = "status";
    status.innerHTML = `
        <div class="processing-panel">
            <div class="processing-header">
                <span id="phaseText">${phases[0]}</span>
                <span id="liveElapsed">00:00</span>
            </div>
            <div class="progress-track" aria-label="Analysis in progress">
                <div class="progress-bar"></div>
            </div>
            <div class="processing-details">
                <div><strong>Started:</strong> ${formatTimestamp(startedAtClient)}</div>
                <div><strong>Elapsed:</strong> <span id="elapsedValue">00:00</span></div>
            </div>
            <div class="processing-message">
                Local RAG pipeline: extraction → chunking → embeddings → retrieval → LLM → report
            </div>
        </div>`;

    timerInterval = setInterval(updateTimer, 250);
    phaseInterval = setInterval(updatePhase, 2500);
}

function updateTimer() {
    if (!startedAtClient) return;
    const elapsed = Date.now() - startedAtClient.getTime();
    const value = formatDuration(elapsed);
    document.getElementById("liveElapsed")?.replaceChildren(document.createTextNode(value));
    document.getElementById("elapsedValue")?.replaceChildren(document.createTextNode(value));
}

function updatePhase() {
    currentPhase = (currentPhase + 1) % phases.length;
    const phase = document.getElementById("phaseText");
    if (phase) phase.textContent = phases[currentPhase];
}

function stopProcessing() {
    if (timerInterval) clearInterval(timerInterval);
    if (phaseInterval) clearInterval(phaseInterval);
    timerInterval = null;
    phaseInterval = null;
    analyzeButton.disabled = false;
    analyzeButton.textContent = "🔍 Analyze Resume";
}

function renderReport(data, clientMs, completedAtClient) {
    const result = data.result || {};
    const p = data.processing || {};
    const rag = p.rag || {};
    const serverMs = Number(p.totalMilliseconds || clientMs);

    report.innerHTML = `
        <div class="completion-banner">
            <div>
                <div class="completion-title">✅ Analysis Completed</div>
                <div class="completion-time">Server processing time: <strong>${formatDuration(serverMs)}</strong></div>
            </div>
            <div class="completion-meta">
                <div><strong>Started</strong><br>${formatTimestamp(p.startedAt || startedAtClient)}</div>
                <div><strong>Completed</strong><br>${formatTimestamp(p.completedAt || completedAtClient)}</div>
                <div><strong>Browser elapsed</strong><br>${formatDuration(clientMs)}</div>
            </div>
        </div>

        <section class="report-section">
            <h2>⚡ Performance Metrics</h2>
            <div class="metric-grid">
                ${metricCard("Total", p.totalMilliseconds)}
                ${metricCard("Extraction", p.extractionMilliseconds)}
                ${metricCard("Normalization", p.normalizationMilliseconds)}
                ${metricCard("Chunking", p.chunkingMilliseconds)}
                ${metricCard("Requirements", p.requirementExtractionMilliseconds)}
                ${metricCard("Embeddings", p.embeddingMilliseconds)}
                ${metricCard("Retrieval", p.retrievalMilliseconds)}
                ${metricCard("LLM", p.llmMilliseconds)}
                ${metricCard("Prompt", p.promptCharacters, true, " chars")}
            </div>
        </section>

        <section class="report-section">
            <h2>🔎 RAG Quality Metrics</h2>
            <p class="metric-note">These are retrieval-quality proxies, not ground-truth model accuracy. True accuracy requires a labeled benchmark dataset.</p>
            <div class="metric-grid">
                ${percentMetricCard("Evidence Coverage", rag.evidenceCoverageRate)}
                ${percentMetricCard("High-Confidence Retrieval", rag.highConfidenceRequirementRate)}
                ${percentMetricCard("Semantic + Lexical Agreement", rag.semanticLexicalAgreement)}
                ${numberMetricCard("Avg Semantic Similarity", rag.averageTopSemanticSimilarity, 3)}
                ${numberMetricCard("Avg Hybrid Score", rag.averageTopHybridScore, 3)}
                ${numberMetricCard("Evidence / Requirement", rag.averageEvidencePerRequirement, 1)}
                ${numberMetricCard("Unique Sections", rag.uniqueRetrievedSections, 0)}
                ${numberMetricCard("Chunks Embedded", p.chunksEmbedded, 0)}
                ${numberMetricCard("Chunks Created", p.resumeChunks, 0)}
            </div>
        </section>

        <section class="report-section">
            <h2>🎯 Match Scores</h2>
            <div class="score-grid">
                ${scoreCard("Overall Match", result.overallMatch)}
                ${scoreCard("Technical Fit", result.technicalFit)}
                ${scoreCard("Skills Match", result.skillsMatch)}
                ${scoreCard("Experience", result.experienceMatch)}
                ${scoreCard("Responsibilities", result.responsibilitiesMatch)}
                ${scoreCard("Seniority", result.seniorityMatch)}
                ${scoreCard("Education", result.educationMatch)}
                ${scoreCard("Domain", result.domainMatch)}
                ${scoreCard("Leadership", result.leadershipMatch)}
                ${scoreCard("Achievements", result.achievementMatch)}
                ${scoreCard("ATS Score", result.atsScore)}
            </div>
        </section>

        <section class="report-section">
            <h2>📌 ${escapeHtml(result.jobTitle || "Job Analysis")}</h2>
            <p><strong>Candidate:</strong> ${escapeHtml(result.candidateName || "Not identified")} &nbsp; <strong>Seniority:</strong> ${escapeHtml(result.seniority || "Not identified")}</p>
            <p>${escapeHtml(result.executiveVerdict || "")}</p>
        </section>

        ${renderListSection("💪 Strongest Evidence", result.strongestEvidence)}
        ${renderListSection("⚠️ Critical Gaps", result.criticalGaps)}
        ${renderListSection("🔑 Existing Keywords", result.existingKeywords)}
        ${renderListSection("❌ Missing Keywords", result.missingKeywords)}
        ${renderRequirements(result.requirements)}
        ${renderListSection("💡 Recommendations", result.recommendations)}
        ${renderListSection("🎤 Interview Risks", result.interviewRisks)}
        ${renderAts(result.ats)}
    `;
}

function metricCard(title, value, raw = false, suffix = "") {
    const n = Number(value || 0);
    return `<div class="metric-card"><div class="metric-label">${escapeHtml(title)}</div><div class="metric-value">${raw ? Math.round(n).toLocaleString() : formatDuration(n)}${suffix}</div></div>`;
}

function percentMetricCard(title, value) {
    const n = Math.max(0, Math.min(100, Number(value || 0)));
    return `<div class="metric-card"><div class="metric-label">${escapeHtml(title)}</div><div class="metric-value">${n.toFixed(1)}%</div><div class="score-track"><div class="score-fill" style="width:${n}%"></div></div></div>`;
}

function numberMetricCard(title, value, decimals) {
    const n = Number(value || 0);
    return `<div class="metric-card"><div class="metric-label">${escapeHtml(title)}</div><div class="metric-value">${n.toFixed(decimals)}</div></div>`;
}

function scoreCard(title, score) {
    const value = Math.max(0, Math.min(100, Number(score) || 0));
    return `<div class="score-card"><div class="score-title">${escapeHtml(title)}</div><div class="score-value">${value}%</div><div class="score-track"><div class="score-fill" style="width:${value}%"></div></div></div>`;
}

function renderListSection(title, items) {
    if (!Array.isArray(items) || !items.length) return "";
    return `<section class="report-section"><h2>${title}</h2><ul class="analysis-list">${items.map(x => `<li>${escapeHtml(x)}</li>`).join("")}</ul></section>`;
}

function renderRequirements(requirements) {
    if (!Array.isArray(requirements) || !requirements.length) return "";
    return `<section class="report-section"><h2>📊 Requirement Analysis</h2><div class="table-container"><table><thead><tr><th>Requirement</th><th>Evidence</th><th>Status</th><th>Recommendation</th></tr></thead><tbody>${requirements.map(r => `<tr><td>${escapeHtml(r.requirement)}</td><td>${escapeHtml(r.evidence)}</td><td><span class="status-badge ${getStatusClass(r.status)}">${escapeHtml(r.status)}</span></td><td>${escapeHtml(r.recommendation)}</td></tr>`).join("")}</tbody></table></div></section>`;
}

function renderAts(ats) {
    if (!ats) return "";
    return `<section class="report-section"><h2>🤖 ATS Assessment</h2>${renderListSection("ATS Strengths", ats.strengths)}${renderListSection("ATS Risks", ats.risks)}</section>`;
}

function getStatusClass(status) {
    const s = (status || "").toLowerCase();
    return s === "strong" || s === "partial" || s === "missing" ? s : "";
}

function formatDuration(ms) {
    const totalSeconds = Math.max(0, Math.round(Number(ms || 0) / 1000));
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return hours ? `${pad(hours)}:${pad(minutes)}:${pad(seconds)}` : `${pad(minutes)}:${pad(seconds)}`;
}

function pad(v) { return String(v).padStart(2, "0"); }

function formatTimestamp(value) {
    const date = value instanceof Date ? value : new Date(value);
    return date.toLocaleString([], { year: "numeric", month: "short", day: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function showStatus(message, type) {
    status.className = `status ${type}`;
    status.textContent = message;
}

function escapeHtml(value) {
    return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;");
}
