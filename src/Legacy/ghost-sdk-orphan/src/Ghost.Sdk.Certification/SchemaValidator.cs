namespace Ghost.Sdk.Certification;

/// <summary>
/// Default implementation of ISchemaValidator.
/// Validates SpiderSpec schema for correctness.
/// </summary>
public sealed class SchemaValidator : ISchemaValidator
{
    /// <inheritdoc />
    public Task<SchemaValidationResult> ValidateAsync(
        Contracts.SpiderSpec spec,
        CancellationToken ct = default)
    {
        List<string> errors = [];

        // Validate that the entry step exists
        if (!spec.Steps.ContainsKey(spec.EntryStepId))
        {
            errors.Add($"Entry step '{spec.EntryStepId}' not found in steps");
        }

        // Validate each step
        foreach (var (stepId, step) in spec.Steps)
        {
            // Validate step kind
            if (string.IsNullOrWhiteSpace(step.Kind))
            {
                errors.Add($"Step '{stepId}' has empty kind");
                continue;
            }

            // Validate step kind is known
            if (!IsValidStepKind(step.Kind))
            {
                errors.Add($"Step '{stepId}' has unknown kind '{step.Kind}'");
            }

            // Step-specific validation
            switch (step.Kind)
            {
                case Contracts.StepKinds.HttpFetch:
                case Contracts.StepKinds.BrowserFetch:
                    if (step is not Contracts.HttpFetchStep fetchStep)
                    {
                        errors.Add($"Step '{stepId}' of kind '{step.Kind}' is not a fetch step");
                    }
                    else if (!spec.Steps.ContainsKey(fetchStep.RequestStepId))
                    {
                        errors.Add($"Step '{stepId}' references non-existent request step '{fetchStep.RequestStepId}'");
                    }
                    break;

                case Contracts.StepKinds.ParseHtml:
                    if (step is not Contracts.ParseHtmlStep parseStep)
                    {
                        errors.Add($"Step '{stepId}' of kind '{step.Kind}' is not a parse step");
                    }
                    else if (!spec.Steps.ContainsKey(parseStep.ResponseStepId))
                    {
                        errors.Add($"Step '{stepId}' references non-existent response step '{parseStep.ResponseStepId}'");
                    }
                    else if (parseStep.Selectors.Count == 0)
                    {
                        errors.Add($"Step '{stepId}' has no selectors");
                    }
                    break;

                case Contracts.StepKinds.EmitItem:
                    if (step is not Contracts.EmitItemStep emitStep)
                    {
                        errors.Add($"Step '{stepId}' of kind '{step.Kind}' is not an emit step");
                    }
                    else if (!spec.Steps.ContainsKey(emitStep.ParseStepId))
                    {
                        errors.Add($"Step '{stepId}' references non-existent parse step '{emitStep.ParseStepId}'");
                    }
                    else if (string.IsNullOrWhiteSpace(emitStep.ItemType))
                    {
                        errors.Add($"Step '{stepId}' has empty item type");
                    }
                    break;

                case Contracts.StepKinds.FollowLinks:
                    if (step is not Contracts.FollowLinksStep followStep)
                    {
                        errors.Add($"Step '{stepId}' of kind '{step.Kind}' is not a follow links step");
                    }
                    else if (!spec.Steps.ContainsKey(followStep.ParseStepId))
                    {
                        errors.Add($"Step '{stepId}' references non-existent parse step '{followStep.ParseStepId}'");
                    }
                    else if (string.IsNullOrWhiteSpace(followStep.LinkSelector))
                    {
                        errors.Add($"Step '{stepId}' has empty link selector");
                    }
                    break;
            }
        }

        // Validate there are no orphaned steps (steps that are never referenced)
        var referencedSteps = new HashSet<string> { spec.EntryStepId };
        foreach (var step in spec.Steps.Values)
        {
            switch (step.Kind)
            {
                case Contracts.StepKinds.HttpFetch:
                case Contracts.StepKinds.BrowserFetch:
                    if (step is Contracts.HttpFetchStep fetchStep)
                    {
                        referencedSteps.Add(fetchStep.RequestStepId);
                    }
                    break;

                case Contracts.StepKinds.ParseHtml:
                    if (step is Contracts.ParseHtmlStep parseStep)
                    {
                        referencedSteps.Add(parseStep.ResponseStepId);
                    }
                    break;

                case Contracts.StepKinds.EmitItem:
                    if (step is Contracts.EmitItemStep emitStep)
                    {
                        referencedSteps.Add(emitStep.ParseStepId);
                    }
                    break;

                case Contracts.StepKinds.FollowLinks:
                    if (step is Contracts.FollowLinksStep followStep)
                    {
                        referencedSteps.Add(followStep.ParseStepId);
                    }
                    break;
            }
        }

        var orphanedSteps = spec.Steps.Keys.Where(id => !referencedSteps.Contains(id)).ToList();
        foreach (var orphanedStep in orphanedSteps)
        {
            errors.Add($"Step '{orphanedStep}' is never referenced (orphaned)");
        }

        return Task.FromResult(new SchemaValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors));
    }

    /// <summary>
    /// Checks if a step kind is valid.
    /// </summary>
    /// <param name="kind">The step kind to check</param>
    /// <returns>True if the step kind is valid, false otherwise</returns>
    private static bool IsValidStepKind(string kind)
    {
        return kind switch
        {
            Contracts.StepKinds.BuildRequest => true,
            Contracts.StepKinds.HttpFetch => true,
            Contracts.StepKinds.BrowserFetch => true,
            Contracts.StepKinds.ParseHtml => true,
            Contracts.StepKinds.EmitItem => true,
            Contracts.StepKinds.FollowLinks => true,
            _ => false
        };
    }
}
