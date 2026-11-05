param (
    [string]$TargetDir = "dist\FlowWindows-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$TargetDir = $TargetDir.Trim('\"').TrimEnd('\')
$makepri = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe"
$binX64 = Join-Path $repoRoot "src\FlowWindows.App\bin\Release\net8.0-windows10.0.19041.0\win-x64"
$staging = Join-Path $repoRoot "dist\pri_staging"
$assetsDir = Join-Path $repoRoot "src\FlowWindows.App\Assets"

# Ensure absolute target dir
if (-not [System.IO.Path]::IsPathRooted($TargetDir)) {
    $TargetDir = Join-Path $repoRoot $TargetDir
}

Write-Host "Staging files for PRI generation from '$binX64'..."

# 1. Prepare clean staging folder
if (Test-Path $staging) {
    Remove-Item -Recurse -Force $staging
}
New-Item -ItemType Directory -Path "$staging\Pages", "$staging\Controls", "$staging\Assets" -Force | Out-Null

# 2. Copy compiled XBFs to root of staging
Copy-Item "$binX64\*.xbf" -Destination $staging
Copy-Item "$binX64\Pages\*.xbf" -Destination "$staging\Pages"
Copy-Item "$binX64\Controls\*.xbf" -Destination "$staging\Controls"

# 3. Copy Assets and referenced PRIs
Copy-Item "$assetsDir\*" -Destination "$staging\Assets" -Recurse
if (Test-Path "$binX64\Microsoft.UI.pri") {
    Copy-Item "$binX64\Microsoft.UI.pri" -Destination "$staging\Microsoft.UI.pri"
}
if (Test-Path "$binX64\Microsoft.UI.Xaml.Controls.pri") {
    Copy-Item "$binX64\Microsoft.UI.Xaml.Controls.pri" -Destination "$staging\Microsoft.UI.Xaml.Controls.pri"
}

# 4. Generate priconfig.xml without packaging split but with PRI indexer
$priConfigContent = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<resources targetOsVersion="10.0.0" majorVersion="1">
	<index root="\" startIndexAt="\">
		<default>
			<qualifier name="Language" value="en-US"/>
			<qualifier name="Contrast" value="standard"/>
			<qualifier name="Scale" value="100"/>
			<qualifier name="HomeRegion" value="001"/>
			<qualifier name="TargetSize" value="256"/>
			<qualifier name="LayoutDirection" value="LTR"/>
			<qualifier name="Theme" value="dark"/>
			<qualifier name="AlternateForm" value=""/>
			<qualifier name="DXFeatureLevel" value="DX9"/>
			<qualifier name="Configuration" value=""/>
			<qualifier name="DeviceFamily" value="Universal"/>
			<qualifier name="Custom" value=""/>
		</default>
		<indexer-config type="folder" foldernameAsQualifier="true" filenameAsQualifier="true" qualifierDelimiter="."/>
		<indexer-config type="PRI"/>
	</index>
</resources>
"@

Set-Content -Path "$staging\priconfig.xml" -Value $priConfigContent -Encoding UTF8

# 5. Run makepri
$outputPri = "$staging\resources.pri"
& $makepri new /pr $staging /cf "$staging\priconfig.xml" /of $outputPri /o

# 6. Deploy to TargetDir
if (Test-Path $TargetDir) {
    Write-Host "Deploying PRI to $TargetDir..."
    Copy-Item $outputPri -Destination "$TargetDir\resources.pri" -Force
    Copy-Item $outputPri -Destination "$TargetDir\FlowWindows.App.pri" -Force
    
    if ((Resolve-Path $TargetDir).Path -ne (Resolve-Path $binX64).Path) {
        Write-Host "Deploying XBF to $TargetDir..."
        Copy-Item "$binX64\*.xbf" -Destination $TargetDir -Force
        New-Item -ItemType Directory -Path "$TargetDir\Pages", "$TargetDir\Controls" -Force | Out-Null
        Copy-Item "$binX64\Pages\*.xbf" -Destination "$TargetDir\Pages" -Force
        Copy-Item "$binX64\Controls\*.xbf" -Destination "$TargetDir\Controls" -Force
    }
    
    if (-not (Test-Path "$TargetDir\Assets")) {
        New-Item -ItemType Directory -Path "$TargetDir\Assets" -Force | Out-Null
    }
    Copy-Item "$assetsDir\*" -Destination "$TargetDir\Assets" -Recurse -Force
}

Write-Host "PRI build and staging complete."
