# compilar.ps1
# Compila QRRemito.cs -> bin\CRUFLQRRemito.dll con el compilador C# que trae Windows (.NET Framework 4.x).
# No requiere Visual Studio.
#
# USO (PowerShell normal, desde cualquier carpeta):
#   powershell -ExecutionPolicy Bypass -File .\ufl\compilar.ps1
#
# IMPORTANTE:
#   - /platform:x86 es obligatorio: Crystal Reports XI R2 y el visor de Bejerman son de 32 bits.
#   - El nombre CRUFLQRRemito es obligatorio: Crystal (u2lcom.dll) solo detecta componentes
#     cuyo ProgID empieza con "CRUFL".
#   - Si la DLL ya esta registrada y en uso (Crystal, Bejerman o un PowerShell de 32 bits que la
#     cargo), la compilacion falla con CS0016: cerrar esos procesos y volver a compilar.

$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { Write-Host "No se encontro csc.exe de .NET Framework 4.x: $csc" -ForegroundColor Red; exit 1 }

$fuente = Join-Path $PSScriptRoot "QRRemito.cs"
$bin    = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$salida = Join-Path $bin "CRUFLQRRemito.dll"

& $csc /nologo /target:library /platform:x86 /optimize+ "/out:$salida" $fuente
if ($LASTEXITCODE -ne 0) { Write-Host "Error de compilacion." -ForegroundColor Red; exit 1 }
Write-Host "OK: $salida" -ForegroundColor Green
Write-Host "Siguiente paso: scripts\instalar_qr_remito.ps1 (como administrador)."
