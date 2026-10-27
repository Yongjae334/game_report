using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DumbFrog.Logging;

namespace DumbFrog.Arcade
{
    // 인게임 씬은 이 컴포넌트 하나가 경기장과 UI를 구성합니다.
    public sealed class ArcadeMatch : MonoBehaviour
    {
        [Header("화면 리소스")]
        public Texture2D backgroundTexture;
        public Texture2D groundTexture;
        public Texture2D ballTexture;
        public Texture2D playerSheet;
        public Texture2D spikeSheet;
        public Texture2D cpuSheet;
        public Font koreanFont;
        [Header("화면 표시 설정")]
        [Tooltip("아래쪽 조작키 설명을 표시합니다.")]
        public bool showControlHints = false;
        [Tooltip("점수판에 나 / 도플갱어 이름을 표시합니다.")]
        public bool showPlayerNames = false;
        [Range(24, 72)] public int scoreFontSize = 48;
        [Header("공 크기")]
        [Tooltip("공의 지름입니다. 보이는 크기와 충돌 범위가 함께 바뀝니다. 기존 크기는 0.64입니다.")]
        [Range(0.4f, 1.4f)] public float ballDiameter = 0.84f;
        [Header("스파이크 / 승리 / 패배 시트 · 같은 폭 프레임을 가로로")]
        [Tooltip("CPU와 2P 스파이크 (준비 → 타격 → 마무리)")]
        public Texture2D cpuSpikeSheet;
        public Texture2D playerWinSheet;
        public Texture2D playerLoseSheet;
        public Texture2D cpuLoseSheet;
        [Range(1, 8)] public int spikeFrameCount = 3;
        [Range(1, 12)] public int winFrameCount = 6;
        [Range(1, 8)] public int loseFrameCount = 2;
        [Tooltip("경기가 끝난 뒤 결과 창을 띄우기 전에 승리/패배 모션을 보여주는 시간(초)")]
        [Range(0f, 5f)] public float resultMotionTime = 2.6f;
        [Header("슬라이딩 시트 · 같은 폭 프레임을 가로로 (시작 → 미끄러짐 → 마무리)")]
        public Texture2D playerSlideTexture;
        public Texture2D cpuSlideTexture;
        [Range(1, 8)] public int slideFrameCount = 3;
        [Tooltip("1이면 걷기 이미지와 같은 픽셀 크기로 그립니다.")]
        [Range(0.5f, 2f)] public float slideScale = 1f;
        public float slideVisualYOffset = 0f;
        [Header("걷기 / 대기 애니메이션 · 걷기 시트도 같은 폭 프레임")]
        [Range(1, 16)] public int walkFrameCount = 8;
        [Tooltip("최고 속도로 달릴 때의 초당 프레임 수. 실제 재생 속도는 이동 속도에 비례합니다.")]
        [Range(1f, 20f)] public float walkFramesPerSecond = 11f;
        [Tooltip("멈췄을 때 서 있는 프레임(0부터). 반 주기 뒤 프레임도 서 있는 자세로 씁니다.")]
        public int idleFrame = 2;
        [Tooltip("멈춰 있을 때 숨 쉬듯 살짝 움직입니다.")]
        public bool animateWhileIdle = true;
        [Tooltip("프레임별 Y 보정(원본 픽셀). 개수가 걷기 프레임 수와 같을 때만 적용됩니다.")]
        public float[] walkFrameYPixels = new float[0];
        [Range(0f, 5f)] public float walkBobMultiplier = 1f;
        [Header("경기")]
        [Range(1, 21)] public int winningScore = 11;
        [Range(0.08f, 0.35f)] public float cpuReaction = 0.14f;
        [Range(0.7f, 1.05f)] public float cpuSpeed = 0.94f;
        [Tooltip("CPU 실력. 높을수록 스파이크를 자주 노리고 점프 타이밍과 위치가 정확해집니다.")]
        [Range(0f, 1f)] public float cpuSkill = 0.55f;
        public string menuSceneName = "MainMenu";

        private VolleySimulation game;
        private VolleyInput input = new VolleyInput();
        private VolleyInput input2 = new VolleyInput();
        private bool twoPlayer;
        private float resultSelectDelay;
        // 테스트 로그용 이전 상태
        private readonly bool[] bothMoveHeld = new bool[2];
        private readonly int[] animState = { -1, -1 };
        private Vector2 pausedBall;
        private float loggedDiameter, loggedCpuSpeed, loggedCpuReaction, loggedCpuSkill;
        private int loggedFontSize, loggedWinningScore;
        private bool loggedNames, loggedHints;
        private Camera gameCamera;
        private SpriteRenderer leftVisual, rightVisual, ballVisual, leftShadow, rightShadow, ballShadow;
        private SpriteRenderer[] ghosts = new SpriteRenderer[8];
        private Vector2[] history = new Vector2[8];
        private Sprite[] leftRun, rightRun, leftSpike, rightSpike, leftSlide, rightSlide, leftWin, leftLose, rightLose;
        // 승리 모션: 만세(0,1) → 뒤돌아 엉덩이 흔들기(2,3) → 별과 함께 기뻐하기(4,5)
        private static readonly int[] winSequence = { 0, 1, 0, 1, 0, 1, 2, 3, 2, 3, 4, 5, 4, 5, 4, 5 };
        private float resultPending, endStartTime;
        private readonly float[] endY = new float[2];
        private float leftPhase, rightPhase;
        private Sprite whiteSprite, circleSprite;
        private Texture2D circleTexture;
        private List<Sprite> createdSprites = new List<Sprite>();
        private Text leftScore, rightScore, message, detail, modalTitle, modalDetail, primaryLabel;
        private GameObject modal, controlsPanel, playerNameLabel, cpuNameLabel;
        private Button primaryButton;
        private bool paused, leaving;
        private float previousFixedStep, animationTime, leftSquash, rightSquash, ballAngle, flash;
        private int seenHit, seenPoint, previousStage;
        private Vector2 previousLeft, previousRight, previousBall;
        private readonly Color orange = new Color(1f, 0.69f, 0.16f);
        private readonly Color purple = new Color(0.79f, 0.52f, 1f);
        private Burst[] bursts = new Burst[12];

