<#
.SYNOPSIS
    Post-bootstrap verification script. Checks that the template was applied correctly.
.DESCRIPTION
    Runs structural and (optionally) content checks against a repo that completed
    the 3-phase bootstrap process. Returns exit code 0 when all checks pass, 1 otherwise.

    If ongoing-tasks/_bootstrap/decisions.md still exists on disk, bonus content checks
    run automatically (ADR stubs, deleted recipes).
.EXAMPLE
    .\scripts\verify-bootstrap.ps1
    .\scripts\verify-bootstrap.ps1 -RepoRoot C:\repos\my-project
#>
[CmdletBinding()]
param(
    [string]$RepoRoot
)

if (-not $RepoRoot) {
    if ($PSScriptRoot) {
        $RepoRoot = Split-Path $PSScriptRoot -Parent
    } else {
        $RepoRoot = $PWD.Path
    }
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# -- Helpers -------------------------------------------------------------------

$script:Passes  = 0
$script:Warns   = 0
$script:Fails   = 0

function Write-Pass  { param([string]$Msg) $script:Passes++; Write-Host "  PASS  $Msg" -ForegroundColor Green }
function Write-Warn  { param([string]$Msg) $script:Warns++;  Write-Host "  WARN  $Msg" -ForegroundColor Yellow }
function Write-Fail  { param([string]$Msg) $script:Fails++;  Write-Host "  FAIL  $Msg" -ForegroundColor Red }

function Resolve-Repo { param([string]$Relative) Join-Path $RepoRoot $Relative }

# -- Checks --------------------------------------------------------------------

Write-Host "`nVerify Bootstrap -- $RepoRoot`n" -ForegroundColor Cyan
Write-Host "-- Structural checks --" -ForegroundColor Cyan

# 1. No <<...>> placeholders outside _template/ folders
$placeholderHits = Get-ChildItem -Path $RepoRoot -Recurse -File -Include '*.md','*.json','*.yaml','*.yml','*.xml','*.ps1','*.cs','*.csproj','*.py','*.ts','*.js' |
    Where-Object { $_.FullName -notmatch '[/\\]_template[/\\]' -and $_.FullName -notmatch '[/\\]_bootstrap[/\\]' } |
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -and $content -match '<<[A-Z_]+>>') {
            [PSCustomObject]@{
                File    = $_.FullName.Substring($RepoRoot.Length).TrimStart('\','/')
                Matches = ([regex]::Matches($content, '<<[A-Z_]+>>') | ForEach-Object { $_.Value }) -join ', '
            }
        }
    }

if ($placeholderHits) {
    Write-Fail "Unresolved placeholders found:"
    $placeholderHits | ForEach-Object { Write-Host "         $($_.File): $($_.Matches)" -ForegroundColor Red }
} else {
    Write-Pass "No unresolved placeholders outside _template/ folders"
}

# 2. At least one of CLAUDE.md / .github/copilot-instructions.md must exist
$claudePath  = Resolve-Repo 'CLAUDE.md'
$copilotPath = Resolve-Repo '.github\copilot-instructions.md'
$claudeExists  = Test-Path $claudePath
$copilotExists = Test-Path $copilotPath

if (-not $claudeExists -and -not $copilotExists) {
    Write-Fail "Neither CLAUDE.md nor .github/copilot-instructions.md exists"
} else {
    $which = @()
    if ($claudeExists)  { $which += 'CLAUDE.md' }
    if ($copilotExists) { $which += '.github/copilot-instructions.md' }
    Write-Pass "Instruction file(s) present: $($which -join ', ')"

    # If both exist, they must match
    if ($claudeExists -and $copilotExists) {
        $claudeHash  = (Get-FileHash $claudePath  -Algorithm SHA256).Hash
        $copilotHash = (Get-FileHash $copilotPath -Algorithm SHA256).Hash
        if ($claudeHash -eq $copilotHash) {
            Write-Pass "CLAUDE.md and copilot-instructions.md are in sync"
        } else {
            Write-Fail "CLAUDE.md and copilot-instructions.md differ (must be byte-identical)"
        }
    }
}

# 3. docs/architecture/overview.md exists and has content beyond placeholders
$archPath = Resolve-Repo 'docs\architecture\overview.md'
if (-not (Test-Path $archPath)) {
    Write-Fail "docs/architecture/overview.md is missing"
} else {
    $archContent = Get-Content $archPath -Raw
    if ($archContent -match '<<[A-Z_]+>>') {
        Write-Fail "docs/architecture/overview.md still contains placeholders"
    } elseif ($archContent.Trim().Length -lt 50) {
        Write-Warn "docs/architecture/overview.md exists but looks minimal (< 50 chars)"
    } else {
        Write-Pass "docs/architecture/overview.md present with content"
    }
}

# 4. .ignix/review-instructions.md
$ignixPath = Resolve-Repo '.ignix\review-instructions.md'
if (-not (Test-Path $ignixPath)) {
    Write-Warn ".ignix/review-instructions.md missing (OK if repo doesn't use automated PR review)"
} else {
    Write-Pass ".ignix/review-instructions.md present"
}

