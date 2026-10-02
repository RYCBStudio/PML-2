<#
.SYNOPSIS
    将指定目录下的多个类型源代码文件合并为 Txt，并转换为 PDF（软著申请用）。
.DESCRIPTION
    支持多个文件扩展名（如 *.cs, *.axaml, *.resx, *.iss），按完整路径排序，
    每个文件前显示其相对路径，文件内容紧跟其后，文件间空两行。
    生成 UTF-8 编码的 Txt 文件，再通过 Word COM 对象导出为 PDF。
.PARAMETER SourceDir
    源代码根目录，默认为当前目录（`.`）。
.PARAMETER OutputTxt
    输出的 Txt 文件路径，默认为当前目录下的 `SourceCode.txt`。
.PARAMETER OutputPdf
    输出的 PDF 文件路径，默认为与 Txt 同目录下的 `SourceCode.pdf`。
.PARAMETER FilePattern
    要匹配的文件扩展名模式，支持逗号分隔，例如 "*.cs,*.axaml,*.resx,*.iss"。
.EXAMPLE
    .\MergeSourceToPdf.ps1 -FilePattern "*.cs,*.axaml,*.resx,*.iss"
    合并当前目录下所有指定类型的文件。
#>

param(
    [string]$SourceDir = ".",
    [string]$OutputTxt = "SourceCode.txt",
    [string]$OutputPdf = "SourceCode.pdf",
    [string]$FilePattern = "*.cs"
)

# 将逗号分隔的模式拆分为数组
$patterns = $FilePattern -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

if ($patterns.Count -eq 0) {
    Write-Warning "未提供有效的文件模式。"
    exit 1
}

# 转为绝对路径
$SourceDir = Resolve-Path -Path $SourceDir -ErrorAction Stop
$OutputTxt = [System.IO.Path]::GetFullPath($OutputTxt)
$OutputPdf = [System.IO.Path]::GetFullPath($OutputPdf)

Write-Host "正在收集文件（模式：$($patterns -join ', ')）..." -ForegroundColor Cyan

# 使用 -Include 支持多个模式（注意 -Include 需配合 -Path 使用）
$files = Get-ChildItem -Path $SourceDir -Include $patterns -Recurse | Sort-Object FullName

if ($files.Count -eq 0) {
    Write-Warning "未找到任何匹配指定模式的文件，请检查目录和模式。"
    exit 1
}

Write-Host "共找到 $($files.Count) 个文件，正在生成 Txt..." -ForegroundColor Cyan

$sb = [System.Text.StringBuilder]::new()
$baseLen = $SourceDir.Path.Length + 1   # 用于截取相对路径

foreach ($file in $files) {
    $relative = $file.FullName.Substring($baseLen)   # 相对路径
    [void]$sb.AppendLine($relative)
    # 读取文件内容，若无法读取（如二进制）则跳过并提示
    try {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
        [void]$sb.Append($content)
    } catch {
        Write-Warning "无法读取文件（可能是二进制）: $relative ，将跳过内容。"
        [void]$sb.AppendLine("<< 文件内容无法读取 >>")
    }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine()   # 文件间空两行
}

# 写入 Txt（UTF-8 with BOM）
$sb.ToString() | Out-File -FilePath $OutputTxt -Encoding UTF8
Write-Host "Txt 已生成：$OutputTxt" -ForegroundColor Green

# 尝试使用 Word 导出 PDF
Write-Host "正在尝试转换为 PDF（需安装 Microsoft Word）..." -ForegroundColor Cyan
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $doc = $word.Documents.Open($OutputTxt, $null, $false)
    $doc.SaveAs([ref]$OutputPdf, [ref]17)   # 17 = wdFormatPDF
    $doc.Close()
    $word.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
    Write-Host "PDF 已生成：$OutputPdf" -ForegroundColor Green
} catch {
    Write-Warning "Word COM 不可用，无法自动转换 PDF。请手动将 '$OutputTxt' 转为 PDF（例如用 Word 打开后另存为 PDF，或使用虚拟打印机）。"
}

# 清理 COM 对象
[System.GC]::Collect()
[System.GC]::WaitForPendingFinalizers()