        private sealed class Burst
        {
            public SpriteRenderer renderer;
            public Vector2 velocity;
            public float life;
        }

        private void Awake()
        {
            previousFixedStep = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1f / 120f;
            Time.timeScale = 1f;
            Application.targetFrameRate = 120;
            twoPlayer = MatchSettings.TwoPlayer;
            game = new VolleySimulation();
            game.RightIsHuman = twoPlayer;
            game.WinningScore = winningScore;
            game.CpuReaction = cpuReaction;
            game.CpuSpeed = cpuSpeed;
            game.CpuSkill = cpuSkill;
            game.BallRadius = Mathf.Clamp(ballDiameter, 0.4f, 1.4f) * 0.5f;
            game.SideNames = new[] { twoPlayer ? "1P" : "플레이어", twoPlayer ? "2P" : "CPU" };
            game.OnLog = GameLog.Write;
            GameLog.Font = koreanFont;
            BuildWorld(); BuildUI();
            GameLog.Write("TC-081", (twoPlayer ? "2인 대전" : "CPU 대전") + " 새 경기 시작 → 점수 0 : 0");
            LogSettings(true);
            ApplyDisplaySettings();
            ResetVisualHistory();
            previousStage = game.Stage;
            UpdateScore();
            GameLog.Write("TC-106", "점수판 표시 " + LeftName + " " + leftScore.text + " : " + rightScore.text + " " + RightName);
        }

        // 시작 시 한 번, 이후에는 Inspector에서 값이 바뀔 때마다 기록합니다(TC-080, TC-115 ~ TC-120).
        private void LogSettings(bool force)
        {
            float diameter = Mathf.Clamp(ballDiameter, 0.4f, 1.4f);
            if (force || diameter != loggedDiameter)
                GameLog.Write("TC-115", "공 지름 " + diameter.ToString("0.00") + " (표시 크기, 충돌 반경 함께 적용)");
            if (force || winningScore != loggedWinningScore)
                GameLog.Write("TC-116", "승리 점수 " + winningScore + "점" + (force ? "" : " (다음 경기부터 적용)"));
            if (!twoPlayer && (force || cpuReaction != loggedCpuReaction))
                GameLog.Write("TC-080", "CPU 반응 간격 " + cpuReaction.ToString("0.00") + "초");
            if (!twoPlayer && (force || cpuSkill != loggedCpuSkill))
                GameLog.Write("TC-080", "CPU 실력 " + cpuSkill.ToString("0.00") + " (스파이크 시도 빈도, 타이밍 정확도)");
            if (!twoPlayer && (force || cpuSpeed != loggedCpuSpeed))
                GameLog.Write("TC-117", "CPU 이동 속도 " + cpuSpeed.ToString("0.00") + "배");
            if (force || showPlayerNames != loggedNames)
                GameLog.Write("TC-118", "이름 표시 " + (showPlayerNames || twoPlayer ? "켜짐 (" + LeftName + " / " + RightName + ")" : "꺼짐"));
            if (force || showControlHints != loggedHints)
                GameLog.Write("TC-119", "조작키 안내 " + (showControlHints ? "켜짐" : "꺼짐"));
            if (force || scoreFontSize != loggedFontSize)
                GameLog.Write("TC-120", "점수판 글자 크기 " + Mathf.Clamp(scoreFontSize, 24, 72));
            loggedDiameter = diameter; loggedWinningScore = winningScore; loggedCpuReaction = cpuReaction; loggedCpuSkill = cpuSkill;
            loggedCpuSpeed = cpuSpeed; loggedNames = showPlayerNames; loggedHints = showControlHints; loggedFontSize = scoreFontSize;
        }

        private Sprite MakeSprite(Texture2D texture, Rect rect, Vector2 pivot, float ppu)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
            createdSprites.Add(sprite); return sprite;
        }

        private Sprite FullSprite(Texture2D texture, Vector2 pivot, float ppu)
        {
            return MakeSprite(texture, new Rect(0, 0, texture.width, texture.height), pivot, ppu);
        }

        private Sprite[] Slice(Texture2D texture, int[] edges, float displayHeight)
        {
            Sprite[] result = new Sprite[edges.Length - 1];
            texture.filterMode = FilterMode.Point;
            for (int i = 0; i < result.Length; i++)
                result[i] = MakeSprite(texture, new Rect(edges[i], 0, edges[i + 1] - edges[i], texture.height),
                    new Vector2(0.5f, 0f), texture.height / displayHeight);
            return result;
        }

        // 같은 폭으로 나뉜 가로 시트를 자릅니다. 각 칸의 아래 가운데가 몸 중심/바닥입니다.
        private Sprite[] SliceEvenly(Texture2D texture, int count, float ppu)
        {
            if (texture == null) return null;
            count = Mathf.Clamp(count, 1, texture.width);
            texture.filterMode = FilterMode.Point;
            Sprite[] result = new Sprite[count];
            float width = texture.width / (float)count;
            for (int i = 0; i < count; i++)
                result[i] = MakeSprite(texture, new Rect(Mathf.Round(i * width), 0f, Mathf.Round((i + 1) * width) - Mathf.Round(i * width), texture.height),
                    new Vector2(0.5f, 0f), ppu);
            return result;
        }

