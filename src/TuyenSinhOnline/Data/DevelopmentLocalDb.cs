using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace TuyenSinhOnline.Data;

public static class DevelopmentLocalDb
{
    // Chỉ chuẩn bị instance riêng của bài tập; không thay đổi SQL Server khác.
    public static async Task EnsureReadyAsync(string connectionString)
    {
        if (!OperatingSystem.IsWindows()) return;
        var connection = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.DataSource, @"(localdb)\TuyenSinhOnlineDev",
                StringComparison.OrdinalIgnoreCase)) return;

        var instances = await RunAsync("info");
        if (!instances.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(name => name.Trim().Equals("TuyenSinhOnlineDev", StringComparison.OrdinalIgnoreCase)))
        {
            await RunAsync("create", "TuyenSinhOnlineDev");
        }
        await RunAsync("start", "TuyenSinhOnlineDev");

        // Tự động kiểm tra và gắn lại (attach) file database nếu file đã tồn tại nhưng chưa được gắn vào instance
        if (!string.IsNullOrWhiteSpace(connection.InitialCatalog))
        {
            try
            {
                var masterBuilder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master"
                };
                await using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
                await masterConn.OpenAsync();
                await using var checkCmd = masterConn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @dbName";
                checkCmd.Parameters.AddWithValue("@dbName", connection.InitialCatalog);
                var exists = (int?)await checkCmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var mdfPath = Path.Combine(userProfile, $"{connection.InitialCatalog}.mdf");
                    var ldfPath = Path.Combine(userProfile, $"{connection.InitialCatalog}_log.ldf");
                    if (File.Exists(mdfPath))
                    {
                        await using var attachCmd = masterConn.CreateCommand();
                        attachCmd.CommandText = File.Exists(ldfPath)
                            ? $"CREATE DATABASE [{connection.InitialCatalog}] ON (FILENAME = '{mdfPath}'), (FILENAME = '{ldfPath}') FOR ATTACH"
                            : $"CREATE DATABASE [{connection.InitialCatalog}] ON (FILENAME = '{mdfPath}') FOR ATTACH";
                        await attachCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi attach tự động nếu có cơ chế khác xử lý
            }
        }
    }

    private static async Task<string> RunAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("SqlLocalDB.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Không thể mở công cụ SQL Server LocalDB.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("SQL Server LocalDB khởi động quá lâu. Hãy chạy scripts/Start-Website.ps1 để kiểm tra.");
            }
            var result = await output;
            var errorText = await error;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Không thể chuẩn bị LocalDB TuyenSinhOnlineDev. " + result + errorText);
            return result;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Không tìm thấy SqlLocalDB.exe. Cần cài SQL Server Express LocalDB hoặc đặt kết nối SQL Server riêng theo README.", ex);
        }
    }
}
