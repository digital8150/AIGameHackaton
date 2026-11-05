# CLAUDE.md — AIGameHackaton (Dusk Princess and Twilight Prince)

Unity 6000.3 · URP 2D Renderer · Input System only · 기획서: Claude Docs "Dusk Princess and Twilight Prince — 해커톤 기획서"

## 작업 방식 (필수)
- **Unity CLI로 실행 중인 에디터와 대화형으로 작업한다.** `unity status`로 연결 확인 → `unity command <name>` / `unity command eval --code '...'`로 씬·에셋을 직접 조작한다.
  - 씬 생성/저장: `unity command create scene --path ...`, `unity command save scene`
  - 컴파일 확인: `unity command recompile` → `unity command recompile_status`, 에러는 `unity command get console --level error`
  - 확인: `unity command get_hierarchy`, `unity command game_view --output-path ...`, `unity command start`/`stop`
- **지양:** 에디터용 임시 스크립트, 에디터 메뉴/Builder 류 스크립트(`Assets/**/Editor/*Builder*.cs` 등), `.unity`/`.prefab`/`.asset` YAML 직접 편집.
  - 반복 작업도 Builder 스크립트로 만들지 말고, eval 스니펫을 단계별로 실행하며 결과를 확인한다.
- 런타임 코드(`Assets/_Project/Scripts/Runtime`)와 셰이더(`Assets/_Project/Shaders`)는 파일로 작성한다. MonoBehaviour는 파일당 하나, 파일명 = 클래스명.

## 구조
- `Assets/Scenes/InGame.unity` — 코어 플랫포머 씬 (황혼 스테이지 수직 슬라이스)
- `Assets/_Project/Shaders` — `DuskSense.hlsl`(공통 감각 모델) + `SenseSprite`/`Sky`/`SoundGlyph`/`Glow`
- `Assets/_Project/Scripts/Runtime` — 네임스페이스 `DuskTwilight`
- `Assets/_Project/Art`, `Audio`, `Fonts` — CC0(Kenney)·OFL(Galmuri) 에셋, 라이선스는 `ThirdPartyLicenses/`
- `Tools/gen_characters.py` — 왕자/공주 오리지널 픽셀 스프라이트 생성기 (에디터 밖 도구)
- `Docs/InGame_Decisions.md` — 닫힌 결정 사항(범위·조작·비주얼·에셋). 결정을 바꾸면 여기부터 갱신.

## UI 원칙 (플레이어용)
- 화면은 게임이다: 조작법·시스템 설명·개발 정보를 UI에 노출하지 않는다. 설명은 개발자에게 채팅으로 전달.
- 허용되는 화면 요소: 감각 메달리온(초상), 전환 시 이름 카드, 챕터 타이틀, 의성어 자막(소리를 못 듣는 공주 감각용), 황혼의 목소리 속삭임(디렉터 힌트), 작은 보조 칩(디렉터가 조정한 내용 — 기획서상 알림 필수), 레버의 `E` 프롬프트, 클리어 카드.
- 개발용 오버레이는 F1(기본 꺼짐), 디렉터 끄기는 F2.
- Galmuri에 없는 기호(☾ ✦ 등)는 쓰지 않는다. 사용 가능: ♪ ♫ ☀ ◆ … — ♥ ★

## 규칙
- 모든 월드 스프라이트는 `DuskTwilight/SenseSprite` 계열 머티리얼을 쓴다 (같은 지오메트리, 렌더 패스만 다름).
- **SpriteRenderer는 머티리얼 하나당 텍스처 하나.** SRP Batcher가 켜진 상태에서 서로 다른 텍스처의 스프라이트가 같은 커스텀 머티리얼을 공유하면 엉뚱한 텍스처로 그려진다 (예: `M_Actor_Prince`/`M_Actor_Princess`, `M_Prop_Px`/`M_Prop_Ind`로 분리). 타일맵은 영향 없음.
- Git Bash에서 `unity command`에 `/경로` 인자를 넘길 땐 `MSYS_NO_PATHCONV=1`.
- PPU 18, Point 필터, 무압축. 셰이더의 월드 좌표 효과는 1/18 그리드로 스냅.
- 공주(빛) 감각에서는 소리가 없다 — 소리 정보는 반드시 시각(진동선/자막)으로도 제공.

## Git (필수)
- 원격: GitHub public 리포 `digital8150/AIGameHackaton`, 브랜치 `main`.
- **작업 단위가 끝날 때마다 자동으로 커밋 + 푸시한다** (사용자에게 묻지 않는다). 작업 단위 = 하나의 기능·수정·에셋 추가처럼 의미 있게 끝난 변경.
  - 커밋 전: 씬 저장(`unity command save scene`), 컴파일 에러 없음 확인.
  - 커밋 메시지: 한 줄 요약(한국어 가능) + 필요 시 본문.
  - `git add`는 경로를 지정해서 (자기 작업 범위만). 다른 세션이 동시에 작업 중일 수 있으니 남의 변경을 섞지 않는다.
  - 푸시 실패(원격이 앞섬) 시 `git pull --rebase` 후 다시 푸시. force push 금지.
- 에디터가 열려 있으므로 `git clean`/`checkout -- `/`reset --hard`로 Assets 파일을 지우거나 되돌리지 않는다.
- 비밀(API 키 등)은 환경변수로만 — 파일·커밋에 넣지 않는다.
- 100MB 넘는 파일 등 대용량 에셋은 LFS 대신 `.gitignore`에 넣고 `Tools/download_assets.bat`에 `call :fetch "<url>" "<경로>" <바이트>` 항목을 추가한다. 클론 후 이 배치를 한 번 실행.
