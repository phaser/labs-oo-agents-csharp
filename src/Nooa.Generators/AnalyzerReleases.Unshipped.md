; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NOOA0001 | Nooa | Error | Agent class must be partial
NOOA0002 | Nooa | Error | Agentic method must return a task
NOOA0003 | Nooa | Error | Agentic method must not be static
NOOA0004 | Nooa | Error | Generic agent types and methods are not supported
NOOA0005 | Nooa | Error | Agentic method parameters must be passed by value
NOOA0006 | Nooa | Error | Agentic method must be a partial declaration without a body
NOOA0007 | Nooa | Error | Invalid placeholder in a doc comment
NOOA0008 | Nooa | Error | Agent class must implement IAgenticTarget
NOOA0009 | Nooa | Error | Agent class must be public or internal
