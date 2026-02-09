<#
.SYNOPSIS
Avalonia .NET 10 多平台一键发布脚本
适配 MEFrpLauncherX 项目配置，支持6平台+SHA256校验+交互确认
修复：版本号解析错乱问题（多Version节点导致）
#>

# 1. 配置固定变量（根据项目文件优化）
$PROJ_ROOT = "F:/VSProj/repos/MEFrpLauncherX"
$PROJ_SLN = "MEFrpLauncherX.sln"
$TARGET_FRAMEWORK = "net10.0" # 与csproj一致
$RIDS = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64") # 全平台覆盖
$SHA256_JSON_FILENAME = "sha256_checksums.json"
$MAIN_PROJ_NAME = "MEFrpLauncherX" # 主项目名称

# 2. 读取主项目版本号（修复核心：精准匹配根节点下的Version主节点，排除多节点干扰）
$csprojPath = Join-Path -Path $PROJ_ROOT -ChildPath "$MAIN_PROJ_NAME.csproj"
if (-not (Test-Path -Path $csprojPath)) {
    Write-Error "主项目文件不存在: $csprojPath"
    exit 1
}
# 关键修复：XPath精准定位 <Project> 根节点下的 <Version> 节点，避免匹配AssemblyVersion/FileVersion等
$versionNode = Select-Xml -Path $csprojPath -XPath "//Project/PropertyGroup/Version[1]" | Select-Object -ExpandProperty Node -ErrorAction SilentlyContinue
if (-not $versionNode -or [string]::IsNullOrEmpty($versionNode.InnerText.Trim())) {
    Write-Error "未在 $MAIN_PROJ_NAME.csproj 的 PropertyGroup 中找到有效主版本号（Version节点）"
    exit 1
}
$VERSION = $versionNode.InnerText.Trim() # 去除首尾空格，避免版本号含空白字符
Write-Host "`n检测到项目主版本号: $VERSION`n" -ForegroundColor Cyan

# 3. 定义SHA256计算+JSON生成函数
function New-FileSha256Json {
    param(
        [Parameter(Mandatory=$true)][string]$PublishDir,
        [Parameter(Mandatory=$true)][string]$JsonFile
    )

    $sha256Dict = [ordered]@{}
    $sha256Provider = [System.Security.Cryptography.SHA256]::Create()

    try {
        $allFiles = Get-ChildItem -Path $PublishDir -File -Recurse | Where-Object {
            $_.FullName -ne $JsonFile
        }

        Write-Host "`n开始计算 [$($allFiles.Count)] 个文件的SHA256值..." -ForegroundColor Blue
        foreach ($file in $allFiles) {
            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $hashBytes = $sha256Provider.ComputeHash($fileBytes)
            $sha256Value = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })

            $relativePath = $file.FullName.Substring($PublishDir.Length + 1)
            $relativePath = $relativePath -replace '\\', '/'

            $sha256Dict[$relativePath] = $sha256Value
            Write-Host "  计算完成: $relativePath" -ForegroundColor Gray
        }

        $sha256Dict | ConvertTo-Json -Indent 2 | Out-File -Path $JsonFile -Encoding utf8
        Write-Host "SHA256校验文件生成完成: $JsonFile" -ForegroundColor Green
    }
    catch {
        Write-Error "计算SHA256或生成JSON失败: $_"
        exit 1
    }
    finally {
        $sha256Provider.Dispose()
    }
}

# 4. 遍历所有 RID 执行发布+SHA256+JSON生成
foreach ($RID in $RIDS) {
    # 输出路径严格匹配要求：PROJ_ROOT/bin/Release/RID/publish/pml-VERSION/
    $outputPath = Join-Path -Path $PROJ_ROOT -ChildPath "bin/Release/$RID/publish/pml-$VERSION"
    $sha256JsonPath = Join-Path -Path $outputPath -ChildPath $SHA256_JSON_FILENAME

    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "开始发布 [$RID] 平台..." -ForegroundColor Green
    # 发布参数与项目配置对齐
    dotnet publish $PROJ_SLN `
        -c Release `
        -r $RID `
        --self-contained true `
        -o $outputPath `
        -p:Version=$VERSION `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:PublishAot=false ` # 与csproj的PublishAot一致
        -p:DebugType=none ` # Release模式关闭调试信息
        -p:DebugSymbols=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "[$RID] 平台发布失败！终止后续操作"
        exit $LASTEXITCODE
    }
    Write-Host "[$RID] 平台发布完成，输出路径: $outputPath" -ForegroundColor Green

    # 生成SHA256校验文件
    New-FileSha256Json -PublishDir $outputPath -JsonFile $sha256JsonPath
}

# 5. 交互确认步骤（检查Fonts和更新日志）
Write-Host "`n=====================================" -ForegroundColor Yellow
Write-Host "⚠️  请确认以下事项（基于项目依赖）：" -ForegroundColor Yellow
Write-Host "1. 是否已更新 MEFrpLauncherX.Fonts 项目的字体文件？"
Write-Host "2. 是否已编写 v$VERSION 版本的更新日志？"
Write-Host "=====================================" -ForegroundColor Yellow
Read-Host -Prompt "确认完成后请按 Enter 键继续" | Out-Null

# 6. 发布完成最终提示
Write-Host "`n🎉 所有平台发布+SHA256校验完成！最终版本: v$VERSION" -ForegroundColor Magenta
Write-Host "各平台输出路径: $PROJ_ROOT/bin/Release/*/publish/pml-$VERSION/" -ForegroundColor Magenta
Write-Host "各平台校验文件: 每个发布目录下的 $SHA256_JSON_FILENAME" -ForegroundColor Magenta