        private SpriteRenderer Render(string name, Sprite sprite, Vector3 position, int order, Color tint)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false); obj.transform.position = position;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = order; renderer.color = tint;
            return renderer;
        }

        private SpriteRenderer Rectangle(string name, float x, float y, float width, float height, int order, Color tint)
        {
            SpriteRenderer renderer = Render(name, whiteSprite, new Vector3(x, y, 0f), order, tint);
            renderer.transform.localScale = new Vector3(width, height, 1f); return renderer;
        }

        private void BuildWorld()
        {
            GameObject cameraObject = new GameObject("MatchCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 4.65f, -10f);
            gameCamera = cameraObject.GetComponent<Camera>();
            gameCamera.orthographic = true; gameCamera.orthographicSize = 5.65f;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = new Color(0.035f, 0.055f, 0.10f);
            whiteSprite = FullSprite(Texture2D.whiteTexture, new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
            circleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            circleTexture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - distance) * 8f));
            }
            circleTexture.SetPixels(pixels); circleTexture.Apply();
            circleSprite = FullSprite(circleTexture, new Vector2(0.5f, 0.5f), 32f);
            SpriteRenderer background = Render("BeachBackground", FullSprite(backgroundTexture, new Vector2(0.5f, 0.5f), 100f),
                new Vector3(0f, 4.65f, 0f), -30, Color.white);
            background.transform.localScale = new Vector3(20.10f / background.sprite.bounds.size.x, 11.30f / background.sprite.bounds.size.y, 1f);
            Rectangle("GroundBase", 0f, -0.64f, 20.1f, 1.28f, -15, new Color(0.38f, 0.25f, 0.16f));
            Sprite ground = MakeSprite(groundTexture, new Rect(0f, 0f, groundTexture.width, 101f), new Vector2(0.5f, 1f), 100f);
            SpriteRenderer grass = Render("GrassCourt", ground, Vector3.zero, -10, Color.white);
            grass.transform.localScale = new Vector3(20.1f / ground.bounds.size.x, 1.15f, 1f);
            Rectangle("NetBody", 0f, VolleySimulation.NetHeight * 0.5f, 0.20f, VolleySimulation.NetHeight, 6, new Color(0.15f, 0.19f, 0.28f));
            Rectangle("NetRimLeft", -0.11f, VolleySimulation.NetHeight * 0.5f, 0.045f, VolleySimulation.NetHeight, 7, Color.white);
            Rectangle("NetRimRight", 0.11f, VolleySimulation.NetHeight * 0.5f, 0.045f, VolleySimulation.NetHeight, 7, new Color(0.64f, 0.71f, 0.77f));
            for (int i = 1; i < 10; i++) Rectangle("NetMesh", 0f, i * 0.23f, 0.24f, 0.024f, 8, new Color(0.74f, 0.81f, 0.9f));
            Rectangle("NetTop", 0f, VolleySimulation.NetHeight, 0.30f, 0.10f, 9, Color.white);
            Rectangle("CenterMark", 0f, -0.07f, 0.4f, 0.12f, 9, Color.white);
            if (playerSlideTexture == null) playerSlideTexture = Resources.Load<Texture2D>("DumbFrogMotion/PlayerSlide");
            if (cpuSlideTexture == null) cpuSlideTexture = Resources.Load<Texture2D>("DumbFrogMotion/CPUSlide");
            leftRun = SliceEvenly(playerSheet, walkFrameCount, playerSheet.height / 1.72f);
            rightRun = SliceEvenly(cpuSheet, walkFrameCount, cpuSheet.height / 1.72f);
            // 슬라이딩도 걷기와 같은 픽셀 밀도로 그려 크기가 튀지 않게 합니다.
            float slide = Mathf.Clamp(slideScale, 0.5f, 2f);
            leftSlide = SliceEvenly(playerSlideTexture, slideFrameCount, leftRun[0].pixelsPerUnit / slide);
            rightSlide = SliceEvenly(cpuSlideTexture, slideFrameCount, rightRun[0].pixelsPerUnit / slide);
            leftSpike = Slice(spikeSheet, new int[] { 0, 61, 124, 185 }, 1.72f);
            // CPU/2P 스파이크와 승리·패배 이미지도 각 캐릭터 걷기와 같은 픽셀 밀도로 그립니다.
            rightSpike = SliceEvenly(cpuSpikeSheet, spikeFrameCount, rightRun[0].pixelsPerUnit);
            leftWin = SliceEvenly(playerWinSheet, winFrameCount, leftRun[0].pixelsPerUnit);
            leftLose = SliceEvenly(playerLoseSheet, loseFrameCount, leftRun[0].pixelsPerUnit);
            rightLose = SliceEvenly(cpuLoseSheet, loseFrameCount, rightRun[0].pixelsPerUnit);
            leftVisual = Render("Player", leftRun[0], Vector3.zero, 12, Color.white); leftVisual.flipX = true;
            rightVisual = Render("Doppelganger", rightRun[0], Vector3.zero, 12, Color.white);
            leftShadow = Render("PlayerShadow", circleSprite, Vector3.zero, 0, new Color(0f, 0f, 0f, 0.23f));
            rightShadow = Render("CPUShadow", circleSprite, Vector3.zero, 0, new Color(0f, 0f, 0f, 0.23f));
            ballShadow = Render("BallShadow", circleSprite, Vector3.zero, 0, new Color(0f, 0f, 0f, 0.20f));
            Sprite ballSprite = MakeSprite(ballTexture, new Rect(8f, 10f, 47f, 47f), new Vector2(0.5f, 0.5f), 47f / 0.64f);
            ballVisual = Render("Ball", ballSprite, Vector3.zero, 20, Color.white);
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = Render("BallTrail", ballSprite, Vector3.zero, 19 - i, Color.clear);
            for (int i = 0; i < bursts.Length; i++) bursts[i] = new Burst { renderer = Render("HitSpark", whiteSprite, Vector3.zero, 22, Color.clear), life = 0f };
        }

        private void Update()
        {
            if (leaving) return;
            ApplyDisplaySettings();
            LogSettings(false);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (game.Stage == VolleySimulation.Finished) GameLog.Write("TC-105", "결과 화면에서 ESC → 일시정지로 전환하지 않음");
                else SetPaused(!paused, paused ? "TC-099" : "TC-096");
            }
            if (resultPending > 0f)
            {
                // 경기 종료 직후에는 승리/패배 모션을 먼저 보여준 뒤 결과 창을 띄웁니다.
                resultPending -= Time.unscaledDeltaTime;
                if (resultPending <= 0f) ShowResult();
            }
            if (resultSelectDelay > 0f)
            {
                // 2P가 Enter(스파이크)를 누르던 중 결과창이 떠도 바로 '한 판 더'가 눌리지 않게 잠시 기다립니다.
                resultSelectDelay -= Time.unscaledDeltaTime;
                if (resultSelectDelay <= 0f && modal.activeSelf) EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
            }
            if (paused || game.Stage == VolleySimulation.Finished)
            {
                if (AnyGameKeyDown())
                {
                    if (paused) GameLog.Write("TC-098", "일시정지 중 조작 입력 → 캐릭터 정지 유지", 0.5f);
                    else GameLog.Write("TC-094", "경기 종료 후 조작 입력 → 득점, 진행 없음", 0.5f);
                }
                ClearInput(); return;
            }
            // 1P: D/G 이동, R 점프, Z 스파이크, X 슬라이딩 (CPU 대전도 같은 키)
            ReadPlayer(input, 0, KeyCode.D, KeyCode.G, KeyCode.R, KeyCode.F, KeyCode.Z, KeyCode.None, KeyCode.X, 1f);
            // 2P: ←/→ 이동, ↑ 점프, Enter 스파이크, 오른쪽 Shift 슬라이딩
            if (twoPlayer)
                ReadPlayer(input2, 1, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
                    KeyCode.Return, KeyCode.KeypadEnter, KeyCode.RightShift, -1f);
        }

        private static readonly KeyCode[] gameKeys = { KeyCode.D, KeyCode.G, KeyCode.R, KeyCode.F, KeyCode.Z, KeyCode.X,
            KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.RightShift };

        private static bool AnyGameKeyDown()
        {
            foreach (KeyCode key in gameKeys) if (Input.GetKeyDown(key)) return true;
            return false;
        }

        // forward는 상대 코트 방향(1P +1 / 2P -1). 아래키+스파이크는 내리꽂기, 뒤쪽 방향키+스파이크는 높게 넘깁니다.
        private void ReadPlayer(VolleyInput target, int index, KeyCode left, KeyCode right, KeyCode jump, KeyCode down,
            KeyCode spike, KeyCode spikeAlt, KeyCode slide, float forward)
        {
            bool l = Input.GetKey(left), r = Input.GetKey(right), d = Input.GetKey(down);
            bool spikeDown = Input.GetKeyDown(spike) || Input.GetKeyDown(spikeAlt);
            target.move = (r ? 1f : 0f) - (l ? 1f : 0f);
            target.jump |= Input.GetKeyDown(jump);
            target.slide |= Input.GetKeyDown(slide);
            target.spike |= spikeDown;
            bool back = target.move * forward < -0.1f;
            target.aim = d ? -1 : back ? 1 : 0;
            string who = game.SideNames[index];
            if (l && r && !bothMoveHeld[index]) GameLog.Write("TC-022", who + " 좌우 동시 입력 → 이동 상쇄");
            bothMoveHeld[index] = l && r;
            if (spikeDown && d && (l || r))
                GameLog.Write("TC-050", who + " 아래+방향키 동시 스파이크 입력 → 아래쪽 스파이크 우선");
        }

        private void ClearInput()
        {
            ClearInput(input); ClearInput(input2);
        }

        private static void ClearInput(VolleyInput target)
        {
            target.move = 0f; target.jump = false; target.slide = false; target.spike = false; target.aim = 0;
        }

        private static void ConsumePresses(VolleyInput target)
        {
            target.jump = false; target.slide = false; target.spike = false;
        }

        private string LeftName { get { return twoPlayer ? "1P" : "나"; } }
        private string RightName { get { return twoPlayer ? "2P" : "도플갱어"; } }

        private void FixedUpdate()
        {
            if (paused || leaving || game.Stage == VolleySimulation.Finished) return;
            previousLeft = new Vector2(game.Left.x, game.Left.y);
            previousRight = new Vector2(game.Right.x, game.Right.y);
            previousBall = new Vector2(game.Ball.x, game.Ball.y);
            float oldLeftY = game.Left.y, oldRightY = game.Right.y;
            VolleyInput rightInput = twoPlayer ? input2 : game.ThinkCPU(Time.fixedDeltaTime);
            game.Step(input, rightInput, Time.fixedDeltaTime);
            ConsumePresses(input); ConsumePresses(input2);
            if (oldLeftY > 0.02f && game.Left.y <= 0f) leftSquash = 1f;
            if (oldRightY > 0.02f && game.Right.y <= 0f) rightSquash = 1f;
            if (game.Stage == VolleySimulation.Ready && previousStage != VolleySimulation.Ready) ResetVisualHistory();
            if (game.Stage != previousStage)
            {
                if (game.Stage == VolleySimulation.Rally)
                {
                    message.text = "시작!"; detail.text = ""; flash = 0.45f;
                    GameLog.Write("INFO", "랠리 시작");
                }
                previousStage = game.Stage;
            }
            if (game.HitSerial != seenHit)
            {
                seenHit = game.HitSerial; EmitHit();
            }
            if (game.PointSerial != seenPoint)
            {
                seenPoint = game.PointSerial; UpdateScore();
                GameLog.Write("TC-084", "점수판 갱신 → " + leftScore.text + " : " + rightScore.text);
                if (twoPlayer) message.text = game.LastPoint == 0 ? "1P 득점!" : "2P 득점!";
                else message.text = game.LastPoint == 0 ? "내 득점!" : "도플갱어 득점";
                GameLog.Write(game.LastPoint == 0 ? "TC-087" : "TC-088", "'" + message.text + "' 문구 표시");
                message.color = game.LastPoint == 0 ? orange : purple;
                detail.text = "";
                if (game.Stage == VolleySimulation.Finished) BeginResultMotion();
            }
            for (int i = history.Length - 1; i > 0; i--) history[i] = history[i - 1];
            history[0] = new Vector2(game.Ball.x, game.Ball.y);
        }

        private void LateUpdate()
        {
            if (game == null) return;
            float dt = paused ? 0f : Time.unscaledDeltaTime;
            animationTime += dt;
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            if (paused || game.Stage != VolleySimulation.Rally) t = 1f;
            DrawPlayer(game.Left, leftVisual, leftShadow, leftRun, leftSlide, previousLeft, t, 0, ref leftSquash, ref leftPhase, dt);
            DrawPlayer(game.Right, rightVisual, rightShadow, rightRun, rightSlide, previousRight, t, 1, ref rightSquash, ref rightPhase, dt);
            Vector2 ballPosition = Vector2.Lerp(previousBall, new Vector2(game.Ball.x, game.Ball.y), t);
            ballVisual.transform.position = new Vector3(ballPosition.x, ballPosition.y, 0f);
            ballAngle -= game.Ball.vx * dt * 48f;
            ballVisual.transform.rotation = Quaternion.Euler(0f, 0f, ballAngle);
            ballVisual.color = game.SpikeGlow > 0f ? new Color(1f, 0.88f, 0.74f) : Color.white;
            ballShadow.transform.position = new Vector3(ballPosition.x, 0.025f, 0f);
            float ballScale = Mathf.Clamp(0.8f - ballPosition.y * 0.045f, 0.36f, 0.8f);
            ballShadow.transform.localScale = new Vector3(ballScale, 0.12f, 1f);
            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i].transform.position = new Vector3(history[i].x, history[i].y, 0f);
                ghosts[i].transform.localScale = Vector3.one * (Mathf.Clamp(ballDiameter, 0.4f, 1.4f) / 0.64f) * (1f - i * 0.055f);
                ghosts[i].color = game.SpikeGlow > 0f && game.Stage == VolleySimulation.Rally ?
                    new Color(1f, 0.6f, 0.28f, 0.30f * (1f - i / 8f)) : Color.clear;
            }
            UpdateBursts(dt);
            if (game.Stage == VolleySimulation.Ready)
            {
                message.text = "준비!"; message.color = Color.white;
                detail.text = game.Server == 0 ? (twoPlayer ? "1P 서브" : "내 서브") : RightName + " 서브";
            }
            else if (game.Stage == VolleySimulation.Rally)
            {
                flash -= dt;
                if (flash <= 0f) { message.text = ""; detail.text = ""; }
            }
            FitCamera();
        }

        private void BeginResultMotion()
        {
            endStartTime = animationTime;
            endY[0] = game.Left.y; endY[1] = game.Right.y;
            if (twoPlayer) message.text = game.Winner == 0 ? "1P 승리!" : "2P 승리!";
            else message.text = game.Winner == 0 ? "승리!" : "패배...";
            message.color = game.Winner == 0 ? orange : purple;
            detail.text = "";
            GameLog.Write("INFO", game.SideNames[game.Winner] + " 승리 모션 / " + game.SideNames[1 - game.Winner] + " 패배 모션 표시");
            resultPending = Mathf.Max(0.01f, resultMotionTime);
        }

        // 경기가 끝나면 이긴 쪽은 승리 모션, 진 쪽은 쓰러진 모션을 반복합니다.
        private void DrawEndPose(VolleyBody body, SpriteRenderer renderer, SpriteRenderer shadow, Sprite[] runFrames, int side, float dt)
        {
            float t = animationTime - endStartTime;
            // 공중에서 경기가 끝났다면 바닥으로 내려옵니다.
            endY[side] = Mathf.Max(0f, endY[side] - dt * 9f);
            float y = endY[side];
            bool won = game.Winner == side;
            Sprite[] frames = won ? (side == 0 ? leftWin : null) : (side == 0 ? leftLose : rightLose);
            renderer.transform.localScale = Vector3.one;
            if (frames == null)
            {
                // 전용 승리 이미지가 없으면 제자리에서 폴짝폴짝 뛰며 기뻐합니다.
                float hop = Mathf.Abs(Mathf.Sin(t * 6f));
                renderer.sprite = runFrames[0];
                renderer.flipX = side == 0;
                renderer.transform.localScale = new Vector3(1f + (1f - hop) * 0.06f, 1f - (1f - hop) * 0.06f, 1f);
                y += hop * 0.45f;
            }
            else if (won)
            {
                renderer.sprite = frames[winSequence[(int)(t * 6f) % winSequence.Length] % frames.Length];
                renderer.flipX = false;
            }
            else
            {
                renderer.sprite = frames[(int)(t * 2.5f) % frames.Length];
                renderer.flipX = false;
            }
            renderer.transform.position = new Vector3(body.x, y, 0f);
            shadow.transform.position = new Vector3(body.x, 0.035f, 0f);
            shadow.transform.localScale = new Vector3(Mathf.Max(0.8f, 1.55f - y * 0.14f), 0.18f, 1f);
        }

        private bool IsRestFrame(float phase, int count)
        {
            int frame = (int)phase % count, rest = ((idleFrame % count) + count) % count;
            return frame == rest || frame == (rest + count / 2) % count;
        }

        private void DrawPlayer(VolleyBody body, SpriteRenderer renderer, SpriteRenderer shadow, Sprite[] frames, Sprite[] slideFrames,
            Vector2 previous, float t, int side, ref float squash, ref float phase, float dt)
        {
            if (game.Stage == VolleySimulation.Finished)
            {
                DrawEndPose(body, renderer, shadow, frames, side, dt);
                return;
            }
            Vector2 position = Vector2.Lerp(previous, new Vector2(body.x, body.y), t);
            bool sliding = body.slide > 0f;
            bool airborne = body.y > 0.08f;
            // 스파이크 입력 후 0.24초 동안 준비 → 타격 → 마무리 프레임을 차례로 보여줍니다.
            Sprite[] spikeFrames = side == 0 ? leftSpike : rightSpike;
            float spikeElapsed = VolleySimulation.SpikeCooldown - body.spikeCooldown;
            bool spiking = !sliding && spikeFrames != null && body.spikeCooldown > 0f && spikeElapsed < 0.24f;
            bool grounded = !sliding && !airborne;
            int count = frames.Length;
            float fps = Mathf.Max(1f, walkFramesPerSecond);
            bool moving = grounded && Mathf.Abs(body.vx) > 0.3f;
            if (moving)
            {
                // 이동 거리만큼 발을 옮겨 미끄러지듯 걷지 않게 하고, 뒷걸음질은 거꾸로 재생합니다.
                float forward = side == 0 ? 1f : -1f;
                phase += body.vx * forward / VolleySimulation.RunSpeed * fps * dt;
            }
            else if (grounded && !IsRestFrame(phase, count))
            {
                // 멈추면 가장 가까운 서 있는 자세까지 마저 걸은 뒤 멈춥니다.
                phase += fps * dt;
            }
            phase = Mathf.Repeat(phase, count);
            int frame = Mathf.Clamp((int)phase, 0, count - 1);
            // 공중에서는 다리를 벌린 자세(상승) / 반 주기 뒤 자세(하강)로 고정합니다.
            if (airborne) frame = body.vy > 0f ? 0 : (count / 2) % count;
            LogAnimation(side, sliding ? 3 : spiking ? 4 : airborne ? (body.vy > 0f ? 2 : 5) : moving ? 1 : 0, slideFrames);
            float yOffset = 0f;
            squash = Mathf.Max(0f, squash - dt * 7f);
            if (sliding && slideFrames != null)
            {
                // 슬라이딩 시간에 맞춰 시작 → 미끄러짐 → 마무리 프레임을 차례로 보여줍니다.
                float progress = 1f - body.slide / VolleySimulation.SlideTime;
                renderer.sprite = slideFrames[Mathf.Clamp((int)(progress * slideFrames.Length), 0, slideFrames.Length - 1)];
                // 슬라이드 원본은 오른쪽을 향하므로 미끄러지는 방향에 맞춰 반전합니다.
                renderer.flipX = body.facing < 0f;
                renderer.transform.localScale = Vector3.one;
                yOffset = slideVisualYOffset;
            }
            else
            {
                renderer.sprite = spiking ? spikeFrames[Mathf.Clamp((int)(spikeElapsed / 0.08f), 0, spikeFrames.Length - 1)] : frames[frame];
                renderer.flipX = side == 0;
                float breathe = animateWhileIdle && grounded && !moving && !spiking
                    ? Mathf.Sin(animationTime * 3.2f + side * 1.3f) * 0.022f : 0f;
                renderer.transform.localScale = sliding ? new Vector3(1.25f, 0.60f, 1f)
                    : new Vector3(1f + squash * 0.15f - breathe * 0.5f, 1f - squash * 0.12f + breathe, 1f);
                if (grounded && !spiking && walkFrameYPixels != null && walkFrameYPixels.Length == count)
                {
                    // 동일한 프레임 번호로 이미지와 Y 보정을 함께 선택합니다.
                    yOffset = walkFrameYPixels[frame] / frames[frame].pixelsPerUnit * Mathf.Max(0f, walkBobMultiplier);
                }
            }
            renderer.transform.position = new Vector3(position.x, position.y + yOffset, 0f);
            shadow.transform.position = new Vector3(position.x, 0.035f, 0f);
            shadow.transform.localScale = new Vector3(Mathf.Max(0.8f, 1.55f - position.y * 0.14f), 0.18f, 1f);
        }

        private void FitCamera()
        {
            float target = 16f / 9f, actual = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (actual > target)
            {
                float width = target / actual; gameCamera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                float height = actual / target; gameCamera.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }

        private void ResetVisualHistory()
        {
            previousLeft = new Vector2(game.Left.x, game.Left.y);
            previousRight = new Vector2(game.Right.x, game.Right.y);
            previousBall = new Vector2(game.Ball.x, game.Ball.y);
            for (int i = 0; i < history.Length; i++) history[i] = previousBall;
        }

        // 애니메이션 상태가 바뀔 때 기록합니다. 0 대기, 1 이동, 2 상승, 3 슬라이딩, 4 스파이크, 5 하강
        private void LogAnimation(int side, int state, Sprite[] slideFrames)
        {
            if (state == animState[side]) return;
            animState[side] = state;
            string who = game.SideNames[side];
            switch (state)
            {
                case 0: GameLog.Write("TC-107", who + " 대기 애니메이션" + (animateWhileIdle ? " (숨쉬기)" : ""), 1.5f); break;
                case 1: GameLog.Write("TC-108", who + " 이동 애니메이션 재생", 1.5f); break;
                case 5: GameLog.Write("TC-109", who + " 점프 상승 → 하강 프레임 전환", 0.3f); break;
                case 3:
                    GameLog.Write(side == 0 ? "TC-110" : "TC-111", who + (slideFrames != null
                        ? " 슬라이딩 전용 이미지 " + slideFrames.Length + "프레임 표시" : " 슬라이딩 이미지 없음 → 납작 효과로 대체"));
                    break;
                case 4: GameLog.Write("TC-112", who + " 스파이크 전용 애니메이션 표시"); break;
            }
        }

        private void EmitHit()
        {
            if (game.LastHitWasSpike)
            {
                GameLog.Write("TC-049", "스파이크 타격 연출(큰 이펙트, 공 발광) 표시");
                GameLog.Write("TC-113", "스파이크 공 잔상 효과 표시");
            }
            GameLog.Write("TC-114", "타격 이펙트 표시", 1f);
            Color tint = game.LastHitSide == 0 ? orange : purple;
            for (int i = 0; i < bursts.Length; i++)
            {
                Burst b = bursts[i];
                float angle = (i / (float)bursts.Length) * Mathf.PI * 2f;
                float speed = game.LastHitWasSpike ? 4.8f : 2.7f;
                b.life = 0.22f; b.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                b.renderer.transform.position = new Vector3(game.Ball.x, game.Ball.y, 0f);
                b.renderer.transform.localScale = Vector3.one * (game.LastHitWasSpike ? 0.10f : 0.06f);
                b.renderer.color = tint;
            }
        }

        private void UpdateBursts(float dt)
        {
            foreach (Burst b in bursts)
            {
                if (b.life <= 0f) continue;
                b.life -= dt; b.renderer.transform.position += (Vector3)(b.velocity * dt);
                Color c = b.renderer.color; c.a = Mathf.Clamp01(b.life / 0.22f); b.renderer.color = c;
            }
        }

        private RectTransform UIObject(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }

        private Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color tint, bool block = false)
        {
            RectTransform rect = UIObject(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = tint; image.raycastTarget = block; return image;
        }

        private Text Label(string name, Transform parent, string text, Vector2 size, Vector2 position, int fontSize, Color tint)
        {
            RectTransform rect = UIObject(name, parent, size, position);
            Text label = rect.gameObject.AddComponent<Text>(); label.font = koreanFont; label.text = text;
            label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter; label.color = tint;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button UIButton(string name, Transform parent, string text, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            Image image = Panel(name, parent, size, position, new Color(0.96f, 0.83f, 0.53f), true);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(1f, 0.95f, 0.83f);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(0.74f, 0.68f, 0.54f); button.colors = colors;
            Label("Label", image.transform, text, size - new Vector2(12f, 4f), Vector2.zero, 24, new Color(0.16f, 0.13f, 0.18f));
            button.onClick.AddListener(action); return button;
        }

        private void BuildUI()
        {
            GameObject ui = new GameObject("MatchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(transform, false);
            Canvas canvas = ui.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            CanvasScaler scaler = ui.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Transform parent = ui.transform;
            Color dark = new Color(0.065f, 0.085f, 0.15f, 0.90f);
            Image l = Panel("PlayerScoreCard", parent, new Vector2(264f, 74f), new Vector2(-430f, 304f), dark);
            playerNameLabel = Label("Name", l.transform, LeftName, new Vector2(150f, 54f), new Vector2(-32f, 0f), 27, orange).gameObject;
            leftScore = Label("Score", l.transform, "0", new Vector2(76f, 64f), new Vector2(84f, 0f), 48, Color.white);
            Image r = Panel("CPUScoreCard", parent, new Vector2(264f, 74f), new Vector2(430f, 304f), dark);
            rightScore = Label("Score", r.transform, "0", new Vector2(76f, 64f), new Vector2(-84f, 0f), 48, Color.white);
            cpuNameLabel = Label("Name", r.transform, RightName, new Vector2(170f, 54f), new Vector2(32f, 0f), 27, purple).gameObject;
            Image target = Panel("MatchTarget", parent, new Vector2(220f, 36f), new Vector2(0f, 320f), dark);
            Label("Text", target.transform, winningScore + "점 선취", new Vector2(220f, 36f), Vector2.zero, 23, Color.white);
            UIButton("PauseButton", parent, "일시정지 · ESC", new Vector2(190f, 36f), new Vector2(0f, 271f), () => SetPaused(true, "TC-101"));
            Image footer = Panel("Controls", parent, new Vector2(1180f, 34f), new Vector2(0f, -332f), dark);
            controlsPanel = footer.gameObject;
            string hint = twoPlayer
                ? "1P  D/G 이동  R 점프  Z 스파이크  X 슬라이딩      2P  ←/→ 이동  ↑ 점프  Enter 스파이크  오른쪽 Shift 슬라이딩"
                : "D / G 이동    R 점프    Z 스파이크    X 슬라이딩    F+Z 아래로 / D+Z 높게";
            Label("Hint", footer.transform, hint, new Vector2(1150f, 34f), Vector2.zero, 20, Color.white);
            message = Label("Announcement", parent, "준비!", new Vector2(1000f, 80f), new Vector2(0f, 90f), 58, Color.white);
            Outline outline = message.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.09f, 0.06f, 0.14f, 0.85f); outline.effectDistance = new Vector2(3f, -3f);
            detail = Label("AnnouncementDetail", parent, "내 서브", new Vector2(1000f, 44f), new Vector2(0f, 34f), 28, Color.white);
            Outline detailOutline = detail.gameObject.AddComponent<Outline>(); detailOutline.effectColor = new Color(0.09f, 0.06f, 0.14f, 0.85f); detailOutline.effectDistance = new Vector2(2f, -2f);
            Image shade = Panel("PauseAndResult", parent, Vector2.zero, Vector2.zero, new Color(0.025f, 0.03f, 0.07f, 0.83f), true);
            RectTransform sr = shade.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
            modal = shade.gameObject;
            Image card = Panel("Card", shade.transform, new Vector2(660f, 390f), Vector2.zero, new Color(0.075f, 0.085f, 0.145f));
            Panel("TopAccent", card.transform, new Vector2(660f, 5f), new Vector2(0f, 192f), orange);
            modalTitle = Label("Title", card.transform, "잠깐 쉬기", new Vector2(610f, 74f), new Vector2(0f, 106f), 52, Color.white);
            modalDetail = Label("Detail", card.transform, "ESC를 누르면 계속할 수 있어요", new Vector2(610f, 68f), new Vector2(0f, 36f), 25, new Color(0.80f, 0.84f, 0.92f));
            primaryButton = UIButton("Primary", card.transform, "계속하기", new Vector2(370f, 58f), new Vector2(0f, -49f), PrimaryAction);
            primaryLabel = primaryButton.GetComponentInChildren<Text>();
            UIButton("MainMenu", card.transform, "시작화면", new Vector2(370f, 58f), new Vector2(0f, -123f), ReturnToMenu);
            modal.SetActive(false);
            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }

        // Update에서 적용해 Inspector의 실행 중 변경도 화면/판정에 함께 반영합니다.
        private void ApplyDisplaySettings()
        {
            float diameter = Mathf.Clamp(ballDiameter, 0.4f, 1.4f);
            game.BallRadius = diameter * 0.5f;
            game.CpuReaction = cpuReaction;
            game.CpuSpeed = cpuSpeed;
            game.CpuSkill = cpuSkill;
            if (ballVisual != null) ballVisual.transform.localScale = Vector3.one * (diameter / 0.64f);
            if (controlsPanel != null) controlsPanel.SetActive(showControlHints);
            // 2인 대전은 어느 쪽이 1P/2P인지 알 수 있도록 이름을 항상 표시합니다.
            bool names = showPlayerNames || twoPlayer;
            if (playerNameLabel != null) playerNameLabel.SetActive(names);
            if (cpuNameLabel != null) cpuNameLabel.SetActive(names);
            if (leftScore != null && rightScore != null)
            {
                leftScore.rectTransform.anchoredPosition = new Vector2(names ? 84f : 0f, 0f);
                rightScore.rectTransform.anchoredPosition = new Vector2(names ? -84f : 0f, 0f);
                Vector2 size = new Vector2(names ? 76f : 240f, 64f);
                leftScore.rectTransform.sizeDelta = rightScore.rectTransform.sizeDelta = size;
                leftScore.fontSize = rightScore.fontSize = Mathf.Clamp(scoreFontSize, 24, 72);
            }
        }

        private void UpdateScore() { leftScore.text = game.LeftScore.ToString(); rightScore.text = game.RightScore.ToString(); }

        // tc: 이 일시정지/재개를 일으킨 테스트 케이스 번호(ESC, 버튼, 창 포커스 등)
        private void SetPaused(bool value, string tc)
        {
            if (game.Stage == VolleySimulation.Finished || leaving || paused == value) return;
            paused = value; ClearInput(); modal.SetActive(value);
            Vector2 ball = new Vector2(game.Ball.x, game.Ball.y);
            if (value)
            {
                pausedBall = ball;
                string cause = tc == "TC-101" ? "일시정지 버튼" : tc == "TC-103" ? "창 포커스 해제" : "ESC";
                GameLog.Write(tc, cause + " → 일시정지 화면 표시");
            }
            else
            {
                GameLog.Write(tc, (tc == "TC-100" ? "계속하기 버튼" : "ESC") + " → 일시정지 해제, 경기 재개");
                GameLog.Write("TC-097", "일시정지 동안 공 이동 거리 " + Vector2.Distance(pausedBall, ball).ToString("0.000"));
            }
            if (value)
            {
                modalTitle.text = "잠깐 쉬기"; modalTitle.color = Color.white;
                modalDetail.text = "ESC를 누르면 계속할 수 있어요"; primaryLabel.text = "계속하기";
                EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
            }
            else EventSystem.current.SetSelectedGameObject(null);
        }

        private void ShowResult()
        {
            modal.SetActive(true); ClearInput();
            if (twoPlayer) modalTitle.text = game.Winner == 0 ? "1P 승리!" : "2P 승리!";
            else modalTitle.text = game.Winner == 0 ? "승리!" : "다시 도전!";
            modalTitle.color = game.Winner == 0 ? orange : purple;
            modalDetail.text = LeftName + "  " + game.LeftScore + "  :  " + game.RightScore + "  " + RightName;
            primaryLabel.text = "한 판 더";
            if (twoPlayer) { EventSystem.current.SetSelectedGameObject(null); resultSelectDelay = 0.8f; }
            else EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
        }

        private void PrimaryAction()
        {
            if (game.Stage == VolleySimulation.Finished)
            {
                game.WinningScore = winningScore;
                game.Restart(); seenHit = 0; seenPoint = 0; previousStage = game.Stage; resultSelectDelay = 0f; resultPending = 0f;
                paused = false; modal.SetActive(false); ClearInput(); ResetVisualHistory(); UpdateScore();
                EventSystem.current.SetSelectedGameObject(null);
                GameLog.Write("TC-095", "'한 판 더' → 점수, 승리 상태, 위치 초기화 (" + leftScore.text + " : " + rightScore.text + ", " + winningScore + "점 선취)");
            }
            else SetPaused(false, "TC-100");
        }

        private void ReturnToMenu()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(menuSceneName)) { Debug.LogError("MainMenu 씬을 Build Settings에 등록하세요."); return; }
            GameLog.Write("TC-102", "시작화면 버튼 → MainMenu 씬으로 이동");
            leaving = true; Time.timeScale = 1f; SceneManager.LoadSceneAsync(menuSceneName);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && game != null && game.Stage == VolleySimulation.Rally) SetPaused(true, "TC-103");
        }

        private void OnDestroy()
        {
            Time.fixedDeltaTime = previousFixedStep;
            foreach (Sprite sprite in createdSprites) if (sprite != null) Destroy(sprite);
            if (circleTexture != null) Destroy(circleTexture);
        }
    }
}
