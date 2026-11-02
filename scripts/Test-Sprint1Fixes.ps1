#requires -Version 7.0
# Run only against an isolated CNPM_Sprint1_Fix_* database.
# Verifies time precision, Vietnamese validation, and stable method links.
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [ValidatePattern('^CNPM_Sprint1_Fix_[a-zA-Z0-9_]+$')][string]$DatabaseName = 'CNPM_Sprint1_Fix_20261102',
    [Parameter(Mandatory = $true)]
    [string]$DemoPassword,
    [string]$OutputPath = '.local/fix-results/focused-tests.json'
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

$admin=New-TestClient
$fixture='P'+[Guid]::NewGuid().ToString('N').Substring(0,8).ToUpperInvariant()
$ids=@{}
function Post-Page([string]$Path,[hashtable]$Data,[int[]]$Methods=@()) {
    $page=Send-TestRequest $admin $Path
    Assert-True ($page.Status -eq 200) ('Cannot get '+$Path)
    $pairs=[Collections.Generic.List[Collections.Generic.KeyValuePair[string,string]]]::new()
    foreach($key in $Data.Keys){$pairs.Add([Collections.Generic.KeyValuePair[string,string]]::new([string]$key,[string]$Data[$key]))}
    $pairs.Add([Collections.Generic.KeyValuePair[string,string]]::new('__RequestVerificationToken',(Get-FormToken $page.Body)))
    foreach($methodId in $Methods){$pairs.Add([Collections.Generic.KeyValuePair[string,string]]::new('SelectedMethodIds',[string]$methodId))}
    $content=[Net.Http.FormUrlEncodedContent]::new($pairs)
    $response=$admin.PostAsync($Path,$content).GetAwaiter().GetResult()
    try{[pscustomobject]@{Status=[int]$response.StatusCode;Location=[string]$response.Headers.Location;Body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}}finally{$response.Dispose();$content.Dispose()}
}
function Find-Id([string]$Path,[string]$Code){$page=Send-TestRequest $admin $Path;$match=[regex]::Match($page.Body,'<tr data-row-id="(\d+)" data-code="'+$Code+'"');Assert-True $match.Success ('Missing row '+$Code);[int]$match.Groups[1].Value}
function Field-Value([string]$Html,[string]$Name){$tag=[regex]::Match($Html,'<input\b[^>]*\bname="'+$Name+'"[^>]*>');[Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'\bvalue="([^"]*)"').Groups[1].Value)}
function Read-AuditSql([string]$Query){$sqlOutput=@(& sqlcmd -S '.' -d $DatabaseName -E -C -h -1 -W -Q ('SET NOCOUNT ON; '+$Query));if($LASTEXITCODE -ne 0){throw 'SQL read failed'};($sqlOutput | Where-Object {-not[string]::IsNullOrWhiteSpace($_)}) -join '|'}
try{
    $login=Submit-Login $admin 'admin.demo@tuyensinh.local' $DemoPassword
    Assert-RedirectPath $login '/Dashboard'
    $roundData=@{Id=0;Code=$fixture;Name='Kiểm thử độ chính xác thời gian';AdmissionYear=2027;StartAt='2027-04-01T08:00:17';EndAt='2027-06-30T23:59:59';IsActive='true'}
    $created=Post-Page '/AdmissionRounds/Create' $roundData
    Assert-True ($created.Status -eq 302) 'Create precision fixture failed'
    $ids.Round=Find-Id '/AdmissionRounds' $fixture
    $page=Send-TestRequest $admin ('/AdmissionRounds/Edit/'+$ids.Round)
    $displayStart=Field-Value $page.Body 'StartAt';$displayEnd=Field-Value $page.Body 'EndAt'
    Test-Case 'Mở biểu mẫu sửa giữ nguyên độ chính xác giây của thời gian trong SQL' {
        Assert-True ($displayStart -eq '2027-04-01T08:00:17' -and $displayEnd -eq '2027-06-30T23:59:59') ('StartAt hiển thị='+$displayStart+'; EndAt hiển thị='+$displayEnd+'; mong đợi giữ :17 và :59.')
    }
    Test-Case 'Lưu biểu mẫu không thay đổi không được làm đổi thời điểm đóng' {
        $editData=$roundData.Clone();$editData.Id=$ids.Round;$editData.StartAt=$displayStart;$editData.EndAt=$displayEnd
        $saved=Post-Page ('/AdmissionRounds/Edit/'+$ids.Round) $editData
        Assert-True ($saved.Status -eq 302) 'Save fixture failed'
        $stored=Read-AuditSql ('SELECT CONVERT(varchar(30),EndAt,126) FROM dbo.AdmissionRounds WHERE Id='+[int]$ids.Round)
        Assert-True ($stored -eq '2027-06-30T23:59:59') ('EndAt trước lưu=2027-06-30T23:59:59; sau lưu='+$stored)
    }
    foreach($suffix in 'A','B'){$created=Post-Page '/AdmissionMethods/Create' @{Id=0;Code=$fixture+$suffix;Name='Phương thức '+$suffix;IsActive='true'};Assert-RedirectPath $created '/AdmissionMethods';$ids[$suffix]=Find-Id '/AdmissionMethods' ($fixture+$suffix)}
    $created=Post-Page '/Majors/Create' @{Id=0;Code=$fixture;Name='Ngành kiểm tra liên kết';IsActive='true'};Assert-RedirectPath $created '/Majors';$ids.Major=Find-Id '/Majors' $fixture
    Test-Case 'Một ngành trong đợt lưu đồng thời hai phương thức' {
        $created=Post-Page '/Quotas/Create' @{Id=0;RoundId=$ids.Round;MajorId=$ids.Major;Quota=12;IsEnabled='true'} @($ids.A,$ids.B)
        Assert-RedirectPath $created '/Quotas'
        $ids.Quota=[int](Read-AuditSql ('SELECT Id FROM dbo.RoundMajors WHERE RoundId='+[int]$ids.Round+' AND MajorId='+[int]$ids.Major))
        $count=[int](Read-AuditSql ('SELECT COUNT(*) FROM dbo.RoundMajorMethods WHERE IsEnabled=1 AND RoundMajorId='+[int]$ids.Quota))
        Assert-True ($count -eq 2) ('Số phương thức='+$count)
    }
    Test-Case 'Bỏ chọn rồi chọn lại phương thức giữ nguyên ID liên kết lịch sử' {
        $linkQuery='SELECT Id FROM dbo.RoundMajorMethods WHERE RoundMajorId='+[int]$ids.Quota+' AND MethodId='+[int]$ids.B
        $before=Read-AuditSql $linkQuery
        $edit=@{Id=$ids.Quota;RoundId=$ids.Round;MajorId=$ids.Major;Quota=12;IsEnabled='true'}
        Assert-RedirectPath (Post-Page ('/Quotas/Edit/'+$ids.Quota) $edit @($ids.A)) '/Quotas'
        Assert-RedirectPath (Post-Page ('/Quotas/Edit/'+$ids.Quota) $edit @($ids.A,$ids.B)) '/Quotas'
        $after=Read-AuditSql $linkQuery
        Assert-True ($before -eq $after) 'Link ID changed'
    }
    Test-Case 'Chỉ tiêu thập phân bị chặn bằng thông báo tiếng Việt' {
        $response=Post-Page '/Quotas/Create' @{Id=0;RoundId=$ids.Round;MajorId=$ids.Major;Quota='1.5';IsEnabled='false'}
        $html=[Net.WebUtility]::HtmlDecode($response.Body)
        Assert-True ($response.Status -eq 200 -and $html.Contains("Giá trị '1.5' không hợp lệ cho Chỉ tiêu.") -and -not $html.Contains('The value')) 'Thông báo nhập số chưa được Việt hóa.'
    }
    Test-Case 'Số vượt giới hạn kiểu int cũng được báo bằng tiếng Việt' {
        $response=Post-Page '/Quotas/Create' @{Id=0;RoundId=$ids.Round;MajorId=$ids.Major;Quota='2147483648';IsEnabled='false'}
        $html=[Net.WebUtility]::HtmlDecode($response.Body)
        Assert-True ($response.Status -eq 200 -and $html.Contains('không hợp lệ cho Chỉ tiêu.') -and -not $html.Contains('The value')) 'Thông báo tràn số chưa được Việt hóa.'
    }
}finally{
    foreach($client in $clients){$client.Dispose()}
    $failCount=@($results|Where-Object status -eq 'FAIL').Count
    $report=[ordered]@{timestampUtc=[DateTimeOffset]::UtcNow.ToString('o');total=$results.Count;passed=$results.Count-$failCount;failed=$failCount;fixturePrefix=$fixture;fixtureIds=$ids;database=$DatabaseName;cases=@($results.ToArray())}
    if(-not[IO.Path]::IsPathRooted($OutputPath)){$OutputPath=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputPath}
    $report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Host ('Focused checks: '+$report.passed+'/'+$report.total+' PASS')
}
if($failCount){exit 1};exit 0