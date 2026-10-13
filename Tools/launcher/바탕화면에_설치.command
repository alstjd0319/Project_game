#!/bin/bash
# 귀문을 바탕화면(~/Desktop)에 설치한다 (macOS) — 더블클릭.
#   바탕화면의 "귀문.app"을 더블클릭하면 Unity·프로젝트 폴더 없이 게임이 바로 실행된다.
#   빌드가 없으면 먼저 빌드하고, 이미 바탕화면에 있으면 새 빌드로 바꿔 놓는다.
#   코드·그림을 바꾼 뒤 갱신하려면 이 스크립트를 다시 실행:  ./바탕화면에_설치.command --rebuild
cd "$(dirname "$0")" || exit 1
ARGS="--build-only"
[ "$1" = "--rebuild" ] && ARGS="--rebuild --build-only"
./귀문_실행.command $ARGS || exit 1

ROOT="$(cd ../.. && pwd)"
SRC="$ROOT/Builds/Mac/귀문.app"
DEST="$HOME/Desktop/귀문.app"
if [ ! -d "$SRC" ]; then echo "[귀문] 빌드 결과를 찾지 못했습니다: $SRC"; exit 1; fi
rm -rf "$DEST"
cp -R "$SRC" "$DEST"
echo "[귀문] 바탕화면에 설치했습니다: $DEST"
echo "       바탕화면의 '귀문'을 더블클릭하면 게임이 시작됩니다."
