using System;

namespace DumbFrog.Arcade
{
    // Unity 물리/프레임 속도에 의존하지 않는 경기 계산. 좌표 y=0이 바닥입니다.
    public sealed class VolleyBody
    {
        public float x = 0f;
        public float y = 0f;
        public float vx = 0f;
        public float vy = 0f;
        public float slide = 0f;
        public float slideCooldown = 0f;
        public float spikeWindow = 0f;
        public float spikeCooldown = 0f;
        public float jumpBuffer = 0f;
        public float touchLock = 0f;
        public float facing = 1f;
        public int aim = 0;
    }

    public sealed class VolleyInput
    {
        public float move = 0f;
        public bool jump = false;
        public bool slide = false;
        public bool spike = false;
        public int aim = 0;
    }

    public sealed class VolleySimulation
    {
        public const float HalfCourt = 9.5f;
        public const float Ceiling = 9.0f;
        public const float NetHeight = 2.35f;
        public const float NetHalfWidth = 0.12f;
        public const float PlayerRadius = 0.7f;
        public float BallRadius = 0.32f;
        public const float BallGravity = 12.8f;
        public const float PlayerGravity = 29f;
        public const float RunSpeed = 7.6f;
        public const float SlideTime = 0.30f;
        public const float SpikeWindow = 0.20f;
        public const float SpikeCooldown = 0.32f;
        public const int Ready = 0;
        public const int Rally = 1;
        public const int Point = 2;
        public const int Finished = 3;
        public VolleyBody Left = new VolleyBody();
        public VolleyBody Right = new VolleyBody();
        public VolleyBody Ball = new VolleyBody();
        public VolleyInput CpuInput = new VolleyInput();
        public int Stage = 0;
        public int LeftScore = 0;
        public int RightScore = 0;
        public int WinningScore = 11;
        public int Server = 0;
        public int Winner = -1;
        public int LastPoint = -1;
        public int HitSerial = 0;
        public int PointSerial = 0;
        public int LastHitSide = 0;
        public bool LastHitWasSpike = false;
        public float StageTime = 0f;
        public float SpikeGlow = 0f;
        public float CpuReaction = 0.14f;
        public float CpuSpeed = 0.94f;
        // CPU 실력(0~1): 스파이크를 노리는 빈도와 점프 타이밍·위치 정확도
        public float CpuSkill = 0.55f;
        // true면 오른쪽도 사람(2P)이 조작합니다. CPU 속도 보정/스파이크 제한을 적용하지 않습니다.
        public bool RightIsHuman = false;
        public float Clock = 0f;
        // 로그에 쓰는 이름(왼쪽, 오른쪽)
        public string[] SideNames = { "플레이어", "CPU" };
        // 테스트 로그 출력: (TC 번호, 내용, 같은 TC 최소 간격)
        public Action<string, string, float> OnLog;
        private readonly float[] slideStart = new float[2];
        private readonly int[] lastMove = new int[2];
        private readonly int[] edgeState = new int[2];
        private readonly bool[] slideRejected = new bool[2];
        private readonly bool[] stopping = new bool[2];
        private float lastCpuTarget = 999f;
        // CPU 점프 예정 시각(Clock 기준, 음수면 계획 없음)과 예측용 가상 공
        private float cpuJumpAt = -1f;
        private readonly VolleyBody forecast = new VolleyBody();
        private readonly Random cpuRandom = new Random();
        private int cpuDecisionSerial = -1;
        private bool cpuWantsSpike = true;
        private float cpuTimingError, cpuPositionError;
        private static readonly int[] aimDeepFirst = { 0, -1, 1 };
        private static readonly int[] aimShortFirst = { -1, 0, 1 };
        private float cpuClock = 0f;
        private float cpuTarget = 5f;
        private float cpuJumpWait = 0f;
        private float cpuNoise = 0f;

        public VolleySimulation() { Restart(); }

        public void Restart()
        {
            LeftScore = 0; RightScore = 0; Server = 0; Winner = -1; LastPoint = -1;
            HitSerial = 0; PointSerial = 0; LastHitWasSpike = false;
            ResetServe(); StageTime = 1.5f;
        }

