using UnityEngine;
using System.Collections; // 딜레이(타이머)를 위해 필요합니다.

public class GameManager : MonoBehaviour
{
    public static GameManager instance; // 어디서든 매니저를 부를 수 있게 하는 마법의 단어

    [Header("현재 점수")]
    public int score1P = 0;
    public int score2P = 0;

    [Header("게임 오브젝트 연결")]
    public Transform player1;
    public Transform player2;
    public Transform ball;

    private Rigidbody2D ballRb;

    // 라운드 시작 시 플레이어들이 서있던 원래 위치를 기억할 변수
    private Vector3 p1StartPos;
    private Vector3 p2StartPos;

    // 점수가 나고 리셋되는 동안(1초) 중복으로 점수가 오르는 것을 막는 잠금장치
    private bool isWaitingForNextRound = false;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        ballRb = ball.GetComponent<Rigidbody2D>();

        // 게임 시작할 때 플레이어들의 처음 위치를 기억해둡니다.
        p1StartPos = player1.position;
        p2StartPos = player2.position;
    }

    // 공이 바닥에 닿았을 때 호출될 함수
    public void PointScored(bool isLeftCourt)
    {
        if (isWaitingForNextRound) return; // 이미 점수가 났으면 무시
        isWaitingForNextRound = true;

        bool did1PScore = false;

        if (isLeftCourt)
        {
            // 공이 1P 진영(왼쪽)에 떨어졌으므로 2P 득점!
            score2P++;
            Debug.Log("🎉 2P 득점! (현재 점수 - 1P: " + score1P + " / 2P: " + score2P + ")");
        }
        else
        {
            // 공이 2P 진영(오른쪽)에 떨어졌으므로 1P 득점!
            score1P++;
            did1PScore = true;
            Debug.Log("🎉 1P 득점! (현재 점수 - 1P: " + score1P + " / 2P: " + score2P + ")");
        }

        // 득점 후 1초 뒤에 리셋 (너무 바로 리셋되면 어색하니까요!)
        StartCoroutine(ResetRoundRoutine(did1PScore));
    }

    IEnumerator ResetRoundRoutine(bool did1PScore)
    {
        yield return new WaitForSeconds(1.0f); // 1초 대기

        // 1. 플레이어들을 원래 위치로 되돌리고 멈춰 세움
        player1.position = p1StartPos;
        player2.position = p2StartPos;
        player1.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        player2.GetComponent<Rigidbody2D>().velocity = Vector2.zero;

        // 2. 공의 속도와 회전 초기화
        ballRb.velocity = Vector2.zero;
        ballRb.angularVelocity = 0f;

        // 3. 점수를 얻은 사람의 머리 위(y = 5)에서 공을 다시 떨어뜨림
        if (did1PScore) ball.position = new Vector3(p1StartPos.x, 5f, 0f);
        else ball.position = new Vector3(p2StartPos.x, 5f, 0f);

        isWaitingForNextRound = false; // 잠금 해제 (다음 라운드 시작)
    }
}