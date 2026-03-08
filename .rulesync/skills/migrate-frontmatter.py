#!/usr/bin/env python3
"""
Skill Frontmatter Migration Script

Validates and updates skill frontmatter across the dotnet-agent-harness toolkit.
Supports multiple output formats and optional auto-fix capabilities.

Usage:
    python migrate-frontmatter.py check
    python migrate-frontmatter.py check --skill dotnet-testing-xunit
    python migrate-frontmatter.py check --fix
    python migrate-frontmatter.py check --report migration-report.md
    python migrate-frontmatter.py check --json --output report.json

Dependencies:
    - Python 3.8+
    - PyYAML (pip install pyyaml)
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple

# Try to import PyYAML, provide helpful error if not available
try:
    import yaml
except ImportError:
    print("Error: PyYAML is required but not installed.")
    print("\nInstall it with one of these commands:")
    print("  pip install pyyaml")
    print("  pip3 install pyyaml")
    print("  python3 -m pip install pyyaml")
    print("  apt-get install python3-yaml  (on Debian/Ubuntu)")
    sys.exit(1)


# ═══════════════════════════════════════════════════════════════════════════════
# CONSTANTS AND CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

# Valid categories per TAXONOMY.md
VALID_CATEGORIES: Set[str] = {
    "fundamentals",
    "testing",
    "architecture",
    "web",
    "data",
    "performance",
    "security",
    "operations",
    "ui-frameworks",
    "developer-experience",
}

# Category name mappings (old -> new)
CATEGORY_RENAMES: Dict[str, str] = {
    "devops": "operations",
    "platforms": "ui-frameworks",
    "tooling": "developer-experience",
}

# Valid complexity levels
VALID_COMPLEXITIES: Set[str] = {"beginner", "intermediate", "advanced"}

# Valid target platforms
VALID_TARGETS: Set[str] = {
    "*",
    "claudecode",
    "opencode",
    "codexcli",
    "copilot",
    "geminicli",
    "antigravity",
    "factorydroid",
}

# Subcategory taxonomy extracted from TAXONOMY.md
SUBCATEGORY_TAXONOMY: Dict[str, Set[str]] = {
    "fundamentals": {
        "coding-standards",
        "language-patterns",
        "design-principles",
        "dependency-injection",
        "configuration",
        "diagnostics",
        "overview",
    },
    "testing": {
        "fundamentals",
        "frameworks",
        "assertions",
        "mocking",
        "test-data",
        "integration",
        "specialized",
        "coverage",
    },
    "architecture": {
        "patterns",
        "domain-modeling",
        "messaging",
        "resilience",
        "overview",
    },
    "web": {
        "minimal-apis",
        "mvc",
        "blazor",
        "api-design",
        "security",
        "validation",
    },
    "data": {
        "ef-core",
        "data-access",
        "caching",
        "messaging",
        "serialization",
        "search",
    },
    "performance": {
        "patterns",
        "memory",
        "benchmarking",
        "profiling",
        "aot",
    },
    "security": {
        "owasp",
        "crypto",
        "auth",
        "secrets",
    },
    "operations": {
        "github-actions",
        "azure-devops",
        "containers",
        "deployment",
        "release",
        "ci-cd",
    },
    "ui-frameworks": {
        "maui",
        "wpf",
        "winui",
        "uno",
        "winforms",
        "blazor",
    },
    "developer-experience": {
        "cli",
        "analyzers",
        "msbuild",
        "nuget",
        "project",
        "docs",
        "serena",
        "rulesync",
    },
}

# Required frontmatter fields
REQUIRED_FIELDS: List[str] = [
    "name",
    "description",
    "category",
    "subcategory",
    "complexity",
    "targets",
]

# Default values for missing fields
DEFAULT_VALUES: Dict[str, Any] = {
    "license": "MIT",
    "version": "1.0.0",
    "author": "dotnet-agent-harness",
    "invocable": True,
    "tags": [],
    "related_skills": [],
}

# ANSI color codes for console output
COLORS = {
    "reset": "\033[0m",
    "bold": "\033[1m",
    "dim": "\033[2m",
    "red": "\033[91m",
    "green": "\033[92m",
    "yellow": "\033[93m",
    "blue": "\033[94m",
    "magenta": "\033[95m",
    "cyan": "\033[96m",
}


# ═══════════════════════════════════════════════════════════════════════════════
# DATA CLASSES
# ═══════════════════════════════════════════════════════════════════════════════


@dataclass
class ValidationError:
    """Represents a single validation error for a skill."""

    field: str
    message: str
    severity: str = "error"  # error, warning, info
    auto_fixable: bool = False
    suggested_value: Optional[str] = None


@dataclass
class SkillValidationResult:
    """Contains all validation results for a single skill."""

    skill_path: Path
    skill_name: str
    frontmatter: Dict[str, Any] = field(default_factory=dict)
    errors: List[ValidationError] = field(default_factory=list)
    warnings: List[ValidationError] = field(default_factory=list)
    is_valid: bool = False
    has_fixable_issues: bool = False
    fixed: bool = False


@dataclass
class MigrationReport:
    """Aggregated results from the entire migration run."""

    total_skills: int = 0
    valid_skills: int = 0
    skills_with_errors: int = 0
    skills_with_warnings: int = 0
    skills_fixed: int = 0
    results: List[SkillValidationResult] = field(default_factory=list)
    started_at: datetime = field(default_factory=datetime.now)
    completed_at: Optional[datetime] = None

    @property
    def duration_seconds(self) -> float:
        """Calculate the duration of the migration run."""
        end = self.completed_at or datetime.now()
        return (end - self.started_at).total_seconds()


# ═══════════════════════════════════════════════════════════════════════════════
# UTILITY FUNCTIONS
# ═══════════════════════════════════════════════════════════════════════════════


def colorize(text: str, color: str) -> str:
    """Apply ANSI color to text if terminal supports it."""
    if not sys.stdout.isatty():
        return text
    return f"{COLORS.get(color, '')}{text}{COLORS['reset']}"


def print_header(text: str) -> None:
    """Print a formatted header."""
    print(f"\n{colorize('=' * 60, 'bold')}")
    print(colorize(f"  {text}", "bold"))
    print(f"{colorize('=' * 60, 'bold')}\n")


def print_subheader(text: str) -> None:
    """Print a formatted subheader."""
    print(f"\n{colorize(text, 'cyan')}")
    print(colorize("-" * len(text), "dim"))


def parse_frontmatter(content: str) -> Tuple[Optional[Dict[str, Any]], Optional[str]]:
    """
    Parse YAML frontmatter from markdown content.

    Args:
        content: Raw file content

    Returns:
        Tuple of (frontmatter_dict, error_message)
        If parsing fails, returns (None, error_message)
    """
    # Early exit: Check for frontmatter delimiters
    if not content.startswith("---"):
        return None, "No frontmatter found (file does not start with ---)"

    # Find the end of frontmatter
    end_match = re.search(r"\n---\s*\n", content[3:])
    if not end_match:
        return None, "Malformed frontmatter: closing --- not found"

    frontmatter_text = content[3 : 3 + end_match.start()]

    try:
        frontmatter = yaml.safe_load(frontmatter_text)
        if not isinstance(frontmatter, dict):
            return None, "Frontmatter is not a valid YAML dictionary"
        return frontmatter, None
    except yaml.YAMLError as e:
        return None, f"YAML parsing error: {e}"


def extract_frontmatter_text(content: str) -> Optional[str]:
    """Extract the raw frontmatter text from markdown content."""
    if not content.startswith("---"):
        return None

    end_match = re.search(r"\n---\s*\n", content[3:])
    if not end_match:
        return None

    return content[: 3 + end_match.end()]


def find_skills_directory() -> Path:
    """Find the skills directory relative to the script location."""
    script_dir = Path(__file__).parent.resolve()
    return script_dir


def discover_skills(
    skills_dir: Path, specific_skill: Optional[str] = None
) -> List[Path]:
    """
    Discover all skill directories containing SKILL.md files.

    Args:
        skills_dir: Root directory to search
        specific_skill: If provided, only return this specific skill

    Returns:
        List of paths to SKILL.md files
    """
    if specific_skill:
        skill_path = skills_dir / specific_skill / "SKILL.md"
        if skill_path.exists():
            return [skill_path]
        return []

    skill_files = []
    for item in skills_dir.iterdir():
        if item.is_dir() and not item.name.startswith("."):
            skill_md = item / "SKILL.md"
            if skill_md.exists():
                skill_files.append(skill_md)

    return sorted(skill_files)


# ═══════════════════════════════════════════════════════════════════════════════
# FIELD VALIDATORS
# ═══════════════════════════════════════════════════════════════════════════════


def validate_name(value: Any, result: SkillValidationResult) -> List[ValidationError]:
    """Validate the 'name' field."""
    errors = []

    if value is None:
        errors.append(
            ValidationError(
                field="name",
                message="Required field 'name' is missing",
                severity="error",
                auto_fixable=True,
                suggested_value=result.skill_name,
            )
        )
    elif not isinstance(value, str):
        errors.append(
            ValidationError(
                field="name",
                message=f"'name' must be a string, got {type(value).__name__}",
                severity="error",
            )
        )
    elif not value:
        errors.append(
            ValidationError(
                field="name",
                message="'name' cannot be empty",
                severity="error",
                auto_fixable=True,
                suggested_value=result.skill_name,
            )
        )
    elif value != result.skill_name:
        errors.append(
            ValidationError(
                field="name",
                message=f"'name' ({value}) does not match directory name ({result.skill_name})",
                severity="warning",
                auto_fixable=True,
                suggested_value=result.skill_name,
            )
        )

    return errors


def validate_description(value: Any) -> List[ValidationError]:
    """Validate the 'description' field."""
    errors = []

    if value is None:
        errors.append(
            ValidationError(
                field="description",
                message="Required field 'description' is missing",
                severity="error",
                auto_fixable=False,
            )
        )
    elif not isinstance(value, str):
        errors.append(
            ValidationError(
                field="description",
                message=f"'description' must be a string, got {type(value).__name__}",
                severity="error",
            )
        )
    elif not value.strip():
        errors.append(
            ValidationError(
                field="description",
                message="'description' cannot be empty or whitespace",
                severity="error",
                auto_fixable=False,
            )
        )
    elif len(value) > 200:
        errors.append(
            ValidationError(
                field="description",
                message=f"'description' is too long ({len(value)} chars, max 200 recommended)",
                severity="warning",
            )
        )

    return errors


def validate_category(value: Any) -> List[ValidationError]:
    """Validate the 'category' field."""
    errors = []

    if value is None:
        errors.append(
            ValidationError(
                field="category",
                message="Required field 'category' is missing",
                severity="error",
                auto_fixable=False,
            )
        )
    elif not isinstance(value, str):
        errors.append(
            ValidationError(
                field="category",
                message=f"'category' must be a string, got {type(value).__name__}",
                severity="error",
            )
        )
    elif value in CATEGORY_RENAMES:
        new_name = CATEGORY_RENAMES[value]
        errors.append(
            ValidationError(
                field="category",
                message=f"Category '{value}' is deprecated, use '{new_name}'",
                severity="warning",
                auto_fixable=True,
                suggested_value=new_name,
            )
        )
    elif value not in VALID_CATEGORIES:
        errors.append(
            ValidationError(
                field="category",
                message=f"Invalid category '{value}'. Valid: {', '.join(sorted(VALID_CATEGORIES))}",
                severity="error",
                auto_fixable=False,
            )
        )

    return errors


def validate_subcategory(value: Any, category: Optional[str]) -> List[ValidationError]:
    """Validate the 'subcategory' field."""
    errors = []

    if value is None:
        errors.append(
            ValidationError(
                field="subcategory",
                message="Required field 'subcategory' is missing",
                severity="error",
                auto_fixable=False,
            )
        )
        return errors

    if not isinstance(value, str):
        errors.append(
            ValidationError(
                field="subcategory",
                message=f"'subcategory' must be a string, got {type(value).__name__}",
                severity="error",
            )
        )
        return errors

    # Get the effective category (handle renamed categories)
    effective_category = category
    if category in CATEGORY_RENAMES:
        effective_category = CATEGORY_RENAMES[category]

    if effective_category and effective_category in SUBCATEGORY_TAXONOMY:
        valid_subcategories = SUBCATEGORY_TAXONOMY[effective_category]
        if value not in valid_subcategories:
            errors.append(
                ValidationError(
                    field="subcategory",
                    message=f"Invalid subcategory '{value}' for category '{effective_category}'",
                    severity="error",
                    auto_fixable=False,
                )
            )
            # Add hint about valid subcategories
            valid_list = ", ".join(sorted(valid_subcategories))
            errors.append(
                ValidationError(
                    field="subcategory",
                    message=f"Valid subcategories for '{effective_category}': {valid_list}",
                    severity="info",
                )
            )

    return errors


def validate_complexity(value: Any) -> List[ValidationError]:
    """Validate the 'complexity' field."""
    errors = []

    if value is None:
        errors.append(
            ValidationError(
                field="complexity",
                message="Required field 'complexity' is missing",
                severity="error",
                auto_fixable=True,
                suggested_value="intermediate",
            )
        )
    elif not isinstance(value, str):
        errors.append(
            ValidationError(
                field="complexity",
                message=f"'complexity' must be a string, got {type(value).__name__}",
                severity="error",
            )
        )
    elif value not in VALID_COMPLEXITIES:
        errors.append(
            ValidationError(
                field="complexity",
                message=f"Invalid complexity '{value}'. Valid: {', '.join(sorted(VALID_COMPLEXITIES))}",
                severity="error",
                auto_fixable=True,
                suggested_value="intermediate",
            )
        )

    return errors


def validate_targets(value: Any) -> List[ValidationError]:
    """Validate the 'targets' field."""
    errors = []

    if value is None:
        errors.append(
            ValidationError(
                field="targets",
                message="Required field 'targets' is missing",
                severity="error",
                auto_fixable=True,
                suggested_value="['*']",
            )
        )
    elif isinstance(value, str):
        # Single target as string - convert to list
        errors.append(
            ValidationError(
                field="targets",
                message="'targets' should be a list, not a string",
                severity="warning",
                auto_fixable=True,
                suggested_value=f"['{value}']",
            )
        )
    elif not isinstance(value, list):
        errors.append(
            ValidationError(
                field="targets",
                message=f"'targets' must be a list, got {type(value).__name__}",
                severity="error",
            )
        )
    elif not value:
        errors.append(
            ValidationError(
                field="targets",
                message="'targets' list is empty",
                severity="warning",
                auto_fixable=True,
                suggested_value="['*']",
            )
        )
    else:
        # Validate each target
        invalid_targets = [t for t in value if t not in VALID_TARGETS]
        if invalid_targets:
            errors.append(
                ValidationError(
                    field="targets",
                    message=f"Invalid target(s): {', '.join(invalid_targets)}",
                    severity="error",
                )
            )

    return errors


# ═══════════════════════════════════════════════════════════════════════════════
# CORE VALIDATION LOGIC
# ═══════════════════════════════════════════════════════════════════════════════


def validate_skill(skill_path: Path) -> SkillValidationResult:
    """
    Validate a single skill's frontmatter.

    Args:
        skill_path: Path to the SKILL.md file

    Returns:
        SkillValidationResult with all validation details
    """
    skill_name = skill_path.parent.name
    result = SkillValidationResult(skill_path=skill_path, skill_name=skill_name)

    # Early exit: Check file exists and is readable
    if not skill_path.exists():
        result.errors.append(
            ValidationError(
                field="file",
                message=f"File does not exist: {skill_path}",
                severity="error",
            )
        )
        return result

    try:
        content = skill_path.read_text(encoding="utf-8")
    except Exception as e:
        result.errors.append(
            ValidationError(
                field="file", message=f"Failed to read file: {e}", severity="error"
            )
        )
        return result

    # Parse frontmatter
    frontmatter, error = parse_frontmatter(content)
    if error:
        result.errors.append(
            ValidationError(field="frontmatter", message=error, severity="error")
        )
        return result

    result.frontmatter = frontmatter or {}

    # Validate each required field
    all_errors = []

    # Name validation
    all_errors.extend(validate_name(result.frontmatter.get("name"), result))

    # Description validation
    all_errors.extend(validate_description(result.frontmatter.get("description")))

    # Category validation
    category_errors = validate_category(result.frontmatter.get("category"))
    all_errors.extend(category_errors)

    # Subcategory validation (depends on category)
    category = result.frontmatter.get("category")
    all_errors.extend(
        validate_subcategory(result.frontmatter.get("subcategory"), category)
    )

    # Complexity validation
    all_errors.extend(validate_complexity(result.frontmatter.get("complexity")))

    # Targets validation
    all_errors.extend(validate_targets(result.frontmatter.get("targets")))

    # Separate errors and warnings
    for err in all_errors:
        if err.severity == "error":
            result.errors.append(err)
        else:
            result.warnings.append(err)
        if err.auto_fixable:
            result.has_fixable_issues = True

    # Determine overall validity
    result.is_valid = len(result.errors) == 0

    return result


def fix_skill(result: SkillValidationResult) -> bool:
    """
    Attempt to auto-fix issues in a skill's frontmatter.

    Args:
        result: The validation result with issues to fix

    Returns:
        True if fixes were applied, False otherwise
    """
    if not result.has_fixable_issues:
        return False

    skill_path = result.skill_path
    try:
        content = skill_path.read_text(encoding="utf-8")
    except Exception as e:
        result.errors.append(
            ValidationError(
                field="file",
                message=f"Failed to read file for fixing: {e}",
                severity="error",
            )
        )
        return False

    frontmatter = dict(result.frontmatter)
    fixes_applied = []

    # Apply fixes based on validation errors
    for error in result.errors + result.warnings:
        if not error.auto_fixable or not error.suggested_value:
            continue

        field = error.field

        if field == "name":
            frontmatter["name"] = error.suggested_value
            fixes_applied.append(f"Set 'name' to '{error.suggested_value}'")

        elif field == "category" and error.suggested_value:
            frontmatter["category"] = error.suggested_value
            fixes_applied.append(
                f"Updated category from deprecated name to '{error.suggested_value}'"
            )

        elif field == "complexity":
            frontmatter["complexity"] = error.suggested_value
            fixes_applied.append(f"Set 'complexity' to '{error.suggested_value}'")

        elif field == "targets":
            # Parse the suggested value
            try:
                targets = eval(error.suggested_value)
                frontmatter["targets"] = targets
                fixes_applied.append(f"Fixed 'targets' format")
            except:
                frontmatter["targets"] = ["*"]
                fixes_applied.append("Set 'targets' to ['*']")

    # Ensure required fields exist with defaults if still missing
    for field in REQUIRED_FIELDS:
        if field not in frontmatter or frontmatter[field] is None:
            if field in DEFAULT_VALUES:
                frontmatter[field] = DEFAULT_VALUES[field]
                fixes_applied.append(f"Added missing '{field}' with default value")

    # Generate new frontmatter YAML
    try:
        # Custom YAML representer to handle multiline strings nicely
        def str_representer(dumper, data):
            if "\n" in data:
                return dumper.represent_scalar("tag:yaml.org,2002:str", data, style="|")
            return dumper.represent_scalar("tag:yaml.org,2002:str", data)

        yaml.add_representer(str, str_representer)

        new_frontmatter = yaml.dump(
            frontmatter, default_flow_style=False, allow_unicode=True, sort_keys=False
        )

        # Find the old frontmatter boundaries
        old_frontmatter_text = extract_frontmatter_text(content)
        if not old_frontmatter_text:
            return False

        # Replace frontmatter in content
        new_frontmatter_text = f"---\n{new_frontmatter}---\n"
        new_content = content.replace(old_frontmatter_text, new_frontmatter_text)

        # Write back
        skill_path.write_text(new_content, encoding="utf-8")

        result.fixed = True
        return True

    except Exception as e:
        result.errors.append(
            ValidationError(
                field="fix", message=f"Failed to apply fixes: {e}", severity="error"
            )
        )
        return False


# ═══════════════════════════════════════════════════════════════════════════════
# REPORT GENERATORS
# ═══════════════════════════════════════════════════════════════════════════════


def generate_console_report(report: MigrationReport, verbose: bool = False) -> None:
    """Generate a console report of the migration results."""
    print_header("Skill Frontmatter Migration Report")

    print(f"Started:  {report.started_at.strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Duration: {report.duration_seconds:.2f}s")
    print()

    # Summary statistics
    print_subheader("Summary")
    print(f"  {colorize('Total skills scanned:', 'bold')}   {report.total_skills}")
    print(f"  {colorize('Valid skills:', 'green')}         {report.valid_skills}")
    print(f"  {colorize('Skills with errors:', 'red')}     {report.skills_with_errors}")
    print(
        f"  {colorize('Skills with warnings:', 'yellow')} {report.skills_with_warnings}"
    )
    if report.skills_fixed > 0:
        print(f"  {colorize('Skills fixed:', 'green')}        {report.skills_fixed}")

    # Valid skills
    valid_results = [r for r in report.results if r.is_valid]
    if valid_results:
        print_subheader(f"Valid Skills ({len(valid_results)})")
        for result in valid_results:
            print(f"  {colorize('✓', 'green')} {result.skill_name}")
            if verbose and result.warnings:
                for warning in result.warnings:
                    print(
                        f"    {colorize('⚠', 'yellow')} {warning.field}: {warning.message}"
                    )

    # Invalid skills
    invalid_results = [r for r in report.results if not r.is_valid]
    if invalid_results:
        print_subheader(f"Skills with Errors ({len(invalid_results)})")
        for result in invalid_results:
            print(f"\n  {colorize('✗', 'red')} {colorize(result.skill_name, 'bold')}")
            print(f"    Path: {result.skill_path}")
            for error in result.errors:
                fixable_marker = (
                    f" {colorize('[auto-fixable]', 'green')}"
                    if error.auto_fixable
                    else ""
                )
                print(
                    f"    {colorize('•', 'red')} {colorize(error.field, 'yellow')}: {error.message}{fixable_marker}"
                )
            if verbose and result.warnings:
                for warning in result.warnings:
                    print(
                        f"    {colorize('⚠', 'yellow')} {warning.field}: {warning.message}"
                    )

    # Fixed skills
    fixed_results = [r for r in report.results if r.fixed]
    if fixed_results:
        print_subheader(f"Skills Fixed ({len(fixed_results)})")
        for result in fixed_results:
            print(f"  {colorize('✓', 'green')} {result.skill_name}")

    # Summary message
    print()
    if report.skills_with_errors == 0:
        print(colorize("✓ All skills have valid frontmatter!", "green"))
    else:
        print(
            colorize(
                f"✗ {report.skills_with_errors} skill(s) have validation errors.", "red"
            )
        )
        if any(r.has_fixable_issues for r in invalid_results):
            print(colorize("  Run with --fix to auto-fix eligible issues.", "yellow"))


def generate_json_report(
    report: MigrationReport, output_path: Optional[str] = None
) -> str:
    """Generate a JSON report of the migration results."""
    data = {
        "metadata": {
            "started_at": report.started_at.isoformat(),
            "completed_at": report.completed_at.isoformat()
            if report.completed_at
            else None,
            "duration_seconds": report.duration_seconds,
        },
        "summary": {
            "total_skills": report.total_skills,
            "valid_skills": report.valid_skills,
            "skills_with_errors": report.skills_with_errors,
            "skills_with_warnings": report.skills_with_warnings,
            "skills_fixed": report.skills_fixed,
        },
        "skills": [],
    }

    for result in report.results:
        skill_data = {
            "name": result.skill_name,
            "path": str(result.skill_path),
            "is_valid": result.is_valid,
            "fixed": result.fixed,
            "frontmatter": result.frontmatter,
            "errors": [
                {
                    "field": e.field,
                    "message": e.message,
                    "severity": e.severity,
                    "auto_fixable": e.auto_fixable,
                    "suggested_value": e.suggested_value,
                }
                for e in result.errors
            ],
            "warnings": [
                {
                    "field": w.field,
                    "message": w.message,
                    "severity": w.severity,
                    "auto_fixable": w.auto_fixable,
                }
                for w in result.warnings
            ],
        }
        data["skills"].append(skill_data)

    json_output = json.dumps(data, indent=2)

    if output_path:
        Path(output_path).write_text(json_output, encoding="utf-8")

    return json_output


def generate_markdown_report(
    report: MigrationReport, output_path: Optional[str] = None
) -> str:
    """Generate a Markdown report of the migration results."""
    lines = [
        "# Skill Frontmatter Migration Report",
        "",
        f"**Generated:** {report.started_at.strftime('%Y-%m-%d %H:%M:%S')}",
        f"**Duration:** {report.duration_seconds:.2f}s",
        "",
        "## Summary",
        "",
        "| Metric | Count |",
        "|--------|-------|",
        f"| Total Skills Scanned | {report.total_skills} |",
        f"| Valid Skills | {report.valid_skills} |",
        f"| Skills with Errors | {report.skills_with_errors} |",
        f"| Skills with Warnings | {report.skills_with_warnings} |",
    ]

    if report.skills_fixed > 0:
        lines.append(f"| Skills Fixed | {report.skills_fixed} |")

    lines.extend(["", "---", ""])

    # Valid skills section
    valid_results = [r for r in report.results if r.is_valid]
    if valid_results:
        lines.extend(
            [
                f"## Valid Skills ({len(valid_results)})",
                "",
            ]
        )
        for result in valid_results:
            lines.append(f"- ✓ **{result.skill_name}**")
        lines.append("")

    # Invalid skills section
    invalid_results = [r for r in report.results if not r.is_valid]
    if invalid_results:
        lines.extend(
            [
                f"## Skills with Errors ({len(invalid_results)})",
                "",
            ]
        )

        for result in invalid_results:
            lines.extend(
                [
                    f"### {result.skill_name}",
                    "",
                    f"**Path:** `{result.skill_path}`",
                    "",
                    "**Errors:**",
                    "",
                ]
            )

            for error in result.errors:
                fixable = " (auto-fixable)" if error.auto_fixable else ""
                lines.append(f"- ❌ **{error.field}:** {error.message}{fixable}")

            lines.append("")

            if result.warnings:
                lines.extend(
                    [
                        "**Warnings:**",
                        "",
                    ]
                )
                for warning in result.warnings:
                    lines.append(f"- ⚠️ **{warning.field}:** {warning.message}")
                lines.append("")

    # Fixed skills section
    fixed_results = [r for r in report.results if r.fixed]
    if fixed_results:
        lines.extend(
            [
                f"## Skills Fixed ({len(fixed_results)})",
                "",
            ]
        )
        for result in fixed_results:
            lines.append(f"- ✓ **{result.skill_name}**")
        lines.append("")

    # Action items
    lines.extend(
        [
            "## Action Items",
            "",
        ]
    )

    if report.skills_with_errors == 0:
        lines.append("✅ **All skills have valid frontmatter!**")
    else:
        lines.append(f"⚠️ **{report.skills_with_errors} skill(s) require attention.**")
        lines.append("")
        lines.append("Run the following command to auto-fix eligible issues:")
        lines.append("```bash")
        lines.append("python migrate-frontmatter.py check --fix")
        lines.append("```")

    lines.append("")

    md_output = "\n".join(lines)

    if output_path:
        Path(output_path).write_text(md_output, encoding="utf-8")

    return md_output


# ═══════════════════════════════════════════════════════════════════════════════
# MAIN COMMAND HANDLERS
# ═══════════════════════════════════════════════════════════════════════════════


def handle_check_command(args: argparse.Namespace) -> int:
    """
    Execute the 'check' command.

    Args:
        args: Parsed command-line arguments

    Returns:
        Exit code (0 for success, 1 for errors found)
    """
    skills_dir = find_skills_directory()
    skill_files = discover_skills(skills_dir, args.skill)

    if args.skill and not skill_files:
        print(colorize(f"Error: Skill '{args.skill}' not found in {skills_dir}", "red"))
        return 1

    report = MigrationReport(total_skills=len(skill_files))

    print(colorize(f"Scanning {len(skill_files)} skill(s)...", "blue"))

    # Validate each skill
    for skill_path in skill_files:
        result = validate_skill(skill_path)
        report.results.append(result)

        if result.is_valid:
            report.valid_skills += 1
        else:
            report.skills_with_errors += 1

        if result.warnings:
            report.skills_with_warnings += 1

    # Apply fixes if requested
    if args.fix:
        print(colorize("Applying auto-fixes...", "yellow"))
        for result in report.results:
            if result.has_fixable_issues:
                if fix_skill(result):
                    report.skills_fixed += 1
                    # Re-validate to update status
                    new_result = validate_skill(result.skill_path)
                    result.errors = new_result.errors
                    result.warnings = new_result.warnings
                    result.is_valid = new_result.is_valid
                    result.frontmatter = new_result.frontmatter

    report.completed_at = datetime.now()

    # Generate output based on format
    if args.json:
        json_output = generate_json_report(report, args.output)
        if not args.output:
            print(json_output)
        else:
            print(colorize(f"JSON report written to: {args.output}", "green"))

    elif args.markdown or args.report:
        md_output = generate_markdown_report(report, args.report or args.output)
        if not args.report and not args.output:
            print(md_output)
        else:
            output_path = args.report or args.output
            print(colorize(f"Markdown report written to: {output_path}", "green"))

    else:
        generate_console_report(report, verbose=args.verbose)

    # Return appropriate exit code
    return 0 if report.skills_with_errors == 0 else 1


def main() -> int:
    """Main entry point for the CLI."""
    parser = argparse.ArgumentParser(
        description="Validate and migrate skill frontmatter for dotnet-agent-harness",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Check all skills
  python migrate-frontmatter.py check

  # Check specific skill
  python migrate-frontmatter.py check --skill dotnet-testing-xunit

  # Check and auto-fix issues
  python migrate-frontmatter.py check --fix

  # Generate JSON report
  python migrate-frontmatter.py check --json --output report.json

  # Generate Markdown report
  python migrate-frontmatter.py check --report migration-report.md
        """,
    )

    subparsers = parser.add_subparsers(dest="command", help="Available commands")

    # 'check' command
    check_parser = subparsers.add_parser("check", help="Validate skill frontmatter")
    check_parser.add_argument("--skill", help="Check a specific skill by name")
    check_parser.add_argument(
        "--fix", action="store_true", help="Auto-fix eligible issues"
    )
    check_parser.add_argument("--json", action="store_true", help="Output as JSON")
    check_parser.add_argument(
        "--markdown", action="store_true", help="Output as Markdown"
    )
    check_parser.add_argument("--report", help="Generate Markdown report to file")
    check_parser.add_argument(
        "--output", "-o", help="Output file path (for JSON/Markdown)"
    )
    check_parser.add_argument(
        "--verbose",
        "-v",
        action="store_true",
        help="Show verbose output including warnings",
    )

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return 1

    if args.command == "check":
        return handle_check_command(args)

    return 0


if __name__ == "__main__":
    sys.exit(main())
