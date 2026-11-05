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
- 타이틀: `Title` 씬(빌드 0) = `Prefabs/UI/TitleScreen` 인스턴스. 연출 셰이더 `Shaders/Title/`(Backdrop·Portrait·Logo·Button·Panel·Gem·Embers, 머티리얼 `Materials/Title/M_Title_*`), 스크립트 `Scripts/Runtime/Title/`(TitleScreen·TitleLayer·TitleLogo·TitleButton·SettingsPanel·UIShaderSize). 아트는 픽셀이 아닌 NAI 일러스트 — 어둡고 muted한 세피아 동화 톤(밝은 표정·고채도 금지), 프롬프트 `Art Prompts/[Title] 06_Title_Screen.txt`. 설정값은 `GameSettings`(PlayerPrefs), 볼륨은 `Audio/DuskMixer`(Master/Music/SFX 노출 파라미터) + `AudioRouter`(Systems 프리팹에도 있음, 그룹 없는 AudioSource를 SFX로 라우팅). 폰트: 로고 Cinzel Decorative, 한글 Gowun Batang (OFL).
- 스테이지 씬 (빌드 순서, 기획: Notion "스테이지 기획 _ Pyorang"): `Stage1_FirstTwilight`(1단계 첫 황혼, 방 6) → `Stage2_SunsetLake`(2단계 노을이 잠긴 호수) → `Stage3_EchoCave`(3단계 메아리가 깨우는 동굴) → `Stage4_TwilightGate`(4단계 황혼의 문, 88m) → `Stage5_TwilightBridge`(5단계 황혼의 다리, 180m, 6구간 `StageSection`, 엔딩). 다음 장은 `GameFlow.nextScene`. 구 프로토타입 `Ch0_Prologue`·`Ch1_PrinceInDark`·`Ch2_PrincessInLight`·`InGame`은 빌드 뒤쪽에 남겨 둠.
- 말 퍼즐: `WordTarget`(공통: 선택 창·음성·빛 글자) ← `WordAltar`(정답 → 다리/문, 프리팹 WordAltar·WordDoor), `WordFillBridge`(아무 말 → 칸 채우기, 누적/교체·깜빡이). 위험 지대 `Hazard`(Lake 프리팹). 메아리 기믹: 왕자 메아리는 소/중/대(5/10/15m, 누른 시간·음량), 종 6m 고정. `EchoSwitch`/`EchoResonator`(종)는 `EchoCaller.Called`(일부러 부른 메아리)와 `EchoResonator.Rang`(종소리, 연쇄 공명)에만 반응 — 발소리·체크포인트 메아리로는 켜지지 않는다. 반대로 계단 `WordStair`, 구간 리셋 `StageSection`. `SwitchGate`(필요/금지 스위치 → 문 상승, 오브젝트 표시, 컴포넌트 활성). 가짜 벽 = 충돌 없는 `Fake Walls` 타일맵(M_FakeWall*).
- `Assets/_Project/Scripts/Runtime/Tutorial` — 단계별 튜토리얼(Section/Director/Signals/View/Veil/Marker), `AI` — Whisper 음성(`VoiceInput`/`VoiceCommands`)과 Jev(`JevClient`/`JevDirectorBrain`/`VoiceIntent`)
- Whisper 모델 `Assets/StreamingAssets/Whisper/ggml-base.bin`은 git 밖 — `Tools/download_assets.bat`. Jev 키는 환경변수 `TYPESAFE_API_KEY` (없으면 규칙/키워드 폴백).
- `Assets/_Project/Shaders` — `DuskSense.hlsl`(공통 감각 모델) + `SenseSprite`/`Sky`/`SoundGlyph`/`Glow`
- `Assets/_Project/Scripts/Runtime` — 네임스페이스 `DuskTwilight`
- `Assets/_Project/Art`, `Audio`, `Fonts` — CC0(Kenney)·OFL(Galmuri) 에셋, 라이선스는 `ThirdPartyLicenses/`
- `Tools/gen_env_nai.py` — NovelAI V5 에셋 생성기. 프롬프트는 `Art Prompts/`의 txt 섹션을 그대로 읽는다(예: `05:2`), 검은 배경은 투명 배경으로 치환. 토큰은 환경변수 `NAI_API_KEY`. 결과는 `Tools/_nai_out/`(git 밖)에서 골라 `Assets/_Project/Art/Env`로.
- `Tools/make_tileable.py` — 이미지 경계를 교차 블렌딩해 반복 가능하게 만든다 (`--axis x|y|xy`). 지형 채움·윗선 텍스처용.
- `Tools/gen_characters.py` — 왕자/공주 오리지널 픽셀 스프라이트 생성기 (에디터 밖 도구)
- `Assets/_Project/Prefabs` — `Chapter/`(Systems·MainCamera·Sky·GlobalVolume·HUD·Voice·Backdrop·TwilightPrince·DuskPrincess: 모든 챕터 공용 리그), `Gimmicks/`(Lever·TwilightGate·SoundEmitter·Checkpoint·ReunionDoor·LightPlatform·WordAltar·DirectorZone), `Tutorial/`(TutorialVeil·TutorialMarker), `UI/`(TutorialPanel), `Environment/`(Env_* 2.5D 원경·중경·전경 레이어, 텍스처마다 `M_Env_*` 머티리얼). UI 레이아웃은 코드로 만들지 않는다 — HUD는 `Chapter/HUD`의 HUD Canvas, 단어 선택 창은 `Gimmicks/WordAltar`의 Word Panel(선택지는 Option Template 복제), 튜토리얼 목표 줄은 Row Template/Key 복제, 빛 글자는 `Gimmicks/LightLetter`. 스크립트는 참조된 요소를 애니메이션만 한다. 씬에 있는 것은 전부 이 프리팹의 인스턴스이고 챕터별 차이는 오버라이드다.
- `Docs/InGame_Decisions.md` — 닫힌 결정 사항(범위·조작·비주얼·에셋). 결정을 바꾸면 여기부터 갱신.

