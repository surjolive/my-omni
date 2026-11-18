# MY OMNI agents

This repository follows the native Windows workbench architecture that already exists in the C# application. The project should keep the current WinForms implementation stable while adding the modular AI, workspace, terminal, and IDE concepts described in the product brief.

## Agent workflow

1. Project analysis
2. Task planning
3. User approval for destructive actions
4. Execution in the smallest safe scope
5. Validation against the project-specific build path
6. Final reporting with explicit limits and evidence

## Required guardrails

- Preserve the current OmniRoute integration and secure API storage strategy.
- Never hardcode secrets or expose API keys in application code.
- Do not claim feature completion without running the project build or a relevant verification step.
- Avoid replacing working functionality with a broad migration unless the existing app is explicitly being rebuilt.
- Keep environment-specific constraints explicit, especially around Windows-only controls and native runtime packaging.

## Agent modules

- Planner Agent: scope changes, break work into phases, record risk and dependencies.
- Coding Agent: implement code changes in the existing app or scaffold modules without breaking current behavior.
- Debugging Agent: reproduce issues, isolate root cause, and verify fixes with build or test evidence.
- Research Agent: inspect APIs and runtime contracts before assuming behavior, especially OmniRoute and Ollama.
- Testing Agent: validate modules with the smallest relevant test and capture pass/fail evidence.
- Documentation Agent: keep architecture docs, setup notes, and process steps aligned with the real implementation.
- Code Review Agent: inspect for security, user consent, and destructive operation safeguards.
- Project Analysis Agent: compare current state against the target IDE architecture and call out missing pieces honestly.

## Scope for this repository

The current implementation is a native Windows desktop app. The future layered architecture should be adapted around that fact rather than replaced outright.
