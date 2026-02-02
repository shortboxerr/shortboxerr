using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

public static class DecisionEngineEndpoints
{
    public static void MapDecisionEngineEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/decision")
            .WithTags("Decision Engine")
            .WithOpenApi();

        // POST evaluate candidates
        group.MapPost("/evaluate", (
            IDecisionEngine engine,
            EvaluateCandidatesRequest request) =>
        {
            var candidates = request.Candidates.Select(c => c.ToModel()).ToList();
            var target = request.Target.ToModel();
            
            var ranked = engine.EvaluateAndRank(candidates, target);
            var best = ranked.FirstOrDefault(e => e.Accepted);
            var (shouldAutoGrab, autoGrabReason) = engine.CheckAutoGrab(ranked);
            
            var result = new EvaluationResultDto
            {
                RankedCandidates = ranked.Select(CandidateEvaluationDto.FromModel).ToList(),
                BestCandidate = best != null ? CandidateEvaluationDto.FromModel(best) : null,
                ShouldAutoGrab = shouldAutoGrab,
                AutoGrabReason = autoGrabReason,
                TotalCandidates = ranked.Count,
                AcceptedCandidates = ranked.Count(e => e.Accepted),
                RejectedCandidates = ranked.Count(e => !e.Accepted)
            };
            
            return Results.Ok(result);
        })
        .WithName("EvaluateCandidates")
        .WithDescription("Evaluates and ranks a list of candidates against a target. Returns ranked list with explanations.");

        // POST evaluate single candidate
        group.MapPost("/evaluate/single", (
            IDecisionEngine engine,
            EvaluateSingleCandidateRequest request) =>
        {
            var evaluation = engine.Evaluate(request.Candidate.ToModel(), request.Target.ToModel());
            return Results.Ok(CandidateEvaluationDto.FromModel(evaluation));
        })
        .WithName("EvaluateSingleCandidate")
        .WithDescription("Evaluates a single candidate against a target. Returns detailed explanation.");

        // GET explanation for a candidate evaluation (useful for debugging)
        group.MapPost("/explain", (
            IDecisionEngine engine,
            EvaluateCandidatesRequest request) =>
        {
            var candidates = request.Candidates.Select(c => c.ToModel()).ToList();
            var target = request.Target.ToModel();
            
            var ranked = engine.EvaluateAndRank(candidates, target);
            
            // Return detailed explanations for all candidates
            var explanations = ranked.Select(e => new
            {
                candidate = new
                {
                    e.Candidate.Id,
                    e.Candidate.ReleaseTitle,
                    e.Candidate.Source
                },
                accepted = e.Accepted,
                score = e.Score,
                rejectionReason = e.RejectionReason?.ToString(),
                explanation = new
                {
                    e.Explanation.Summary,
                    e.Explanation.BaseScore,
                    e.Explanation.Penalties,
                    e.Explanation.FinalScore,
                    scoringFactors = e.Explanation.ScoringFactors.Select(f => new
                    {
                        f.Name,
                        f.Points,
                        f.Reason
                    }),
                    checks = e.Explanation.Checks.Select(c => new
                    {
                        c.CheckName,
                        c.Passed,
                        c.Details
                    })
                }
            });
            
            return Results.Ok(explanations);
        })
        .WithName("ExplainDecisions")
        .WithDescription("Returns detailed explanations for all candidate evaluations.");
    }
}

