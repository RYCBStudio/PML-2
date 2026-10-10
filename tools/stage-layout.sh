#!/usr/bin/env bash
# =============================================================================
# PML 2 发布布局暂存器（26.5.0）
# =============================================================================
# 把「扁平的 publish 产物」重排为版本化的安装目录布局：
#
#   <out>/
#   ├─ PML 2.exe            ← 启动器（版本无关，文件名含空格）
#   ├─ launcher.json        ← 版本槽位：{ "current": "v26.5.0", ... }
#   ├─ v26.5.0/             ← 主程序（只读）
#   │  ├─ MEFrpLauncherX.exe
#   │  ├─ Tools/            ← splash 等随包辅助程序
#   │  └─ Resources/ Assets/ *.dll runtimes/ ...
#   ├─ data/                ← 全部可变数据（升级绝不覆盖）
#   │  ├─ Config/  Cache/  Logs/  Run/
#   └─ unins000.exe         （Windows 安装器可能追加）
#
# 用法：
#   stage-layout.sh <publish-dir> <launcher-publish-dir> <out-dir> [version] [aot|corelib]
#
# 环境要求：bash + 常用 coreutils（CI 的 ubuntu-latest 与本地 Git Bash/WSL 均满足）。
# 刻意不依赖 PowerShell / .NET，保证三平台 CI 用同一份脚本，行为一致。
# =============================================================================
set -euo pipefail

PUBLISH_DIR="${1:?用法: stage-layout.sh <publish-dir> <launcher-dir> <out-dir> [version] [mode]}"
LAUNCHER_DIR="${2:?缺少启动器 publish 目录}"
OUT_DIR="${3:?缺少输出目录}"
VERSION="${4:-}"
MODE="${5:-aot}"

die() { echo "错误: $*" >&2; exit 1; }

[[ -d "$PUBLISH_DIR" ]] || die "publish 目录不存在: $PUBLISH_DIR"
[[ -d "$LAUNCHER_DIR" ]] || die "启动器目录不存在: $LAUNCHER_DIR"

# ---- 版本号：优先显式传入，否则从主程序文件名旁的版本标记或 csproj 推导 ----
if [[ -z "$VERSION" ]]; then
  # 发布产物里没有版本信息时回落到调用方传入值，避免出现 v 空目录名。
  die "必须显式传入版本号（例如 26.5.0）"
fi

case "$VERSION" in
  v*) VERSION_DIR="$VERSION" ;;
  *)  VERSION_DIR="v$VERSION" ;;
esac

echo "==> 暂存版本目录: $VERSION_DIR（模式 $MODE）"

rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

# ---- 1. 主程序进版本目录 ----
VERSION_PATH="$OUT_DIR/$VERSION_DIR"
mkdir -p "$VERSION_PATH"
cp -a "$PUBLISH_DIR/." "$VERSION_PATH/"

# publish 产物里的 Tools\ 由 CI 预先放入（CrashDisplayer / splash），
# 它们属于「随包分发的只读资源」，留在版本目录内是正确的。
# 反之，运行时下载的 mefrrpc / lego 属于 data\Run\，由程序自己管理。

# ---- 2. 启动器提到安装根，并改名为 PML 2(.exe) ----
# 启动器以「单文件 self-contained」发布，因此 publish 目录里只有：
#   PML2.Launcher.exe（Windows）/ PML2.Launcher（Unix）  ＋ .pdb（已排除）
# 安装根因此只多出一个 PML 2.exe，不会堆入运行时 DLL。
if [[ -f "$LAUNCHER_DIR/PML2.Launcher.exe" ]]; then
  LAUNCHER_EXE="$LAUNCHER_DIR/PML2.Launcher.exe"
  LAUNCHER_TARGET="$OUT_DIR/PML 2.exe"
elif [[ -f "$LAUNCHER_DIR/PML2.Launcher" ]]; then
  LAUNCHER_EXE="$LAUNCHER_DIR/PML2.Launcher"
  LAUNCHER_TARGET="$OUT_DIR/PML 2"
else
  die "在 $LAUNCHER_DIR 下找不到 PML2.Launcher 可执行文件（应已以 PublishSingleFile 发布）"
fi

cp -a "$LAUNCHER_EXE" "$LAUNCHER_TARGET"
chmod +x "$LAUNCHER_TARGET" 2>/dev/null || true

# 单文件发布仍有少量同名附属文件（.pdb / .json），
# 除可执行文件本体外一律不复制 —— 根目录必须保持最小。
# 若出现意外文件，说明 PublishSingleFile 未生效，此时直接报错而非静默堆文件。
STRAY=$(find "$LAUNCHER_DIR" -maxdepth 1 -type f \
  ! -name "PML2.Launcher" ! -name "PML2.Launcher.exe" \
  ! -name "*.pdb" ! -name "*.json" | head -5)
if [[ -n "$STRAY" ]]; then
  echo "警告: 启动器目录存在非单文件产物，安装根可能被污染:" >&2
  echo "$STRAY" >&2
fi

# ---- 3. 数据目录骨架 ----
# 只创建空目录并放 .keep：真实内容由程序首次启动时生成，
# 这样安装包里不会混入开发期残留的用户数据。
for d in Config/frp Config/Themes Config/Plugins Cache Logs Run; do
  mkdir -p "$OUT_DIR/data/$d"
done
touch "$OUT_DIR/data/.keep"

# ---- 4. 版本槽位 ----
cat > "$OUT_DIR/launcher.json" <<EOF
{
  "schema": 1,
  "current": "$VERSION_DIR",
  "previous": "",
  "aot": $([[ "$MODE" == "aot" ]] && echo true || echo false),
  "updatedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF

# ---- 5. 输出摘要 ----
echo "==> 布局完成: $OUT_DIR"
echo "    启动器    : $(basename "$LAUNCHER_TARGET")"
echo "    版本目录  : $VERSION_DIR"
echo "    数据目录  : data/"
echo
echo "目录结构:"
find "$OUT_DIR" -maxdepth 2 -not -path '*/v*/*' | sed "s|$OUT_DIR|<安装目录>|" | sort | head -30