        private void ResetBody(VolleyBody p, float x, float facing)
        {
            p.x = x; p.y = 0f; p.vx = 0f; p.vy = 0f;
            p.slide = 0f; p.slideCooldown = 0f; p.spikeWindow = 0f;
            p.spikeCooldown = 0f; p.jumpBuffer = 0f; p.touchLock = 0f;
            p.facing = facing; p.aim = 0;
        }

        private void ResetServe()
        {
            ResetBody(Left, -5.6f, 1f); ResetBody(Right, 5.6f, -1f);
            ResetBody(Ball, Server == 0 ? -5.6f : 5.6f, 1f);
            Ball.y = 5.2f; SpikeGlow = 0f;
            Stage = Ready; StageTime = 0.9f;
            cpuClock = 0f; cpuTarget = 5.6f; cpuJumpWait = 0.3f; lastCpuTarget = 999f; cpuJumpAt = -1f;
            Log("TC-089", "캐릭터와 공이 서브 위치로 초기화");
            Log("TC-090", "서브: " + Who(Server));
        }

        private void Log(string tc, string message, float minInterval = 0f)
        {
            if (OnLog != null) OnLog(tc, message, minInterval);
        }

        private bool IsCpu(int side) { return side == 1 && !RightIsHuman; }
        private string Who(int side) { return SideNames[side]; }
        private static string F1(float v) { return v.ToString("0.0"); }
        private static string F2(float v) { return v.ToString("0.00"); }
        private static string Dir(int move) { return move < 0 ? "왼쪽" : "오른쪽"; }
        private static string AimName(int aim) { return aim > 0 ? "높은 스파이크" : aim < 0 ? "아래쪽 스파이크" : "기본 스파이크"; }

        public bool Grounded(VolleyBody p) { return p.y <= 0.0001f; }
        public float BodyY(VolleyBody p) { return p.y + (p.slide > 0f ? 0.40f : 0.72f); }
        public float BodyRadius(VolleyBody p) { return p.slide > 0f ? 0.83f : PlayerRadius; }

        public void Step(VolleyInput leftInput, VolleyInput rightInput, float dt)
        {
            if (dt <= 0f || Stage == Finished) return;
            dt = Min(dt, 0.02f);
            Clock += dt;
            SpikeGlow = Max(0f, SpikeGlow - dt);
            if (Stage != Rally)
            {
                StageTime -= dt;
                if (StageTime <= 0f)
                {
                    if (Stage == Point) ResetServe();
                    else
                    {
                        Stage = Rally; StageTime = 0f;
                        Log("TC-051", "랠리 시작 → 공에 중력(" + F1(BallGravity) + ") 적용, 낙하 시작");
                    }
                }
                return;
            }
            BufferInput(Left, leftInput, 0); BufferInput(Right, rightInput, 1);
            // 高速球도 네트/몸을 관통하지 않도록 120Hz 틱을 다시 두 번 나눕니다.
            float h = dt * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                MovePlayer(Left, leftInput, 0, h);
                MovePlayer(Right, rightInput, 1, h);
                Ball.vy -= BallGravity * h;
                Ball.x += Ball.vx * h; Ball.y += Ball.vy * h;
                ResolveCourt(Ball);
                ResolvePlayer(Left, 0); ResolvePlayer(Right, 1);
                // 몸 충돌 뒤 네트/벽 안쪽으로 밀려난 경우에도 즉시 분리합니다.
                ResolveCourt(Ball);
                if (Ball.y <= BallRadius)
                {
                    Score(Ball.x < 0f ? 1 : 0);
                    break;
                }
            }
        }

