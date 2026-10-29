# desinstalar_qr_remito.ps1
# Revierte la instalacion de la funcion de Crystal "QRRemito" (CRUFLQRRemito.dll).
#
# USO (PowerShell como ADMINISTRADOR, con Bejerman y Crystal cerrados):
#   powershell -ExecutionPolicy Bypass -File .\desinstalar_qr_remito.ps1
#   (opcional) -Destino "D:\Bejerman\QR\ufl"   si se instalo en otra carpeta
#
# Nota: NO borra u2lcom.dll de C:\Windows\crystal (es parte de Crystal y no molesta).
# Los RPT que usen QRRemitoMainGenerarQR mostraran un error al imprimir si se desinstala:
# antes de desinstalar, quitar la imagen del QR del RPT o restaurar el RPT original.

param(
    [string]$Destino = "C:\Bejerman\QR\ufl"
)

$regasm = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"
$dll = Join-Path $Destino "CRUFLQRRemito.dll"

$esAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $esAdmin) { Write-Host "Ejecutar PowerShell como Administrador." -ForegroundColor Red; exit 1 }

if (Get-Process -Name crw32, SBRptRdoEXEMP -ErrorAction SilentlyContinue) {
    Write-Host "Cerrar Crystal Reports y Bejerman antes de desinstalar." -ForegroundColor Red; exit 1
}

if (Test-Path $dll) {
    & $regasm $dll /unregister /tlb
    Remove-Item (Join-Path $Destino "CRUFLQRRemito.dll") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $Destino "CRUFLQRRemito.tlb") -Force -ErrorAction SilentlyContinue
    Write-Host "Desregistrada y eliminada de $Destino" -ForegroundColor Green
} else {
    Write-Host "No se encontro $dll (nada para desinstalar)." -ForegroundColor Yellow
}

$tmp = Join-Path $env:TEMP "QRRemito"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "Borrados los QR temporales de $tmp" }
