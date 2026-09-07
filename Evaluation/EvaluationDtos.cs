namespace FreeResumeScanner.Evaluation;

public sealed class RequirementStatusEvaluationRequest
{
    public List<RequirementStatusCase> Cases { get; set; } = [];
}

public sealed class RequirementStatusCase
{
    public string Expected { get; set; } = "";
    public string Predicted { get; set; } = "";
}

public sealed class RequirementStatusEvaluationResponse
{
    public int SampleCount { get; init; }
    public double AccuracyPercent { get; init; }
    public double MacroPrecisionPercent { get; init; }
    public double MacroRecallPercent { get; init; }
    public double MacroF1Percent { get; init; }
    public Dictionary<string, Dictionary<string, int>> ConfusionMatrix { get; init; } = new();
}