        private void BufferInput(VolleyBody p, VolleyInput input, int side)
        {
            TrackMove(input, side);
            if (input.jump)
            {
                if (p.slide > 0f) Log("TC-038", Who(side) + " 슬라이딩 중 점프 입력 → 즉시 점프하지 않음");
                else if (!Grounded(p)) Log("TC-026", Who(side) + " 공중 점프 입력 → 추가 점프 없음");
                p.jumpBuffer = 0.12f;
            }
            if (input.spike)
            {
                if (p.spikeCooldown > 0f)
                {
                    if (!IsCpu(side)) Log("TC-047", Who(side) + " 스파이크 쿨타임 중(" + F2(p.spikeCooldown) + "초 남음) → 무시");
                }
                else
                {
                    p.spikeWindow = SpikeWindow; p.spikeCooldown = SpikeCooldown; p.aim = input.aim;
                    if (IsCpu(side)) { }
                    else if (Grounded(p)) Log("TC-044", Who(side) + " 지면에서 스파이크 입력 → 타격 판정 없음");
                    else Log("TC-046", Who(side) + " 스파이크 판정 시작 (0.20초, " + AimName(input.aim) + ")");
                }
            }
            if (input.slide)
            {
                if (!Grounded(p)) Log("TC-037", Who(side) + " 공중 슬라이딩 입력 → 시작 안 함");
                else if (p.slideCooldown > 0f)
                {
                    Log(IsCpu(side) ? "TC-078" : "TC-035", Who(side) + " 슬라이딩 쿨타임 중(" + F2(p.slideCooldown) + "초 남음) → 재발동 안 함");
                    slideRejected[side] = true;
                }
                else
                {
                    p.slide = SlideTime; p.slideCooldown = 0.72f;
                    if (Abs(input.move) > 0.1f) p.facing = Sign(input.move);
                    slideStart[side] = Clock;
                    string tc = IsCpu(side) ? "TC-077" : Abs(input.move) <= 0.1f ? "TC-031" : p.facing < 0f ? "TC-032" : "TC-033";
                    Log(tc, Who(side) + " 슬라이딩 시작 → " + Dir((int)p.facing));
                    if (slideRejected[side] && !IsCpu(side)) Log("TC-036", Who(side) + " 쿨타임 종료 후 슬라이딩 다시 실행");
                    slideRejected[side] = false;
                }
            }
        }

        // 이동 입력이 바뀌는 순간만 기록합니다(시작 / 방향 전환 / 정지).
        private void TrackMove(VolleyInput input, int side)
        {
            int move = Abs(input.move) > 0.1f ? (int)Sign(input.move) : 0;
            int last = lastMove[side];
            if (move == last) return;
            lastMove[side] = move;
            if (IsCpu(side))
            {
                if (move == 0) Log("TC-069", "CPU 목표 위치 근처 → 이동 정지", 1.5f);
                else Log(move < 0 ? "TC-067" : "TC-068", "CPU " + Dir(move) + " 이동", 1.5f);
                return;
            }
            if (move == 0) Log("TC-020", Who(side) + " 이동키 해제 → 감속 후 정지");
            else if (last != 0) Log("TC-021", Who(side) + " 방향 전환 → " + Dir(move));
            else Log(side == 0 ? (move < 0 ? "TC-016" : "TC-017") : (move < 0 ? "TC-018" : "TC-019"), Who(side) + " " + Dir(move) + " 이동 시작");
        }

