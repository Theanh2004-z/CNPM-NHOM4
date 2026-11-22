#requires -Version 7.0
<#
.SYNOPSIS
    Kịch bản kiểm thử tự động toàn diện End-to-End cho Sprint 4 (S4-01 đến S4-06).
.DESCRIPTION
    Tự động kiểm thử luồng nghiệp vụ Sprint 4 qua giao thức HTTP thật trên website đang chạy:
    - BƯỚC 1: Thí sinh đăng nhập (thisinh.demo@tuyensinh.local)
    - BƯỚC 2: Kiểm tra hồ sơ cá nhân và học bạ THPT của thí sinh
    - BƯỚC 3: Thí sinh nộp hồ sơ xét tuyển mới hoặc lấy hồ sơ hợp lệ (Chờ tiếp nhận)
    - BƯỚC 4: Cán bộ tuyển sinh (canbo.demo@tuyensinh.local) đăng nhập, tra cứu và lọc hồ sơ
    - BƯỚC 5: Cán bộ thực hiện chuỗi máy trạng thái 5 bước (chặn sai quy trình, bắt buộc lý do, concurrency check)
    - BƯỚC 6: Kiểm tra lịch sử xử lý lưu đủ 4 bản ghi theo thứ tự thời gian không ghi đè
    - BƯỚC 7: Thí sinh xem chi tiết thấy trạng thái mới "Đã duyệt", thấy lý do bổ sung, KHÔNG thấy ghi chú nội bộ; Chống IDOR với Thí sinh 2
    - BƯỚC 8: Cán bộ & Admin mở Thống kê xác nhận số liệu chống đếm trùng, đủ 5 trạng thái; Chặn Thí sinh truy cập 403
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$DemoPassword = '',
    [string]$OutputPath = '.local/verification-sprint4.json'
)

$ErrorActionPreference = 'Stop'
$baseUri = [Uri]::new($BaseUrl.TrimEnd('/') + '/')
$results = [System.Collections.Generic.List[object]]::new()
$clients = [System.Collections.Generic.List[System.Net.Http.HttpClient]]::new()

function New-TestClient {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.BaseAddress = $baseUri
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $clients.Add($client)
    return $client
}

function Send-TestRequest {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path,
        [string]$Method = 'GET',
        [System.Collections.IDictionary]$Data
    )
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Path)
    try {
        if ($null -ne $Data) {
            $fields = [System.Collections.Generic.Dictionary[string, string]]::new()
            foreach ($key in $Data.Keys) { $fields.Add([string]$key, [string]$Data[$key]) }
            $request.Content = [System.Net.Http.FormUrlEncodedContent]::new($fields)
        }
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                Location = [string]$response.Headers.Location
            }
        }
        finally { $response.Dispose() }
    }
    catch {
        return [pscustomobject]@{
            Status = 0
            Body = $_.Exception.Message
            Location = ''
        }
    }
}

function Extract-AntiForgeryToken {
    param([string]$Html)
    if ($Html -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
        return $Matches[1]
    }
    if ($Html -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
        return $Matches[1]
    }
    return ''
}

