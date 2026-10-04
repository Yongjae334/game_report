using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 및 액션 수치")]
    public float moveSpeed = 7f;
    public float jumpForce = 18f;      // 💡 점프력을 12 -> 18로 확 올렸습니다!
    public float slideSpeed = 20f;
    public float slideDuration = 0.45f;

    [Header("스파이크 설정")]
    public float spikeForce = 15f;
    public float spikeRadius = 3.5f;

    [Header("플레이어 설정")]
    public bool isPlayer1 = true;
    public bool isCPU = false;

    [Header("코트 경계 제한 (X좌표)")]
    public float mapEnd = 12f;
    public float centerLine = 0.5f;

    private Rigidbody2D rb;
    private Transform ballTransform;
    private Animator animator;
    private CapsuleCollider2D bodyCollider;

    private bool isGrounded = false;
    private bool isSliding = false;
    private float slideTimer = 0f;
    private float facingDirection = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        animator = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
    }

    void Update()
    {
        float moveInput = 0f;
        bool jumpInput = false;
        bool slideInput = false;
        bool spikeInput = false;

        // --- 1. 입력 받기 ---
        if (isCPU)
        {
            if (ballTransform == null)
            {
                GameObject ball = GameObject.FindGameObjectWithTag("Ball");
                if (ball != null) ballTransform = ball.transform;
            }

            if (ballTransform != null)
            {
                Rigidbody2D ballRb = ballTransform.GetComponent<Rigidbody2D>();

                Vector2 ballVelocity = Vector2.zero;

                if (ballRb != null)
                    ballVelocity = ballRb.velocity;

                // 실제 캐릭터 판정 중심
                Vector2 cpuCenter = bodyCollider != null
                    ? (Vector2)bodyCollider.bounds.center
                    : (Vector2)transform.position;

                float targetX;

                // CPU가 오른쪽 코트에 있는지 확인
                bool cpuIsRight = transform.position.x > 0;

                // 공이 CPU 코트에 있는지 확인
                bool ballOnCpuSide;

                if (cpuIsRight)
                    ballOnCpuSide = ballTransform.position.x > centerLine;
                else
                    ballOnCpuSide = ballTransform.position.x < -centerLine;


                // 공이 CPU 쪽으로 넘어왔을 때
                if (ballOnCpuSide)
                {
                    targetX = ballTransform.position.x;

                    // 빠른 공만 살짝 예측
                    if (Mathf.Abs(ballVelocity.x) > 3f)
                    {
                        targetX += ballVelocity.x * 0.15f;
                    }
                }

                // 공이 상대편에 있을 때
                else
                {
                    // ★ 네트에 붙지 않고 수비 준비 위치에서 대기
                    targetX = cpuIsRight ? 5f : -5f;
                }


                // 자기 코트 밖으로 못 나가게 제한
                if (cpuIsRight)
                    targetX = Mathf.Clamp(targetX, centerLine, mapEnd);
                else
                    targetX = Mathf.Clamp(targetX, -mapEnd, -centerLine);

                // 이동
                if (targetX < transform.position.x - 0.1f)
                    moveInput = -1f;
                else if (targetX > transform.position.x + 0.1f)
                    moveInput = 1f;

                // Capsule Collider 중심 기준으로 거리 계산
                float distanceX =
                    Mathf.Abs(ballTransform.position.x - cpuCenter.x);

                float distance =
                    Vector2.Distance(cpuCenter, ballTransform.position);

                bool isBallAbove =
                    ballTransform.position.y > cpuCenter.y + 0.2f;

                // 점프
                if (isGrounded && isBallAbove && distanceX < 3.7f)
                {
                    jumpInput = true;
                }

                // 빨간 스파이크 범위 안에 공이 있는지 직접 확인
                Collider2D[] spikeHits = Physics2D.OverlapCircleAll(cpuCenter, spikeRadius);

                foreach (Collider2D hit in spikeHits)
                {
                    if (hit.CompareTag("Ball"))
                    {
                        spikeInput = true;
                        break;
                    }
                }

                // 낮고 빠른 공은 슬라이딩
                bool lowBall =
                    ballTransform.position.y < cpuCenter.y + 1.3f;

                bool fastBall =
                    Mathf.Abs(ballVelocity.x) > 3f;

                if (isGrounded && lowBall && fastBall && distanceX < 4f)
                {
                    slideInput = true;
                }
            }
        }
        else
        {
            if (isPlayer1)
            {
                if (Input.GetKey(KeyCode.A)) moveInput = -1f;
                else if (Input.GetKey(KeyCode.D)) moveInput = 1f;
                if (Input.GetKeyDown(KeyCode.Space)) jumpInput = true;
                if (Input.GetKeyDown(KeyCode.J)) slideInput = true;
                if (Input.GetKeyDown(KeyCode.K)) spikeInput = true;
            }
            else
            {
                if (Input.GetKey(KeyCode.LeftArrow)) moveInput = -1f;
                else if (Input.GetKey(KeyCode.RightArrow)) moveInput = 1f;
                if (Input.GetKeyDown(KeyCode.UpArrow)) jumpInput = true;
                if (Input.GetKeyDown(KeyCode.DownArrow)) slideInput = true;
                if (Input.GetKeyDown(KeyCode.RightShift)) spikeInput = true;
            }
        }

        if (moveInput != 0 && !isSliding) facingDirection = moveInput;

        // --- 2~4. 액션 적용 ---
        if (slideInput && isGrounded && !isSliding)
        {
            isSliding = true;
            slideTimer = slideDuration;
        }

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0) isSliding = false;
        }

        if (jumpInput && isGrounded && !isSliding)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            isGrounded = false;

            if (animator != null)
            {
                animator.SetTrigger("JumpSpike");
            }
        }

        if (spikeInput)
        {
            if (isCPU)
            {
                // CPU는 실제로 점프 중이면 스파이크 허용
                if (!isGrounded || Mathf.Abs(rb.velocity.y) > 0.1f)
                    SpikeAttack();
            }
            else
            {
                if (!isGrounded)
                    SpikeAttack();
            }
        }

        // --- 5. 최종 이동 ---
        if (isSliding) rb.velocity = new Vector2(facingDirection * slideSpeed, rb.velocity.y);
        else rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        // 구역 제한
        float currentX = transform.position.x;

        if (isPlayer1)
            currentX = Mathf.Clamp(currentX, -mapEnd, -centerLine);
        else
            currentX = Mathf.Clamp(currentX, centerLine, mapEnd);

        transform.position = new Vector3(
            currentX,
            transform.position.y,
            transform.position.z
        );

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveInput));
        }
    }

    void SpikeAttack()
    {
        Debug.Log(gameObject.name + " 스파이크 함수 호출됨!");

        Vector2 spikeCenter = bodyCollider != null
    ? (Vector2)bodyCollider.bounds.center
    : (Vector2)transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(spikeCenter, spikeRadius);

        Debug.Log(gameObject.name + " 범위 안 Collider 개수: " + hits.Length);

        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Ball"))
            {
                Debug.Log(gameObject.name + " 공 찾음! 스파이크 적용!");

                Rigidbody2D ballRb = hit.GetComponent<Rigidbody2D>();

                if (ballRb != null)
                {
                    float dirX = (transform.position.x < 0) ? 1.5f : -1.5f;
                    Vector2 spikeDir = new Vector2(dirX, -0.1f).normalized;

                    ballRb.velocity = Vector2.zero;
                    ballRb.AddForce(spikeDir * spikeForce, ForceMode2D.Impulse);
                    break;
                }
            }
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        CapsuleCollider2D col = GetComponent<CapsuleCollider2D>();

        Vector2 center = col != null
            ? (Vector2)col.bounds.center
            : (Vector2)transform.position;

        Gizmos.DrawWireSphere(center, spikeRadius);
    }
}