# NovelAI Diffusion V5 (NAI V5) 프롬프트 작동 원리 및 작성 가이드

프로젝트(`Project-Vertex`)의 게임 아트 에셋 생성에 사용되는 **NAI V5**의 프롬프트 문법, 가중치 시스템, 자연어 및 텍스트 렌더링(T2I 내 텍스트 삽입) 작동 방식을 정리한 문서입니다.

---

## 1. NAI V5의 핵심 변화 요약

1. **태그(Danbooru) + 영어 자연어(Natural Language) 하이브리드 지원**
   - 기존의 쉼표(`,`)로 구분하는 단부루(Danbooru) 태그 방식과 **완전한 영어 문장(자연어 서술)**을 동시에 이해합니다.
   - 다국어(영어, 일본어, 중국어 등) 입력을 지원하지만, **영어 태그 + 영어 자연어 문장** 조합이 가장 높은 정확도와 일관성을 보입니다.
2. **확장된 컨텍스트 길이**
   - V5 Full 기준 최대 약 **1,471 토큰**까지 지원하여, 태그 나열 뒤에 긴 자연어 묘사 문단을 덧붙여도 앞부분 프롬프트가 잘리지 않고 반영됩니다.
3. **강력한 이미지 내 텍스트 렌더링 (`Text:` 문법 및 자연어 텍스트)**
   - 총기 각인, 드론 스텐실(`R&C Company`), 간판, UI 요소 등의 영문 텍스트를 이미지 안에 깨지지 않고 직접 그려낼 수 있습니다.

---

## 2. 프롬프트 구조: 태그 vs 영어 자연어 역할 분담

NAI V5에서는 **앞쪽에 배치된 프롬프트일수록 더 높은 기본 중요도**를 가집니다. 현재 폴더 내 프롬프트(`[Battle] Prompts - Enemy 1.txt`, `Enemy Mecha.txt`)처럼 **앞부분은 핵심 태그, 뒷부분은 자연어 문장**으로 구성하는 하이브리드 방식이 정석입니다.

| 구분 | 적합한 용도 | 작성 예시 |
| :--- | :--- | :--- |
| **단부루 태그 (Tags)** | 인원수/종류, 고정된 외형, 복장 파츠, 카메라 앵글, 게임 스프라이트 규격, 화풍/작가 태그 | `1girl, solo, character profile, white outline, game sprite, full body, {3/4 profile}, simple background` |
| **영어 자연어 (Natural Language)** | 복잡한 구도, 좌우 방향성, 손에 쥔 무기/소품의 구체적 위치, 재질감 및 분위기 묘사, 조명 방향 | `A sinister near-future armed bandit standing in an aggressive combat stance facing left. He wears a rugged tactical gas mask...` |

### 권장 배치 순서
1. **[주제 / 인원수]** (`1girl, solo` / `1man, solo` / `no humans, mecha`)
2. **[게임 에셋 규격 및 구도 태그]** (`game sprite, character profile, white outline, full body, standing, facing left, {3/4 profile}`)
3. **[외형 / 복장 / 장비 태그]** (`black beret, white tactical jacket, holding handgun, {revolver}`)
4. **[구체적 상황 및 디테일 (영어 자연어 문장)]** (`A lethal near-future military reconnaissance drone hovering in mid-air facing left...`)
5. **[베이스 프롬프트 (작가 배합 / 퀄리티 / 스타일 태그)]** (`[Base Prompts].txt` 내용 결합)
6. **[텍스트 렌더링 지시어]** (필요 시 맨 마지막에 `Text:` 배치)

---

## 3. 가중치(Weight) 문법 완벽 정리

NAI V5는 **괄호 중첩 방식(`{}`, `[]`)**과 **수치 지정 방식(`숫자::태그::`)** 두 가지를 모두 지원합니다.

### ① 괄호 가중치 (`{}` / `[]`) — 미세 조정용
* **`{태그}` (강화):** 겹칠 때마다 가중치가 **$1.05$배**씩 곱해집니다.
* **`[태그]` (약화):** 겹칠 때마다 가중치가 **$1.05$로 나누어져** 약해집니다.

| 표기 | 배율 계산 ($1.05^n$) | 실제 가중치 (약) | 주 용도 |
| :---: | :---: | :---: | :--- |
| `{tag}` | $1.05^1$ | **1.05x** | 특정 파츠 가볍게 강조 (예: `{revolver}`, `{3/4 profile}`) |
| `{{tag}}` | $1.05^2$ | **1.10x** | 묻히기 쉬운 의상/특징 강조 |
| `{{{tag}}}` | $1.05^3$ | **1.16x** | 핵심 포인트 강하게 고정 |
| `[tag]` | $1 / 1.05^1$ | **0.95x** | 영향력 살짝 줄이기 |
| `[[tag]]` | $1 / 1.05^2$ | **0.91x** | 부차적인 요소 힘 빼기 |

