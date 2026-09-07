using FreeResumeScanner.Evaluation;
using Microsoft.AspNetCore.Mvc;

namespace FreeResumeScanner.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class EvaluationController : ControllerBase
{
    private static readonly string[] Labels = ["Strong", "Partial", "Missing"];

    [HttpPost("requirement-status")]
    [Produces("application/json")]
    public ActionResult<RequirementStatusEvaluationResponse> Evaluate(
        [FromBody] RequirementStatusEvaluationRequest request)
    {
        if (request.Cases.Count == 0)
            return BadRequest(new { error = "At least one labeled case is required." });

        var cases = request.Cases
            .Select(x => new
            {
                Expected = Normalize(x.Expected),
                Predicted = Normalize(x.Predicted)
            })
            .Where(x => Labels.Contains(x.Expected, StringComparer.OrdinalIgnoreCase)
                     && Labels.Contains(x.Predicted, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (cases.Count == 0)
            return BadRequest(new { error = "Cases must use Strong, Partial or Missing labels." });

        var matrix = Labels.ToDictionary(
            actual => actual,
            _ => Labels.ToDictionary(predicted => predicted, _ => 0, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in cases)
            matrix[item.Expected][item.Predicted]++;

        var accuracy = cases.Count(x => x.Expected.Equals(x.Predicted, StringComparison.OrdinalIgnoreCase))
                       / (double)cases.Count * 100;

        var precision = new List<double>();
        var recall = new List<double>();
        var f1 = new List<double>();

        foreach (var label in Labels)
        {
            var tp = matrix[label][label];
            var fp = Labels.Where(x => !x.Equals(label, StringComparison.OrdinalIgnoreCase))
                           .Sum(actual => matrix[actual][label]);
            var fn = Labels.Where(x => !x.Equals(label, StringComparison.OrdinalIgnoreCase))
                           .Sum(predicted => matrix[label][predicted]);

            var p = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
            var r = tp + fn == 0 ? 0 : (double)tp / (tp + fn);
            var f = p + r == 0 ? 0 : 2 * p * r / (p + r);

            precision.Add(p);
            recall.Add(r);
            f1.Add(f);
        }

        return Ok(new RequirementStatusEvaluationResponse
        {
            SampleCount = cases.Count,
            AccuracyPercent = Math.Round(accuracy, 2),
            MacroPrecisionPercent = Math.Round(precision.Average() * 100, 2),
            MacroRecallPercent = Math.Round(recall.Average() * 100, 2),
            MacroF1Percent = Math.Round(f1.Average() * 100, 2),
            ConfusionMatrix = matrix
        });
    }

    private static string Normalize(string value)
        => value.Trim().Equals("strong", StringComparison.OrdinalIgnoreCase) ? "Strong" :
           value.Trim().Equals("partial", StringComparison.OrdinalIgnoreCase) ? "Partial" :
           value.Trim().Equals("missing", StringComparison.OrdinalIgnoreCase) ? "Missing" : value.Trim();
}
