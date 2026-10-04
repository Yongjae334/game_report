using UnityEngine;

public class Ball : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 부딪힌 상대가 'Ground(바닥)' 태그를 가지고 있다면?
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 공의 현재 X 위치가 0보다 작으면 왼쪽 코트, 크면 오른쪽 코트로 판정
            bool isLeftCourt = transform.position.x < 0;

            // 게임 매니저에게 점수 판정을 맡깁니다.
            GameManager.instance.PointScored(isLeftCourt);
        }
    }
}