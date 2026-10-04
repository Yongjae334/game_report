using UnityEngine;
using UnityEngine.UI;

namespace DumbFrog.MenuMotion
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuEmberGraphic : MaskableGraphic
    {
        private struct Ember
        {
            public float age, lifetime, originX, sway, phase, size;
            public Color tint;
            public bool tail;
        }

        private Ember[] particles;
        private readonly System.Random random = new System.Random(14019);
        private float riseSpeed = 0.8f;
        private float opacity = 0.7f;
        private float fadeIn;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Configure(int count, float speed, float alpha)
        {
            count = Mathf.Clamp(count, 0, 160);
            riseSpeed = Mathf.Clamp(speed, 0.1f, 2f);
            opacity = Mathf.Clamp01(alpha);
            if (particles == null || particles.Length != count)
            {
                particles = new Ember[count];
                for (int i = 0; i < count; i++)
                {
                    particles[i] = Spawn(i);
                    particles[i].age = Next(0f, particles[i].lifetime);
                }
            }
            SetVerticesDirty();
        }

        private float Next(float min, float max) { return Mathf.Lerp(min, max, (float)random.NextDouble()); }

        private Ember Spawn(int index)
        {
            bool warm = (index & 1) == 0;
            return new Ember
            {
                age = 0f,
                lifetime = Next(12f, 24f),
                originX = warm ? Next(0.035f, 0.43f) : Next(0.57f, 0.965f),
                sway = Next(0.003f, 0.018f),
                phase = Next(0f, Mathf.PI * 2f),
                size = Next(1.5f, 3.6f),
                tint = warm ? Color.Lerp(new Color(1f, 0.32f, 0.035f), new Color(1f, 0.88f, 0.36f), Next(0f, 1f)) :
                    Color.Lerp(new Color(0.56f, 0.15f, 1f), new Color(1f, 0.38f, 0.94f), Next(0f, 1f)),
                tail = index % 6 == 0
            };
        }

        private void Update()
        {
            if (particles == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            fadeIn = Mathf.Min(1f, fadeIn + dt / 0.8f);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].age += dt * riseSpeed;
                if (particles[i].age >= particles[i].lifetime) particles[i] = Spawn(i);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (particles == null || opacity <= 0f) return;
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            float scale = Mathf.Clamp(r.width / 1280f, 0.5f, 2f);
            foreach (Ember p in particles)
            {
                float progress = p.age / p.lifetime;
                float lifeAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.12f)) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.65f) / 0.35f)));
                float x = r.xMin + r.width * (p.originX + Mathf.Sin(p.age * 0.45f + p.phase) * p.sway);
                float y = r.yMin + r.height * (progress * 1.08f - 0.04f);
                float size = p.size * scale;
                float alpha = opacity * fadeIn * lifeAlpha;
                Color c = p.tint;
                c.a = alpha * 0.1f;
                Quad(vh, x, y, size * 4f, size * 4f, c);
                c.a = alpha * 0.22f;
                Quad(vh, x, y, size * 2f, size * 2f, c);
                if (p.tail)
                {
                    c.a = alpha * 0.2f;
                    Quad(vh, x, y - size * 2f, size * 0.65f, size * 4f, c);
                }
                c.a = alpha;
                Quad(vh, x, y, size, size, c);
            }
        }

        private static void Quad(VertexHelper vh, float x, float y, float width, float height, Color tint)
        {
            int start = vh.currentVertCount;
            float hw = width * 0.5f, hh = height * 0.5f;
            Color32 c = tint;
            vh.AddVert(new Vector3(x - hw, y - hh, 0f), c, Vector2.zero);
            vh.AddVert(new Vector3(x - hw, y + hh, 0f), c, Vector2.up);
            vh.AddVert(new Vector3(x + hw, y + hh, 0f), c, Vector2.one);
            vh.AddVert(new Vector3(x + hw, y - hh, 0f), c, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
