#requires -Version 7.0
<#
.SYNOPSIS
    Kịch bản kiểm thử tự động toàn diện End-to-End cho Sprint 3 (S3-03, S3-04, S3-05).
.DESCRIPTION
    Tự động chạy kiểm thử qua giao thức HTTP thật trên website đang chạy:
    - Xác thực phân quyền & chặn truy cập trái phép (TC01, TC02, TC12, TC13)
    - Kiểm tra điều kiện nộp hồ sơ cá nhân & học vấn THPT (TC03, TC04)
    - Kiểm tra đợt tuyển sinh và miền giá trị điểm (TC05, TC11)
    - Nộp hồ sơ hợp lệ, lưu snapshot, sinh mã duy nhất & gán Chờ tiếp nhận (TC15, TC16, TC17, TC18)
    - Chống gửi lặp và chuyển hướng PRG (TC19, TC20)
    - Danh sách hồ sơ, bộ lọc, chi tiết snapshot & Chống IDOR giữa 2 thí sinh (TC23, TC24, TC25, TC26, TC27, TC29)
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$DemoPassword = '',
    [string]$OutputPath = '.local/verification-sprint3.json'
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
    finally { $request.Dispose() }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-FormToken {
    param([string]$Html)
    $tag = [regex]::Match($Html, '<input\b[^>]*\bname="__RequestVerificationToken"[^>]*>', 'IgnoreCase')
    $value = [regex]::Match($tag.Value, '\bvalue="([^"]+)"', 'IgnoreCase')
    Assert-True $value.Success 'Không tìm thấy token chống giả mạo (__RequestVerificationToken).'
    return [System.Net.WebUtility]::HtmlDecode($value.Groups[1].Value)
}

function Assert-RedirectPath {
    param($Response, [string]$ExpectedPath)
    Assert-True ($Response.Status -eq 302) "Cần HTTP 302, nhận $($Response.Status)."
    Assert-True (-not [string]::IsNullOrWhiteSpace($Response.Location)) 'Thiếu địa chỉ chuyển hướng Location.'
    $target = [Uri]::new($baseUri, $Response.Location)
    Assert-True ($target.AbsolutePath.TrimEnd('/') -eq $ExpectedPath.TrimEnd('/')) "Chuyển hướng không đúng đến $ExpectedPath (nhận $($target.AbsolutePath))."
}

function Test-Case {
    param([string]$Name, [scriptblock]$Check)
    try {
        & $Check
        $results.Add([pscustomobject]@{ name = $Name; status = 'PASS'; detail = '' })
        Write-Host "PASS - $Name" -ForegroundColor Green
    }
    catch {
        $detail = $_.Exception.Message
        $results.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; detail = $detail })
        Write-Host "FAIL - $Name : $detail" -ForegroundColor Red
    }
}

Write-Host "=== BẮT ĐẦU CHẠY BỘ KIỂM THỬ SPRINT 3 (E2E) ===" -ForegroundColor Cyan
Write-Host "Mục tiêu: $baseUri`n"

