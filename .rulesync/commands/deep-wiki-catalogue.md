---
description: Generate hierarchical wiki structure as JSON
targets: ['*']
---

# /deep-wiki:catalogue

Generate only the hierarchical wiki structure as JSON

## Usage

````bash
/deep-wiki:catalogue
```text

## Description

Creates a structured table of contents without generating actual content pages. Useful for:

- Reviewing wiki structure before full generation
- Custom page generation pipelines
- Integration with other tools

## Output Format

```json
{
  "title": "Project Wiki",
  "sections": [
    {
      "title": "Architecture",
      "path": "architecture",
      "subsections": [...]
    }
  ]
}
```text
````