# 5. template-readme.md exists
$tplReadme = Resolve-Repo 'template-readme.md'
if (-not (Test-Path $tplReadme)) {
    Write-Fail "template-readme.md missing"
} else {
    Write-Pass "template-readme.md present"
}

# 6. ADR folder has more than just readme + template
$adrDir = Resolve-Repo 'adr'
if (-not (Test-Path $adrDir)) {
    Write-Warn "adr/ folder missing"
} else {
    $adrFiles = @(Get-ChildItem $adrDir -File -Filter '*.md' |
        Where-Object { $_.Name -ne 'readme.md' -and $_.Name -ne '_template.md' })
    if ($adrFiles.Count -eq 0) {
        Write-Warn "adr/ has no ADR stubs (expected if no pending ADRs were listed in interview)"
    } else {
        Write-Pass "adr/ contains $($adrFiles.Count) ADR file(s)"
    }
}

# 7. _bootstrap/ cleanup reminder
$bootstrapDir = Resolve-Repo 'ongoing-tasks\_bootstrap'
if (Test-Path $bootstrapDir) {
    Write-Warn "ongoing-tasks/_bootstrap/ still exists -- delete after reviewing the Apply report"
} else {
    Write-Pass "ongoing-tasks/_bootstrap/ cleaned up"
}

# 8. prompts/ folder has expected core files
$expectedPrompts = @(
    'prompts\readme.md'
    'prompts\session-start.md'
    'prompts\execution.md'
)
$missingPrompts = $expectedPrompts | Where-Object { -not (Test-Path (Resolve-Repo $_)) }
if ($missingPrompts) {
    Write-Fail "Missing core prompt files: $($missingPrompts -join ', ')"
} else {
    Write-Pass "Core prompt files present (prompts/readme.md, session-start.md, execution.md)"
}

# -- Bonus content checks (only if decisions.md exists) -----------------------

$decisionsPath = Resolve-Repo 'ongoing-tasks\_bootstrap\decisions.md'
if (Test-Path $decisionsPath) {
    Write-Host "`n-- Content checks (decisions.md found) --" -ForegroundColor Cyan

    $decisionsContent = Get-Content $decisionsPath -Raw

    # Bonus A: Each pending ADR title should have a matching stub file
    $adrSection = $false
    $pendingTitles = @()
    foreach ($line in (Get-Content $decisionsPath)) {
        if ($line -match '^\s*##\s+Pending ADRs') { $adrSection = $true; continue }
        if ($adrSection -and $line -match '^\s*##\s') { break }
        if ($adrSection -and $line -match '^\s*-\s+(.+)') {
            $title = $Matches[1].Trim()
            if ($title -and $title -ne '...') { $pendingTitles += $title }
        }
    }

    if ($pendingTitles.Count -gt 0) {
        if (Test-Path $adrDir) {
            $adrStubs = @(Get-ChildItem $adrDir -File -Filter '*.md' |
                Where-Object { $_.Name -ne 'readme.md' -and $_.Name -ne '_template.md' })
        } else {
            $adrStubs = @()
        }

        if ($adrStubs.Count -ge $pendingTitles.Count) {
            $stubCount = $adrStubs.Count
            $titleCount = $pendingTitles.Count
            Write-Pass "ADR stub count ($stubCount) matches or exceeds pending titles ($titleCount)"
        } else {
            $stubCount = $adrStubs.Count
            $titleCount = $pendingTitles.Count
            Write-Fail "Expected $titleCount ADR stubs but found $stubCount"
            $titleList = $pendingTitles -join ', '
            Write-Host "         Pending titles: $titleList" -ForegroundColor Red
        }
    } else {
        Write-Pass "No pending ADR titles in decisions.md -- nothing to check"
    }

    # Bonus B: Recipes marked DELETE should not exist
    $recipeSection = $false
    $deletePattern = '\|\s*(.+?\.md)\s*\|\s*DELETE'
    foreach ($line in (Get-Content $decisionsPath)) {
        if ($line -match '^\s*##\s+Recipe decisions') { $recipeSection = $true; continue }
        if ($recipeSection -and $line -match '^\s*##\s') { break }
        if ($recipeSection -and $line -match $deletePattern) {
            $stub = $Matches[1].Trim()
            $stubPath = Resolve-Repo "docs\methodology\$stub"
            if (Test-Path $stubPath) {
                Write-Fail "Recipe marked DELETE still exists: docs/methodology/$stub"
            } else {
                Write-Pass "Deleted recipe confirmed gone: docs/methodology/$stub"
            }
        }
    }
}

# -- Summary -------------------------------------------------------------------

Write-Host "`n-- Summary --" -ForegroundColor Cyan
Write-Host "  $script:Passes passed, $script:Warns warnings, $script:Fails failures`n"

if ($script:Fails -gt 0) {
    Write-Host "BOOTSTRAP VERIFICATION FAILED" -ForegroundColor Red
    exit 1
} elseif ($script:Warns -gt 0) {
    Write-Host "BOOTSTRAP VERIFICATION PASSED WITH WARNINGS" -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "BOOTSTRAP VERIFICATION PASSED" -ForegroundColor Green
    exit 0
}
