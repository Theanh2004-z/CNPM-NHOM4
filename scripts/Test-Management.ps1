#requires -Version 7.0
# Run against a freshly started server, or after one minute without account POSTs.
# The final case intentionally exhausts the per-IP account request limit.
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [Parameter(Mandatory = $true)]
    [string]$DemoPassword,
    [string]$OutputPath = '.local/management-verification.json'
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

$admin = New-TestClient
$caseData = @{}
$tag = 'T' + [Guid]::NewGuid().ToString('N').Substring(0, 8).ToUpperInvariant()
function Post-Form($Client, [string]$Path, [hashtable]$Data) {
    $page = Send-TestRequest $Client $Path
    Assert-True ($page.Status -eq 200) ('Không mở được form: ' + $Path)
    $Data.__RequestVerificationToken = Get-FormToken $page.Body
    Send-TestRequest $Client $Path 'POST' $Data
}
function Expect-Error($Response, [string]$Text) {
    Assert-True ($Response.Status -eq 200) ('Cần form báo lỗi, nhận ' + $Response.Status)
    Assert-True ([Net.WebUtility]::HtmlDecode($Response.Body).Contains($Text)) ('Thiếu lỗi: ' + $Text)
}
function Find-RowId([string]$Path, [string]$Code) {
    $page = Send-TestRequest $admin $Path
    $row = [regex]::Match($page.Body, '<tr data-row-id="(\d+)" data-code="' + [regex]::Escape($Code) + '"')
    Assert-True $row.Success ('Không tìm thấy bản ghi ' + $Code)
    [int]$row.Groups[1].Value
}
function Major-Data([string]$Code, [string]$Name = 'Ngành kiểm thử', [bool]$Active = $true) {
    @{ Id='0'; Code=$Code; Name=$Name; Description='Mô tả kiểm thử'; IsActive=$Active.ToString().ToLowerInvariant() }
}
function Method-Data([string]$Code, [string]$Name = 'Phương thức kiểm thử') {
    @{ Id='0'; Code=$Code; Name=$Name; RequirementsDescription='Điều kiện kiểm thử'; IsActive='true' }
}
function Round-Data([string]$Code, [string]$Start = '2026-11-01T08:00', [string]$End = '2027-01-01T17:00') {
    @{ Id='0'; Code=$Code; Name='Đợt kiểm thử'; AdmissionYear='2026'; StartAt=$Start; EndAt=$End; IsActive='true' }
}
try {
    Test-Case 'Quản trị đăng nhập và mở đủ bốn chức năng' {
        Assert-RedirectPath (Submit-Login $admin 'admin.demo@tuyensinh.local' $DemoPassword) '/Dashboard'
        foreach ($path in '/Majors','/AdmissionMethods','/AdmissionRounds','/Quotas') {
            Assert-True ((Send-TestRequest $admin $path).Status -eq 200) ('Không mở được ' + $path)
        }
    }
    Test-Case 'Khách chưa đăng nhập bị chặn ở cả bốn chức năng' {
        $guest = New-TestClient
        foreach ($path in '/Majors','/AdmissionMethods','/AdmissionRounds','/Quotas') { Assert-RedirectPath (Send-TestRequest $guest $path) '/Account/Login' }
    }
    foreach ($email in 'thisinh.demo@tuyensinh.local','canbo.demo@tuyensinh.local') {
        Test-Case ('Tài khoản sai quyền không đọc/ghi được chức năng: ' + $email) {
            $client = New-TestClient
            Assert-RedirectPath (Submit-Login $client $email $DemoPassword) '/Dashboard'
            foreach ($path in '/Majors','/AdmissionMethods','/AdmissionRounds','/Quotas') {
                Assert-Denied $client $path
                $write = Send-TestRequest $client ($path + '/Create') 'POST' @{Code='KHONGDUOCGHI'}
                Assert-RedirectPath $write '/Account/AccessDenied'
            }
        }
    }
    Test-Case 'Thao tác quản trị thiếu CSRF bị từ chối' {
        Assert-True ((Send-TestRequest $admin '/Majors/Create' 'POST' (Major-Data $tag)).Status -eq 400) 'CSRF chưa được chặn.'
    }
    Test-Case 'Tên ngành chỉ có khoảng trắng bị từ chối' {
        Expect-Error (Post-Form $admin '/Majors/Create' (Major-Data ($tag+'BAD') '   ')) 'Vui lòng nhập tên ngành.'
    }
    Test-Case 'Tạo ngành, chuẩn hóa mã và đọc lại từ danh sách' {
        Assert-RedirectPath (Post-Form $admin '/Majors/Create' (Major-Data (' '+$tag.ToLowerInvariant()+' ') 'Công nghệ kiểm thử')) '/Majors'
        $caseData.MajorId = Find-RowId '/Majors' $tag
    }
    Test-Case 'Mã ngành trùng bị từ chối' { Expect-Error (Post-Form $admin '/Majors/Create' (Major-Data $tag.ToLowerInvariant())) 'Mã ngành đã tồn tại.' }
    Test-Case 'Sửa ngành không bị báo trùng với chính bản ghi' {
        $data=Major-Data $tag 'Ngành đã cập nhật'; $data.Id=$caseData.MajorId
        Assert-RedirectPath (Post-Form $admin ('/Majors/Edit/'+$caseData.MajorId) $data) '/Majors'
        Assert-True ([Net.WebUtility]::HtmlDecode((Send-TestRequest $admin ('/Majors?q='+$tag)).Body).Contains('Ngành đã cập nhật')) 'Không lưu tên ngành.'
    }
    Test-Case 'Mã phương thức trùng bị từ chối sau khi tạo thành công' {
        Assert-RedirectPath (Post-Form $admin '/AdmissionMethods/Create' (Method-Data $tag 'Học bạ kiểm thử')) '/AdmissionMethods'
        $caseData.MethodId=Find-RowId '/AdmissionMethods' $tag
        Expect-Error (Post-Form $admin '/AdmissionMethods/Create' (Method-Data $tag.ToLowerInvariant())) 'Mã phương thức đã tồn tại.'
    }
    Test-Case 'Sửa phương thức và đọc lại điều kiện xét tuyển' {
        $data=Method-Data $tag 'Phương thức đã cập nhật'; $data.Id=$caseData.MethodId; $data.RequirementsDescription='Điều kiện đã cập nhật'
        Assert-RedirectPath (Post-Form $admin ('/AdmissionMethods/Edit/'+$caseData.MethodId) $data) '/AdmissionMethods'
        Assert-True ([Net.WebUtility]::HtmlDecode((Send-TestRequest $admin '/AdmissionMethods').Body).Contains('Điều kiện đã cập nhật')) 'Không lưu điều kiện.'
    }
    Test-Case 'Đợt có thời gian đảo ngược bị từ chối' { Expect-Error (Post-Form $admin '/AdmissionRounds/Create' (Round-Data $tag '2026-12-01T08:00' '2026-11-01T08:00')) 'Thời điểm đóng phải sau thời điểm mở.' }
    Test-Case 'Đợt có hai thời điểm bằng nhau bị từ chối' { Expect-Error (Post-Form $admin '/AdmissionRounds/Create' (Round-Data $tag '2026-12-01T08:00' '2026-12-01T08:00')) 'Thời điểm đóng phải sau thời điểm mở.' }
    Test-Case 'Tạo đợt hợp lệ và chống trùng mã đợt' {
        $response=Post-Form $admin '/AdmissionRounds/Create' (Round-Data $tag)
        Assert-True ($response.Status -eq 302 -and $response.Location -match '/AdmissionRounds/Details/') 'Không lưu được đợt.'
        $caseData.RoundId=Find-RowId '/AdmissionRounds' $tag
        Expect-Error (Post-Form $admin '/AdmissionRounds/Create' (Round-Data $tag.ToLowerInvariant())) 'Mã đợt tuyển sinh đã tồn tại.'
    }
    Test-Case 'Thiếu phương thức trên cấu hình bật bị từ chối' {
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=10;IsEnabled='true'}) 'Chọn ít nhất một phương thức'
    }
    Test-Case 'Chỉ tiêu âm bị từ chối' {
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=-1;IsEnabled='true';SelectedMethodIds=$caseData.MethodId}) 'Chỉ tiêu phải là số nguyên không âm.'
    }
    Test-Case 'Chỉ tiêu thập phân bị từ chối' {
        $response=Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota='1.5';IsEnabled='true';SelectedMethodIds=$caseData.MethodId}
        Assert-True ($response.Status -eq 200 -and $response.Body -match 'validation-summary-errors') 'Chỉ tiêu thập phân được chấp nhận.'
    }
    Test-Case 'Phương thức giả mạo không tồn tại bị từ chối' {
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=10;IsEnabled='true';SelectedMethodIds=2147483647}) 'Phương thức không tồn tại'
    }
    Test-Case 'Tạo cấu hình chỉ tiêu bằng 0 và phương thức áp dụng' {
        $response=Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=0;IsEnabled='true';SelectedMethodIds=$caseData.MethodId}
        Assert-RedirectPath $response '/Quotas'
        $page=Send-TestRequest $admin ('/Quotas?roundId='+$caseData.RoundId)
        $row=[regex]::Match([Net.WebUtility]::HtmlDecode($page.Body),'<tr data-row-id="(\d+)">(?:(?!</tr>).)*'+$tag+'(?:(?!</tr>).)*</tr>','Singleline')
        Assert-True $row.Success 'Không đọc lại được cấu hình.'
        $caseData.QuotaId=[int]$row.Groups[1].Value
    }
    Test-Case 'Cặp ngành và đợt trùng bị từ chối' {
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=10;IsEnabled='true';SelectedMethodIds=$caseData.MethodId}) 'Ngành này đã có cấu hình'
    }
    Test-Case 'Sửa chỉ tiêu và lưu lại phương thức liên kết' {
        $response=Post-Form $admin ('/Quotas/Edit/'+$caseData.QuotaId) @{Id=$caseData.QuotaId;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=42;IsEnabled='true';SelectedMethodIds=$caseData.MethodId}
        Assert-RedirectPath $response '/Quotas'
        $page=Send-TestRequest $admin ('/Quotas/Edit/'+$caseData.QuotaId)
        Assert-True ($page.Body -match 'name="Quota"[^>]*value="42"') 'Chỉ tiêu không giữ lại sau khi tải lại.'
    }
    Test-Case 'Ẩn ngành vẫn giữ chỉ tiêu và liên kết' {
        $page=Send-TestRequest $admin '/Majors'
        $response=Send-TestRequest $admin ('/Majors/SetActive/'+$caseData.MajorId) 'POST' @{isActive='false';__RequestVerificationToken=(Get-FormToken $page.Body)}
        Assert-RedirectPath $response '/Majors'
        $quota=Send-TestRequest $admin ('/Quotas?roundId='+$caseData.RoundId)
        Assert-True ($quota.Body.Contains($tag)) 'Ẩn ngành làm mất cấu hình.'
        Assert-True ([Net.WebUtility]::HtmlDecode($quota.Body).Contains('Ngành đã ẩn')) 'Không hiển thị ngành đã ẩn.'
    }
    Test-Case 'Ngành đã ẩn không thể dùng để tạo cấu hình mới' {
        $extra=$tag+'NEW'; $response=Post-Form $admin '/AdmissionRounds/Create' (Round-Data $extra)
        $newRoundId=Find-RowId '/AdmissionRounds' $extra
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$newRoundId;MajorId=$caseData.MajorId;Quota=1;IsEnabled='true';SelectedMethodIds=$caseData.MethodId}) 'Ngành học không tồn tại hoặc đã ẩn.'
        $caseData.NewRoundId=$newRoundId
    }
    Test-Case 'Sử dụng lại ngành' {
        $page=Send-TestRequest $admin '/Majors'
        Assert-RedirectPath (Send-TestRequest $admin ('/Majors/SetActive/'+$caseData.MajorId) 'POST' @{isActive='true';__RequestVerificationToken=(Get-FormToken $page.Body)}) '/Majors'
    }
    Test-Case 'Ngừng phương thức vẫn giữ liên kết cũ, không cho chọn mới' {
        $page=Send-TestRequest $admin '/AdmissionMethods'
        Assert-RedirectPath (Send-TestRequest $admin ('/AdmissionMethods/SetActive/'+$caseData.MethodId) 'POST' @{isActive='false';__RequestVerificationToken=(Get-FormToken $page.Body)}) '/AdmissionMethods'
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.NewRoundId;MajorId=$caseData.MajorId;Quota=1;IsEnabled='true';SelectedMethodIds=$caseData.MethodId}) 'Phương thức không tồn tại hoặc đã ngừng sử dụng.'
        Assert-True ([Net.WebUtility]::HtmlDecode((Send-TestRequest $admin ('/Quotas/Edit/'+$caseData.QuotaId)).Body).Contains('Đã ngừng sử dụng.')) 'Mất liên kết cũ khi phương thức ngừng.'
    }
    Test-Case 'Tắt cấu hình không xóa bản ghi' {
        Assert-RedirectPath (Post-Form $admin ('/Quotas/Edit/'+$caseData.QuotaId) @{Id=$caseData.QuotaId;RoundId=$caseData.RoundId;MajorId=$caseData.MajorId;Quota=42;IsEnabled='false'}) '/Quotas'
        Assert-True ((Send-TestRequest $admin ('/Quotas/Edit/'+$caseData.QuotaId)).Status -eq 200) 'Cấu hình bị xóa.'
    }
    Test-Case 'Đợt ngừng sử dụng không cho cấu hình mới' {
        $page=Send-TestRequest $admin '/AdmissionRounds'
        Assert-RedirectPath (Send-TestRequest $admin ('/AdmissionRounds/SetActive/'+$caseData.NewRoundId) 'POST' @{isActive='false';__RequestVerificationToken=(Get-FormToken $page.Body)}) '/AdmissionRounds'
        Expect-Error (Post-Form $admin '/Quotas/Create' @{Id=0;RoundId=$caseData.NewRoundId;MajorId=$caseData.MajorId;Quota=1;IsEnabled='false'}) 'Đợt tuyển sinh không tồn tại hoặc đã ngừng sử dụng.'
    }
    Test-Case 'Không đổi đợt của cấu hình lịch sử qua dữ liệu giả mạo' {
        Expect-Error (Post-Form $admin ('/Quotas/Edit/'+$caseData.QuotaId) @{Id=$caseData.QuotaId;RoundId=$caseData.NewRoundId;MajorId=$caseData.MajorId;Quota=1;IsEnabled='false'}) 'Không thể chuyển cấu hình'
    }
    Test-Case 'ID ngành không tồn tại trả 404' { Assert-True ((Send-TestRequest $admin '/Majors/Edit/2147483647').Status -eq 404) 'Thiếu 404.' }
}
finally {
    foreach($client in $clients) { $client.Dispose() }
    $failCount=@($results | Where-Object status -eq 'FAIL').Count
    $report=[ordered]@{timestampUtc=[DateTimeOffset]::UtcNow.ToString('o');baseUrl=$BaseUrl;total=$results.Count;passed=$results.Count-$failCount;failed=$failCount;fixturePrefix=$tag;fixtureIds=$caseData;note='Chỉ chạy trên database thử riêng. Các bản ghi kiểm thử được giữ lại; báo cáo không chứa mật khẩu.';cases=@($results.ToArray())}
    if(-not [IO.Path]::IsPathRooted($OutputPath)) { $OutputPath=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputPath }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Host ('Kết quả: '+$report.passed+'/'+$report.total+' PASS. Báo cáo: '+$OutputPath)
}
if($failCount -gt 0) { exit 1 }; exit 0