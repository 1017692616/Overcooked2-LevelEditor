param(
    [string]$GameDir = "",
    [string]$AssetRipperExport = "",
    [string]$ToolsDir = "",
    [switch]$InstallMissing,
    [switch]$LaunchAssetRipper,
    [switch]$SkipDlc
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok($Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

function Write-Warn($Message) {
    Write-Host "WARN $Message" -ForegroundColor Yellow
}

function Get-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "..")).Path
}

function Get-DefaultToolsDir($RepoRoot) {
    if ($env:OC2LE_TOOLS_DIR) {
        return $env:OC2LE_TOOLS_DIR
    }
    if (Test-Path "F:\") {
        return "F:\OC2LevelEditorTools"
    }
    return (Join-Path $RepoRoot ".oc2-tools")
}

function Get-CommandPath($Name) {
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    return $null
}

function Get-SteamRoots {
    $roots = New-Object System.Collections.Generic.List[string]
    $registryPaths = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam"
    )
    foreach ($path in $registryPaths) {
        try {
            $item = Get-ItemProperty -Path $path -ErrorAction Stop
            if ($item.SteamPath) { $roots.Add(($item.SteamPath -replace "/", "\")) }
            if ($item.InstallPath) { $roots.Add(($item.InstallPath -replace "/", "\")) }
        } catch {
        }
    }
    $roots.Add("C:\Program Files (x86)\Steam")
    $roots.Add("C:\Program Files\Steam")
    $roots.Add("D:\SteamLibrary")
    $roots.Add("E:\SteamLibrary")
    $roots.Add("F:\SteamLibrary")
    return $roots | Select-Object -Unique
}

function Find-OC2GameDir {
    param([string]$ManualGameDir)

    if ($ManualGameDir) {
        $resolved = Resolve-Path $ManualGameDir -ErrorAction SilentlyContinue
        if ($resolved) {
            return $resolved.Path
        }
        throw "GameDir was provided but does not exist: $ManualGameDir"
    }

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($root in Get-SteamRoots) {
        if (-not (Test-Path $root)) { continue }
        $candidates.Add((Join-Path $root "steamapps\common\Overcooked! 2"))

        $libraryFile = Join-Path $root "steamapps\libraryfolders.vdf"
        if (Test-Path $libraryFile) {
            $text = Get-Content -Raw $libraryFile
            foreach ($match in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
                $libraryPath = $match.Groups[1].Value -replace "\\\\", "\"
                $candidates.Add((Join-Path $libraryPath "steamapps\common\Overcooked! 2"))
            }
        }
    }

    foreach ($drive in [System.IO.DriveInfo]::GetDrives()) {
        if (-not $drive.IsReady) { continue }
        $candidates.Add((Join-Path $drive.RootDirectory.FullName "SteamLibrary\steamapps\common\Overcooked! 2"))
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate "Overcooked2_Data\StreamingAssets\Windows")) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Find-Unity2017 {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\2017.4.8f1\Editor\Unity.exe",
        "C:\Program Files\Unity\Editor\Unity.exe",
        "D:\Unity\Hub\Editor\2017.4.8f1\Editor\Unity.exe",
        "E:\Unity\Hub\Editor\2017.4.8f1\Editor\Unity.exe",
        "F:\Unity\Hub\Editor\2017.4.8f1\Editor\Unity.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }
    return $null
}

function Ensure-Python {
    $python = Get-CommandPath "python"
    if ($python) {
        Write-Ok "Python found: $python"
        return "python"
    }

    $py = Get-CommandPath "py"
    if ($py) {
        Write-Ok "Python launcher found: $py"
        return "py -3"
    }

    if (-not $InstallMissing) {
        Write-Warn "Python 3 was not found. Re-run with -InstallMissing, or install Python 3 manually."
        return $null
    }

    $winget = Get-CommandPath "winget"
    if (-not $winget) {
        Write-Warn "winget was not found. Please install Python 3 manually."
        return $null
    }

    Write-Step "Installing Python 3 with winget"
    & winget install -e --id Python.Python.3.12
    $python = Get-CommandPath "python"
    if ($python) {
        return "python"
    }
    Write-Warn "Python was installed, but this PowerShell session cannot see it yet. Close PowerShell and run the script again."
    return $null
}

function Invoke-Python {
    param(
        [string]$PythonCommand,
        [string[]]$Arguments
    )

    if ($PythonCommand -eq "py -3") {
        & py -3 @Arguments
    } else {
        & $PythonCommand @Arguments
    }
}

function Ensure-UnityPy {
    param([string]$PythonCommand)

    if (-not $PythonCommand) { return }

    Write-Step "Checking Python package: UnityPy"
    Invoke-Python $PythonCommand @("-m", "pip", "show", "UnityPy") | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "UnityPy is installed"
        return
    }

    Write-Step "Installing UnityPy"
    Invoke-Python $PythonCommand @("-m", "pip", "install", "UnityPy")
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install UnityPy"
    }
}

function Ensure-StreamingAssetsLink {
    param(
        [string]$RepoRoot,
        [string]$GameRoot
    )

    $source = Join-Path $GameRoot "Overcooked2_Data\StreamingAssets\Windows"
    $target = Join-Path $RepoRoot "Assets\StreamingAssets\Windows"

    if (-not (Test-Path $source)) {
        throw "Game StreamingAssets folder was not found: $source"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null

    if (Test-Path $target) {
        $item = Get-Item $target
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Ok "StreamingAssets junction already exists: $target"
            return
        }
        Write-Warn "A real folder already exists at $target. It was left untouched."
        return
    }

    Write-Step "Creating local StreamingAssets junction"
    cmd /c mklink /J "$target" "$source" | Out-Null
    if (-not (Test-Path $target)) {
        throw "Failed to create junction: $target"
    }
    Write-Ok "$target -> $source"
}

function Get-AssetRipperExe {
    param([string]$ToolsRoot)

    $assetRipperDir = Join-Path $ToolsRoot "AssetRipper"
    if (-not (Test-Path $assetRipperDir)) {
        return $null
    }
    $exe = Get-ChildItem -Path $assetRipperDir -Filter "AssetRipper*.exe" -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        Select-Object -First 1
    if ($exe) {
        return $exe.FullName
    }
    return $null
}

function Install-AssetRipper {
    param([string]$ToolsRoot)

    $existing = Get-AssetRipperExe $ToolsRoot
    if ($existing) {
        Write-Ok "AssetRipper found: $existing"
        return $existing
    }

    if (-not $InstallMissing) {
        Write-Warn "AssetRipper was not found. Re-run with -InstallMissing to download it."
        return $null
    }

    Write-Step "Downloading latest AssetRipper release"
    $assetRipperDir = Join-Path $ToolsRoot "AssetRipper"
    New-Item -ItemType Directory -Force -Path $assetRipperDir | Out-Null

    $release = Invoke-RestMethod "https://api.github.com/repos/AssetRipper/AssetRipper/releases/latest"
    $asset = $release.assets |
        Where-Object { $_.name -match "\.zip$" -and $_.name -match "(win|windows|x64)" } |
        Select-Object -First 1
    if (-not $asset) {
        $asset = $release.assets | Where-Object { $_.name -match "\.zip$" } | Select-Object -First 1
    }
    if (-not $asset) {
        throw "Could not find an AssetRipper zip asset in the latest GitHub release."
    }

    $zipPath = Join-Path $assetRipperDir $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $assetRipperDir -Force

    $exe = Get-AssetRipperExe $ToolsRoot
    if (-not $exe) {
        throw "AssetRipper downloaded, but no AssetRipper executable was found under $assetRipperDir"
    }
    Write-Ok "AssetRipper ready: $exe"
    return $exe
}

function Copy-AssemblyCSharp {
    param(
        [string]$RepoRoot,
        [string]$ExportPath
    )

    if (-not $ExportPath) {
        Write-Warn "No -AssetRipperExport path was provided. Assembly-CSharp copy was skipped."
        return
    }

    $resolved = Resolve-Path $ExportPath -ErrorAction SilentlyContinue
    if (-not $resolved) {
        throw "AssetRipper export path does not exist: $ExportPath"
    }

    $exportRoot = $resolved.Path
    $source = $exportRoot
    if ((Split-Path -Leaf $source) -ne "Assembly-CSharp") {
        $source = Join-Path $exportRoot "Assets\Scripts\Assembly-CSharp"
    }
    if (-not (Test-Path $source)) {
        throw "Could not find Assembly-CSharp under: $exportRoot"
    }

    $target = Join-Path $RepoRoot "Assets\Scripts\Assembly-CSharp"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null

    Write-Step "Copying Assembly-CSharp"
    Copy-Item -Path $source -Destination (Split-Path -Parent $target) -Recurse -Force
    Write-Ok "Copied to $target"

    $patchSource = Join-Path $RepoRoot "Assembly-CSharp-Patch"
    if (Test-Path $patchSource) {
        Write-Step "Applying Assembly-CSharp-Patch"
        Copy-Item -Path (Join-Path $patchSource "*") -Destination $target -Recurse -Force
        Write-Ok "Patch files copied"
    }
}

function Generate-DlcReferences {
    param(
        [string]$RepoRoot,
        [string]$GameRoot,
        [string]$PythonCommand
    )

    if ($SkipDlc) {
        Write-Warn "DLC reference generation skipped by -SkipDlc"
        return
    }
    if (-not $PythonCommand) {
        Write-Warn "Python is missing, so DLC reference generation was skipped."
        return
    }

    $script = Join-Path $RepoRoot "tools\generate_dlc_assets.py"
    $streamingAssets = Join-Path $GameRoot "Overcooked2_Data\StreamingAssets\Windows"
    Write-Step "Generating DLC lightweight references"
    Push-Location $RepoRoot
    try {
        Invoke-Python $PythonCommand @($script, "--game-streaming-assets", $streamingAssets)
        if ($LASTEXITCODE -ne 0) {
            throw "DLC reference generation failed"
        }
    } finally {
        Pop-Location
    }
}

$repoRoot = Get-RepoRoot
if (-not $ToolsDir) {
    $ToolsDir = Get-DefaultToolsDir $repoRoot
}
New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null

Write-Step "Overcooked! 2 Level Editor setup"
Write-Host "Project: $repoRoot"
Write-Host "Tools:   $ToolsDir"

Write-Step "Checking Unity"
$unity = Find-Unity2017
if ($unity) {
    Write-Ok "Unity 2017 found: $unity"
} else {
    Write-Warn "Unity 2017.4.8f1 was not found in common locations. Install it with Unity Hub, then open this project."
}

Write-Step "Finding Overcooked! 2"
$gameRoot = Find-OC2GameDir $GameDir
if (-not $gameRoot) {
    throw "Overcooked! 2 was not found. Pass -GameDir `"F:\SteamLibrary\steamapps\common\Overcooked! 2`" if needed."
}
Write-Ok "Game found: $gameRoot"

Ensure-StreamingAssetsLink $repoRoot $gameRoot

Write-Step "Checking Python"
$pythonCommand = Ensure-Python
if ($pythonCommand) {
    Ensure-UnityPy $pythonCommand
}

Write-Step "Checking AssetRipper"
$assetRipperExe = Install-AssetRipper $ToolsDir
if ($LaunchAssetRipper -and $assetRipperExe) {
    Start-Process -FilePath $assetRipperExe -ArgumentList "`"$gameRoot\Overcooked2_Data`"" -WorkingDirectory (Split-Path -Parent $assetRipperExe)
    Write-Warn "AssetRipper was launched. Export a Unity Project, then re-run this script with -AssetRipperExport pointing to the exported project."
}

Copy-AssemblyCSharp $repoRoot $AssetRipperExport
Generate-DlcReferences $repoRoot $gameRoot $pythonCommand

Write-Step "Done"
Write-Host "Next:"
Write-Host "1. Open this folder with Unity 2017.4.8f1."
Write-Host "2. In Unity, run Tools > OC2 Setup > Check Environment."
Write-Host "3. Open Assets/LevelSets/codex_demo/scenes/s_codex_demo_1.unity."
