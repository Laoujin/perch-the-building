$ErrorActionPreference = 'Stop'

dotnet build src/Perch.Desktop/Perch.Desktop.csproj -c Debug

$id = [System.IO.Path]::GetRandomFileName().Split('.')[0]
$copyDir = "src\Perch.Desktop\bin\Debug\$id"
Copy-Item "src\Perch.Desktop\bin\Debug\net10.0-windows" $copyDir -Recurse

$branch = git rev-parse --abbrev-ref HEAD 2>$null
Write-Host "Launching from $copyDir"
if ($branch) {
    Start-Process "$copyDir\Perch.Desktop.exe" -ArgumentList "--branch", $branch
} else {
    Start-Process "$copyDir\Perch.Desktop.exe"
}
