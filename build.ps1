<#
    Compila el proyecto sin ensuciar la consola: solo imprime errores y avisos.

    .\build.ps1              Debug
    .\build.ps1 -Release     Release
    .\build.ps1 -Publish     publica los dos .exe de publish\ (secuencial)
#>
param(
    [switch]$Release,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Invoke-Dotnet {
    param([string]$Label, [string[]]$Arguments)

    $output = & dotnet @Arguments 2>&1
    $code = $LASTEXITCODE

    # Solo lo que importa: diagnósticos reales del compilador, sin el ruido de MSBuild.
    $issues = @($output |
        Where-Object { $_ -match ':\s*(error|warning)\s+[A-Z]+\d+' } |
        Select-Object -Unique)

    foreach ($issue in $issues) {
        Write-Output $issue
    }

    if ($code -ne 0) {
        Write-Output "FALLO: $Label (codigo $code)"
        exit $code
    }

    $errors = @($issues | Where-Object { $_ -match ':\s*error\s+' }).Count
    Write-Output "$Label OK ($errors errores)"
}

if (-not $Publish) {
    $configuration = if ($Release) { 'Release' } else { 'Debug' }

    Invoke-Dotnet $configuration @('build', 'PcAnalyzer.slnx', '-c', $configuration, '-v', 'q', '-nologo')
    exit 0
}

# Publicar en serie: en paralelo los dos publish chocan en obj\Release (CS2012).
Invoke-Dotnet 'exe-ligero' @('publish', 'PcAnalyzer.App', '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'false', '-p:PublishSingleFile=true', '-o', "$root\publish\exe-ligero")

Invoke-Dotnet 'exe-portable' @('publish', 'PcAnalyzer.App', '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'true', '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', "$root\publish\exe-portable")

foreach ($folder in 'exe-ligero', 'exe-portable') {
    $exe = Get-Item "$root\publish\$folder\PcAnalyzer.exe"
    Write-Output ("{0}: {1} MB" -f $folder, [math]::Round($exe.Length / 1MB, 1))
}