        private void MovePlayer(VolleyBody p, VolleyInput input, int side, float dt)
        {
            p.slideCooldown = Max(0f, p.slideCooldown - dt);
            p.spikeCooldown = Max(0f, p.spikeCooldown - dt);
            float window = p.spikeWindow;
            p.spikeWindow = Max(0f, p.spikeWindow - dt);
            if (window > 0f && p.spikeWindow <= 0f && !IsCpu(side) && !Grounded(p))
                Log("TC-045", Who(side) + " 스파이크 판정 시간 종료 → 공이 범위 밖이라 타격 없음");
            p.touchLock = Max(0f, p.touchLock - dt);
            p.jumpBuffer = Max(0f, p.jumpBuffer - dt);
            float move = Clamp(input.move, -1f, 1f);
            if (p.slide > 0f)
            {
                p.slide = Max(0f, p.slide - dt); p.vx = p.facing * 12.5f;
                if (p.slide <= 0f && !IsCpu(side)) Log("TC-034", Who(side) + " 슬라이딩 종료 (지속 " + F2(Clock - slideStart[side]) + "초)");
            }
            else
            {
                float speed = side == 0 || RightIsHuman ? RunSpeed : RunSpeed * CpuSpeed;
                float before = p.vx;
                p.vx = Approach(p.vx, move * speed, (Abs(move) > 0.1f ? 100f : 115f) * dt);
                if (Abs(move) > 0.1f) p.facing = Sign(move);
                // 이동키를 뗀 뒤 완전히 멈춘 순간을 기록합니다(이후 불필요한 수평 이동 없음).
                if (Abs(move) > 0.1f) stopping[side] = false;
                else if (before != 0f && p.vx == 0f) stopping[side] = true;
                if (stopping[side] && !IsCpu(side))
                {
                    stopping[side] = false;
                    Log("TC-030", Who(side) + " 정지 완료 → 입력 없는 동안 수평 이동 없음");
                }
            }
            if (p.jumpBuffer > 0f && Grounded(p) && p.slide <= 0f)
            {
                p.vy = 12.8f; p.jumpBuffer = 0f;
                bool running = Abs(p.vx) > 0.5f;
                string tc = IsCpu(side) ? "TC-071" : side == 1 ? "TC-024" : running ? "TC-025" : "TC-023";
                Log(tc, Who(side) + (running ? " 이동 중 점프" : " 점프"));
            }
            bool wasAirborne = p.y > 0f;
            p.vy -= PlayerGravity * dt;
            p.x += p.vx * dt; p.y += p.vy * dt;
            if (p.y < 0f)
            {
                p.y = 0f; p.vy = 0f;
                if (wasAirborne && !IsCpu(side)) Log("TC-027", Who(side) + " 착지");
            }
            float radius = BodyRadius(p);
            float min = side == 0 ? -HalfCourt + radius : NetHalfWidth + radius;
            float max = side == 0 ? -NetHalfWidth - radius : HalfCourt - radius;
            float bounded = Clamp(p.x, min, max);
            if (bounded != p.x) p.vx = 0f;
            // 경계에 막히기 시작한 순간만 기록합니다. -1: 왼쪽 경계, 1: 오른쪽 경계
            int edge = bounded > p.x ? -1 : bounded < p.x ? 1 : 0;
            if (edge != 0 && edge != edgeState[side])
            {
                bool net = side == 0 ? edge > 0 : edge < 0;
                string tc = IsCpu(side) ? "TC-079" : p.slide > 0f ? "TC-040" : net ? "TC-029" : "TC-028";
                Log(tc, Who(side) + (net ? " 네트 앞 경계에서 멈춤" : " 벽 경계에서 멈춤") + (p.slide > 0f ? " (슬라이딩 중)" : ""));
            }
            edgeState[side] = edge;
            p.x = bounded;
        }

        private void ResolveCourt(VolleyBody b)
        {
            // 착지 예측용 가상 공(PredictLandingX)은 기록하지 않습니다.
            bool real = b == Ball;
            float wall = HalfCourt - BallRadius;
            if (b.x < -wall)
            {
                b.x = -wall; b.vx = Abs(b.vx) * 0.94f;
                if (real) Log("TC-054", "공 좌측 벽 충돌 → 오른쪽으로 반사");
            }
            if (b.x > wall)
            {
                b.x = wall; b.vx = -Abs(b.vx) * 0.94f;
                if (real) Log("TC-055", "공 우측 벽 충돌 → 왼쪽으로 반사");
            }
            if (b.y > Ceiling - BallRadius)
            {
                b.y = Ceiling - BallRadius; b.vy = -Abs(b.vy) * 0.92f;
                if (real) Log("TC-056", "공 천장 충돌 → 아래로 반사");
            }
            // 가장 가까운 네트 표면점과 원을 비교해 상단 모서리도 자연스럽게 튕깁니다.
            float nearestX = Clamp(b.x, -NetHalfWidth, NetHalfWidth);
            float nearestY = Clamp(b.y, 0f, NetHeight);
            float dx = b.x - nearestX, dy = b.y - nearestY;
            float distance = Sqrt(dx * dx + dy * dy);
            if (distance < BallRadius)
            {
                float nx = 0f, ny = 0f;
                if (distance > 0.0001f) { nx = dx / distance; ny = dy / distance; }
                else if (b.y >= NetHeight - 0.08f) { ny = 1f; }
                else { nx = b.x < 0f ? -1f : 1f; }
                b.x = nearestX + nx * (BallRadius + 0.002f);
                b.y = nearestY + ny * (BallRadius + 0.002f);
                float incoming = b.vx * nx + b.vy * ny;
                if (incoming < 0f)
                {
                    if (real) LogNet(nx, ny, Sqrt(b.vx * b.vx + b.vy * b.vy));
                    b.vx -= incoming * 1.82f * nx;
                    b.vy -= incoming * 1.82f * ny;
                    if (ny > 0.5f && Abs(b.vx) < 0.45f) b.vx = b.x < 0f ? -0.65f : 0.65f;
                }
            }
        }

