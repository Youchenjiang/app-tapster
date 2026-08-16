# Tapster Launcher (No Console Window)
# Uses dotnet run to handle MSIX deployment automatically

Set-Location $PSScriptRoot
Start-Process dotnet -ArgumentList "run --project Tapster.Fluent.csproj" -WindowStyle Hidden
