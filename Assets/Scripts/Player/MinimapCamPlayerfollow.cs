using UnityEngine;

namespace Mood.Player
{
    [AddComponentMenu("MOOD/Player/Minimap Cam Playerfollow")]
    [DisallowMultipleComponent]
    public sealed class MinimapCamPlayerfollow : MonoBehaviour
    {
        [SerializeField] private Transform player; // 플레이어의 Transform
        [SerializeField] private  Vector3 offset = new Vector3(0, 30, 0); // 미니맵 카메라와 플레이어의 거리 (높이)

        void LateUpdate() // 플레이어 이동 후 업데이트
        {
            if (player != null)
            {
                // 플레이어 위치 + 오프셋 위치로 카메라 이동
                Vector3 newPosition = player.position + offset;
                transform.position = newPosition;
            }
        }
    }
}
