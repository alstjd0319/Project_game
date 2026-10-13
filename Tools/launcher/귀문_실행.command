#!/bin/bash
# 귀문 런처 (macOS) — 더블클릭하면 게임을 실행한다.
#   • 빌드(Builds/Mac/귀문.app)가 없으면 Unity로 먼저 빌드한다 (처음 한 번만 몇 분 걸린다).
#   • 코드·그림을 바꾼 뒤 다시 빌드하려면 터미널에서:  ./귀문_실행.command --rebuild
#   • Unity가 이 프로젝트를 열고 있으면 빌드할 수 없다 → 에디터를 닫거나, 에디터 메뉴 Tools → ParryRL → 게임 빌드 (macOS)를 쓴다.
#   • 빌드만 하고 실행은 안 하려면:  ./귀문_실행.command --build-only   (바탕화면 설치 스크립트가 쓴다)
#   • Unity 위치가 다르면:  UNITY_PATH=/경로/Unity.app/Contents/MacOS/Unity ./귀문_실행.command
cd "$(dirname "$0")/../.." || exit 1
ROOT="$PWD"
APP="$ROOT/Builds/Mac/귀문.app"

if [ "$1" = "--rebuild" ] && [ -d "$APP" ]; then
  echo "[귀문] 기존 빌드를 지우고 다시 빌드합니다."
  rm -rf "$APP"
fi

if [ ! -d "$APP" ]; then
  VERSION=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
  UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
  if [ ! -x "$UNITY" ]; then
    echo "[귀문] Unity $VERSION 을(를) 찾지 못했습니다: $UNITY"
    echo "       Unity Hub로 설치하거나 UNITY_PATH 환경변수에 Unity 실행 파일 경로를 넣어 주세요."
    read -n 1 -s -r -p "아무 키나 누르면 닫힙니다."
    exit 1
  fi
  mkdir -p Logs
  echo "[귀문] 빌드 중... (로그: Logs/build_mac.log)"
  if ! "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
       -executeMethod ParryRL.EditorTools.GameBuilder.BuildMac -logFile "$ROOT/Logs/build_mac.log"; then
    echo "[귀문] 빌드에 실패했습니다. Logs/build_mac.log 를 확인하세요."
    echo "       (Unity가 이 프로젝트를 열고 있으면 먼저 닫아 주세요.)"
    read -n 1 -s -r -p "아무 키나 누르면 닫힙니다."
    exit 1
  fi
fi

if [ "$1" = "--build-only" ] || [ "$2" = "--build-only" ]; then
  echo "[귀문] 빌드 완료: $APP"
  exit 0
fi

echo "[귀문] 실행합니다."
open "$APP"