        private void LogNet(float nx, float ny, float speed)
        {
            string part = ny > 0.85f ? "상단" : ny > 0.3f ? "모서리" : nx < 0f ? "좌측" : "우측";
            string tc = speed > 15f ? "TC-061" : ny > 0.85f ? "TC-059" : ny > 0.3f ? "TC-060" : nx < 0f ? "TC-057" : "TC-058";
            Log(tc, "공 네트 " + part + " 충돌 (속도 " + F1(speed) + ") → 관통 없이 반사");
        }

        private void ResolvePlayer(VolleyBody p, int side)
        {
            float dx = Ball.x - p.x, dy = Ball.y - BodyY(p);
            float distance = Sqrt(dx * dx + dy * dy);
            float reach = BodyRadius(p) + BallRadius;
            if (p.spikeWindow > 0f && p.y > 0.12f && distance < reach + 0.23f && dy > -0.25f
                && (side == 0 || RightIsHuman || CanCPUSpike()))
            {
                Strike(p, side); return;
            }
            if (distance >= reach) return;
            float nx = distance > 0.0001f ? dx / distance : 0f;
            float ny = distance > 0.0001f ? dy / distance : 1f;
            Ball.x = p.x + nx * (reach + 0.003f);
            Ball.y = BodyY(p) + ny * (reach + 0.003f);
            float incoming = (Ball.vx - p.vx) * nx + (Ball.vy - p.vy) * ny;
            if (p.touchLock > 0f || incoming > 0.05f)
            {
                if (p.touchLock > 0f) Log("TC-063", "공이 " + Who(side) + " 몸에 연속 접촉 → 재가속 없이 밀어냄", 0.6f);
                return;
            }
            float towardOpponent = side == 0 ? 1f : -1f;
            Ball.vx = Clamp(nx * 8.8f + p.vx * 0.42f, -11f, 11f);
            if (Abs(Ball.vx) < 1.7f) Ball.vx = towardOpponent * 1.7f;
            Ball.vy = Clamp(9f + Max(0f, ny) * 2.7f + Max(0f, p.vy) * 0.13f, 9f, 13f);
            if (p.slide > 0f)
            {
                Ball.vx = towardOpponent * 7.2f + nx * 2.0f; Ball.vy = 11.4f;
            }
            p.touchLock = 0.10f; SpikeGlow = 0f;
            LastHitSide = side; LastHitWasSpike = false; HitSerial++;
            string tc = p.slide > 0f ? (side == 0 ? "TC-039" : "TC-064") : side == 0 ? "TC-052" : "TC-053";
            Log(tc, Who(side) + (p.slide > 0f ? " 슬라이딩 수비로 공 받음" : " 몸통에 공 충돌") +
                " → 반사 (vx " + F1(Ball.vx) + ", vy " + F1(Ball.vy) + ")");
        }

        // CPU는 왼쪽(상대 코트)을 바라봅니다. 등 뒤/몸 아래 공을 잡아채지 않습니다.
        private bool CanCPUSpike()
        {
            float dx = Ball.x - Right.x, dy = Ball.y - BodyY(Right);
            float reach = BodyRadius(Right) + BallRadius + 0.2f;
            return Right.y > 0.12f && Right.slide <= 0f && Right.touchLock <= 0f
                && dx <= -0.12f && dy >= 0.05f && dx * dx + dy * dy <= reach * reach;
        }

