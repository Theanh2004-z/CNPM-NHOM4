#requires -Version 7.0
# Run against a freshly started server, or after one minute without account POSTs.
# The final case intentionally exhausts the per-IP account request limit.
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [Parameter(Mandatory = $true)]
    [string]$DemoPassword,
    [string]$OutputPath = '.local/verification.json'
)

$ErrorActionPreference = 'Stop'
$baseUri = [Uri]::new($BaseUrl.TrimEnd('/') + '/')
$results = [System.Collections.Generic.List[object]]::new()
$clients = [System.Collections.Generic.List[System.Net.Http.HttpClient]]::new()
$state = @{
    Email = 'sprint1.' + [Guid]::NewGuid().ToString('N') + '@example.com'
    Password = 'S1!aA' + [Guid]::NewGuid().ToString('N')
    Registered = $false
    Locked = $false
}

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
    Assert-True $value.Success 'Không tìm thấy token chống giả mạo trong biểu mẫu.'
    return [System.Net.WebUtility]::HtmlDecode($value.Groups[1].Value)
}

function Assert-RedirectPath {
    param($Response, [string]$ExpectedPath)
    Assert-True ($Response.Status -eq 302) "Cần HTTP 302, nhận $($Response.Status)."
    Assert-True (-not [string]::IsNullOrWhiteSpace($Response.Location)) 'Thiếu địa chỉ chuyển hướng.'
    $target = [Uri]::new($baseUri, $Response.Location)
    Assert-True ($target.Authority -eq $baseUri.Authority -and $target.Scheme -eq $baseUri.Scheme) 'Chuyển hướng ra ngoài website.'
    Assert-True ($target.AbsolutePath.TrimEnd('/') -eq $ExpectedPath.TrimEnd('/')) "Chuyển hướng không đến $ExpectedPath."
}

function Assert-PasswordNotRendered {
    param([string]$Html, [string]$Password)
    $decoded = [System.Net.WebUtility]::HtmlDecode($Html)
    Assert-True (-not $decoded.Contains($Password)) 'Mật khẩu xuất hiện trong HTML trả về.'
}

function Assert-Denied {
    param([System.Net.Http.HttpClient]$Client, [string]$Path)
    $response = Send-TestRequest $Client $Path
    Assert-RedirectPath $response '/Account/AccessDenied'
    $denied = Send-TestRequest $Client $response.Location
    Assert-True ($denied.Status -eq 403) "Trang từ chối truy cập cần HTTP 403, nhận $($denied.Status)."
}

function Test-Case {
    param([string]$Name, [scriptblock]$Check)
    try {
        & $Check
        $results.Add([pscustomobject]@{ name = $Name; status = 'PASS'; detail = '' })
        Write-Host "PASS - $Name"
    }
    catch {
        # Never include submitted form data, passwords or HTML in this report.
        $detail = $_.Exception.Message
        $results.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; detail = $detail })
        Write-Host "FAIL - $Name : $detail"
    }
}

function Submit-Login {
    param([System.Net.Http.HttpClient]$Client, [string]$Email, [string]$Password, [string]$ReturnUrl = '')
    $form = Send-TestRequest $Client '/Account/Login'
    Assert-True ($form.Status -eq 200) "Không mở được biểu mẫu đăng nhập: HTTP $($form.Status)."
    return Send-TestRequest $Client '/Account/Login' 'POST' @{
        Email = $Email; Password = $Password; RememberMe = 'false'; ReturnUrl = $ReturnUrl
        __RequestVerificationToken = Get-FormToken $form.Body
    }
}