> [!TIP]
> **네거티브 프롬프트(Undesired Content)에서의 괄호:**
> 네거티브 칸에 `{bad hands}`를 넣으면 "나쁜 손을 **더 강하게 배제**"하고, `[signature]`를 넣으면 "서명 배제 강도를 **약화**"합니다.

### ② 수치형 가중치 (`숫자::태그::`) — 정밀 배합 및 제거용
괄호를 여러 개 치는 대신 정확한 배수를 지정할 때 사용합니다. 반드시 **끝에 `::`를 붙여 닫아주어야** 뒤따르는 프롬프트에 가중치가 번지지 않습니다.

* **강화 (`> 1.0`):** `1.2::artist:yd (orange maru)::` 또는 `2.0::anime coloring::`
* **약화 (`0.0 ~ 1.0`):** `0.2::artist:crimson comics::`, `0.5::artist:santa (sunflower)::` (작가 그림체를 살짝만 섞을 때 매우 유용)
* **음수 가중치 (`< 0.0`, 개념 제거/반전):** `-1::multiple views::`, `-2::monochrome::`, `-3::official art, official style::`
  * 메인 프롬프트 내부에서도 특정 스타일이나 요소가 튀어나오는 것을 직접 억제할 수 있습니다.

> [!WARNING]
> **가중치 사용 시 주의점**
> - 닫는 기호 `::`를 빠뜨리면 그 줄 이후의 모든 태그에 해당 가중치가 적용됩니다.
> - V5는 문맥 이해력이 높아서 가중치를 과도하게 주면(예: `3.0::` 이상 또는 `{{{{{{}}}}}}` 남발) 외곽선이 깨지거나 색감이 타버리는 현상(Overbaking)이 발생할 수 있습니다.

---

## 4. 영어 텍스트 생성 (Text Rendering in T2I)

V5에서 크게 강화된 기능으로, 이미지 내부에 원하는 영문 텍스트(로고, 기체 마킹, 대사 등)를 선명하게 그려 넣을 수 있습니다.

### 방법 A: 공식 `Text:` 전용 구문 (가장 정확함)
1. 프롬프트 본문 중간에 `text`, `english text` (또는 기체 스텐실의 경우 `military warning stencils`) 태그를 포함합니다.
2. 프롬프트의 **가장 마지막 줄**에 `Text:`를 적고 출력할 문자열을 입력합니다.
   - **주의:** `Text:` 뒤에는 다른 일반 태그가 오면 안 됩니다 (`Text:` 이후의 모든 글자를 그림 속 글씨로 그리려고 시도하기 때문).

```text
no humans, mecha, robot, game sprite, military warning stencils, english text, ...
Text:
R&C Company
```

### 방법 B: 자연어 내 따옴표 지시
프롬프트 문장 안에서 텍스트가 들어갈 위치와 내용을 함께 서술하는 방식입니다 (`[Battle] Prompts - Enemy Mecha.txt`에서 사용 중인 방식).
- 예시: `...with military warning stencils and the text "R&C Company" printed on the UAV chassis.`
- 만약 자연어 방식만으로 글자가 잘 안 나오거나 철자가 틀린다면, 자연어 설명은 유지하되 프롬프트 **맨 끝에 `Text: R&C Company`**를 함께 붙여주면 성공률이 대폭 올라갑니다.
- **참고:** NAI의 `Add Quality Tags` 옵션이 켜져 있으면 내부적으로 `no text`가 자동 추가되어 짧은 텍스트 생성을 방해할 수 있습니다. 글자가 잘 안 찍힐 때는 품질 태그 자동 추가 옵션을 끄고 수동으로 품질 태그(`masterpiece, best quality, very aesthetic`)를 넣는 것이 좋습니다.

---

## 5. 현재 프로젝트 프롬프트 파일 구성 및 점검 팁

현재 [`아트 프롬프트/아트`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8) 폴더의 프롬프트들은 다음과 같은 모듈 구조를 따르고 있습니다:

1. **[`[Base Prompts].txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/%5BBase%20Prompts%5D.txt)**
   - 아티스트 가중치 믹스(`0.2:: ~ 1.2::`) + 최신 연도 태그(`year 2026, year 2025`) + 품질 태그 + 채색(`2.0:: anime coloring::`) + 음수 가중치 배제(`-1::multiple views::` 등).
   - **팁:** 첫 줄에 `1girl,`이 고정되어 있으므로, 남성 적군(`1man`)이나 메카/드론(`no humans`)을 뽑을 때는 첫 줄의 `1girl,`을 해당 캐릭터의 주체 태그로 교체해야 충돌이 나지 않습니다.
2. **[`Undesired Contents - Base Prompts.txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/Undesired%20Contents%20-%20Base%20Prompts.txt)**
   - 네거티브 프롬프트 프리셋 (저화질, 인체 왜곡, 원치 않는 작가풍 및 멀티뷰 방지).