        private void Strike(VolleyBody p, int side)
        {
            float direction = side == 0 ? 1f : -1f;
            float dx = Ball.x - p.x, dy = Ball.y - BodyY(p);
            float length = Max(0.001f, Sqrt(dx * dx + dy * dy));
            float reach = BodyRadius(p) + BallRadius + 0.03f;
            Ball.x = p.x + dx / length * Max(length, reach);
            Ball.y = BodyY(p) + dy / length * Max(length, reach);
            if (p.aim > 0) { Ball.vx = direction * 12.8f; Ball.vy = 10.3f; }
            else if (p.aim < 0) { Ball.vx = direction * 18.5f; Ball.vy = -7f; }
            else { Ball.vx = direction * 19f; Ball.vy = Ball.y < NetHeight + 0.55f ? 6.5f : -1.6f; }
            p.spikeWindow = 0f; p.touchLock = 0.18f;
            SpikeGlow = 0.7f; LastHitSide = side; LastHitWasSpike = true; HitSerial++;
            string tc = IsCpu(side) ? "TC-074" : p.aim > 0 ? "TC-042" : p.aim < 0 ? "TC-043" : "TC-041";
            Log(tc, Who(side) + " " + AimName(p.aim) + " 성공 → 상대 코트 방향 (vx " + F1(Ball.vx) + ", vy " + F1(Ball.vy) + ")");
        }

        private void Score(int side)
        {
            if (Stage != Rally) return;
            Log("TC-065", "공 바닥 도달 (" + (Ball.x < 0f ? "왼쪽" : "오른쪽") + " 코트, x " + F1(Ball.x) + ")");
            if (side == 0) LeftScore++; else RightScore++;
            LastPoint = side; Server = side; PointSerial++;
            Log(side == 0 ? "TC-082" : "TC-083", Who(side) + " 득점 → " + LeftScore + " : " + RightScore);
            Ball.y = BallRadius; Ball.vx = 0f; Ball.vy = 0f;
            Left.vx = 0f; Right.vx = 0f; SpikeGlow = 0f;
            if (LeftScore >= WinningScore || RightScore >= WinningScore)
            {
                Winner = side; Stage = Finished; StageTime = 0f;
                Log(side == 0 ? "TC-092" : "TC-093", Who(side) + " " + WinningScore + "점 선취 승리 → 결과 화면");
            }
            else
            {
                Stage = Point; StageTime = 1.05f;
                if ((side == 0 ? LeftScore : RightScore) == WinningScore - 1)
                    Log("TC-091", Who(side) + " " + (WinningScore - 1) + "점 → 경기 계속");
                Log("TC-086", "득점 후 랠리 중단 → 다음 서브 대기");
            }
        }

        public VolleyInput ThinkCPU(float dt)
        {
            CpuInput.move = 0f; CpuInput.jump = false; CpuInput.slide = false; CpuInput.spike = false; CpuInput.aim = 0;
            if (Stage != Rally) return CpuInput;
            cpuClock -= dt; cpuJumpWait = Max(0f, cpuJumpWait - dt);
            // 반응 간격마다 공 궤적을 다시 읽고 계획(설 위치, 점프 시각)을 세웁니다.
            if (cpuClock <= 0f)
            {
                cpuClock = Max(0.06f, CpuReaction); cpuNoise += 1f;
                PlanCPU();
            }
            float error = cpuTarget - Right.x;
            if (Abs(error) > 0.1f) CpuInput.move = Clamp(error / 0.35f, -1f, 1f);
            if (cpuJumpAt >= 0f && Clock >= cpuJumpAt)
            {
                if (!Grounded(Right) || Right.slide > 0f) { }
                else if (cpuJumpWait <= 0f) { CpuInput.jump = true; cpuJumpWait = 0.6f; }
                else Log("TC-072", "CPU 점프 대기시간 중(" + F2(cpuJumpWait) + "초) → 연속 점프 안 함", 0.5f);
                cpuJumpAt = -1f;
            }
            float dx = Ball.x - Right.x, dy = Ball.y - BodyY(Right);
            int aim;
            if (Right.spikeCooldown <= 0f && CanCPUSpike() && ChooseSpikeAim(out aim))
            {
                CpuInput.spike = true; CpuInput.aim = aim;
                Log("TC-073", "CPU 공중 스파이크 시도 (" + AimName(aim) + ")");
            }
            else if (Right.y > 0.12f && Right.slide <= 0f && Right.spikeCooldown <= 0f)
            {
                float reach = BodyRadius(Right) + BallRadius + 0.2f;
                if (dx * dx + dy * dy <= reach * reach)
                {
                    if (dx > -0.12f) Log("TC-075", "공이 CPU 등 뒤에 있음 → 스파이크 안 함", 0.5f);
                    else if (dy < 0.05f) Log("TC-076", "공이 CPU 몸 중심보다 아래 → 스파이크 안 함", 0.5f);
                }
            }
            if (Grounded(Right) && cpuJumpAt < 0f && Ball.x > 0.3f && Ball.y < 1.4f && Ball.vy < 0f &&
                Abs(dx) > 1.1f && Abs(dx) < 3.1f)
            {
                if (Right.slideCooldown <= 0f) { CpuInput.slide = true; CpuInput.move = Sign(dx); }
                else Log("TC-078", "CPU 슬라이딩 쿨타임 중 → 반복하지 않음", 0.5f);
            }
            return CpuInput;
        }