## UI 원칙 (플레이어용)
- 화면은 게임이다: 조작법·시스템 설명·개발 정보를 UI에 노출하지 않는다. 설명은 개발자에게 채팅으로 전달.
- 허용되는 화면 요소: 감각 메달리온(초상), 전환 시 이름 카드, 챕터 타이틀, 의성어 자막(소리를 못 듣는 공주 감각용), 황혼의 목소리 속삭임(디렉터 힌트), 작은 보조 칩(디렉터가 조정한 내용 — 기획서상 알림 필수), 레버의 `E` 프롬프트, 클리어 카드.
- 개발용 오버레이는 F1(기본 꺼짐), 디렉터 끄기는 F2.
- Galmuri에 없는 기호(☾ ✦ 등)는 쓰지 않는다. 사용 가능: ♪ ♫ ☀ ◆ … — ♥ ★

## 규칙
- **씬에 놓인 프리팹 인스턴스의 값을 eval로 바꿀 때는 `SerializedObject`(또는 `PrefabUtility.RecordPrefabInstancePropertyModifications`)로 바꾼다.** 필드를 직접 대입하면 오버라이드로 기록되지 않아 저장 시 사라진다(Systems·MainCamera 등 기존 인스턴스에서 실제로 발생).
- **스테이지 구성 요소는 하드코딩하지 않는다.** 수치·문구·타이밍은 인스펙터 필드(`[Header]`/`[Tooltip]`/`[Range]`)로 노출하고, 범위·반경·연결은 `OnDrawGizmos`로 씬 뷰에 그린다. 새 기믹/배치는 `Prefabs/`의 프리팹 인스턴스로 놓는다 (씬 전용 수치는 오버라이드, 모든 챕터 공통 변경은 프리팹 자체에). 참조는 비워 두면 자동 연결되게(예: Lever→부모 TwilightGate, 디렉터→씬의 SoundEmitter/LightPlatform).
- 모든 월드 스프라이트는 `DuskTwilight/SenseSprite` 계열 머티리얼을 쓴다 (같은 지오메트리, 렌더 패스만 다름).
- **SpriteRenderer는 머티리얼 하나당 텍스처 하나.** SRP Batcher가 켜진 상태에서 서로 다른 텍스처의 스프라이트가 같은 커스텀 머티리얼을 공유하면 엉뚱한 텍스처로 그려진다 (예: `M_Actor_Prince`/`M_Actor_Princess`, `M_Prop_Px`/`M_Prop_Ind`로 분리). 타일맵은 영향 없음.
- Git Bash에서 `unity command`에 `/경로` 인자를 넘길 땐 `MSYS_NO_PATHCONV=1`.
- 타일맵을 eval로 다시 칠한 뒤에는 `TilemapCollider2D`를 껐다 켜고 `CompositeCollider2D.GenerateGeometry()` — 안 하면 이전(복사 원본) 충돌 형태가 남는다. 플레이 모드에서 레이캐스트로 확인.
- 튜토리얼 문구는 플레이어용 조작·이야기만. F1/F2·디렉터 같은 개발 기능은 가르치지 않는다.
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
