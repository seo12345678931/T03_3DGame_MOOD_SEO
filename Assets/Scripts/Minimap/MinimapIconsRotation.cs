using UnityEngine;
using UnityEngine.Serialization;

// 리지드바디가 담겨 있는 일부 오브젝트가 옆에 누워서 쓰러지거나,
// 아예 오브젝트 자체가 옆으로 기울일 때,
// 미니맵 아이콘도 같이 쓰러지는 현상이 나타남.
// 이 문제를 해결하기 위한 만든 코드이며, 누워있더라도 항상 위에서 바라보게 고정시킬 수 있음.
// 아이템, 폭발물에만 사용했으며 적은 테스트결과 사용안해도 무방함.
// 코드출처: https://bonnate.tistory.com/282
namespace Mood.Minimap
{
    [AddComponentMenu("MOOD/Minimap/Minimap Icons Rotation")]
    [DisallowMultipleComponent]
    public sealed class MinimapIconsRotation : MonoBehaviour
    {
        [Header("바라볼 대상")] // 프리펩 폴더의 Canvas 1에 할당하는 걸 추천
        [SerializeField] private Transform transformTarget;

        [Header("각도의 오프셋 사전설정")] 
        [SerializeField] private Vector3 eulerAnglesOffset;

        [Header("로컬 회전값 or 월드 회전값?")] 
        [SerializeField] private bool isLocalRotation = false;

        [Header("동기화 할 축(x,y,z)")] 
        [SerializeField] bool syncX;
        [SerializeField] bool syncY;
        [SerializeField] bool syncZ;
        
        private Vector3 GetRotation()
        {
            return new Vector3(
                syncX ? transformTarget.eulerAngles.x : 0f + eulerAnglesOffset.x,
                syncY ? transformTarget.eulerAngles.y : 0f + eulerAnglesOffset.y,
                syncZ ? transformTarget.eulerAngles.z : 0f + eulerAnglesOffset.z);
        }

        private void Update()
        {
            if (isLocalRotation)
                transform.localEulerAngles = GetRotation();
            else
                transform.eulerAngles = GetRotation();
        }
    }
}
