param(
    [ValidateRange(1024, 65535)][int]$Port = 5080,
    [switch]$NoDemo,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Can cai .NET SDK 9 truoc khi chay. Xem README.md.'
}
if (-not (Get-Command SqlLocalDB.exe -ErrorAction SilentlyContinue)) {
    throw 'Can cai SQL Server Express LocalDB, hoac cau hinh SQL Server rieng theo README.md.'
}

$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local\dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:ASPNETCORE_ENVIRONMENT = 'Development'

$instances = @(& SqlLocalDB.exe info)
if ($instances -notcontains 'TuyenSinhOnlineDev') {
    & SqlLocalDB.exe create TuyenSinhOnlineDev
    if ($LASTEXITCODE -ne 0) { throw 'Khong tao duoc LocalDB.' }
}
& SqlLocalDB.exe start TuyenSinhOnlineDev
if ($LASTEXITCODE -ne 0) { throw 'Khong khoi dong duoc LocalDB.' }

$localDirectory = Join-Path $projectRoot '.local'
New-Item -ItemType Directory -Path $localDirectory -Force | Out-Null
$passwordPath = Join-Path $localDirectory 'demo-password.txt'
$accountPath = Join-Path $localDirectory 'demo-accounts.txt'
if (-not $NoDemo) {
    if (-not (Test-Path -LiteralPath $passwordPath)) {
        $randomBytes = New-Object byte[] 18
        $randomGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $randomGenerator.GetBytes($randomBytes) } finally { $randomGenerator.Dispose() }
        $demoPassword = 'Ts1!Aa' + [Convert]::ToBase64String($randomBytes)
        Set-Content -LiteralPath $passwordPath -Value $demoPassword -Encoding ascii -NoNewline
    }
    $env:DemoAccounts__Enabled = 'true'
    $env:DemoAccounts__Password = (Get-Content -LiteralPath $passwordPath -Raw).Trim()
    @(
        'Tai khoan demo chi dung tren may phat trien:',
        'Thi sinh: thisinh.demo@tuyensinh.local',
        'Can bo: canbo.demo@tuyensinh.local',
        'Quan tri: admin.demo@tuyensinh.local',
        ('Mat khau chung: ' + $env:DemoAccounts__Password),
        'Tep nay va demo-password.txt nam trong .local, khong duoc dua len Git.'
    ) | Set-Content -LiteralPath $accountPath -Encoding utf8
    Write-Host ('Tai khoan va mat khau demo: ' + $accountPath)
} else {
    $env:DemoAccounts__Enabled = 'false'
    Remove-Item Env:DemoAccounts__Password -ErrorAction SilentlyContinue
}

& dotnet restore TuyenSinhOnline.sln --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Tai thu vien that bai. Kiem tra ket noi Internet.' }
& dotnet build TuyenSinhOnline.sln --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Build that bai. Xem thong bao phia tren.' }

$websiteUrl = 'http://localhost:' + $Port
Write-Host ('Mo website: ' + $websiteUrl)
Write-Host 'Dung website bang Ctrl+C trong cua so nay.'
if (-not $NoBrowser) { Start-Process $websiteUrl }
try {
    & dotnet run --project src/TuyenSinhOnline --no-build --no-launch-profile --urls $websiteUrl
    if ($LASTEXITCODE -ne 0) { throw 'Website dung do loi. Xem thong bao phia tren.' }
} finally {
    Remove-Item Env:DemoAccounts__Password -ErrorAction SilentlyContinue
    Remove-Item Env:DemoAccounts__Enabled -ErrorAction SilentlyContinue
}
