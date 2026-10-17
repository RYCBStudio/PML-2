#!/usr/bin/env pwsh
# =============================================================================
# 把 resx 中新增的文案同步到 PublicResXFileCodeGenerator 生成的 Designer.cs
# =============================================================================
# dotnet build 不会重新生成 Designer.cs —— 该生成器只在 VS / Rider 里编辑 resx 时触发。
# 因此用命令行新增文案后必须手动补齐属性，否则 Languages.XXX 与 {x:Static} 都无法编译。
#
# 本脚本读取 resx 的全部 <data name="X"><value>Y</data>，找出 Designer.cs 中缺失的属性，
# 按生成器的排序规则（资源名升序）插入，并完全复用生成器的代码模板，保证下次在 IDE 里
# 重新生成时不会产生无意义 diff。
#
# 用法：pwsh tools/sync-lang-designer.ps1
# =============================================================================
param(
    # 本脚本位于 <repo>/tools/，因此仓库根是上一级。
    # 用 $MyInvocation 取脚本自身路径，避免依赖调用方的工作目录。
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
)

$ErrorActionPreference = 'Stop'

# 生成器用 '.' 与 '/' 等非标识符字符做分隔，这里统一换成 '_'。
# Text.Update.KeepOldVersion -> Text_Update_KeepOldVersion
function ConvertTo-PropertyName([string]$ResourceName) {
    return ($ResourceName -replace '[^A-Za-z0-9_]', '_')
}

# 一个属性块的完整文本，缩进与换行均与生成器输出一致。
# 注意：这里必须显式构造 List<string> 并逐行 Add —— 用 @() 返回会被 PowerShell
# 展平成单个多行字符串，插入时就会挤成一行。
function New-PropertyBlock([string]$ResourceName, [string]$Value) {
    # 摘要注释取 resx 的值；多行值压成单行，与生成器行为一致
    $summary = (($Value -replace '\s+', ' ')).Trim()
    $property = ConvertTo-PropertyName $ResourceName

    $block = [System.Collections.Generic.List[string]]::new()
    $block.Add('        /// <summary>')
    $block.Add("        ///   Looks up a localized string similar to $summary.")
    $block.Add('        /// </summary>')
    $block.Add("        public static string $property {")
    $block.Add('            get {')
    $block.Add("                return ResourceManager.GetString(`"$ResourceName`", resourceCulture);")
    $block.Add('            }')
    $block.Add('        }')

    # 前置逗号是必须的：PowerShell 会把函数返回值自动展开成数组，
    # 单个 List 被展开后又会被当回 List，返回 ,$block 才能保持原类型。
    return ,$block
}

# 读取 resx 的 name -> value
function Read-Resx([string]$Path) {
    [xml]$xml = Get-Content -Raw -Encoding UTF8 $Path
    $map = [ordered]@{}
    foreach ($node in $xml.root.data) {
        $map[[string]$node.name] = [string]$node.value
    }
    return $map
}

# 在 $lines 中找到「资源名第一个大于 $name 的属性」所在块的起始行号。
# 块的起始即其 '/// <summary>' 行；若找不到更大的属性则返回 -1（表示应追加到末尾）。
function Find-InsertionLine([System.Collections.Generic.List[string]]$lines, [string]$name) {
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch 'ResourceManager\.GetString\("(?<name>[^"]+)"') {
            continue
        }

        if ([string]::CompareOrdinal($Matches['name'], $name) -le 0) {
            continue
        }

        # 从 GetString 行向上回溯到该属性的文档注释起点
        for ($j = $i; $j -ge 0; $j--) {
            if ($lines[$j].Trim() -eq '/// <summary>') {
                return $j
            }
        }
        return $i
    }

    return -1
}

foreach ($culture in @('', '.en-US', '.zh-hant')) {
    $baseName = if ($culture) { "Languages$culture" } else { 'Languages' }
    $resxPath = Join-Path $Root "MEFrpLauncherX.Core/Languages/$baseName.resx"
    $designerPath = Join-Path $Root "MEFrpLauncherX.Core/Languages/$baseName.Designer.cs"

    $entries = Read-Resx $resxPath

    # zh-hant 只提供运行时资源（回退到主语言的 Designer），没有独立 Designer.cs
    if (-not (Test-Path $designerPath)) {
        Write-Host "${baseName}: 无 Designer.cs（运行时回退到主语言），跳过"
        continue
    }

    $text = [System.IO.File]::ReadAllText($designerPath)

    $existing = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($m in [regex]::Matches($text, 'ResourceManager\.GetString\("(?<name>[^"]+)"')) {
        [void]$existing.Add($m.Groups['name'].Value)
    }

    $missing = @($entries.Keys | Where-Object { -not $existing.Contains($_) } | Sort-Object)
    if ($missing.Count -eq 0) {
        Write-Host "${baseName}.Designer.cs: 已是最新（$($entries.Count) 条）"
        continue
    }

    Write-Host "${baseName}.Designer.cs: 补齐 $($missing.Count) 条 -> $($missing -join ', ')"

    # 统一按 CRLF 处理，保持与生成器输出一致
    $lines = [System.Collections.Generic.List[string]]@(
        ($text -replace "`r`n", "`n") -split "`n"
    )

    foreach ($name in $missing) {
        $block = New-PropertyBlock $name $entries[$name]
        $at = Find-InsertionLine $lines $name

        if ($at -lt 0) {
            # 排在已有资源之后：插到类体最后一个 '    }' 之前
            $at = $lines.Count - 1
            while ($at -ge 0 -and $lines[$at] -ne '    }') {
                $at--
            }
            if ($at -lt 0) {
                throw "${baseName}.Designer.cs: 无法定位类结尾，请手工插入 $name"
            }
            # at 指向类的收尾 '    }'，往前退一格留出空行
            $at--
        }

        # 组装「属性块 + 其后的空行」并插入到锚点之前。
        # 锚点 $at 指向的是「已有属性的文档注释起点」，它前面已经有一行 '        ' 空行；
        # 因此这里插入「属性块 + 空行」即可，不能再额外加前置空行，否则会出现连续两个空行。
        $insert = [System.Collections.Generic.List[string]]::new()
        foreach ($line in $block) {
            $insert.Add($line)
        }
        $insert.Add('        ')

        $lines.InsertRange($at, $insert.ToArray())
    }

    # 生成器输出带 UTF-8 BOM，写回时必须保留，否则整个文件都会被视作大改动
    $output = ($lines -join "`r`n")
    $utf8WithBom = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($designerPath, $output, $utf8WithBom)
    Write-Host "  已写入 $designerPath"
}