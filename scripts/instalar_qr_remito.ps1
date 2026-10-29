# instalar_qr_remito.ps1
# Instala la funcion de Crystal "QRRemito" (CRUFLQRRemito.dll) en una PC que imprime remitos de Bejerman.
#
# USO (PowerShell como ADMINISTRADOR, con Bejerman y Crystal cerrados):
#   1) Copiar a una carpeta este script junto con CRUFLQRRemito.dll
#      (o ejecutarlo desde el repositorio: busca la DLL en ..\ufl\bin\)
#   2) Ejecutar:
#        powershell -ExecutionPolicy Bypass -File .\instalar_qr_remito.ps1
#      Opcional, otra carpeta de destino:
#        powershell -ExecutionPolicy Bypass -File .\instalar_qr_remito.ps1 -Destino "D:\Bejerman\QR\ufl"
#
# Que hace:
#   - Copia CRUFLQRRemito.dll a la carpeta de destino (por defecto C:\Bejerman\QR\ufl)
#   - La registra con RegAsm de 32 bits (/codebase /tlb), desregistrando antes una version previa
#   - Copia u2lcom.dll (puente de UFL COM de Crystal XI R2) a C:\Windows\crystal\ si falta,
#     porque el visor de reportes de Bejerman busca las UFL en esa carpeta
#   - Verifica que la funcion responda desde un proceso de 32 bits (como Crystal)

param(
    [string]$Destino = "C:\Bejerman\QR\ufl"
)

$ErrorActionPreference = "Stop"
$dllNombre = "CRUFLQRRemito.dll"
$regasm    = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"
$ps32      = "C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
$crystalUfl = "C:\Windows\crystal"

function Paso($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "    OK  $t" -ForegroundColor Green }
function Aviso($t){ Write-Host "    !!  $t" -ForegroundColor Yellow }
function Falla($t){ Write-Host "    XX  $t" -ForegroundColor Red; exit 1 }

# 0) Controles previos
Paso "Controles previos"
$esAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $esAdmin) { Falla "Ejecutar PowerShell como Administrador." }
Ok "Ejecutando como administrador"

if (-not (Test-Path $regasm)) { Falla "No se encontro RegAsm de .NET Framework 4 (32 bits): $regasm" }
Ok ".NET Framework 4.x presente"

$origen = Join-Path $PSScriptRoot $dllNombre
if (-not (Test-Path $origen)) {
    # Estructura del repositorio: scripts\ y ufl\bin\ son carpetas hermanas
    $origen = Join-Path $PSScriptRoot "..\ufl\bin\$dllNombre"
}
if (-not (Test-Path $origen)) { Falla "No se encontro $dllNombre junto a este script ni en ..\ufl\bin\ (compilar con ufl\compilar.ps1)." }
$origen = (Resolve-Path $origen).Path
Ok "DLL encontrada: $origen"

$procs = Get-Process -Name crw32, SBRptRdoEXEMP -ErrorAction SilentlyContinue
if ($procs) { Falla "Cerrar Crystal Reports y Bejerman antes de instalar (procesos abiertos: $($procs.Name -join ', '))." }
Ok "Crystal y el visor de Bejerman estan cerrados"

# 1) Copiar la DLL
Paso "Copiando la DLL a $Destino"
New-Item -ItemType Directory -Force -Path $Destino | Out-Null
$destinoDll = Join-Path $Destino $dllNombre
if (Test-Path $destinoDll) {
    Aviso "Ya hay una version instalada: se desregistra antes de reemplazarla"
    & $regasm $destinoDll /unregister /tlb | Out-Null
}
if ((Resolve-Path $origen).Path -ne $destinoDll) {
    Copy-Item $origen $destinoDll -Force
}
Ok "Copiada"

# 2) Registrar con RegAsm de 32 bits
Paso "Registrando la DLL (RegAsm 32 bits)"
$salida = & $regasm $destinoDll /codebase /tlb 2>&1
$salida | ForEach-Object { Write-Host "    $_" }
if ($LASTEXITCODE -ne 0) { Falla "RegAsm devolvio error ($LASTEXITCODE)." }
Ok "Registrada (la advertencia de 'nombre seguro' es esperable)"

# 3) u2lcom.dll en C:\Windows\crystal
Paso "Verificando u2lcom.dll en $crystalUfl"
$u2lDestino = Join-Path $crystalUfl "u2lcom.dll"
if (Test-Path $u2lDestino) {
    Ok "Ya existe $u2lDestino"
} else {
    $candidatos = @(
        "C:\Program Files (x86)\Business Objects\Common\3.5\bin\u2lcom.dll",
        "C:\Program Files\Business Objects\Common\3.5\bin\u2lcom.dll"
    )
    $u2lOrigen = $candidatos | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $u2lOrigen) {
        $u2lOrigen = Get-ChildItem "C:\Program Files (x86)", "C:\Program Files" -Recurse -Filter "u2lcom.dll" -ErrorAction SilentlyContinue |
                     Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $u2lOrigen) { Falla "No se encontro u2lcom.dll (runtime de Crystal XI R2). Revisar la instalacion de Crystal en esta PC." }
    New-Item -ItemType Directory -Force -Path $crystalUfl | Out-Null
    Copy-Item $u2lOrigen $u2lDestino -Force
    Ok "Copiada desde $u2lOrigen"
}

# 4) Prueba desde un proceso de 32 bits
Paso "Probando la funcion desde un proceso de 32 bits"
$prueba = & $ps32 -NoProfile -Command "`$o = New-Object -ComObject CRUFLQRRemito.Main; `$p = `$o.GenerarQR('PRUEBA-INSTALACION'); if (`$p -and (Test-Path `$p)) { 'OK|' + `$p } else { 'FALLA|' + `$p }"
if ($prueba -like "OK|*") {
    Ok ("QR de prueba generado: " + $prueba.Substring(3))
} else {
    Falla "La funcion no genero el QR de prueba ($prueba). Ver %TEMP%\QRRemito_error.log"
}

Write-Host ""
Write-Host "Instalacion completa." -ForegroundColor Green
Write-Host "En Crystal la funcion aparece como: QRRemitoMainGenerarQR(texto)  (Funciones adicionales -> UFLs de Visual Basic)"
