$configFiles = Get-ChildItem . *.md -rec
foreach ($file in $configFiles)
{
    (Get-Content $file.PSPath) |
    Foreach-Object { $_ -replace "## Контекст", "## Авторы`r`n`r`nkreghek`r`n`r`n## Контекст" } |
    Set-Content $file.PSPath
}