        // 1) 점프해서 공이 몸 앞·위에 오는 순간이 있으면: 그 자리로 가서 정확한 시각에 점프(스파이크 계획)
        // 2) 없으면: 공이 몸 앞쪽 위를 맞도록 공보다 약간 뒤(오른쪽)에 서서 상대 코트로 받아 넘김
        private void PlanCPU()
        {
            cpuJumpAt = -1f;
            if (Ball.x < -0.6f && Ball.vx <= 0f)
            {
                cpuTarget = 4.2f;
                LogTarget("TC-066", "공이 상대 코트로 감 → CPU 기본 위치(x " + F1(cpuTarget) + ")로 복귀");
                return;
            }
            float reach = PlayerRadius + BallRadius;
            float minX = NetHalfWidth + PlayerRadius, maxX = HalfCourt - PlayerRadius;
            float speed = RunSpeed * CpuSpeed;
            float noise = Sin(cpuNoise * 2.17f);
            float skill = Clamp(CpuSkill, 0f, 1f);
            // 공을 누가 새로 칠 때마다 이번 공을 스파이크로 노릴지, 얼마나 정확할지 한 번 정합니다.
            if (cpuDecisionSerial != HitSerial)
            {
                cpuDecisionSerial = HitSerial;
                cpuWantsSpike = cpuRandom.NextDouble() < 0.3 + 0.65 * skill;
                cpuTimingError = (float)(cpuRandom.NextDouble() * 2.0 - 1.0) * (1f - skill) * 0.07f;
                cpuPositionError = (float)(cpuRandom.NextDouble() * 2.0 - 1.0) * (1f - skill) * 0.35f;
            }
            float bumpHeight = 0.72f + reach * 0.8f, bumpX = float.NaN;
            bool canJump = cpuWantsSpike && Grounded(Right) && Right.slide <= 0f;
            float h = 1f / 120f;
            forecast.x = Ball.x; forecast.y = Ball.y; forecast.vx = Ball.vx; forecast.vy = Ball.vy;
            for (int i = 1; i <= 300; i++)
            {
                forecast.vy -= BallGravity * h;
                forecast.x += forecast.vx * h; forecast.y += forecast.vy * h;
                ResolveCourt(forecast);
                if (forecast.y <= BallRadius) break;
                if (forecast.x < 0.45f) continue;
                float t = i * h;
                if (canJump)
                {
                    // 공이 몸 앞(왼쪽)에 오도록 공보다 조금 오른쪽에 섭니다.
                    float bodyX = forecast.x + 0.45f + cpuPositionError;
                    if (bodyX > minX && bodyX < maxX)
                    {
                        // 점프 후 τ초 뒤 몸 중심 높이: 0.72 + 12.8τ - 14.5τ²
                        for (float tau = 0.1f; tau <= 0.44f; tau += 0.02f)
                        {
                            float takeoff = t - tau;
                            if (takeoff < 0.03f) break;
                            float above = forecast.y - (0.72f + 12.8f * tau - 14.5f * tau * tau);
                            if (above < 0.2f || above > reach * 0.85f) continue;
                            if (Abs(bodyX - Right.x) > speed * takeoff + 0.1f) continue;
                            cpuTarget = bodyX;
                            cpuJumpAt = Clock + takeoff + cpuTimingError;
                            LogTarget("TC-070", "CPU 스파이크 계획 → x " + F1(bodyX) + "에서 " + F2(takeoff) + "초 뒤 점프");
                            return;
                        }
                    }
                }
                if (float.IsNaN(bumpX) && forecast.vy < 0f && forecast.y <= bumpHeight) bumpX = forecast.x;
            }
            if (float.IsNaN(bumpX)) bumpX = PredictLandingX(bumpHeight);
            // 공이 몸 앞쪽 위를 맞으면(nx≈-0.5) 상대 코트로 포물선을 그리며 넘어갑니다.
            // 코트 깊숙한 곳일수록 공 뒤에 더 서서 낮고 멀리 보내 네트를 넘깁니다.
            float offset = Clamp(0.45f + (bumpX - 2f) * 0.035f, 0.45f, 0.68f);
            cpuTarget = Clamp(bumpX + reach * offset + noise * 0.08f + cpuPositionError * 0.5f, minX + 0.25f, maxX);
            LogTarget("TC-070", "CPU 예상 착지 위치 계산 → 받아 넘길 위치 x " + F1(cpuTarget));
        }

