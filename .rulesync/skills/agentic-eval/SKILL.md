---
name: agentic-eval
location: file:///.rulesync/skills/agentic-eval/SKILL.md
description: >-
  Patterns and techniques for evaluating and improving AI agent outputs.
targets: ['*']
license: MIT
metadata:
  author: GitHub, Inc. (derived)
  version: '0.0.1'
---

Portions derived from github/awesome-copilot (MIT License). Used under MIT License.

# Agentic Evaluation Patterns

Patterns for self-improvement through iterative evaluation and refinement, built using the `.NET` and
`Microsoft.Extensions.AI` ecosystem.

## Overview

Evaluation patterns enable agents to assess and improve their own outputs, moving beyond single-shot generation to
iterative refinement loops.

`Generate → Evaluate → Critique → Refine → Output`

## When to Use

- **Quality-critical generation**: Code, reports, analysis requiring high accuracy
- **Tasks with clear evaluation criteria**: Defined success metrics exist
- **Content requiring specific standards**: Style guides, compliance, formatting

## Pattern 1: Basic Reflection

Agent evaluates and improves its own output through self-critique.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class BasicReflection(IChatClient chatClient)
{
    public async Task<string> ReflectAndRefineAsync(string task, string[] criteria, int maxIterations = 3, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync($"Complete this task:\n{task}", cancellationToken: cancellationToken);
        var output = response.Text ?? string.Empty;

        for (int i = 0; i < maxIterations; i++)
        {
            var prompt = $"""
                Evaluate this output against criteria:
                {string.Join(", ", criteria)}

                Output:
                {output}

                Rate each criteria. Return ONLY a JSON object where keys are the criteria and values are objects with a 'status' ("PASS" or "FAIL") and 'feedback' string.
                """;

            var critiqueResponse = await chatClient.GetResponseAsync(
                prompt,
                new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
                cancellationToken);

            var critiqueText = critiqueResponse.Text ?? "{}";
            Dictionary<string, CritiqueResult>? critiqueData = null;

            try
            {
                critiqueData = JsonSerializer.Deserialize<Dictionary<string, CritiqueResult>>(critiqueText);
            }
            catch (JsonException)
            {
                // Fallback to empty if json parsing fails
                critiqueData = new Dictionary<string, CritiqueResult>();
            }

            if (critiqueData != null && critiqueData.Count > 0 && critiqueData.Values.All(c => c.Status == "PASS"))
            {
                return output;
            }

            var failed = critiqueData?
                .Where(kvp => kvp.Value.Status == "FAIL")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Feedback) ?? new Dictionary<string, string>();

            if (failed.Count == 0)
            {
                // If nothing explicitly failed but parsing succeeded, break out to avoid unguided infinite loops
                break;
            }

            var failedJson = JsonSerializer.Serialize(failed);

            var refinePrompt = $"Improve the original output to address these failures: {failedJson}\nOriginal Output: {output}";
            var improvedResponse = await chatClient.GetResponseAsync(refinePrompt, cancellationToken: cancellationToken);
            output = improvedResponse.Text ?? string.Empty;
        }

        return output;
    }

    private class CritiqueResult
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("feedback")]
        public string Feedback { get; set; } = string.Empty;
    }
}
```

**Key insight**: Use structured JSON output for reliable parsing of critique results. In `.NET`, you can also use
`IChatClient` directly with structured output models to avoid manual deserialization checking.

## Pattern 2: Evaluator-Optimizer

Separate generation and evaluation into distinct components for clearer responsibilities.

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class EvaluatorOptimizer(IChatClient chatClient, double scoreThreshold = 0.8)
{
    public async Task<string> GenerateAsync(string task, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync($"Complete: {task}", cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public async Task<EvaluationResult> EvaluateAsync(string output, string task, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
            Evaluate output for task: {{task}}

            Output:
            {{output}}

            Return JSON in this format: { "overall_score": 0.0, "dimensions": { "accuracy": 0.0, "clarity": 0.0 } }
            """;

        var response = await chatClient.GetResponseAsync(
            prompt,
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);

        var jsonText = response.Text ?? "{}";

        try
        {
            return JsonSerializer.Deserialize<EvaluationResult>(jsonText) ?? new EvaluationResult();
        }
        catch (JsonException)
        {
            return new EvaluationResult();
        }
    }

    public async Task<string> OptimizeAsync(string output, EvaluationResult feedback, CancellationToken cancellationToken = default)
    {
        var feedbackJson = JsonSerializer.Serialize(feedback);
        var prompt = $"Improve based on feedback: {feedbackJson}\nOutput: {output}";
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public async Task<string> RunAsync(string task, int maxIterations = 3, CancellationToken cancellationToken = default)
    {
        var output = await GenerateAsync(task, cancellationToken);

        for (int i = 0; i < maxIterations; i++)
        {
            var evaluation = await EvaluateAsync(output, task, cancellationToken);
            if (evaluation.OverallScore >= scoreThreshold)
            {
                break;
            }
            output = await OptimizeAsync(output, evaluation, cancellationToken);
        }

        return output;
    }
}

public class EvaluationResult
{
    [JsonPropertyName("overall_score")]
    public double OverallScore { get; set; }

    [JsonPropertyName("dimensions")]
    public Dictionary<string, double> Dimensions { get; set; } = new();
}
```

