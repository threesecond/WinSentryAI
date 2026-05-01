# Code Signing Policy

This document describes the intended code signing policy for WinSentryAI releases.

## Current Status

WinSentryAI v0.9.0 release candidate was published as a portable Windows x64 ZIP. Code signing is being evaluated for the v1.0 release path.

Users should not be asked to disable Smart App Control, SmartScreen, or other Windows security controls as a normal installation step.

## Signing Goals

The goals are:

- Provide a verifiable publisher identity for Windows binaries.
- Protect release assets from tampering after publication.
- Keep the release process reproducible and auditable.
- Maintain a clear boundary between WinSentryAI project artifacts and third-party dependencies.

## Artifacts Intended For Signing

The project intends to sign WinSentryAI-owned release artifacts, such as:

- `WinSentryAI.exe`
- `WinSentryAI.dll`
- Future WinSentryAI installers or packages, if added.

Third-party DLLs from NuGet packages are not re-signed as WinSentryAI-owned binaries. They are distributed under their original upstream licenses and notices.

## Release Integrity

Release ZIP files are temporary build artifacts. After upload to GitHub Releases, local ZIP copies should be deleted or explicitly flagged for deletion. Release pages should list the expected asset name and, when available, checksums or signing information.

## SignPath Foundation

SignPath Foundation is being evaluated as a preferred open-source code signing path. If WinSentryAI is accepted by SignPath Foundation, release signing will follow the SignPath approval and signing workflow.

When using SignPath, the project will:

- Sign only binaries built from this open-source repository.
- Keep the source code and release process public.
- Require release approval before signing.
- Maintain this code signing policy, privacy policy, and security policy.

Free code signing, if approved, would be provided by SignPath.io with a certificate issued to SignPath Foundation.

## Security Boundary

WinSentryAI is a Windows Event Log diagnostic assistant. It is not a vulnerability scanner, exploit tool, or automatic remediation tool. Signed releases must preserve this product boundary.

## Maintainer Responsibility

Maintainers are responsible for:

- Reviewing release contents before signing.
- Ensuring release artifacts do not include local settings, databases, logs, API keys, or private work files.
- Verifying third-party dependency notices when dependencies change.
- Removing stale local release ZIP files after upload.
