$ErrorActionPreference = "Stop"
$sqlFile = "d:\RestaurantPOS\Database\RestaurantPOS_Database_utf16.sql"
# Use the direct pipe name from sqllocaldb info
$connString = "Server=np:\\.\pipe\LOCALDB#BD70ED73\tsql\query;Integrated Security=true;Initial Catalog=master"

Write-Host "Reading SQL file..."
$content = Get-Content -Path $sqlFile -Raw -Encoding unicode

Write-Host "Splitting statements by GO..."
$statements = [System.Text.RegularExpressions.Regex]::Split($content, "(?m)^GO\s*$", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

Write-Host "Connecting to SQL Server via Pipe..."
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()
try {
    $count = 0
    foreach ($stmt in $statements) {
        $cleanStmt = $stmt.Trim()
        if (![string]::IsNullOrWhiteSpace($cleanStmt)) {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $cleanStmt
            $cmd.ExecuteNonQuery() | Out-Null
            $count++
        }
    }
    Write-Host "Restore successful! Executed $count statement blocks."
} catch {
    Write-Host "Error during restore: $_"
    throw
} finally {
    $conn.Close()
}
