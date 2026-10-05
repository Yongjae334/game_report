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
            cpuClock = 0f; cpuTarget = 5.6f; cpuJumpWait = 0.3f;
        }

        public bool Grounded(VolleyBody p) { return p.y <= 0.0001f; }
        public float BodyY(VolleyBody p) { return p.y + (p.slide > 0f ? 0.40f : 0.72f); }
        public float BodyRadius(VolleyBody p) { return p.slide > 0f ? 0.83f : PlayerRadius; }

        public void Step(VolleyInput leftInput, VolleyInput rightInput, float dt)
        {
            if (dt <= 0f || Stage == Finished) return;
            dt = Min(dt, 0.02f);
            SpikeGlow = Max(0f, SpikeGlow - dt);
            if (Stage != Rally)
            {
                StageTime -= dt;
                if (StageTime <= 0f)
                {
                    if (Stage == Point) ResetServe();
                    else { Stage = Rally; StageTime = 0f; }
                }
                return;
            }
            BufferInput(Left, leftInput); BufferInput(Right, rightInput);
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

        private void BufferInput(VolleyBody p, VolleyInput input)
        {
            if (input.jump) p.jumpBuffer = 0.12f;
            if (input.spike && p.spikeCooldown <= 0f)
            {
                p.spikeWindow = 0.20f; p.spikeCooldown = 0.32f; p.aim = input.aim;
            }
            if (input.slide && Grounded(p) && p.slideCooldown <= 0f)
            {
                p.slide = 0.30f; p.slideCooldown = 0.72f;
                if (Abs(input.move) > 0.1f) p.facing = Sign(input.move);
            }
        }

        private void MovePlayer(VolleyBody p, VolleyInput input, int side, float dt)
        {
            p.slideCooldown = Max(0f, p.slideCooldown - dt);
            p.spikeCooldown = Max(0f, p.spikeCooldown - dt);
            p.spikeWindow = Max(0f, p.spikeWindow - dt);
            p.touchLock = Max(0f, p.touchLock - dt);
            p.jumpBuffer = Max(0f, p.jumpBuffer - dt);
            float move = Clamp(input.move, -1f, 1f);
            if (p.slide > 0f)
            {
                p.slide = Max(0f, p.slide - dt); p.vx = p.facing * 12.5f;
            }
            else
            {
                float speed = side == 0 ? 7.6f : 7.6f * CpuSpeed;
                p.vx = Approach(p.vx, move * speed, (Abs(move) > 0.1f ? 100f : 115f) * dt);
                if (Abs(move) > 0.1f) p.facing = Sign(move);
            }
            if (p.jumpBuffer > 0f && Grounded(p) && p.slide <= 0f)
            {
                p.vy = 12.8f; p.jumpBuffer = 0f;
            }
            p.vy -= PlayerGravity * dt;
            p.x += p.vx * dt; p.y += p.vy * dt;
            if (p.y < 0f) { p.y = 0f; p.vy = 0f; }
            float radius = BodyRadius(p);
            float min = side == 0 ? -HalfCourt + radius : NetHalfWidth + radius;
            float max = side == 0 ? -NetHalfWidth - radius : HalfCourt - radius;
            float bounded = Clamp(p.x, min, max);
            if (bounded != p.x) p.vx = 0f;
            p.x = bounded;
        }

        private void ResolveCourt(VolleyBody b)
        {
            float wall = HalfCourt - BallRadius;
            if (b.x < -wall) { b.x = -wall; b.vx = Abs(b.vx) * 0.94f; }
            if (b.x > wall) { b.x = wall; b.vx = -Abs(b.vx) * 0.94f; }
            if (b.y > Ceiling - BallRadius) { b.y = Ceiling - BallRadius; b.vy = -Abs(b.vy) * 0.92f; }
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
                    b.vx -= incoming * 1.82f * nx;
                    b.vy -= incoming * 1.82f * ny;
                    if (ny > 0.5f && Abs(b.vx) < 0.45f) b.vx = b.x < 0f ? -0.65f : 0.65f;
                }
            }
        }

        private void ResolvePlayer(VolleyBody p, int side)
        {
            float dx = Ball.x - p.x, dy = Ball.y - BodyY(p);
            float distance = Sqrt(dx * dx + dy * dy);
            float reach = BodyRadius(p) + BallRadius;
            if (p.spikeWindow > 0f && p.y > 0.12f && distance < reach + 0.23f && dy > -0.25f
                && (side == 0 || CanCPUSpike()))
            {
                Strike(p, side); return;
            }
            if (distance >= reach) return;
            float nx = distance > 0.0001f ? dx / distance : 0f;
            float ny = distance > 0.0001f ? dy / distance : 1f;
            Ball.x = p.x + nx * (reach + 0.003f);
            Ball.y = BodyY(p) + ny * (reach + 0.003f);
            float incoming = (Ball.vx - p.vx) * nx + (Ball.vy - p.vy) * ny;
            if (p.touchLock > 0f || incoming > 0.05f) return;
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
        }

        // CPU는 왼쪽(상대 코트)을 바라봅니다. 등 뒤/몸 아래 공을 잡아채지 않습니다.
        private bool CanCPUSpike()
        {
            float dx = Ball.x - Right.x, dy = Ball.y - BodyY(Right);
            float reach = BodyRadius(Right) + BallRadius + 0.06f;
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
        }

        private void Score(int side)
        {
            if (Stage != Rally) return;
            if (side == 0) LeftScore++; else RightScore++;
            LastPoint = side; Server = side; PointSerial++;
            Ball.y = BallRadius; Ball.vx = 0f; Ball.vy = 0f;
            Left.vx = 0f; Right.vx = 0f; SpikeGlow = 0f;
            if (LeftScore >= WinningScore || RightScore >= WinningScore)
            {
                Winner = side; Stage = Finished; StageTime = 0f;
            }
            else { Stage = Point; StageTime = 1.05f; }
        }

        public VolleyInput ThinkCPU(float dt)
        {
            CpuInput.move = 0f; CpuInput.jump = false; CpuInput.slide = false; CpuInput.spike = false; CpuInput.aim = 0;
            if (Stage != Rally) return CpuInput;
            cpuClock -= dt; cpuJumpWait = Max(0f, cpuJumpWait - dt);
            if (cpuClock <= 0f)
            {
                cpuClock = Max(0.06f, CpuReaction); cpuNoise += 1f;
                cpuTarget = PredictLandingX(1.2f) + 0.28f + Sin(cpuNoise * 2.17f) * 0.22f;
                if (Ball.x < -0.6f && Ball.vx <= 0f) cpuTarget = 4.7f;
                cpuTarget = Clamp(cpuTarget, 1.05f, HalfCourt - 0.85f);
            }
            float error = cpuTarget - Right.x;
            if (Abs(error) > 0.17f) CpuInput.move = Clamp(error / 0.45f, -1f, 1f);
            float futureX = Ball.x + Ball.vx * 0.16f;
            float futureY = Ball.y + Ball.vy * 0.16f - 0.5f * BallGravity * 0.16f * 0.16f;
            if (Grounded(Right) && cpuJumpWait <= 0f && Ball.x > 0.3f &&
                Abs(futureX - Right.x) < 1.3f && futureY > 1.9f && futureY < 4.0f && Ball.vy < 3f)
            {
                CpuInput.jump = true; cpuJumpWait = 0.85f;
            }
            float dx = Ball.x - Right.x, dy = Ball.y - BodyY(Right);
            if (Right.spikeCooldown <= 0f && CanCPUSpike())
            {
                CpuInput.spike = true;
                if (Ball.y > 4.1f && Ball.x < 2.8f) CpuInput.aim = -1;
            }
            if (Grounded(Right) && Ball.x > 0.3f && Ball.y < 1.4f && Ball.vy < 0f &&
                Abs(dx) > 1.1f && Abs(dx) < 3.1f && Right.slideCooldown <= 0f)
            {
                CpuInput.slide = true; CpuInput.move = Sign(dx);
            }
            return CpuInput;
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
