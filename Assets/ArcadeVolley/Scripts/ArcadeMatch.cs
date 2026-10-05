using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        [Header("슬라이딩 이미지 · 비워두면 포함된 이미지 자동 사용")]
        public Texture2D playerSlideTexture;
        public Texture2D cpuSlideTexture;
        [Range(0.6f, 2.4f)] public float slideSpriteWidth = 1.8f;
        public float slideVisualYOffset = 0f;
        [Header("걷기 / 대기 애니메이션")]
        public bool animateWhileIdle = true;
        [Range(1f, 20f)] public float walkFramesPerSecond = 11f;
        [Tooltip("원본 이미지 픽셀 기준 Y 보정. 물리 위치에는 영향을 주지 않습니다.")]
        public float[] walkFrameYPixels = new float[] { 2f, 1f, 0f, 1f, 2f };
        [Range(0f, 5f)] public float walkBobMultiplier = 1f;
        [Header("경기")]
        [Range(1, 21)] public int winningScore = 11;
        [Range(0.08f, 0.35f)] public float cpuReaction = 0.14f;
        [Range(0.7f, 1.05f)] public float cpuSpeed = 0.94f;
        public string menuSceneName = "MainMenu";

        private VolleySimulation game;
        private VolleyInput input = new VolleyInput();
        private Camera gameCamera;
        private SpriteRenderer leftVisual, rightVisual, ballVisual, leftShadow, rightShadow, ballShadow;
        private SpriteRenderer[] ghosts = new SpriteRenderer[8];
        private Vector2[] history = new Vector2[8];
        private Sprite[] leftRun, rightRun, leftSpike;
        private Sprite playerSlideSprite, cpuSlideSprite;
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
            game = new VolleySimulation();
            game.WinningScore = winningScore;
            game.CpuReaction = cpuReaction;
            game.CpuSpeed = cpuSpeed;
            game.BallRadius = Mathf.Clamp(ballDiameter, 0.4f, 1.4f) * 0.5f;
            BuildWorld(); BuildUI();
            ApplyDisplaySettings();
            ResetVisualHistory();
            previousStage = game.Stage;
            UpdateScore();
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
            playerSlideSprite = MakeSlideSprite(playerSlideTexture, 2f / 64f);
            cpuSlideSprite = MakeSlideSprite(cpuSlideTexture, 11f / 64f);
            leftRun = Slice(playerSheet, new int[] { 0, 58, 118, 179, 241, 299 }, 1.72f);
            rightRun = Slice(cpuSheet, new int[] { 0, 59, 119, 181, 243, 301 }, 1.72f);
            leftSpike = Slice(spikeSheet, new int[] { 0, 61, 124, 185 }, 1.72f);
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
            if (Input.GetKeyDown(KeyCode.Escape) && game.Stage != VolleySimulation.Finished) SetPaused(!paused);
            if (paused || game.Stage == VolleySimulation.Finished)
            {
                ClearInput(); return;
            }
            input.move = ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1f : 0f) -
                ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1f : 0f);
            input.jump |= Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow);
            input.slide |= Input.GetKeyDown(KeyCode.J);
            input.spike |= Input.GetKeyDown(KeyCode.K);
            input.aim = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0;
        }

        private void ClearInput()
        {
            input.move = 0f; input.jump = false; input.slide = false; input.spike = false;
        }

        private void FixedUpdate()
        {
            if (paused || leaving || game.Stage == VolleySimulation.Finished) return;
            previousLeft = new Vector2(game.Left.x, game.Left.y);
            previousRight = new Vector2(game.Right.x, game.Right.y);
            previousBall = new Vector2(game.Ball.x, game.Ball.y);
            float oldLeftY = game.Left.y, oldRightY = game.Right.y;
            game.Step(input, game.ThinkCPU(Time.fixedDeltaTime), Time.fixedDeltaTime);
            input.jump = false; input.slide = false; input.spike = false;
            if (oldLeftY > 0.02f && game.Left.y <= 0f) leftSquash = 1f;
            if (oldRightY > 0.02f && game.Right.y <= 0f) rightSquash = 1f;
            if (game.Stage == VolleySimulation.Ready && previousStage != VolleySimulation.Ready) ResetVisualHistory();
            if (game.Stage != previousStage)
            {
                if (game.Stage == VolleySimulation.Rally) { message.text = "시작!"; detail.text = ""; flash = 0.45f; }
                previousStage = game.Stage;
            }
            if (game.HitSerial != seenHit)
            {
                seenHit = game.HitSerial; EmitHit();
            }
            if (game.PointSerial != seenPoint)
            {
                seenPoint = game.PointSerial; UpdateScore();
                message.text = game.LastPoint == 0 ? "내 득점!" : "도플갱어 득점";
                message.color = game.LastPoint == 0 ? orange : purple;
                detail.text = "";
                if (game.Stage == VolleySimulation.Finished) ShowResult();
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
            DrawPlayer(game.Left, leftVisual, leftShadow, leftRun, previousLeft, t, 0, ref leftSquash, dt);
            DrawPlayer(game.Right, rightVisual, rightShadow, rightRun, previousRight, t, 1, ref rightSquash, dt);
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
                detail.text = game.Server == 0 ? "내 서브" : "도플갱어 서브";
            }
            else if (game.Stage == VolleySimulation.Rally)
            {
                flash -= dt;
                if (flash <= 0f) { message.text = ""; detail.text = ""; }
            }
            FitCamera();
        }

        private Sprite MakeSlideSprite(Texture2D texture, float bottomPivot)
        {
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            // 원본의 아래 투명 여백(플레이어 2px / CPU 11px)을 피벗으로 보정합니다.
            return FullSprite(texture, new Vector2(0.5f, bottomPivot), texture.width);
        }

        private void DrawPlayer(VolleyBody body, SpriteRenderer renderer, SpriteRenderer shadow, Sprite[] frames,
            Vector2 previous, float t, int side, ref float squash, float dt)
        {
            Vector2 position = Vector2.Lerp(previous, new Vector2(body.x, body.y), t);
            bool sliding = body.slide > 0f;
            bool airborne = body.y > 0.08f;
            bool spiking = !sliding && side == 0 && body.spikeWindow > 0f;
            bool walking = !sliding && !airborne && !spiking;
            int frame = (animateWhileIdle || Mathf.Abs(body.vx) > 0.3f)
                ? (int)(animationTime * Mathf.Max(1f, walkFramesPerSecond)) % frames.Length : 0;
            if (airborne) frame = body.vy > 0f ? 2 : 3;
            Sprite slideSprite = side == 0 ? playerSlideSprite : cpuSlideSprite;
            float yOffset = 0f;
            squash = Mathf.Max(0f, squash - dt * 7f);
            if (sliding && slideSprite != null)
            {
                renderer.sprite = slideSprite;
                // 두 슬라이드 원본 모두 오른쪽을 향하므로 이동 방향에 맞춰 반전합니다.
                renderer.flipX = body.facing < 0f;
                renderer.transform.localScale = Vector3.one * Mathf.Clamp(slideSpriteWidth, 0.6f, 2.4f);
                yOffset = slideVisualYOffset;
            }
            else
            {
                renderer.sprite = spiking ? leftSpike[(int)(animationTime * 15f) % leftSpike.Length] : frames[frame];
                renderer.flipX = side == 0;
                renderer.transform.localScale = sliding ? new Vector3(1.25f, 0.60f, 1f)
                    : new Vector3(1f + squash * 0.15f, 1f - squash * 0.12f, 1f);
                if (walking && walkFrameYPixels != null && walkFrameYPixels.Length > frame)
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

        private void EmitHit()
        {
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
            playerNameLabel = Label("Name", l.transform, "나", new Vector2(150f, 54f), new Vector2(-32f, 0f), 27, orange).gameObject;
            leftScore = Label("Score", l.transform, "0", new Vector2(76f, 64f), new Vector2(84f, 0f), 48, Color.white);
            Image r = Panel("CPUScoreCard", parent, new Vector2(264f, 74f), new Vector2(430f, 304f), dark);
            rightScore = Label("Score", r.transform, "0", new Vector2(76f, 64f), new Vector2(-84f, 0f), 48, Color.white);
            cpuNameLabel = Label("Name", r.transform, "도플갱어", new Vector2(170f, 54f), new Vector2(32f, 0f), 27, purple).gameObject;
            Image target = Panel("MatchTarget", parent, new Vector2(220f, 36f), new Vector2(0f, 320f), dark);
            Label("Text", target.transform, winningScore + "점 선취", new Vector2(220f, 36f), Vector2.zero, 23, Color.white);
            UIButton("PauseButton", parent, "일시정지 · ESC", new Vector2(190f, 36f), new Vector2(0f, 271f), () => SetPaused(true));
            Image footer = Panel("Controls", parent, new Vector2(1180f, 34f), new Vector2(0f, -332f), dark);
            controlsPanel = footer.gameObject;
            Label("Hint", footer.transform, "A / D 이동    SPACE 점프    J 슬라이드    K 스파이크    W+K 높게 / S+K 아래로", new Vector2(1150f, 34f), Vector2.zero, 20, Color.white);
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
            if (ballVisual != null) ballVisual.transform.localScale = Vector3.one * (diameter / 0.64f);
            if (controlsPanel != null) controlsPanel.SetActive(showControlHints);
            if (playerNameLabel != null) playerNameLabel.SetActive(showPlayerNames);
            if (cpuNameLabel != null) cpuNameLabel.SetActive(showPlayerNames);
            if (leftScore != null && rightScore != null)
            {
                leftScore.rectTransform.anchoredPosition = new Vector2(showPlayerNames ? 84f : 0f, 0f);
                rightScore.rectTransform.anchoredPosition = new Vector2(showPlayerNames ? -84f : 0f, 0f);
                Vector2 size = new Vector2(showPlayerNames ? 76f : 240f, 64f);
                leftScore.rectTransform.sizeDelta = rightScore.rectTransform.sizeDelta = size;
                leftScore.fontSize = rightScore.fontSize = Mathf.Clamp(scoreFontSize, 24, 72);
            }
        }

        private void UpdateScore() { leftScore.text = game.LeftScore.ToString(); rightScore.text = game.RightScore.ToString(); }

        private void SetPaused(bool value)
        {
            if (game.Stage == VolleySimulation.Finished || leaving) return;
            paused = value; ClearInput(); modal.SetActive(value);
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
            modalTitle.text = game.Winner == 0 ? "승리!" : "다시 도전!";
            modalTitle.color = game.Winner == 0 ? orange : purple;
            modalDetail.text = "나  " + game.LeftScore + "  :  " + game.RightScore + "  도플갱어";
            primaryLabel.text = "한 판 더";
            EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
        }

        private void PrimaryAction()
        {
            if (game.Stage == VolleySimulation.Finished)
            {
                game.Restart(); seenHit = 0; seenPoint = 0; previousStage = game.Stage;
                paused = false; modal.SetActive(false); ClearInput(); ResetVisualHistory(); UpdateScore();
                EventSystem.current.SetSelectedGameObject(null);
            }
            else SetPaused(false);
        }

        private void ReturnToMenu()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(menuSceneName)) { Debug.LogError("MainMenu 씬을 Build Settings에 등록하세요."); return; }
            leaving = true; Time.timeScale = 1f; SceneManager.LoadSceneAsync(menuSceneName);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && game != null && game.Stage == VolleySimulation.Rally) SetPaused(true);
        }

        private void OnDestroy()
        {
            Time.fixedDeltaTime = previousFixedStep;
            foreach (Sprite sprite in createdSprites) if (sprite != null) Destroy(sprite);
            if (circleTexture != null) Destroy(circleTexture);
        }
    }
}