## Pattern 3: Code-Specific Reflection

Test-driven refinement loop for code generation.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class CodeReflector(IChatClient chatClient)
{
    public async Task<string> ReflectAndFixAsync(string spec, int maxIterations = 3, CancellationToken cancellationToken = default)
    {
        var codeResponse = await chatClient.GetResponseAsync($"Write C# code for: {spec}", cancellationToken: cancellationToken);
        var code = codeResponse.Text ?? string.Empty;

        var testsResponse = await chatClient.GetResponseAsync($"Generate xUnit tests for: {spec}\nCode: {code}", cancellationToken: cancellationToken);
        var tests = testsResponse.Text ?? string.Empty;

        for (int i = 0; i < maxIterations; i++)
        {
            var result = await RunTestsAsync(code, tests, cancellationToken);
            if (result.Success)
            {
                return code;
            }

            var fixResponse = await chatClient.GetResponseAsync($"Fix error: {result.Error}\nCode: {code}", cancellationToken: cancellationToken);
            code = fixResponse.Text ?? string.Empty;
        }

        return code;
    }

    // Stub for actual test execution
    private Task<TestResult> RunTestsAsync(string code, string tests, CancellationToken cancellationToken = default)
    {
        // In a real implementation, you would compile and run the tests dynamically
        return Task.FromResult(new TestResult { Success = false, Error = "Mock test failure" });
    }

    private class TestResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
```

## Evaluation Strategies

### Outcome-Based

Evaluate whether output achieves the expected result.

```csharp
public async Task<string> EvaluateOutcomeAsync(string task, string output, string expected, CancellationToken cancellationToken = default)
{
    var response = await chatClient.GetResponseAsync(
        $"Does output achieve expected outcome? Task: {task}, Expected: {expected}, Output: {output}",
        cancellationToken: cancellationToken
    );
    return response.Text ?? string.Empty;
}
```

### LLM-as-Judge

Use LLM to compare and rank outputs.

```csharp
public async Task<string> LlmJudgeAsync(string outputA, string outputB, string criteria, CancellationToken cancellationToken = default)
{
    var response = await chatClient.GetResponseAsync(
        $"Compare outputs A and B for {criteria}. Which is better and why?\n\nOutput A:\n{outputA}\n\nOutput B:\n{outputB}",
        cancellationToken: cancellationToken
    );
    return response.Text ?? string.Empty;
}
```

### Rubric-Based

Score outputs against weighted dimensions.

```csharp
public class RubricDimension
{
    public double Weight { get; set; }
}

public async Task<double> EvaluateWithRubricAsync(string output, Dictionary<string, RubricDimension> rubric, CancellationToken cancellationToken = default)
{
    var dimensions = string.Join(", ", rubric.Keys);
    var prompt = $"Rate 1-5 for each dimension: {dimensions}\nOutput: {output}\n\nReturn ONLY a JSON dictionary where keys are dimensions and values are numbers.";

    var response = await chatClient.GetResponseAsync(
        prompt,
        new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
        cancellationToken);

    var jsonText = response.Text ?? "{}";
    Dictionary<string, double> scores;

    try
    {
        scores = JsonSerializer.Deserialize<Dictionary<string, double>>(jsonText) ?? new Dictionary<string, double>();
    }
    catch (JsonException)
    {
        scores = new Dictionary<string, double>();
    }

    double totalScore = 0;
    foreach (var dimension in rubric.Keys)
    {
        if (scores.TryGetValue(dimension, out var score))
        {
            totalScore += score * rubric[dimension].Weight;
        }
    }

    return totalScore / 5.0; // Normalize
}
```

## Best Practices

| Practice              | Rationale                                               |
| --------------------- | ------------------------------------------------------- |
| **Clear criteria**    | Define specific, measurable evaluation criteria upfront |
| **Iteration limits**  | Set max iterations (3-5) to prevent infinite loops      |
| **Convergence check** | Stop if output score isn't improving between iterations |
| **Log history**       | Keep full trajectory for debugging and analysis         |
| **Structured output** | Use JSON for reliable parsing of evaluation results     |

## Quick Start Checklist

### Setup

- [ ] Define evaluation criteria/rubric
- [ ] Set score threshold for "good enough"
- [ ] Configure max iterations (default: 3)

### Implementation

- [ ] Implement `GenerateAsync()`
- [ ] Implement `EvaluateAsync()` with structured output
- [ ] Implement `OptimizeAsync()`
- [ ] Wire up the refinement loop

### Safety

- [ ] Add convergence detection
- [ ] Log all iterations for debugging
- [ ] Handle evaluation parse failures gracefully
