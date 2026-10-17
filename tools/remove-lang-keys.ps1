#!/usr/bin/env pwsh
# =============================================================================
# 删除已废弃的多语言文案（resx + 生成的 Designer.cs）
# =============================================================================
# 设置项被移除后，对应文案不再有引用。与其手工在三份 resx 和两份 Designer.cs 里
# 各删一遍（容易漏、且下次在 IDE 里重新生成 Designer.cs 时又会被带回来），
# 不如用一个可复用的脚本按「资源名」精确删除。
#
# 用法：pwsh tools/remove-lang-keys.ps1 -Keys Text.Update.KeepProfile,Text.Update.Foo
param(
    # 本脚本位于 <repo>/tools/，因此仓库根是上一级。
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),

    [Parameter(Mandatory = $true)]
    [string[]]$Keys
)

$ErrorActionPreference = 'Stop'

foreach ($culture in @('', '.en-US', '.zh-hant')) {
    $baseName = if ($culture) { "Languages$culture" } else { 'Languages' }
    $resxPath = Join-Path $Root "MEFrpLauncherX.Core/Languages/$baseName.resx"

    # ---------- resx ----------
    # 用「按行删除」而非 xml.Save()：后者会重排缩进 / 引号 / 声明，
    # 把整个文件改成与 Visual Studio 生成器不一致的格式，产生上千行无意义 diff。
    $text = [System.IO.File]::ReadAllText($resxPath)
    $lines = [System.Collections.Generic.List[string]]@(($text -replace "`r`n", "`n") -split "`n")

    $removed = 0
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        # 形如:  <data name="Text.Update.KeepProfile" xml:space="preserve">
        if ($lines[$i] -notmatch '^\s*<data name="(?<name>[^"]+)"') {
            continue
        }
        if ($Keys -notcontains $Matches['name']) {
            continue
        }

        # 向下找到该 data 的 </data>，整块删除（含中间可能的多行 value）
        $end = $i
        while ($end -lt $lines.Count -and $lines[$end] -notmatch '</data>') {
            $end++
        }
        if ($end -ge $lines.Count) {
            throw "${baseName}.resx: 未能定位 $($Matches['name']) 的 </data>"
        }

        $lines.RemoveRange($i, $end - $i + 1)
        $removed++
    }

    if ($removed -eq 0) {
        Write-Host "${baseName}.resx: 无需删除"
    }
    else {
        [System.IO.File]::WriteAllText($resxPath, ($lines -join "`r`n"),
            (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "${baseName}.resx: 已删除 $removed 条"
    }

    # ---------- Designer.cs（仅主语言与 en-US 有）----------
    $designerPath = Join-Path $Root "MEFrpLauncherX.Core/Languages/$baseName.Designer.cs"
    if (-not (Test-Path $designerPath)) {
        continue
    }

    $lines = [System.Collections.Generic.List[string]]@(
        ([System.IO.File]::ReadAllText($designerPath) -replace "`r`n", "`n") -split "`n"
    )

    # 倒序遍历：每删一条会把「文档注释起始 → 属性结束 }」整块连同其后的空行删掉
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        $line = $lines[$i]
        if ($line -notmatch 'ResourceManager\.GetString\("(?<name>[^"]+)"') {
            continue
        }

        $name = $Matches['name']
        if ($Keys -notcontains $name) {
            continue
        }

        # 向上回溯到 '        /// <summary>'，向下找到属性块的收尾 '        }'
        $start = $i
        while ($start -ge 0 -and $lines[$start].Trim() -ne '/// <summary>') {
            $start--
        }
        if ($start -lt 0) {
            throw "${baseName}.Designer.cs: 未能定位 $name 的文档注释起点"
        }

        $end = $i
        while ($end -lt $lines.Count -and $lines[$end] -ne '        }') {
            $end++
        }
        if ($end -ge $lines.Count) {
            throw "${baseName}.Designer.cs: 未能定位 $name 的属性结束"
        }

        # 连同紧随其后的空行（生成器以 8 空格空行分隔属性）一并删除
        if ($end + 1 -lt $lines.Count -and $lines[$end + 1].Trim() -eq '') {
            $end++
        }

        $lines.RemoveRange($start, $end - $start + 1)
        Write-Host "  ${baseName}.Designer.cs: 已删除 $name"
    }

    [System.IO.File]::WriteAllText($designerPath, ($lines -join "`r`n"),
        (New-Object System.Text.UTF8Encoding($true)))
}