3. **캐릭터 / 배틀 스프라이트 개별 프롬프트**
   - [`Character Lobby Prompts _ Antagonist.txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/Character%20Lobby%20Prompts%20_%20Antagonist.txt) & [`[Battle] Prompts - Antagonist.txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/%5BBattle%5D%20Prompts%20-%20Antagonist.txt): 태그 + `{}` 괄호 가중치 위주 구성.
   - [`[Battle] Prompts - Enemy 1.txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/%5BBattle%5D%20Prompts%20-%20Enemy%201.txt) & [`[Battle] Prompts - Enemy Mecha.txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/%5BBattle%5D%20Prompts%20-%20Enemy%20Mecha.txt): 핵심 태그 + `{}` 가중치 + **V5 스타일 영어 자연어 서술 문장** 하이브리드 구성.
   - **주의 (`Enemy Mecha.txt`):** 파일 맨 앞의 `// [NAI v5] 스카웃 유닛...` 주석 텍스트를 그대로 복사해서 NAI 프롬프트 창에 넣으면 AI가 주석까지 프롬프트(특히 한국어/영어 단어인 `Base Prompt의 girl 대신`)로 인식해버려 인간 소녀(`girl`)가 섞여 나올 수 있으니, 실제 생성 시에는 `no humans`부터 복사해 넣는 것이 안전합니다.


---

## 6. 베이스 작가 프롬프트의 '고정 눈매(실눈/아이섀도)' 강제 교정 가이드

현재 [`[Base Prompts].txt`](file:///C:/Users/SKYLIGHT/Documents/Graduation/Project-Vertex/%EC%95%84%ED%8A%B8%20%ED%94%84%EB%A1%AC%ED%94%84%ED%8A%B8/%EC%95%84%ED%8A%B8/%5BBase%20Prompts%5D.txt)에 높은 가중치(`1.0:: ~ 1.2::`)로 들어간 작가진(`yd (orange maru)`, `kat (bu-kunn)`, `dramz`)은 **가로로 길고 위 눈꺼풀이 반쯤 덮인 나른한 눈매 + 붉은 아이섀도/홍조 + 살짝 벌린 입(치아 노출)**이 디폴트 화풍으로 강하게 학습되어 있습니다.

특히 `smug` 태그를 넣으면 이 작가진 특유의 **"가로로 좁고 게슴츠레한 눈"**이 100% 튀어나오므로, 캐릭터 시안의 고유 눈매를 살리려면 **수치형 양수 가중치(`2.0::`) + 음수 가중치(`-3::`)**를 세트로 묶어 작가 디폴트 눈매를 강제로 부숴야 합니다.

### 눈매 타입별 강제 고정 프리셋

| 원하는 눈매 타입 | 전면 배치 양수 가중치 (Positive) | 하단 배치 음수 가중치 (Negative) | 주의사항 |
| :--- | :--- | :--- | :--- |
| **① 당돌·쾌활한 큰 고양이눈**<br>(시안 2: 기어 창 랜서 스타일) | `2.2::wide open eyes, big round eyes, large tall irises::, 1.8::upturned tsurime eyes, V-shaped eyebrows, thick arched upper eyelashes::` | `-3::half-closed eyes, narrowed eyes, lidded eyes, droopy eyes, sleepy eyes::, -2::eyeshadow, makeup, seductive smile::` | `smug` 대신 `confident grin, closed-mouth smile, lively expression` 사용 (`smug`가 눈꺼풀을 반쯤 감김 처리함) |
| **② 날카롭고 매서운 눈매**<br>(시안 1: 혈액 쿠나이 캐릭터 스타일) | `2.0::sharp upturned eyes, tsurime, wide open sharp eyes, V-shaped eyebrows, furrowed eyebrows::` | `-3::droopy eyes, tareme, sleepy eyes, eyeshadow, pink makeup, blushing::, -2::smile, grin::` | 작가 특유의 볼홍조와 눈화장이 안 어울리므로 `-3::eyeshadow, blushing::` 필수 |
| **③ 동그랗고 순한 강아지눈 (처진 눈)** | `2.2::big round eyes, tareme, droopy eyes, wide open eyes, large round pupils::, arched eyebrows` | `-3::tsurime, sharp eyes, narrowed eyes, half-closed eyes, lidded eyes, eyeshadow::` | 작가진의 올라간 실눈 습관을 막기 위해 `-3::tsurime, narrowed eyes::` 필수 |
| **④ 차가운 하라구로 / 지토메(반쯤 감은 경멸/계략 눈)** | `2.0::jitome, flat upper eyelids, oversized round glasses, big eyes::, {knowing smile}, slight smirk` | `-3::eyeshadow, heavy makeup, blushing, open mouth::, -2::mature female, office lady::` | 가로로 좁아지지 않게 `big eyes`와 `jitome(직선 위 눈꺼풀)`를 같이 줌 |