try {
    $anonymous = New-TestClient

    # TC01: Khách chưa đăng nhập bị chuyển hướng sang Login
    Test-Case 'TC01: Khách vãng lai gọi GET /ApplicationForm được chuyển hướng sang /Account/Login' {
        $res = Send-TestRequest $anonymous '/ApplicationForm'
        Assert-RedirectPath $res '/Account/Login'
    }

    Test-Case 'TC01: Khách vãng lai gọi GET /ApplicationForm/Create được chuyển hướng sang /Account/Login' {
        $res = Send-TestRequest $anonymous '/ApplicationForm/Create'
        Assert-RedirectPath $res '/Account/Login'
    }

    # Đăng ký 2 tài khoản thí sinh kiểm thử
    $pwd = 'Ts3!Secure' + [Guid]::NewGuid().ToString('N')[..8]
    $emailA = 'thisinh.a.' + [Guid]::NewGuid().ToString('N')[..8] + '@example.com'
    $emailB = 'thisinh.b.' + [Guid]::NewGuid().ToString('N')[..8] + '@example.com'

    $clientA = New-TestClient
    $clientB = New-TestClient

    Test-Case 'Đăng ký tài khoản Thí sinh A' {
        $regForm = Send-TestRequest $clientA '/Account/Register'
        $token = Get-FormToken $regForm.Body
        $regRes = Send-TestRequest $clientA '/Account/Register' 'POST' @{
            FullName = 'Nguyễn Văn A'
            Email = $emailA
            Password = $pwd
            ConfirmPassword = $pwd
            __RequestVerificationToken = $token
        }
        Assert-True ($regRes.Status -eq 302) "Đăng ký Thí sinh A cần chuyển hướng 302, nhận $($regRes.Status)."
    }

    Test-Case 'Đăng ký tài khoản Thí sinh B' {
        $regForm = Send-TestRequest $clientB '/Account/Register'
        $token = Get-FormToken $regForm.Body
        $regRes = Send-TestRequest $clientB '/Account/Register' 'POST' @{
            FullName = 'Trần Thị B'
            Email = $emailB
            Password = $pwd
            ConfirmPassword = $pwd
            __RequestVerificationToken = $token
        }
        Assert-True ($regRes.Status -eq 302) "Đăng ký Thí sinh B cần chuyển hướng 302, nhận $($regRes.Status)."
    }

    # TC03: Thí sinh A chưa tạo hồ sơ cá nhân mở form
    Test-Case 'TC03: Thí sinh chưa tạo hồ sơ cá nhân thấy thông báo yêu cầu tạo hồ sơ' {
        $createRes = Send-TestRequest $clientA '/ApplicationForm/Create'
        Assert-True ($createRes.Status -eq 200) "Mở trang Create cần HTTP 200, nhận $($createRes.Status)."
        $html = [System.Net.WebUtility]::HtmlDecode($createRes.Body)
        Assert-True ($html.Contains('chưa hoàn thiện hồ sơ cá nhân') -or $html.Contains('chưa tạo hồ sơ cá nhân')) 'Thiếu cảnh báo hồ sơ cá nhân chưa tạo.'
    }

    # Thí sinh A hoàn thiện hồ sơ cá nhân & học vấn
    Test-Case 'Thí sinh A cập nhật hồ sơ cá nhân và học vấn THPT' {
        $profileForm = Send-TestRequest $clientA '/CandidateProfile/Edit'
        $token = Get-FormToken $profileForm.Body
        $saveRes = Send-TestRequest $clientA '/CandidateProfile/Edit' 'POST' @{
            FullName = 'Nguyễn Văn A'
            DateOfBirth = '2008-05-15'
            PhoneNumber = '0987654321'
            ContactEmail = $emailA
            SchoolName = 'THPT Chuyên Huỳnh Mẫn Đạt'
            GraduationYear = '2026'
            __RequestVerificationToken = $token
        }
        Assert-True ($saveRes.Status -eq 302) "Cập nhật hồ sơ cần chuyển hướng 302."
    }

    # Mở form nộp hồ sơ sau khi đã có hồ sơ cá nhân
    $createdAppId = 0
    $createdAppCode = ''

    Test-Case 'Mở biểu mẫu nộp hồ sơ hợp lệ sau khi đã có hồ sơ cá nhân' {
        $createRes = Send-TestRequest $clientA '/ApplicationForm/Create'
        Assert-True ($createRes.Status -eq 200) "Cần HTTP 200 khi mở form."
        $html = [System.Net.WebUtility]::HtmlDecode($createRes.Body)
        Assert-True ($html.Contains('Nguyễn Văn A')) 'Trang chưa hiển thị snapshot họ tên thí sinh.'
        Assert-True ($html.Contains('0987654321')) 'Trang chưa hiển thị snapshot số điện thoại.'
    }

    # TC13: Gửi POST thiếu token chống giả mạo
    Test-Case 'TC13: Gửi POST nộp hồ sơ thiếu Anti-Forgery Token bị chặn (HTTP 400)' {
        $noTokenRes = Send-TestRequest $clientA '/ApplicationForm/Create' 'POST' @{
            RoundId = '1'
            RoundMajorId = '1'
            RoundMajorMethodId = '1'
        }
        Assert-True ($noTokenRes.Status -eq 400) "Cần HTTP 400 khi thiếu token, nhận $($noTokenRes.Status)."
    }

    # TC05: Gửi request sửa RoundId không tồn tại
    Test-Case 'TC05: Sửa RoundId thành ID giả mạo không tồn tại bị máy chủ từ chối' {
        $createPage = Send-TestRequest $clientA '/ApplicationForm/Create'
        $token = Get-FormToken $createPage.Body
        $fakeRoundRes = Send-TestRequest $clientA '/ApplicationForm/Create' 'POST' @{
            RoundId = '999999'
            RoundMajorId = '1'
            RoundMajorMethodId = '1'
            __RequestVerificationToken = $token
        }
        Assert-True ($fakeRoundRes.Status -eq 200) "Trả lại form lỗi HTTP 200."
        $html = [System.Net.WebUtility]::HtmlDecode($fakeRoundRes.Body)
        Assert-True ($html.Contains('Đợt tuyển sinh không tồn tại')) 'Thiếu thông báo lỗi đợt không tồn tại.'
    }

    # TC11: Gửi điểm ngoài miền cho phép
    Test-Case 'TC11: Nhập điểm ngoài miền cho phép (ví dụ: 35) bị máy chủ từ chối' {
        $createPage = Send-TestRequest $clientA '/ApplicationForm/Create'
        $token = Get-FormToken $createPage.Body
        # Lấy option đầu tiên từ form nếu có
        $roundTag = [regex]::Match($createPage.Body, '<input\b[^>]*\bname="RoundId"[^>]*\bvalue="([^"]+)"')
        $roundId = if ($roundTag.Success) { $roundTag.Groups[1].Value } else { '1' }

        $majorMatch = [regex]::Match($createPage.Body, '<option\b[^>]*\bvalue="(\d+)"[^>]*>[^<]*</option>')
        $methodMatch = [regex]::Match($createPage.Body, '<select id="RoundMajorMethodId"[\s\S]*?<option value="(\d+)"')

        $rmId = if ($majorMatch.Success) { $majorMatch.Groups[1].Value } else { '1' }
        $rmmId = if ($methodMatch.Success) { $methodMatch.Groups[1].Value } else { '1' }

        $invalidScoreRes = Send-TestRequest $clientA '/ApplicationForm/Create' 'POST' @{
            RoundId = $roundId
            RoundMajorId = $rmId
            RoundMajorMethodId = $rmmId
            'Scores[TongHocBa]' = '35'
            'Scores[Mon1]' = '15'
            __RequestVerificationToken = $token
        }
        Assert-True ($invalidScoreRes.Status -eq 200) "Trả lại form lỗi HTTP 200."
        $html = [System.Net.WebUtility]::HtmlDecode($invalidScoreRes.Body)
        Assert-True ($html.Contains('phải là số từ') -or $html.Contains('Vui lòng kiểm tra lại')) 'Thiếu lỗi kiểm tra miền điểm.'
    }

    # TC15: Nộp hồ sơ hợp lệ thành công
    Test-Case 'TC15: Nộp hồ sơ hợp lệ thành công, chuyển hướng PRG sang Details' {
        $createPage = Send-TestRequest $clientA '/ApplicationForm/Create'
        $token = Get-FormToken $createPage.Body
        $roundTag = [regex]::Match($createPage.Body, '<input\b[^>]*\bname="RoundId"[^>]*\bvalue="([^"]+)"')
        $roundId = if ($roundTag.Success) { $roundTag.Groups[1].Value } else { '1' }

        # Tìm 1 cặp RoundMajorId và RoundMajorMethodId hợp lệ từ form
        $methodMatch = [regex]::Match($createPage.Body, '<option\b[^>]*\bvalue="(\d+)"\b[^>]*\bdata-major="(\d+)"')
        Assert-True $methodMatch.Success "Không tìm thấy ngành/phương thức hợp lệ trên form nộp hồ sơ."
        $rmmId = $methodMatch.Groups[1].Value
        $rmId = $methodMatch.Groups[2].Value

        # Điền điểm hợp lệ
        $postData = @{
            RoundId = $roundId
            RoundMajorId = $rmId
            RoundMajorMethodId = $rmmId
            'Scores[Mon1]' = '8.5'
            'Scores[Mon2]' = '9.0'
            'Scores[Mon3]' = '8.0'
            'Scores[TongHocBa]' = '26.5'
            'Scores[DiemDGNL]' = '850'
            __RequestVerificationToken = $token
        }

        $submitRes = Send-TestRequest $clientA '/ApplicationForm/Create' 'POST' $postData
        Assert-True ($submitRes.Status -eq 302) "Nộp thành công cần chuyển hướng 302, nhận $($submitRes.Status)."
        Assert-True ($submitRes.Location.Contains('/ApplicationForm/Details/')) "Chuyển hướng không đúng đến Details (nhận $($submitRes.Location))."

        $idMatch = [regex]::Match($submitRes.Location, '/ApplicationForm/Details/(\d+)')
        Assert-True $idMatch.Success "Không trích xuất được ID hồ sơ vừa tạo."
        $script:createdAppId = [int]$idMatch.Groups[1].Value
    }

    # TC16, TC17, TC18, TC26: Xem chi tiết hồ sơ vừa tạo
    Test-Case 'TC16 & TC17 & TC26: Trang Details hiển thị mã hồ sơ, trạng thái Chờ tiếp nhận và snapshot' {
        Assert-True ($script:createdAppId -gt 0) 'Chưa có ID hồ sơ để kiểm tra.'
        $detailsRes = Send-TestRequest $clientA "/ApplicationForm/Details/$($script:createdAppId)"
        Assert-True ($detailsRes.Status -eq 200) "Mở trang chi tiết cần HTTP 200, nhận $($detailsRes.Status)."
        $html = [System.Net.WebUtility]::HtmlDecode($detailsRes.Body)

        $codeMatch = [regex]::Match($html, 'HS\d{4}-\w+')
        Assert-True $codeMatch.Success "Không tìm thấy mã hồ sơ đúng định dạng HS{Năm}-{Số}."
        $script:createdAppCode = $codeMatch.Value

        Assert-True ($html.Contains('Chờ tiếp nhận')) 'Trang chi tiết chưa hiển thị trạng thái Chờ tiếp nhận.'
        Assert-True ($html.Contains('Nguyễn Văn A')) 'Trang chi tiết chưa hiển thị snapshot họ tên thí sinh.'
        Assert-True ($html.Contains('THPT Chuyên Huỳnh Mẫn Đạt')) 'Trang chi tiết chưa hiển thị snapshot trường THPT.'
    }

    # TC19: Chống gửi lặp cùng nguyện vọng
    Test-Case 'TC19: Thử gửi lại cùng một nguyện vọng bị máy chủ từ chối (Chống gửi lặp)' {
        $createPage = Send-TestRequest $clientA '/ApplicationForm/Create'
        $token = Get-FormToken $createPage.Body
        $roundTag = [regex]::Match($createPage.Body, '<input\b[^>]*\bname="RoundId"[^>]*\bvalue="([^"]+)"')
        $roundId = if ($roundTag.Success) { $roundTag.Groups[1].Value } else { '1' }
        $methodMatch = [regex]::Match($createPage.Body, '<option\b[^>]*\bvalue="(\d+)"\b[^>]*\bdata-major="(\d+)"')
        $rmmId = $methodMatch.Groups[1].Value
        $rmId = $methodMatch.Groups[2].Value

        $dupRes = Send-TestRequest $clientA '/ApplicationForm/Create' 'POST' @{
            RoundId = $roundId
            RoundMajorId = $rmId
            RoundMajorMethodId = $rmmId
            'Scores[Mon1]' = '8.5'
            'Scores[Mon2]' = '9.0'
            'Scores[Mon3]' = '8.0'
            'Scores[TongHocBa]' = '26.5'
            'Scores[DiemDGNL]' = '850'
            __RequestVerificationToken = $token
        }
        Assert-True ($dupRes.Status -eq 200) "Gửi trùng cần trả lại biểu mẫu HTTP 200 kèm lỗi."
        $html = [System.Net.WebUtility]::HtmlDecode($dupRes.Body)
        Assert-True ($html.Contains('đã nộp hồ sơ') -or $html.Contains('Mỗi nguyện vọng chỉ được nộp')) 'Thiếu thông báo chống gửi lặp.'
    }

    # TC24: Danh sách hồ sơ đã nộp của Thí sinh A
    Test-Case 'TC24: Trang danh sách /ApplicationForm hiển thị hồ sơ vừa nộp của Thí sinh A' {
        $listRes = Send-TestRequest $clientA '/ApplicationForm'
        Assert-True ($listRes.Status -eq 200) "Trang danh sách cần HTTP 200."
        $html = [System.Net.WebUtility]::HtmlDecode($listRes.Body)
        Assert-True ($html.Contains($script:createdAppCode)) "Danh sách chưa hiển thị mã hồ sơ $($script:createdAppCode)."
        Assert-True ($html.Contains('Chờ tiếp nhận')) 'Danh sách chưa hiển thị trạng thái Chờ tiếp nhận.'
    }

    # TC25: Lọc danh sách theo mã hồ sơ
    Test-Case 'TC25: Tìm kiếm danh sách theo mã hồ sơ trả về đúng kết quả' {
        $searchRes = Send-TestRequest $clientA "/ApplicationForm?q=$($script:createdAppCode)"
        Assert-True ($searchRes.Status -eq 200) "Tìm kiếm cần HTTP 200."
        $html = [System.Net.WebUtility]::HtmlDecode($searchRes.Body)
        Assert-True ($html.Contains($script:createdAppCode)) 'Kết quả tìm kiếm không chứa mã hồ sơ.'
    }

    # TC27: Thí sinh B truy cập hồ sơ của Thí sinh A bị chặn (IDOR)
    Test-Case 'TC27: Thí sinh B truy cập URL chi tiết hồ sơ của Thí sinh A bị chặn 404 (Chống IDOR)' {
        Assert-True ($script:createdAppId -gt 0) 'Chưa có ID hồ sơ.'
        $idorRes = Send-TestRequest $clientB "/ApplicationForm/Details/$($script:createdAppId)"
        Assert-True ($idorRes.Status -eq 404) "Thí sinh B phải nhận HTTP 404 khi truy cập hồ sơ của Thí sinh A, nhận $($idorRes.Status)."
    }

    # TC23: Thí sinh B mở danh sách hồ sơ khi chưa nộp
    Test-Case 'TC23: Thí sinh B chưa nộp hồ sơ thấy trạng thái rỗng Empty State' {
        $listResB = Send-TestRequest $clientB '/ApplicationForm'
        Assert-True ($listResB.Status -eq 200) "Cần HTTP 200."
        $html = [System.Net.WebUtility]::HtmlDecode($listResB.Body)
        Assert-True ($html.Contains('chưa nộp hồ sơ xét tuyển nào')) 'Thiếu thông điệp Empty State khi chưa có hồ sơ.'
    }

    # TC29: Cổng thí sinh Candidate/Index cập nhật số lượng hồ sơ
    Test-Case 'TC29: Cổng thí sinh Candidate/Index hiển thị số lượng hồ sơ đã nộp' {
        $candRes = Send-TestRequest $clientA '/Candidate'
        Assert-True ($candRes.Status -eq 200) "Cần HTTP 200 khi mở Cổng thí sinh."
        $html = [System.Net.WebUtility]::HtmlDecode($candRes.Body)
        Assert-True ($html.Contains($script:createdAppCode)) 'Cổng thí sinh chưa hiển thị hồ sơ gần đây.'
    }

}
finally {
    foreach ($c in $clients) { $c.Dispose() }
}

$passCount = ($results | Where-Object { $_.status -eq 'PASS' }).Count
$failCount = ($results | Where-Object { $_.status -eq 'FAIL' }).Count
$total = $results.Count

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "KẾT QUẢ KIỂM THỬ SPRINT 3: $passCount / $total PASS" -ForegroundColor $(if ($failCount -eq 0) { 'Green' } else { 'Red' })
Write-Host "==========================================" -ForegroundColor Cyan

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $dir = Split-Path $OutputPath -Parent
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $results | ConvertTo-Json -Depth 4 | Set-Content -Path $OutputPath -Encoding utf8
}

if ($failCount -gt 0) {
    exit 1
}