try {
    $anonymous = New-TestClient
    $registeredClient = New-TestClient

    Test-Case 'Trang chủ trả về HTTP 200' {
        $response = Send-TestRequest $anonymous '/'
        Assert-True ($response.Status -eq 200) "Cần HTTP 200, nhận $($response.Status)."
    }

    foreach ($path in '/Candidate', '/Admissions', '/Admin') {
        Test-Case "Khách chưa đăng nhập được chuyển đến đăng nhập: $path" {
            $response = Send-TestRequest $anonymous $path
            Assert-RedirectPath $response '/Account/Login'
        }
    }

    Test-Case 'Đăng ký sai dữ liệu hiện lỗi và không điền lại mật khẩu' {
        $form = Send-TestRequest $anonymous '/Account/Register'
        Assert-True ($form.Status -eq 200) "Biểu mẫu đăng ký trả về HTTP $($form.Status)."
        $response = Send-TestRequest $anonymous '/Account/Register' 'POST' @{
            FullName = ''; Email = 'email-khong-hop-le'; Password = $state.Password
            ConfirmPassword = 'KhacMatKhau1!'
            __RequestVerificationToken = Get-FormToken $form.Body
        }
        Assert-True ($response.Status -eq 200) "Cần biểu mẫu lỗi HTTP 200, nhận $($response.Status)."
        $html = [System.Net.WebUtility]::HtmlDecode($response.Body)
        Assert-True ($html.Contains('Vui lòng nhập họ và tên.')) 'Thiếu lỗi họ tên bắt buộc.'
        Assert-True ($html.Contains('Email không đúng định dạng.')) 'Thiếu lỗi định dạng email.'
        Assert-True ($html.Contains('Mật khẩu nhập lại không khớp.')) 'Thiếu lỗi xác nhận mật khẩu.'
        Assert-PasswordNotRendered $response.Body $state.Password
    }

    Test-Case 'Mật khẩu yếu bị từ chối dù các thông tin khác hợp lệ' {
        $weakClient = New-TestClient
        $form = Send-TestRequest $weakClient '/Account/Register'
        $response = Send-TestRequest $weakClient '/Account/Register' 'POST' @{
            FullName = 'Kiểm Thử Mật Khẩu'; Email = 'weak.' + [Guid]::NewGuid().ToString('N') + '@example.com'
            Password = 'abcdefgh'; ConfirmPassword = 'abcdefgh'
            __RequestVerificationToken = Get-FormToken $form.Body
        }
        Assert-True ($response.Status -eq 200) "Mật khẩu yếu phải trả lại biểu mẫu, nhận HTTP $($response.Status)."
        $html = [System.Net.WebUtility]::HtmlDecode($response.Body)
        Assert-True ($html.Contains('Mật khẩu phải có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.')) 'Thiếu lỗi chính sách mật khẩu.'
        Assert-PasswordNotRendered $response.Body 'abcdefgh'
        $candidate = Send-TestRequest $weakClient '/Candidate'
        Assert-RedirectPath $candidate '/Account/Login'
    }

    Test-Case 'Họ tên chỉ chứa khoảng trắng bị từ chối' {
        $blankNameClient = New-TestClient
        $form = Send-TestRequest $blankNameClient '/Account/Register'
        $response = Send-TestRequest $blankNameClient '/Account/Register' 'POST' @{
            FullName = '   '; Email = 'blank.' + [Guid]::NewGuid().ToString('N') + '@example.com'
            Password = $state.Password; ConfirmPassword = $state.Password
            __RequestVerificationToken = Get-FormToken $form.Body
        }
        Assert-True ($response.Status -eq 200) "Họ tên trống phải trả lại biểu mẫu, nhận HTTP $($response.Status)."
        $html = [System.Net.WebUtility]::HtmlDecode($response.Body)
        Assert-True ($html.Contains('Vui lòng nhập họ và tên.') -or $html.Contains('Họ tên phải từ 2 đến 100 ký tự.')) 'Thiếu lỗi họ tên bắt buộc.'
        Assert-PasswordNotRendered $response.Body $state.Password
        $candidate = Send-TestRequest $blankNameClient '/Candidate'
        Assert-RedirectPath $candidate '/Account/Login'
    }

    Test-Case 'Đăng ký hợp lệ, bỏ qua vai trò quản trị được gửi thêm' {
        $form = Send-TestRequest $registeredClient '/Account/Register'
        $response = Send-TestRequest $registeredClient '/Account/Register' 'POST' @{
            FullName = 'Kiểm thử Sprint Một'; Email = $state.Email
            Password = $state.Password; ConfirmPassword = $state.Password
            Role = 'QuanTriVien'; Roles = 'QuanTriVien'
            __RequestVerificationToken = Get-FormToken $form.Body
        }
        Assert-RedirectPath $response '/Dashboard'
        $state.Registered = $true
        $candidate = Send-TestRequest $registeredClient '/Candidate'
        Assert-True ($candidate.Status -eq 200) "Tài khoản mới chưa vào được khu vực thí sinh: HTTP $($candidate.Status)."
        Assert-Denied $registeredClient '/Admin'
        Assert-Denied $registeredClient '/Admissions'
    }

    Test-Case 'Email đã đăng ký bị từ chối kể cả khi đổi chữ hoa' {
        Assert-True $state.Registered 'Cần tạo tài khoản kiểm thử thành công trước.'
        $other = New-TestClient
        $form = Send-TestRequest $other '/Account/Register'
        $response = Send-TestRequest $other '/Account/Register' 'POST' @{
            FullName = 'Người Kiểm Thử'; Email = $state.Email.ToUpperInvariant()
            Password = $state.Password; ConfirmPassword = $state.Password
            __RequestVerificationToken = Get-FormToken $form.Body
        }
        Assert-True ($response.Status -eq 200) "Email trùng phải trả lại biểu mẫu, nhận HTTP $($response.Status)."
        Assert-True ([System.Net.WebUtility]::HtmlDecode($response.Body).Contains('Email này đã được sử dụng.')) 'Thiếu thông báo email đã được sử dụng.'
        Assert-PasswordNotRendered $response.Body $state.Password
    }

    Test-Case 'Đăng nhập sai hiện lỗi chung và không điền lại mật khẩu' {
        $response = Submit-Login $anonymous $state.Email 'SaiMatKhau1!'
        Assert-True ($response.Status -eq 200) "Đăng nhập sai phải trả lại biểu mẫu, nhận HTTP $($response.Status)."
        Assert-True ([System.Net.WebUtility]::HtmlDecode($response.Body).Contains('Email hoặc mật khẩu không đúng.')) 'Thiếu thông báo đăng nhập thất bại.'
        Assert-PasswordNotRendered $response.Body 'SaiMatKhau1!'
    }

    Test-Case 'Đăng nhập đúng và từ chối ReturnUrl trỏ ra ngoài website' {
        Assert-True $state.Registered 'Cần tạo tài khoản kiểm thử thành công trước.'
        $response = Submit-Login $anonymous $state.Email $state.Password 'https://example.com/'
        Assert-RedirectPath $response '/Dashboard'
        $dashboard = Send-TestRequest $anonymous '/Dashboard'
        Assert-RedirectPath $dashboard '/Candidate'
    }

    Test-Case 'Đăng nhập đúng giữ ReturnUrl nằm trong website' {
        Assert-True $state.Registered 'Cần tạo tài khoản kiểm thử thành công trước.'
        $localReturnClient = New-TestClient
        $response = Submit-Login $localReturnClient $state.Email $state.Password '/Candidate'
        Assert-RedirectPath $response '/Candidate'
        $candidate = Send-TestRequest $localReturnClient '/Candidate'
        Assert-True ($candidate.Status -eq 200) "Trang ReturnUrl hợp lệ trả về HTTP $($candidate.Status)."
    }

    $roles = @(
        @{ Email = 'thisinh.demo@tuyensinh.local'; Own = '/Candidate'; Others = @('/Admissions', '/Admin'); Name = 'Thí sinh' },
        @{ Email = 'canbo.demo@tuyensinh.local'; Own = '/Admissions'; Others = @('/Candidate', '/Admin'); Name = 'Cán bộ tuyển sinh' },
        @{ Email = 'admin.demo@tuyensinh.local'; Own = '/Admin'; Others = @('/Candidate', '/Admissions'); Name = 'Quản trị viên' }
    )
    foreach ($role in $roles) {
        Test-Case "Phân quyền vai trò $($role.Name): đúng khu vực, chặn hai khu vực còn lại" {
            $roleClient = New-TestClient
            $response = Submit-Login $roleClient $role.Email $DemoPassword
            Assert-RedirectPath $response '/Dashboard'
            $dashboard = Send-TestRequest $roleClient '/Dashboard'
            Assert-RedirectPath $dashboard $role.Own
            $allowed = Send-TestRequest $roleClient $role.Own
            Assert-True ($allowed.Status -eq 200) "Khu vực đúng vai trò trả về HTTP $($allowed.Status)."
            foreach ($otherPath in $role.Others) { Assert-Denied $roleClient $otherPath }
        }
    }

    Test-Case 'POST đăng nhập thiếu token chống giả mạo bị từ chối HTTP 400' {
        $freshClient = New-TestClient
        $response = Send-TestRequest $freshClient '/Account/Login' 'POST' @{
            Email = $state.Email; Password = $state.Password
        }
        Assert-True ($response.Status -eq 400) "Cần HTTP 400, nhận $($response.Status)."
    }

    Test-Case 'POST đăng ký thiếu token chống giả mạo bị từ chối HTTP 400' {
        $freshClient = New-TestClient
        $response = Send-TestRequest $freshClient '/Account/Register' 'POST' @{
            FullName = 'Kiểm Thử CSRF'; Email = 'csrf.' + $state.Email
            Password = $state.Password; ConfirmPassword = $state.Password
        }
        Assert-True ($response.Status -eq 400) "Cần HTTP 400, nhận $($response.Status)."
    }

    Test-Case 'Đăng xuất bằng GET bị từ chối và phiên vẫn đăng nhập' {
        $response = Send-TestRequest $registeredClient '/Account/Logout'
        # Conventional MVC routing can return 404 for an action that only accepts POST.
        Assert-True ($response.Status -in 404, 405) "Cần HTTP 404 hoặc 405, nhận $($response.Status)."
        $candidate = Send-TestRequest $registeredClient '/Candidate'
        Assert-True ($candidate.Status -eq 200) 'Yêu cầu GET đã thay đổi phiên đăng nhập.'
    }

    Test-Case 'Đăng xuất thiếu token bị từ chối và phiên vẫn đăng nhập' {
        $response = Send-TestRequest $registeredClient '/Account/Logout' 'POST' @{}
        Assert-True ($response.Status -eq 400) "Cần HTTP 400, nhận $($response.Status)."
        $candidate = Send-TestRequest $registeredClient '/Candidate'
        Assert-True ($candidate.Status -eq 200) 'Yêu cầu đăng xuất không hợp lệ đã thay đổi phiên đăng nhập.'
    }

    Test-Case 'Đăng xuất hợp lệ kết thúc phiên và bảo vệ lại trang thí sinh' {
        $page = Send-TestRequest $registeredClient '/Candidate'
        $response = Send-TestRequest $registeredClient '/Account/Logout' 'POST' @{
            __RequestVerificationToken = Get-FormToken $page.Body
        }
        Assert-RedirectPath $response '/'
        $protected = Send-TestRequest $registeredClient '/Candidate'
        Assert-RedirectPath $protected '/Account/Login'
    }

    Test-Case 'Sai mật khẩu 5 lần khóa tài khoản kiểm thử và chặn cả mật khẩu đúng' {
        Assert-True $state.Registered 'Cần tạo tài khoản kiểm thử thành công trước.'
        $lockoutClient = New-TestClient
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            $wrongPassword = 'Sai' + $attempt + '!aA' + [Guid]::NewGuid().ToString('N')
            $response = Submit-Login $lockoutClient $state.Email $wrongPassword
            Assert-True ($response.Status -eq 200) "Lần đăng nhập sai thứ $attempt cần HTTP 200, nhận $($response.Status)."
            $html = [System.Net.WebUtility]::HtmlDecode($response.Body)
            if ($attempt -lt 5) {
                Assert-True ($html.Contains('Email hoặc mật khẩu không đúng.')) "Lần đăng nhập sai thứ $attempt thiếu thông báo thất bại."
            }
            else {
                Assert-True ($html.Contains('Tài khoản tạm khóa')) 'Sau 5 lần sai chưa có thông báo khóa tài khoản.'
                $state.Locked = $true
            }
            Assert-PasswordNotRendered $response.Body $wrongPassword
        }
        $correctPassword = Submit-Login $lockoutClient $state.Email $state.Password
        Assert-True ($correctPassword.Status -eq 200) "Tài khoản đang khóa phải trả lại biểu mẫu, nhận HTTP $($correctPassword.Status)."
        Assert-True ([System.Net.WebUtility]::HtmlDecode($correctPassword.Body).Contains('Tài khoản tạm khóa')) 'Mật khẩu đúng đã bỏ qua trạng thái khóa tài khoản.'
        Assert-PasswordNotRendered $correctPassword.Body $state.Password
        $protected = Send-TestRequest $lockoutClient '/Candidate'
        Assert-RedirectPath $protected '/Account/Login'
    }

    Test-Case 'Giới hạn yêu cầu đăng nhập trả HTTP 429 khi vượt 20 POST trong một phút' {
        # This is the final HTTP case. Earlier cases make 19 account POSTs in total.
        # Missing CSRF produces 400 while a quota remains, without changing an account.
        $limitClient = New-TestClient
        $first = Send-TestRequest $limitClient '/Account/Login' 'POST' @{}
        $second = Send-TestRequest $limitClient '/Account/Login' 'POST' @{}
        Assert-True ($first.Status -in 400, 429) "Yêu cầu thứ 20 cần HTTP 400 hoặc 429, nhận $($first.Status)."
        Assert-True ($second.Status -eq 429) "Yêu cầu vượt hạn cần HTTP 429, nhận $($second.Status)."
        Assert-True ([System.Net.WebUtility]::HtmlDecode($second.Body).Contains('quá nhiều yêu cầu')) 'Thiếu thông báo giới hạn yêu cầu.'
    }
}
finally {
    foreach ($client in $clients) { $client.Dispose() }
    $failedCount = @($results | Where-Object status -eq 'FAIL').Count
    $report = [ordered]@{
        timestampUtc = [DateTimeOffset]::UtcNow.ToString('o')
        baseUrl = $BaseUrl
        total = $results.Count
        passed = $results.Count - $failedCount
        failed = $failedCount
        createdTestAccount = $(if ($state.Registered) { $state.Email } else { $null })
        testAccountLocked = $state.Locked
        note = 'Tài khoản kiểm thử được giữ lại trong database; ca khóa tài khoản sẽ khóa tài khoản này trong 5 phút. Báo cáo không chứa mật khẩu.'
        cases = @($results.ToArray())
    }
    if (-not [IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath = Join-Path (Split-Path -Parent $PSScriptRoot) $OutputPath
    }
    $outputDirectory = Split-Path -Parent $OutputPath
    if ($outputDirectory) { [IO.Directory]::CreateDirectory($outputDirectory) | Out-Null }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Host "Kết quả: $($report.passed)/$($report.total) PASS. Báo cáo: $OutputPath"
}

if ($failedCount -gt 0) { exit 1 }
exit 0