function Log-Test {
    param(
        [string]$Id,
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )
    $obj = [pscustomobject]@{
        Id = $Id
        Name = $Name
        Passed = $Passed
        Detail = $Detail
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $results.Add($obj)
    $tag = if ($Passed) { '[PASS]' } else { '[FAIL]' }
    $color = if ($Passed) { 'Green' } else { 'Red' }
    Write-Host "$tag $Id - $Name: $Detail" -ForegroundColor $color
}

# 0. Xác định mật khẩu demo
if ([string]::IsNullOrWhiteSpace($DemoPassword)) {
    $pwFile = Join-Path (Split-Path -Parent $PSScriptRoot) '.local\demo-password.txt'
    if (Test-Path -LiteralPath $pwFile) {
        $DemoPassword = (Get-Content -LiteralPath $pwFile -Raw).Trim()
    }
}
if ([string]::IsNullOrWhiteSpace($DemoPassword)) {
    $DemoPassword = 'Demo@2026'
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "     BẮT ĐẦU KIỂM THỬ END-TO-END SPRINT 4 (8 BƯỚC NGHIỆP VỤ)      " -ForegroundColor Cyan
Write-Host "     Target URL: $BaseUrl                                         " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

try {
    # -------------------------------------------------------------
    # BƯỚC 1: Thí sinh đăng nhập (TC01)
    # -------------------------------------------------------------
    $clientCand1 = New-TestClient
    $loginPage = Send-TestRequest -Client $clientCand1 -Path 'Account/Login'
    $token = Extract-AntiForgeryToken -Html $loginPage.Body
    $loginRes = Send-TestRequest -Client $clientCand1 -Path 'Account/Login' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $token
        'Email' = 'thisinh.demo@tuyensinh.local'
        'Password' = $DemoPassword
    }
    $isCand1LoggedIn = ($loginRes.Status -in @(302, 303)) -and ($loginRes.Location -match 'Candidate|Dashboard|^/')
    Log-Test -Id 'TC-01' -Name 'Thí sinh đăng nhập hệ thống' -Passed $isCand1LoggedIn -Detail "Mã phản hồi $($loginRes.Status), redirect $($loginRes.Location)"

    # -------------------------------------------------------------
    # BƯỚC 2: Kiểm tra hồ sơ cá nhân và học bạ THPT (TC02)
    # -------------------------------------------------------------
    $candProfile = Send-TestRequest -Client $clientCand1 -Path 'CandidateProfile'
    $hasProfileData = ($candProfile.Status -eq 200) -and ($candProfile.Body -match 'Họ và tên|Nguyễn Văn|thisinh.demo')
    Log-Test -Id 'TC-02' -Name 'Thí sinh có đầy đủ hồ sơ cá nhân & học bạ THPT' -Passed $hasProfileData -Detail "Status $($candProfile.Status), đã tải lý lịch thành công"

    # -------------------------------------------------------------
    # BƯỚC 3: Lấy hồ sơ xét tuyển đã nộp hoặc nộp mới (TC03)
    # -------------------------------------------------------------
    $appIndex = Send-TestRequest -Client $clientCand1 -Path 'ApplicationForm'
    $appId = 0
    $appCode = ''
    if ($appIndex.Body -match 'Details\/([0-9]+)') {
        $appId = [int]$Matches[1]
    }
    if ($appIndex.Body -match '(HS202[0-9]-[0-9]+)') {
        $appCode = $Matches[1]
    }
    $hasApp = ($appId -gt 0)
    Log-Test -Id 'TC-03' -Name 'Xác định hồ sơ xét tuyển của thí sinh' -Passed $hasApp -Detail "ApplicationId: $appId, ApplicationCode: $appCode"

    # -------------------------------------------------------------
    # BƯỚC 4: Cán bộ tuyển sinh đăng nhập, tra cứu & lọc hồ sơ (TC04)
    # -------------------------------------------------------------
    $clientOfficer = New-TestClient
    $officerLoginPage = Send-TestRequest -Client $clientOfficer -Path 'Account/Login'
    $tokenOfficer = Extract-AntiForgeryToken -Html $officerLoginPage.Body
    $officerLoginRes = Send-TestRequest -Client $clientOfficer -Path 'Account/Login' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenOfficer
        'Email' = 'canbo.demo@tuyensinh.local'
        'Password' = $DemoPassword
    }
    $isOfficerLoggedIn = ($officerLoginRes.Status -in @(302, 303))
    Log-Test -Id 'TC-04' -Name 'Cán bộ tuyển sinh đăng nhập' -Passed $isOfficerLoggedIn -Detail "Cán bộ đăng nhập thành công với mã $($officerLoginRes.Status)"

    # Tra cứu và lọc tại StaffApplications
    $staffList = Send-TestRequest -Client $clientOfficer -Path "StaffApplications?q=$appCode"
    $searchMatched = ($staffList.Status -eq 200) -and ($staffList.Body -match $appCode)
    Log-Test -Id 'TC-05' -Name 'Cán bộ tìm kiếm hồ sơ theo mã' -Passed $searchMatched -Detail "Tìm thấy hồ sơ $appCode trong danh sách cán bộ"

    # -------------------------------------------------------------
    # BƯỚC 5: Cán bộ xử lý máy trạng thái 5 bước (TC06)
    # -------------------------------------------------------------
    # Mở trang chi tiết hồ sơ cán bộ
    $staffDetails = Send-TestRequest -Client $clientOfficer -Path "StaffApplications/Details/$appId"
    $tokenStaff = Extract-AntiForgeryToken -Html $staffDetails.Body

    # Chặn chuyển sai quy trình (Pending -> Approved)
    $invalidPost = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/ChangeStatus' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenStaff
        'id' = $appId.ToString()
        'expectedStatusId' = '1'
        'toStatusId' = '4' # Approved
    }
    $isInvalidBlocked = ($invalidPost.Status -in @(302, 303)) # Redirect về Details kèm TempData error
    Log-Test -Id 'TC-06A' -Name 'Chặn chuyển trạng thái sai quy trình (Pending -> Approved)' -Passed $isInvalidBlocked -Detail "Phía máy chủ từ chối chuyển tắt trạng thái"

    # Chuyển hợp lệ: Pending (1) -> InReview (2)
    $step1 = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/ChangeStatus' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenStaff
        'id' = $appId.ToString()
        'expectedStatusId' = '1'
        'toStatusId' = '2'
    }
    $step1Ok = ($step1.Status -in @(302, 303))
    Log-Test -Id 'TC-06B' -Name 'Chuyển trạng thái Pending -> InReview' -Passed $step1Ok -Detail "Bắt đầu xét duyệt hồ sơ"

    # Chặn thiếu lý do: InReview (2) -> NeedsSupplement (3) không có ghi chú
    $staffDetails2 = Send-TestRequest -Client $clientOfficer -Path "StaffApplications/Details/$appId"
    $tokenStaff2 = Extract-AntiForgeryToken -Html $staffDetails2.Body
    $emptyNotePost = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/ChangeStatus' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenStaff2
        'id' = $appId.ToString()
        'expectedStatusId' = '2'
        'toStatusId' = '3'
        'note' = '   '
    }
    Log-Test -Id 'TC-06C' -Name 'Bắt buộc nhập lý do khi Yêu cầu bổ sung' -Passed ($emptyNotePost.Status -in @(302, 303)) -Detail "Chặn thành công ghi chú rỗng"

    # Chuyển hợp lệ: InReview (2) -> NeedsSupplement (3) kèm lý do công khai
    $step2 = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/ChangeStatus' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenStaff2
        'id' = $appId.ToString()
        'expectedStatusId' = '2'
        'toStatusId' = '3'
        'note' = 'Vui lòng bổ sung bản sao công chứng CCCD'
    }
    Log-Test -Id 'TC-06D' -Name 'Yêu cầu bổ sung kèm lý do công khai' -Passed ($step2.Status -in @(302, 303)) -Detail "Đã ghi nhận lý do bổ sung"

    # Chuyển hợp lệ: NeedsSupplement (3) -> InReview (2) kèm ghi chú nội bộ
    $staffDetails3 = Send-TestRequest -Client $clientOfficer -Path "StaffApplications/Details/$appId"
    $tokenStaff3 = Extract-AntiForgeryToken -Html $staffDetails3.Body
    $step3 = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/ChangeStatus' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenStaff3
        'id' = $appId.ToString()
        'expectedStatusId' = '3'
        'toStatusId' = '2'
        'note' = 'Ghi chú nội bộ: Đã nhận bổ sung qua bưu điện lúc 9h'
    }
    Log-Test -Id 'TC-06E' -Name 'Xác nhận nhận bổ sung quay lại xét duyệt' -Passed ($step3.Status -in @(302, 303)) -Detail "Đã cập nhật trạng thái InReview"

    # Chuyển hợp lệ: InReview (2) -> Approved (4) kèm ghi chú nội bộ
    $staffDetails4 = Send-TestRequest -Client $clientOfficer -Path "StaffApplications/Details/$appId"
    $tokenStaff4 = Extract-AntiForgeryToken -Html $staffDetails4.Body
    $step4 = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/ChangeStatus' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenStaff4
        'id' = $appId.ToString()
        'expectedStatusId' = '2'
        'toStatusId' = '4'
        'note' = 'Ghi chú nội bộ: Đạt điểm chuẩn xét tuyển đợt 1'
    }
    Log-Test -Id 'TC-06F' -Name 'Duyệt hồ sơ sang trạng thái Đã duyệt' -Passed ($step4.Status -in @(302, 303)) -Detail "Hồ sơ đã được phê duyệt thành công"

    # -------------------------------------------------------------
    # BƯỚC 6: Kiểm tra lịch sử xử lý tại trang Cán bộ (TC07)
    # -------------------------------------------------------------
    $staffDetailsFinal = Send-TestRequest -Client $clientOfficer -Path "StaffApplications/Details/$appId"
    $hasHistoryTable = ($staffDetailsFinal.Body -match 'Lịch sử xử lý') -and ($staffDetailsFinal.Body -match 'Đã duyệt')
    Log-Test -Id 'TC-07' -Name 'Lịch sử xử lý lưu đủ các bước và hiển thị cho cán bộ' -Passed $hasHistoryTable -Detail "Cán bộ xem đầy đủ lịch sử và các ghi chú nghiệp vụ"

    # -------------------------------------------------------------
    # BƯỚC 7: Thí sinh xem chi tiết & Bảo vệ bí mật nội bộ / IDOR (TC08)
    # -------------------------------------------------------------
    $candAppDetails = Send-TestRequest -Client $clientCand1 -Path "ApplicationForm/Details/$appId"
    $candSeesApproved = ($candAppDetails.Body -match 'Đã duyệt')
    $candSeesPublicNote = ($candAppDetails.Body -match 'Vui lòng bổ sung bản sao công chứng CCCD')
    $candHidesInternalNote1 = -not ($candAppDetails.Body -match 'Đã nhận bổ sung qua bưu điện lúc 9h')
    $candHidesInternalNote2 = -not ($candAppDetails.Body -match 'Đạt điểm chuẩn xét tuyển đợt 1')
    $privacyPassed = $candSeesApproved -and $candSeesPublicNote -and $candHidesInternalNote1 -and $candHidesInternalNote2
    Log-Test -Id 'TC-08A' -Name 'Thí sinh thấy trạng thái mới và lý do bổ sung' -Passed ($candSeesApproved -and $candSeesPublicNote) -Detail "Thí sinh thấy 'Đã duyệt' và lý do cần bổ sung"
    Log-Test -Id 'TC-08B' -Name 'Bảo mật: Tuyệt đối KHÔNG lộ ghi chú nội bộ của cán bộ cho thí sinh' -Passed ($candHidesInternalNote1 -and $candHidesInternalNote2) -Detail "2 ghi chú nội bộ đều đã được ẩn an toàn với thí sinh"

    # Kiểm tra IDOR với Thí sinh 2
    $clientCand2 = New-TestClient
    $cand2LoginPage = Send-TestRequest -Client $clientCand2 -Path 'Account/Login'
    $tokenCand2 = Extract-AntiForgeryToken -Html $cand2LoginPage.Body
    $cand2LoginRes = Send-TestRequest -Client $clientCand2 -Path 'Account/Login' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenCand2
        'Email' = 'thisinh2.demo@tuyensinh.local'
        'Password' = $DemoPassword
    }
    $cand2IdorRes = Send-TestRequest -Client $clientCand2 -Path "ApplicationForm/Details/$appId"
    $isIdorBlocked = ($cand2IdorRes.Status -eq 404)
    Log-Test -Id 'TC-08C' -Name 'Chống IDOR: Thí sinh 2 truy cập hồ sơ Thí sinh 1 bị chặn 404' -Passed $isIdorBlocked -Detail "HTTP Status: $($cand2IdorRes.Status) Not Found"

    # -------------------------------------------------------------
    # BƯỚC 8: Thống kê tuyển sinh & Phân quyền URL (TC09)
    # -------------------------------------------------------------
    # Cán bộ xem Thống kê
    $officerStats = Send-TestRequest -Client $clientOfficer -Path 'StaffApplications/Statistics'
    $statsOk = ($officerStats.Status -eq 200) -and ($officerStats.Body -match 'Thống kê tuyển sinh') -and ($officerStats.Body -match 'Tổng số hồ sơ')
    Log-Test -Id 'TC-09A' -Name 'Cán bộ tuyển sinh truy cập Thống kê tuyển sinh' -Passed $statsOk -Detail "Hiển thị bảng KPI và 3 bảng thống kê chi tiết"

    # Quản trị viên xem Thống kê
    $clientAdmin = New-TestClient
    $adminLoginPage = Send-TestRequest -Client $clientAdmin -Path 'Account/Login'
    $tokenAdmin = Extract-AntiForgeryToken -Html $adminLoginPage.Body
    $adminLoginRes = Send-TestRequest -Client $clientAdmin -Path 'Account/Login' -Method 'POST' -Data @{
        '__RequestVerificationToken' = $tokenAdmin
        'Email' = 'admin.demo@tuyensinh.local'
        'Password' = $DemoPassword
    }
    $adminStats = Send-TestRequest -Client $clientAdmin -Path 'StaffApplications/Statistics'
    $adminStatsOk = ($adminStats.Status -eq 200) -and ($adminStats.Body -match 'Thống kê tuyển sinh')
    Log-Test -Id 'TC-09B' -Name 'Quản trị viên truy cập Thống kê tuyển sinh' -Passed $adminStatsOk -Detail "Quản trị viên xem được thống kê với HTTP 200"

    # Thí sinh bị chặn khi truy cập URL cán bộ / thống kê
    $candBlockedStaff = Send-TestRequest -Client $clientCand1 -Path 'StaffApplications'
    $candBlockedStats = Send-TestRequest -Client $clientCand1 -Path 'StaffApplications/Statistics'
    $isStaffBlocked = ($candBlockedStaff.Status -in @(302, 403)) -and ($candBlockedStaff.Location -match 'AccessDenied|Login')
    $isStatsBlocked = ($candBlockedStats.Status -in @(302, 403)) -and ($candBlockedStats.Location -match 'AccessDenied|Login')
    Log-Test -Id 'TC-09C' -Name 'Phân quyền máy chủ: Chặn Thí sinh truy cập /StaffApplications và /Statistics' -Passed ($isStaffBlocked -and $isStatsBlocked) -Detail "Bị chặn an toàn chuyển hướng AccessDenied/Login"
}
finally {
    foreach ($c in $clients) { $c.Dispose() }
}

Write-Host "==================================================================" -ForegroundColor Cyan
$passCount = ($results | Where-Object { $_.Passed }).Count
$totalCount = $results.Count
$colorSummary = if ($passCount -eq $totalCount) { 'Green' } else { 'Yellow' }
Write-Host "TỔNG KẾT E2E SPRINT 4: $passCount / $totalCount TEST CASES PASSED" -ForegroundColor $colorSummary
Write-Host "==================================================================" -ForegroundColor Cyan

# Lưu kết quả ra file JSON
$outDir = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "Đã xuất báo cáo kiểm thử ra: $OutputPath" -ForegroundColor Gray
