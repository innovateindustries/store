$ErrorActionPreference = 'Stop'
# Solo lectura: muestra usuario + email del rol FOUNDER (nunca hashes ni passwords).
$root = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $root 'bin\Release\net10.0\publish\Microsoft.Data.Sqlite.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    $dll = Join-Path $root 'bin\Debug\net10.0\Microsoft.Data.Sqlite.dll'
}
Add-Type -Path $dll
$db = Join-Path $root 'app.db'
$conn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$db;Mode=ReadOnly")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT u.UserName, u.Email FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId = u.Id JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = 'FOUNDER'"
$rd = $cmd.ExecuteReader()
$found = $false
while ($rd.Read()) {
    $found = $true
    Write-Output ("FOUNDER | usuario: " + $rd.GetString(0) + " | email: " + $rd.GetString(1))
}
$rd.Close(); $conn.Close()
if (-not $found) { Write-Output 'Sin usuarios con rol FOUNDER (el primero en registrarse lo recibe si no hay fundador).' }