        private void LogTarget(string tc, string message)
        {
            if (Abs(cpuTarget - lastCpuTarget) <= 0.5f) return;
            lastCpuTarget = cpuTarget;
            Log(tc, message, 1f);
        }

        // 상대가 뒤에 있으면 짧게(아래쪽), 앞에 있으면 깊게(기본) 노리고, 네트를 넘는 첫 방향을 고릅니다.
        private bool ChooseSpikeAim(out int aim)
        {
            bool shortFirst = Left.x < -4.6f || (Left.x <= -2.6f && Sin(cpuNoise * 1.3f) > 0f);
            foreach (int candidate in shortFirst ? aimShortFirst : aimDeepFirst)
            {
                if (SpikeClearsNet(candidate)) { aim = candidate; return true; }
            }
            aim = 0;
            return false;
        }

        private bool SpikeClearsNet(int aim)
        {
            forecast.x = Ball.x; forecast.y = Ball.y;
            if (aim > 0) { forecast.vx = -12.8f; forecast.vy = 10.3f; }
            else if (aim < 0) { forecast.vx = -18.5f; forecast.vy = -7f; }
            else { forecast.vx = -19f; forecast.vy = Ball.y < NetHeight + 0.55f ? 6.5f : -1.6f; }
            float h = 1f / 120f;
            for (int i = 0; i < 240; i++)
            {
                forecast.vy -= BallGravity * h;
                forecast.x += forecast.vx * h; forecast.y += forecast.vy * h;
                ResolveCourt(forecast);
                if (forecast.x > 0f && forecast.vx > 0f) return false;
                if (forecast.y <= BallRadius) return forecast.x < -0.3f;
            }
            return forecast.x < 0f;
        }

        public float PredictLandingX(float height)
        {
            VolleyBody forecast = new VolleyBody();
            forecast.x = Ball.x; forecast.y = Ball.y; forecast.vx = Ball.vx; forecast.vy = Ball.vy;
            float dt = 1f / 120f;
            for (int i = 0; i < 420; i++)
            {
                forecast.vy -= BallGravity * dt;
                forecast.x += forecast.vx * dt; forecast.y += forecast.vy * dt;
                ResolveCourt(forecast);
                if (forecast.y <= height && forecast.vy < 0f) return forecast.x;
            }
            return forecast.x;
        }

        private static float Min(float a, float b) { return (float)Math.Min(a, b); }
        private static float Max(float a, float b) { return (float)Math.Max(a, b); }
        private static float Abs(float a) { return (float)Math.Abs(a); }
        private static float Sqrt(float a) { return (float)Math.Sqrt(a); }
        private static float Sin(float a) { return (float)Math.Sin(a); }
        private static float Sign(float a) { return a < 0f ? -1f : 1f; }
        private static float Clamp(float v, float min, float max) { return Max(min, Min(max, v)); }
        private static float Approach(float v, float target, float delta)
        {
            return v < target ? Min(v + delta, target) : Max(v - delta, target);
        }
    }
}
