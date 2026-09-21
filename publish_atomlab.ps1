# Publish AtomLab to GitHub: create the repo if needed, push the sources, make a release and
# upload the built game. ADD-ONLY: never deletes a release, a tag or an asset.
#
# ASCII-ONLY BY RULE. PowerShell 5.1 reads a UTF-8-without-BOM .ps1 as cp1251, so Cyrillic
# pasted into a script is mangled AT PARSE TIME - that is how ZombieShooter 4.2 went out with
# a broken title. Release notes live in release_notes_<ver>.md and are read with explicit UTF8.
#
# Usage: powershell -ExecutionPolicy Bypass -File publish_atomlab.ps1 -Ver 1.0
param([string]$Ver = "1.0")
$ErrorActionPreference = "Stop"

$repoName = "atomLab"
$root     = "C:\Users\PC\Desktop\AtomLab"
$proxy    = "http://127.0.0.1:10808"   # GitHub is not reachable directly from this machine

# Token path is assembled from char codes - see the ASCII-ONLY rule above.
$tokDir  = "C:\Users\PC\Desktop\" + [char]0x0442 + [char]0x043E + [char]0x043A + [char]0x0435 + [char]0x043D + [char]0x044B
$tokFile = $tokDir + "\" + [char]0x0433 + [char]0x0438 + [char]0x0442 + " " + [char]0x0442 + [char]0x043E + [char]0x043A + [char]0x0435 + [char]0x043D + ".txt"
if (-not (Test-Path -LiteralPath $tokFile)) { throw "Token file not found: $tokFile" }
$tok = (Get-Content -LiteralPath $tokFile -Raw).Trim()
if ($tok -match '(gh[pousr]_[A-Za-z0-9]+|github_pat_[A-Za-z0-9_]+)') { $tok = $Matches[1] }
$H = @{ Authorization = "Bearer $tok"; "User-Agent" = "AtomLabPublish"; Accept = "application/vnd.github+json" }

# Probe the token BEFORE touching anything: a 401 discovered mid-upload would leave a release
# half filled.
try { $me = Invoke-RestMethod -Uri "https://api.github.com/user" -Headers $H -Proxy $proxy -TimeoutSec 60 }
catch { throw "GitHub token rejected or proxy down. Nothing touched. $($_.Exception.Message)" }
$owner = $me.login
Write-Host ("Token OK: " + $owner)

# ---- repository ----
$repo = $null
try { $repo = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repoName" -Headers $H -Proxy $proxy -TimeoutSec 60 } catch {}
if (-not $repo) {
    $body = @{ name = $repoName; description = "AtomLab - glue atoms, get real substances. Whole periodic table, 118 elements."; private = $false; has_issues = $true } | ConvertTo-Json
    $repo = Invoke-RestMethod -Uri "https://api.github.com/user/repos" -Headers $H -Proxy $proxy -TimeoutSec 120 `
        -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType "application/json; charset=utf-8"
    Write-Host ("Repo created: " + $repo.html_url)
} else {
    Write-Host ("Repo exists: " + $repo.html_url)
}

# ---- push sources ----
# The token goes in a header, NOT into the remote URL: a URL with a token inside is written
# into .git/config and stays on disk forever.
Push-Location $root
# git writes "No such remote" to stderr, and with $ErrorActionPreference = "Stop" PowerShell
# turns ANY native stderr line into a terminating error - the script died here on a message
# that meant nothing was wrong. Check the remote list instead of removing blind.
$remoteUrl = "https://github.com/$owner/$repoName.git"
$haveOrigin = (git remote) -contains "origin"
if ($haveOrigin) { git remote set-url origin $remoteUrl } else { git remote add origin $remoteUrl }
# The header must be ONE argument. Written inline as -c http.extraHeader="Authorization:
# Bearer x" PowerShell splits it on the space, git never sees the token and falls back to
# asking for a username - which in batch mode is a hard "terminal prompts disabled".
$hdrArg = "http.extraHeader=Authorization: Bearer " + $tok
$env:GIT_TERMINAL_PROMPT = "0"
git -c http.proxy=$proxy -c $hdrArg push -u origin HEAD:main
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "git push failed with code $LASTEXITCODE" }
Pop-Location
Write-Host "Sources pushed."

# ---- package the built game ----
$buildDir = Join-Path $root "Build\Windows"
if (-not (Test-Path (Join-Path $buildDir "AtomLab.exe"))) { throw "No build at $buildDir - build first." }
$zip = Join-Path $root ("dist\AtomLab_" + $Ver + "_Windows.zip")
New-Item -ItemType Directory -Force -Path (Split-Path $zip) | Out-Null
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $buildDir "*") -DestinationPath $zip -CompressionLevel Optimal
$zipMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ("Archive: " + $zip + "  " + $zipMb + " MB")

# ---- release ----
# Refuse to touch an existing release: replacing one means deleting it first.
$exists = $null
try { $exists = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repoName/releases/tags/$Ver" -Headers $H -Proxy $proxy -TimeoutSec 60 } catch {}
if ($exists) { throw "Release $Ver already exists: $($exists.html_url). Nothing touched." }

$notesFile = Join-Path $root ("release_notes_" + $Ver + ".md")
$notes = if (Test-Path $notesFile) { [IO.File]::ReadAllText($notesFile, [Text.Encoding]::UTF8) } else { "AtomLab $Ver" }

$body = @{ tag_name = $Ver; name = ("AtomLab " + $Ver); body = $notes; draft = $false; prerelease = $false } | ConvertTo-Json -Depth 3
$rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repoName/releases" -Headers $H -Proxy $proxy -TimeoutSec 120 `
    -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType "application/json; charset=utf-8"
Write-Host ("Release created: " + $rel.html_url)

$uploadBase = $rel.upload_url -replace '\{.*\}$', ''
$uh = @{ Authorization = "Bearer $tok"; "User-Agent" = "AtomLabPublish" }
$name = Split-Path $zip -Leaf
for ($try = 1; $try -le 3; $try++) {
    try {
        Invoke-RestMethod -Uri ($uploadBase + "?name=$name") -Headers $uh -Proxy $proxy -TimeoutSec 900 `
            -Method Post -InFile $zip -ContentType "application/octet-stream" | Out-Null
        break
    } catch {
        Write-Host ("  attempt $try failed: " + $_.Exception.Message)
        if ($try -eq 3) { throw }
        Start-Sleep -Seconds 5
    }
}

# Journal of what actually went out: name, size, sha256. Costs a millisecond, answers later
# the question "is this the same build".
$h = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
Add-Content -Path (Join-Path $root "published_hashes.txt") -Encoding UTF8 `
    -Value ("{0}  {1}  {2} bytes  sha256:{3}  {4}" -f $Ver, $name, (Get-Item $zip).Length, $h, (Get-Date).ToString("yyyy-MM-dd HH:mm"))

# Read the release BACK from GitHub - do not trust the POST alone.
$check = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repoName/releases/tags/$Ver" -Headers $H -Proxy $proxy -TimeoutSec 60
Write-Host ""
Write-Host ("DONE: " + $check.html_url)
Write-Host ("TITLE: " + $check.name)
$check.assets | ForEach-Object { "  {0,-40} {1,7:N1} MB" -f $_.name, ($_.size / 1MB) }
Write-Host ("REPO: " + $repo.html_url)
