Param (    
    [string]$Target = "Build",
    [string]$Configuration = "Release",
    [string]$Verbosity= "Normal"
)

Invoke-Expression -Command "dotnet cake --target='$Target' --configuration='$Configuration' --verbosity='$Verbosity'" -ErrorAction Stop;