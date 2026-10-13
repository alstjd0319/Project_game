#!/bin/bash
# 귀문을 바탕화면(~/Desktop)에 설치한다 (macOS) — 더블클릭.
#   기본: 바탕화면에 "귀문.app" 바로가기 앱을 만든다. 누를 때마다 프로젝트가 빌드보다 새로우면 자동으로 다시 빌드한 뒤 최신 상태로 실행한다.
#         (이 컴퓨터의 프로젝트 폴더를 가리키므로 폴더를 옮기면 다시 설치. 다른 사람에게 줄 때는 --copy)
#   --copy : 빌드 결과물을 통째로 복사해 둔다 (Unity·프로젝트 없이 실행되지만 자동 갱신은 안 됨, 배포용)
#   --rebuild : 먼저 빌드를 지우고 새로 만든다
cd "$(dirname "$0")" || exit 1
COPY=0; ARGS="--build-only"
for a in "$@"; do
  [ "$a" = "--copy" ] && COPY=1
  [ "$a" = "--rebuild" ] && ARGS="--rebuild --build-only"
done
./귀문_실행.command $ARGS || exit 1

ROOT="$(cd ../.. && pwd)"
SRC="$ROOT/Builds/Mac/귀문.app"
DEST="$HOME/Desktop/귀문.app"
if [ ! -d "$SRC" ]; then echo "[귀문] 빌드 결과를 찾지 못했습니다: $SRC"; exit 1; fi
rm -rf "$DEST"

if [ "$COPY" = 1 ]; then
  cp -R "$SRC" "$DEST"
  echo "[귀문] 바탕화면에 복사했습니다: $DEST (자동 갱신 없음)"
  exit 0
fi

mkdir -p "$DEST/Contents/MacOS"
cat > "$DEST/Contents/Info.plist" <<PL
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>귀문</string>
<key>CFBundleDisplayName</key><string>귀문</string>
<key>CFBundleIdentifier</key><string>kr.gwimun.launcher</string>
<key>CFBundleExecutable</key><string>launch</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSUIElement</key><true/>
</dict></plist>
PL
cat > "$DEST/Contents/MacOS/launch" <<SH
#!/bin/bash
# 귀문 바로가기: 프로젝트가 바뀌었으면 터미널에서 런처로 새로 빌드해 실행, 아니면 바로 실행
ROOT="$ROOT"
APP="\$ROOT/Builds/Mac/귀문.app"
if [ ! -d "\$APP" ] || [ -n "\$(find "\$ROOT/Assets" "\$ROOT/Packages" "\$ROOT/ProjectSettings" -type f -newer "\$APP" -print -quit 2>/dev/null)" ]; then
  open -a Terminal "\$ROOT/Tools/launcher/귀문_실행.command"
else
  open "\$APP"
fi
SH
chmod +x "$DEST/Contents/MacOS/launch"
echo "[귀문] 바탕화면에 바로가기를 만들었습니다: $DEST"
echo "       더블클릭하면 최신 상태로 게임이 시작됩니다 (바뀐 게 있으면 터미널 창에서 빌드 후 실행)."
